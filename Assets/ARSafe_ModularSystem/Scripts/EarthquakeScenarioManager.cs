using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ARSafe.UI;
using MessageType = ARSafe.UI.MessageNotificationController.MessageType;

namespace ARSafe.Modular
{
    /// <summary>
    /// Generates scenario-wide earthquake parameters (magnitude, intensity multipliers, duration) whenever the earthquake disaster activates.
    /// Other systems (camera shake, debris, overlays, decals) subscribe to the exposed events to remain synchronized.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class EarthquakeScenarioManager : MonoBehaviour
    {
        private static readonly EarthquakeScenarioParameters InactiveParameters = new EarthquakeScenarioParameters(
            0f,
            0f,
            string.Empty,
            string.Empty,
            0f,
            1f,
            1f,
            1f,
            1f,
            1f,
            false);

        [Header("Magnitude Generation")]
        [Tooltip("Random magnitude range (Richter scale) sampled when the earthquake scenario begins.")]
        [SerializeField] private Vector2 magnitudeRange = new Vector2(5.2f, 7.4f);

        [Header("Scenario Duration")]
        [Tooltip("Random duration range (seconds) controlling how long shaking, debris, and cracks remain active.")]
        [SerializeField] private Vector2 durationRange = new Vector2(18f, 28f);

        [Header("Shake Scaling")]
        [Tooltip("Multiplier range applied to camera shake amplitude based on normalized magnitude (0 = min range.x, 1 = max range.y).")]
        [SerializeField] private Vector2 shakeMultiplierRange = new Vector2(0.7f, 1.45f);

        [Tooltip("Multiplier range applied to shake noise frequency relative to magnitude.")]
        [SerializeField] private Vector2 shakeFrequencyRange = new Vector2(0.9f, 1.25f);

        [Tooltip("Multiplier range applied to shake ramp duration relative to magnitude.")]
        [SerializeField] private Vector2 shakeDurationRange = new Vector2(0.85f, 1.35f);

        [Header("Debris Scaling")]
        [Tooltip("Multiplier range applied to debris emission rates relative to magnitude.")]
        [SerializeField] private Vector2 debrisRateRange = new Vector2(0.65f, 1.55f);

        [Tooltip("Multiplier range applied to debris lifetime/duration relative to magnitude.")]
        [SerializeField] private Vector2 debrisDurationRange = new Vector2(0.85f, 1.35f);

        [Header("Messaging")]
        [SerializeField] private bool logSelectedParameters = true;

        public static event Action<EarthquakeScenarioParameters> OnParametersUpdated;
        public static event Action<EarthquakeScenarioProgress> OnProgressUpdated;

        public static EarthquakeScenarioParameters CurrentParameters { get; private set; } = InactiveParameters;
        public static EarthquakeScenarioProgress CurrentProgress { get; private set; } = EarthquakeScenarioProgress.Inactive;

        public static EarthquakeScenarioManager Instance { get; private set; }

        private Coroutine scenarioRoutine;
        private bool pendingScenarioStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            CurrentParameters = InactiveParameters;
            CurrentProgress = EarthquakeScenarioProgress.Inactive;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null)
            {
                return;
            }

