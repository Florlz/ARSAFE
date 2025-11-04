/*
 * PURPOSE: Manages the fire disaster scenario with alarm audio and safety notifications
 * DEPENDENCIES: Unity APIs (AudioSource, Coroutines), DisasterTypeManager, MessageNotificationController
 * DATA FLOW: Disaster selection → BeginScenarioIfReady() → Generate parameters → Play alarm → Show notifications → Fire scenario active
 * PERFORMANCE: Singleton pattern, event-based architecture, minimal allocations
 * EDGE CASES: Handles disaster type changes, scene transitions, multiple restarts
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using ARSafe.UI;
using MessageType = ARSafe.UI.MessageNotificationController.MessageType;

namespace ARSafe.Modular
{
    /// <summary>
    /// Manages fire scenario lifecycle including alarm sounds, safety notifications, and fire intensity parameters.
    /// Auto-creates itself and persists across scenes. Integrates with disaster type selection and AR loading flow.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class FireScenarioManager : MonoBehaviour
    {
        private static readonly FireScenarioParameters InactiveParameters = new FireScenarioParameters(
            0f,
            0f,
            string.Empty,
            string.Empty,
            0f,
            1f,
            1f,
            1f,
            false);

        [Header("Fire Intensity Generation")]
        [Tooltip("Random intensity range (0-10 scale) sampled when the fire scenario begins.")]
        [SerializeField] private Vector2 intensityRange = new Vector2(4f, 8f);

        [Header("Scenario Duration")]
        [Tooltip("Random duration range (seconds) controlling how long fire, smoke, and alarms remain active.")]
        [SerializeField] private Vector2 durationRange = new Vector2(30f, 60f);

        [Header("Fire Audio")]
        [Tooltip("Audio source for playing fire alarm (must have clip assigned in Inspector)")]
        [SerializeField] private AudioSource alarmAudioSource;

        [Tooltip("Duration to play fire alarm (seconds). Set to 0 to play until scenario ends.")]
        [SerializeField] private float alarmDuration = 8f;

        [Header("Fire Effect Scaling")]
        [Tooltip("Multiplier range applied to fire particle emission rates relative to intensity.")]
        [SerializeField] private Vector2 fireEmissionRange = new Vector2(0.6f, 1.5f);

        [Tooltip("Multiplier range applied to smoke particle emission rates relative to intensity.")]
        [SerializeField] private Vector2 smokeEmissionRange = new Vector2(0.7f, 1.4f);

        [Tooltip("Multiplier range applied to fire/smoke lifetime relative to intensity.")]
        [SerializeField] private Vector2 particleLifetimeRange = new Vector2(0.8f, 1.3f);

        [Header("Safety Instructions")]
        [Tooltip("Fire safety messages shown to users")]
        [SerializeField] private string[] safetyMessages = new string[]
        {
            "FIRE ALERT! Stay low and evacuate immediately!",
            "Check doors for heat before opening",
            "Cover your mouth and nose - avoid smoke inhalation",
            "Proceed to the nearest emergency exit",
            "Move quickly but do not run - stay calm"
        };

        [Tooltip("Duration to show each safety message (seconds)")]
        [SerializeField] private float messageDuration = 5f;

        [Tooltip("Delay between safety messages (seconds)")]
        [SerializeField] private float messageDelay = 3f;

        [Header("Debug")]
        [SerializeField] private bool logSelectedParameters = true;

        public static event Action<FireScenarioParameters> OnParametersUpdated;
        public static event Action<FireScenarioProgress> OnProgressUpdated;

        public static FireScenarioParameters CurrentParameters { get; private set; } = InactiveParameters;
        public static FireScenarioProgress CurrentProgress { get; private set; } = FireScenarioProgress.Inactive;

        public static FireScenarioManager Instance { get; private set; }

        private Coroutine scenarioRoutine;
        private Coroutine notificationRoutine;
        private Coroutine alarmTimerRoutine;
        private bool pendingScenarioStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            CurrentParameters = InactiveParameters;
            CurrentProgress = FireScenarioProgress.Inactive;
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
                Debug.Log($"[FireScenarioManager] Not in MainScene (currently in '{activeScene.name}'), skipping initialization until MainScene loads.");
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<FireScenarioManager>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Debug.LogWarning("[FireScenarioManager] No manually-placed FireScenarioManager found in MainScene. Creating new instance. NOTE: Inspector references (AudioSource, etc.) will not be available. For alarm sound, please add FireScenarioManager GameObject to MainScene manually with AudioSource component.");
                var managerObject = new GameObject(nameof(FireScenarioManager));
                existing = managerObject.AddComponent<FireScenarioManager>();
            }
            else
            {
                Debug.Log($"[FireScenarioManager] Using manually-placed instance: {existing.gameObject.name}");
            }

            Instance = existing;
        }

        public static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<FireScenarioManager>(FindObjectsInactive.Include);
            if (existing == null)
            {
                var managerObject = new GameObject(nameof(FireScenarioManager));
                existing = managerObject.AddComponent<FireScenarioManager>();
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

            // Defensive audio initialization - force correct settings regardless of Inspector configuration
            if (alarmAudioSource != null)
            {
                alarmAudioSource.playOnAwake = false;  // Prevent auto-play on scene load
                alarmAudioSource.Stop();               // Ensure stopped initially

                if (logSelectedParameters)
                {
                    Debug.Log($"[FireScenarioManager] Fire alarm AudioSource initialized: playOnAwake=false, stopped, loop={alarmAudioSource.loop}");
                }
            }
        }

        private void OnEnable()
        {
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
            HandleDisasterTypeChanged(DisasterTypeManager.SelectedDisasterType);
        }

        private void OnDisable()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            StopAllScenarioRoutines();
            pendingScenarioStart = false;
            StopAlarm();
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Fire)
            {
                pendingScenarioStart = false;
                StopAllScenarioRoutines();
                SetProgress(FireScenarioProgress.Inactive);
                BroadcastParameters(InactiveParameters);
                StopAlarm();
                return;
            }

            // Defer scenario start until welcome flow completes
            pendingScenarioStart = true;
            StopAllScenarioRoutines();
            SetProgress(FireScenarioProgress.Inactive);
            BroadcastParameters(InactiveParameters);
            FireAlertOverlayController.EnsureInstance();
        }

        /// <summary>
        /// Attempts to begin the fire scenario if one has been requested and the disaster type is active.
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

            if (CurrentParameters.IsActive)
            {
                StopAllScenarioRoutines();
                SetProgress(FireScenarioProgress.Inactive);
            }

            GenerateScenarioParameters();
            return true;
        }

        private void GenerateScenarioParameters()
        {
            float intensity = Mathf.Max(0.1f, UnityEngine.Random.Range(intensityRange.x, intensityRange.y));
            float normalized = Mathf.InverseLerp(intensityRange.x, intensityRange.y, intensity);
            float durationSeconds = Mathf.Max(10f, UnityEngine.Random.Range(durationRange.x, durationRange.y));

            string intensityLabel = GetIntensityLabel(intensity, out string responseMessage);

            float fireEmissionMultiplier = Mathf.Lerp(fireEmissionRange.x, fireEmissionRange.y, normalized);
            float smokeEmissionMultiplier = Mathf.Lerp(smokeEmissionRange.x, smokeEmissionRange.y, normalized);
            float lifetimeMultiplier = Mathf.Lerp(particleLifetimeRange.x, particleLifetimeRange.y, normalized);

            var parameters = new FireScenarioParameters(
                intensity,
                normalized,
                intensityLabel,
                responseMessage,
                durationSeconds,
                fireEmissionMultiplier,
                smokeEmissionMultiplier,
                lifetimeMultiplier,
                true);

            if (logSelectedParameters)
            {
                Debug.Log($"[FireScenarioManager] Intensity {intensity:F1} ({intensityLabel}), duration {durationSeconds:F1}s, fire emission x{fireEmissionMultiplier:F2}, smoke x{smokeEmissionMultiplier:F2}");
            }

            BroadcastParameters(parameters);

            // CRITICAL FIX: Force refresh all disaster filters to show fire content
            ForceRefreshAllDisasterFilters();
            ForceRefreshAllProximityDisplays();

            // CRITICAL: Enable wrong-way navigation warnings during fire scenario
            if (ARSafe.UI.ARSafeWrongWayWarning.Instance != null)
            {
                ARSafe.UI.ARSafeWrongWayWarning.Instance.EnableWarnings();

                if (logSelectedParameters)
                {
                    Debug.Log("[FireScenarioManager] Wrong-way navigation warnings ENABLED");
                }
            }

            // Play fire alarm
            PlayAlarm();

            // Start safety notification sequence
            StartNotificationSequence();

            // Start scenario timer
            StartScenarioRoutine(durationSeconds);
        }

        private void BroadcastParameters(FireScenarioParameters parameters)
        {
            CurrentParameters = parameters;
            OnParametersUpdated?.Invoke(parameters);
        }

        private void SetProgress(FireScenarioProgress progress)
        {
            CurrentProgress = progress;
            OnProgressUpdated?.Invoke(progress);
        }

        private string GetIntensityLabel(float intensity, out string responseMessage)
        {
            // Fire intensity scale (0-10)
            // 0-2: Minor, 2-4: Moderate, 4-6: Serious, 6-8: Severe, 8-10: Extreme

            if (intensity < 2f)
            {
                responseMessage = "Maintain calm, check for smoke, prepare to evacuate.";
                return "Minor Fire";
            }
            else if (intensity < 4f)
            {
                responseMessage = "Evacuate calmly using nearest exit. Stay low and avoid smoke.";
                return "Moderate Fire";
            }
            else if (intensity < 6f)
            {
                responseMessage = "Immediate evacuation required. Stay low, avoid smoke.";
                return "Serious Fire";
            }
            else if (intensity < 8f)
            {
                responseMessage = "Critical danger! Evacuate immediately, cover mouth/nose.";
                return "Severe Fire";
            }
            else
            {
                responseMessage = "EXTREME DANGER! Emergency evacuation! Follow exit signs!";
                return "Extreme Fire";
            }
        }

        private void PlayAlarm()
        {
            if (alarmAudioSource == null)
            {
                Debug.LogWarning("[FireScenarioManager] Fire alarm AudioSource not assigned! Please assign an AudioSource with a clip in the Inspector.");
                return;
            }

            if (alarmAudioSource.clip == null)
            {
                Debug.LogWarning("[FireScenarioManager] Fire alarm AudioSource has no clip assigned! Please assign an AudioClip in the Inspector.");
                return;
            }

            // Simply play the AudioSource - clip and settings should be configured in Inspector
            alarmAudioSource.Play();

            if (logSelectedParameters)
            {
                Debug.Log($"[FireScenarioManager] Fire alarm started (clip: {alarmAudioSource.clip.name}, volume: {alarmAudioSource.volume}, loop: {alarmAudioSource.loop}, duration: {alarmDuration}s)");
            }

            // Start timer to stop alarm after specified duration
            if (alarmDuration > 0f)
            {
                if (alarmTimerRoutine != null)
                {
                    StopCoroutine(alarmTimerRoutine);
                }
                alarmTimerRoutine = StartCoroutine(AlarmTimerRoutine());
            }
        }

        private void StopAlarm()
        {
            if (alarmTimerRoutine != null)
            {
                StopCoroutine(alarmTimerRoutine);
                alarmTimerRoutine = null;
            }

            if (alarmAudioSource != null && alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();

                if (logSelectedParameters)
                {
                    Debug.Log("[FireScenarioManager] Fire alarm stopped");
                }
            }
        }

        private IEnumerator AlarmTimerRoutine()
        {
            yield return new WaitForSeconds(alarmDuration);

            if (alarmAudioSource != null && alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();

                if (logSelectedParameters)
                {
                    Debug.Log($"[FireScenarioManager] Fire alarm stopped after {alarmDuration}s duration");
                }
            }

            alarmTimerRoutine = null;
        }

        private void StartNotificationSequence()
        {
            if (notificationRoutine != null)
            {
                StopCoroutine(notificationRoutine);
            }

            notificationRoutine = StartCoroutine(ShowSafetyNotifications());
        }

        private IEnumerator ShowSafetyNotifications()
        {
            if (safetyMessages == null || safetyMessages.Length == 0)
            {
                yield break;
            }

            foreach (var message in safetyMessages)
            {
                if (string.IsNullOrEmpty(message))
                    continue;

                MessageNotificationController.Instance?.ShowMessage(
                    message,
                    MessageType.Warning,
                    messageDuration
                );

                yield return new WaitForSeconds(messageDuration + messageDelay);
            }

            notificationRoutine = null;
        }

        private void StartScenarioRoutine(float durationSeconds)
        {
            StopScenarioRoutine();
            scenarioRoutine = StartCoroutine(ScenarioTimerRoutine(durationSeconds));
        }

        private void StopScenarioRoutine()
        {
            if (scenarioRoutine != null)
            {
                StopCoroutine(scenarioRoutine);
                scenarioRoutine = null;
            }
        }

        private void StopAllScenarioRoutines()
        {
            StopScenarioRoutine();

            if (notificationRoutine != null)
            {
                StopCoroutine(notificationRoutine);
                notificationRoutine = null;
            }

            if (alarmTimerRoutine != null)
            {
                StopCoroutine(alarmTimerRoutine);
                alarmTimerRoutine = null;
            }

            // NOTE: Wrong-way warnings are NOT disabled here
            // They stay active until the user reaches an exit (handled by ARSafeWrongWayWarning.OnExitReached)
            // This ensures navigation guidance continues even after fire scenario ends
        }

        private IEnumerator ScenarioTimerRoutine(float durationSeconds)
        {
            float elapsed = 0f;

            while (elapsed < durationSeconds)
            {
                yield return null;
                elapsed += Time.deltaTime;

                var progress = new FireScenarioProgress(elapsed, durationSeconds, false);
                SetProgress(progress);
            }

            // Scenario complete
            var completeProgress = new FireScenarioProgress(durationSeconds, durationSeconds, true);
            SetProgress(completeProgress);

            // Stop alarm when scenario ends
            StopAlarm();

            // NOTE: Wrong-way warnings stay active until user reaches exit
            // (they are disabled automatically when user arrives at an exit)

            if (logSelectedParameters)
            {
                Debug.Log("[FireScenarioManager] Fire scenario complete - stopping alarm (warnings stay active until exit reached)");
            }

            scenarioRoutine = null;
        }

        private void ForceRefreshAllDisasterFilters()
        {
            var filters = FindObjectsByType<ARSafeDisasterFilter>(FindObjectsSortMode.None);
            foreach (var filter in filters)
            {
                if (filter != null)
                {
                    filter.RefreshVisibility();
                }
            }

            if (logSelectedParameters)
            {
                Debug.Log($"[FireScenarioManager] Forced refresh on {filters.Length} disaster filters");
            }
        }

        private void ForceRefreshAllProximityDisplays()
        {
            var proximityDisplays = FindObjectsByType<ARSafeProximityDisplay>(FindObjectsSortMode.None);
            foreach (var display in proximityDisplays)
            {
                if (display != null)
                {
                    display.RefreshVisibilityImmediate();
                }
            }

            if (logSelectedParameters)
            {
                Debug.Log($"[FireScenarioManager] Forced refresh on {proximityDisplays.Length} proximity displays");
            }
        }
    }

    /// <summary>
    /// Immutable data describing the procedurally generated fire intensity for the active simulation.
    /// </summary>
    public readonly struct FireScenarioParameters
    {
        public FireScenarioParameters(
            float intensity,
            float normalizedIntensity,
            string intensityLabel,
            string responseMessage,
            float durationSeconds,
            float fireEmissionMultiplier,
            float smokeEmissionMultiplier,
            float lifetimeMultiplier,
            bool isActive)
        {
            Intensity = Mathf.Max(0f, intensity);
            NormalizedIntensity = Mathf.Clamp01(normalizedIntensity);
            IntensityLabel = intensityLabel ?? string.Empty;
            ResponseMessage = responseMessage ?? string.Empty;
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            FireEmissionMultiplier = fireEmissionMultiplier;
            SmokeEmissionMultiplier = smokeEmissionMultiplier;
            LifetimeMultiplier = lifetimeMultiplier;
            IsActive = isActive && Intensity > 0f;
        }

        public float Intensity { get; }
        public float NormalizedIntensity { get; }
        public string IntensityLabel { get; }
        public string ResponseMessage { get; }
        public float DurationSeconds { get; }
        public float FireEmissionMultiplier { get; }
        public float SmokeEmissionMultiplier { get; }
        public float LifetimeMultiplier { get; }
        public bool IsActive { get; }
    }

    /// <summary>
    /// Describes the real-time progress of the fire scenario timeline so dependent systems can fade in/out.
    /// </summary>
    public readonly struct FireScenarioProgress
    {
        public static readonly FireScenarioProgress Inactive = new FireScenarioProgress(0f, 0f, false);

        public FireScenarioProgress(float elapsedSeconds, float durationSeconds, bool isComplete)
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