            // Only initialize when MainScene loads (where manual instances should exist)
            // Skip if in MainMenu or other scenes - wait for MainScene to load
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != "MainScene")
            {
                Debug.Log($"[EarthquakeScenarioManager] Not in MainScene (currently in '{activeScene.name}'), skipping initialization until MainScene loads.");
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<EarthquakeScenarioManager>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Debug.LogWarning("[EarthquakeScenarioManager] No manually-placed EarthquakeScenarioManager found in MainScene. Creating new instance. For best results, add EarthquakeScenarioManager GameObject to MainScene manually.");
                var managerObject = new GameObject(nameof(EarthquakeScenarioManager));
                existing = managerObject.AddComponent<EarthquakeScenarioManager>();
            }
            else
            {
                Debug.Log($"[EarthquakeScenarioManager] Using manually-placed instance: {existing.gameObject.name}");
            }

            Instance = existing;
        }

        public static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<EarthquakeScenarioManager>(FindObjectsInactive.Include);
            if (existing == null)
            {
                var managerObject = new GameObject(nameof(EarthquakeScenarioManager));
                existing = managerObject.AddComponent<EarthquakeScenarioManager>();
            }

            Instance = existing;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
            HandleDisasterTypeChanged(DisasterTypeManager.SelectedDisasterType);
        }

        private void OnDisable()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            StopScenarioRoutine();
            pendingScenarioStart = false;
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Earthquake)
            {
                pendingScenarioStart = false;
                StopScenarioRoutine();
                SetProgress(EarthquakeScenarioProgress.Inactive);
                BroadcastParameters(InactiveParameters);
                return;
            }

            // Defer scenario start until welcome flow completes
            pendingScenarioStart = true;
            StopScenarioRoutine();
            SetProgress(EarthquakeScenarioProgress.Inactive);
            BroadcastParameters(InactiveParameters);
            EarthquakeAlertOverlayController.EnsureInstance();
        }

        /// <summary>
        /// Attempts to begin the earthquake scenario if one has been requested and the disaster type is active.
        /// Called by loading/welcome flows once the user starts the simulation.
        /// </summary>
        public static bool BeginScenarioIfReady()
        {
            if (Instance == null)
            {
                return false;
            }

            return Instance.TryBeginScenario();
        }

        private bool TryBeginScenario()
        {
            if (!pendingScenarioStart)
            {
                return CurrentParameters.IsActive;
            }

            pendingScenarioStart = false;

            EarthquakeAlertOverlayController.EnsureInstance();
            if (CurrentParameters.IsActive)
            {
                StopScenarioRoutine();
                SetProgress(EarthquakeScenarioProgress.Inactive);
            }

            GenerateScenarioParameters();
            return true;
        }

        private void GenerateScenarioParameters()
        {
            float magnitude = Mathf.Max(0.1f, UnityEngine.Random.Range(magnitudeRange.x, magnitudeRange.y));
            float normalized = Mathf.InverseLerp(magnitudeRange.x, magnitudeRange.y, magnitude);
            float durationSeconds = Mathf.Max(5f, UnityEngine.Random.Range(durationRange.x, durationRange.y));

            string intensityLabel = GetIntensityLabel(magnitude, out string responseMessage);

            float shakeMultiplier = Mathf.Lerp(shakeMultiplierRange.x, shakeMultiplierRange.y, normalized);
            float frequencyMultiplier = Mathf.Lerp(shakeFrequencyRange.x, shakeFrequencyRange.y, normalized);
            float durationMultiplier = Mathf.Lerp(shakeDurationRange.x, shakeDurationRange.y, normalized);
            float debrisRateMultiplier = Mathf.Lerp(debrisRateRange.x, debrisRateRange.y, normalized);
            float debrisDurationMultiplier = Mathf.Lerp(debrisDurationRange.x, debrisDurationRange.y, normalized);

            var parameters = new EarthquakeScenarioParameters(
                magnitude,
                normalized,
                intensityLabel,
                responseMessage,
                durationSeconds,
                shakeMultiplier,
                frequencyMultiplier,
                durationMultiplier,
                debrisRateMultiplier,
                debrisDurationMultiplier,
                true);

            if (logSelectedParameters)
            {
                Debug.Log($"[EarthquakeScenarioManager] Magnitude {magnitude:F1} ({intensityLabel}), duration {durationSeconds:F1}s, shake x{shakeMultiplier:F2}, debris rate x{debrisRateMultiplier:F2}");
            }

            BroadcastParameters(parameters);

            // CRITICAL FIX: Force refresh all debris controllers to ensure they're subscribed
            // This fixes the issue where debris doesn't fall when manually selecting a location
            // (debris might already be active when scenario starts, missing the OnEnable subscription)
            ForceRefreshAllDebrisControllers();

            // CRITICAL FIX: Force refresh all crack quad controllers to ensure they're subscribed
            // Same issue as debris - cracks might be active before earthquake starts
            ForceRefreshAllCrackQuads();

            // CRITICAL FIX: Force refresh all disaster filters to show earthquake content
            // This fixes the issue where debris/arrows don't show when manually selecting a location
            // (filters might have been initialized before earthquake was selected)
            ForceRefreshAllDisasterFilters();
            ForceRefreshAllProximityDisplays();

            // CRITICAL: Show safety instructions to user when earthquake begins
            MessageNotificationController.Instance?.ShowMessage(
                "⚠️ EARTHQUAKE ALERT! Drop, Cover, and Hold On!",
                MessageType.Warning,
                6f
            );

            // CRITICAL: Enable wrong-way navigation warnings during earthquake scenario
            if (ARSafe.UI.ARSafeWrongWayWarning.Instance != null)
            {
                ARSafe.UI.ARSafeWrongWayWarning.Instance.EnableWarnings();

                if (logSelectedParameters)
                {
                    Debug.Log("[EarthquakeScenarioManager] Wrong-way navigation warnings ENABLED");
                }
            }

            StartScenarioRoutine(durationSeconds);
        }

        private void BroadcastParameters(EarthquakeScenarioParameters parameters)
        {
            CurrentParameters = parameters;
            OnParametersUpdated?.Invoke(parameters);
        }

        private void StartScenarioRoutine(float duration)
        {
            StopScenarioRoutine();
            scenarioRoutine = StartCoroutine(RunScenario(duration));
        }

        private void StopScenarioRoutine()
        {
            if (scenarioRoutine != null)
            {
                StopCoroutine(scenarioRoutine);
                scenarioRoutine = null;
            }

            // NOTE: Wrong-way warnings are NOT disabled here
            // They stay active until the user reaches an exit (handled by ARSafeWrongWayWarning.OnExitReached)
            // This ensures navigation guidance continues even after earthquake shaking ends
        }

        private IEnumerator RunScenario(float duration)
        {
            float elapsed = 0f;
            bool midpointMessageShown = false;
            SetProgress(new EarthquakeScenarioProgress(elapsed, duration, false));

            while (elapsed < duration && DisasterTypeManager.SelectedDisasterType == DisasterType.Earthquake)
            {
                yield return null;
                elapsed += Time.deltaTime;
                SetProgress(new EarthquakeScenarioProgress(Mathf.Min(elapsed, duration), duration, false));

                // Show mid-earthquake safety reminder
                if (!midpointMessageShown && elapsed >= duration * 0.4f)
                {
                    midpointMessageShown = true;
                    MessageNotificationController.Instance?.ShowMessage(
                        "Stay in safe position! Cover your head and neck.",
                        MessageType.Warning,
                        5f
                    );
                }
            }

            // Earthquake complete - show safety completion message
            SetProgress(new EarthquakeScenarioProgress(duration, duration, true));
            MessageNotificationController.Instance?.ShowMessage(
                "✓ Earthquake has ended. Check for hazards before moving.",
                MessageType.Success,
                7f
            );

            // NOTE: Wrong-way warnings stay active until user reaches exit
            // (they are disabled automatically when user arrives at an exit)

            BroadcastParameters(InactiveParameters);
            scenarioRoutine = null;
        }

        private void SetProgress(EarthquakeScenarioProgress progress)
        {
            CurrentProgress = progress;
            OnProgressUpdated?.Invoke(progress);
        }

        /// <summary>
        /// Force all debris controllers in the scene to refresh and re-process current earthquake state.
        /// CRITICAL FIX: Ensures debris falls correctly even when manually selecting a location
        /// (debris might already be active when scenario starts, missing the OnEnable subscription).
        /// </summary>
        private void ForceRefreshAllDebrisControllers()
        {
            var allDebris = UnityEngine.Object.FindObjectsByType<ARSafe.Content.EarthquakeDebrisController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            Debug.Log($"<color=cyan>[EarthquakeScenarioManager] Force refreshing {allDebris.Length} debris controller(s)</color>");

            foreach (var debris in allDebris)
            {
                if (debris == null || !debris.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Force the debris to reprocess current parameters and progress
                // This ensures it starts emitting even if it was already active
                StartCoroutine(RefreshDebrisController(debris));
            }
        }

        /// <summary>
        /// Coroutine to refresh a single debris controller by toggling it.
        /// </summary>
        private System.Collections.IEnumerator RefreshDebrisController(ARSafe.Content.EarthquakeDebrisController debris)
        {
            if (debris == null)
            {
                yield break;
            }

            GameObject debrisObj = debris.gameObject;
            bool wasActive = debrisObj.activeSelf;

            if (!wasActive)
            {
                // If already inactive, no need to refresh
                yield break;
            }

            Debug.Log($"<color=yellow>[EarthquakeScenarioManager] Refreshing debris controller: {debris.name}</color>");

            // Toggle GameObject to force OnDisable → OnEnable cycle
            debrisObj.SetActive(false);
            yield return null; // Wait one frame
            debrisObj.SetActive(true);

            Debug.Log($"<color=green>[EarthquakeScenarioManager] ✓ Debris controller refreshed: {debris.name}</color>");
        }

        /// <summary>
        /// Force all crack quad controllers in the scene to refresh and re-subscribe to events.
        /// CRITICAL FIX: Ensures cracks show correctly even when manually selecting a location
        /// (cracks might already be active when scenario starts, missing the OnEnable subscription).
        /// </summary>
        private void ForceRefreshAllCrackQuads()
        {
            var allCracks = UnityEngine.Object.FindObjectsByType<EarthquakeCrackQuadController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            Debug.Log($"<color=cyan>[EarthquakeScenarioManager] Force refreshing {allCracks.Length} crack quad controller(s)</color>");

            foreach (var crack in allCracks)
            {
                if (crack == null || !crack.gameObject.activeInHierarchy)
                {
                    continue;
                }

                // Force the crack to re-subscribe to OnProgressUpdated event
                StartCoroutine(RefreshCrackQuad(crack));
            }
        }

        /// <summary>
        /// Coroutine to refresh a single crack quad controller by toggling it.
        /// </summary>
        private System.Collections.IEnumerator RefreshCrackQuad(EarthquakeCrackQuadController crack)
        {
            if (crack == null)
            {
                yield break;
            }

            GameObject crackObj = crack.gameObject;
            bool wasActive = crackObj.activeSelf;

            if (!wasActive)
            {
                // If already inactive, no need to refresh
                yield break;
            }

            Debug.Log($"<color=yellow>[EarthquakeScenarioManager] Refreshing crack quad: {crack.name}</color>");

            // Toggle GameObject to force OnDisable → OnEnable cycle
            // This ensures the crack re-subscribes to OnProgressUpdated event
            crackObj.SetActive(false);
            yield return null; // Wait one frame
            crackObj.SetActive(true);

            Debug.Log($"<color=green>[EarthquakeScenarioManager] ✓ Crack quad refreshed: {crack.name}</color>");
        }

        /// <summary>
        /// Force all disaster filters in the scene to refresh visibility.
        /// CRITICAL FIX: Ensures earthquake content shows when manually selecting a location
        /// (filters might have been initialized before earthquake was selected).
        /// </summary>
        private void ForceRefreshAllDisasterFilters()
        {
            // Try to use ARSafeActivationController's method if available
            var activationController = UnityEngine.Object.FindFirstObjectByType<ARSafeActivationController>();
            if (activationController != null)
            {
                activationController.ForceRefreshAllDisasterFilters();
                Debug.Log($"<color=green>[EarthquakeScenarioManager] ✓ Forced disaster filter refresh via ARSafeActivationController</color>");
                return;
            }

            // Fallback: Manually refresh all filters
            var allFilters = UnityEngine.Object.FindObjectsByType<ARSafeDisasterFilter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            Debug.Log($"<color=cyan>[EarthquakeScenarioManager] Force refreshing {allFilters.Length} disaster filter(s)</color>");

            foreach (var filter in allFilters)
            {
                if (filter != null)
                {
                    filter.RefreshVisibility();
                    Debug.Log($"<color=yellow>[EarthquakeScenarioManager] Refreshed filter: {filter.name}</color>");
                }
            }
        }

        /// <summary>
        /// Force all proximity displays in the scene to refresh visibility.
        /// CRITICAL FIX: Ensures directional arrows show when manually selecting a location.
        /// </summary>
        private void ForceRefreshAllProximityDisplays()
        {
            var allProximityDisplays = UnityEngine.Object.FindObjectsByType<ARSafeProximityDisplay>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            Debug.Log($"<color=cyan>[EarthquakeScenarioManager] Force refreshing {allProximityDisplays.Length} proximity display(s)</color>");

            foreach (var display in allProximityDisplays)
            {
                if (display != null && display.enabled)
                {
                    display.RefreshVisibilityImmediate();
                    Debug.Log($"<color=yellow>[EarthquakeScenarioManager] Refreshed proximity display: {display.name}</color>");
                }
            }
        }

        private string GetIntensityLabel(float magnitude, out string responseMessage)
        {
            if (magnitude < 5.5f)
            {
                responseMessage = "Expect light shaking. Secure loose objects and stay alert.";
                return "Moderate";
            }

            if (magnitude < 6.5f)
            {
                responseMessage = "Strong shaking detected. Drop, Cover, and Hold On.";
                return "Strong";
            }

            responseMessage = "Severe shaking. Move away from glass and take immediate shelter.";
            return "Severe";
        }
    }

    /// <summary>
    /// Immutable data describing the procedurally generated earthquake intensity for the active simulation.
    /// </summary>
    public readonly struct EarthquakeScenarioParameters
    {
        public EarthquakeScenarioParameters(
            float magnitude,
            float normalizedMagnitude,
            string intensityLabel,
            string responseMessage,
            float durationSeconds,
            float shakeMultiplier,
            float frequencyMultiplier,
            float durationMultiplier,
            float debrisRateMultiplier,
            float debrisDurationMultiplier,
            bool isActive)
        {
            Magnitude = Mathf.Max(0f, magnitude);
            NormalizedMagnitude = Mathf.Clamp01(normalizedMagnitude);
            IntensityLabel = intensityLabel ?? string.Empty;
            ResponseMessage = responseMessage ?? string.Empty;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            ShakeMultiplier = shakeMultiplier;
            FrequencyMultiplier = frequencyMultiplier;
            DurationMultiplier = durationMultiplier;
            DebrisRateMultiplier = debrisRateMultiplier;
            DebrisDurationMultiplier = debrisDurationMultiplier;
            IsActive = isActive && Magnitude > 0f;
        }

        public float Magnitude { get; }
        public float NormalizedMagnitude { get; }
        public string IntensityLabel { get; }
        public string ResponseMessage { get; }
        public float DurationSeconds { get; }
        public float ShakeMultiplier { get; }
        public float FrequencyMultiplier { get; }
        public float DurationMultiplier { get; }
        public float DebrisRateMultiplier { get; }
        public float DebrisDurationMultiplier { get; }
        public bool IsActive { get; }
    }

    /// <summary>
    /// Describes the real-time progress of the earthquake scenario timeline so dependent systems can fade in/out.
    /// </summary>
    public readonly struct EarthquakeScenarioProgress
    {
        public static readonly EarthquakeScenarioProgress Inactive = new EarthquakeScenarioProgress(0f, 0f, false);

        public EarthquakeScenarioProgress(float elapsedSeconds, float durationSeconds, bool isComplete)
        {
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            ElapsedSeconds = Mathf.Clamp(elapsedSeconds, 0f, DurationSeconds <= 0f ? 0f : durationSeconds);
            NormalizedTime = DurationSeconds > Mathf.Epsilon ? Mathf.Clamp01(ElapsedSeconds / DurationSeconds) : 0f;
            IsComplete = isComplete;
            // IsActive is true only when scenario is running (duration > 0, not complete, elapsed < duration)
            IsActive = !IsComplete && DurationSeconds > Mathf.Epsilon && ElapsedSeconds < DurationSeconds;
        }

        public float DurationSeconds { get; }
        public float ElapsedSeconds { get; }
        public float NormalizedTime { get; }
        public bool IsActive { get; }
        public bool IsComplete { get; }
    }
}
