using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ARSafe.UI;
using MessageType = ARSafe.UI.MessageNotificationController.MessageType;

namespace ARSafe.Modular
{
    /*
     * ARCHITECTURE PLAN: FloodScenarioManager
     *
     * PURPOSE:
     *   - Own the procedural generation and lifecycle of the flood simulation parameters.
     *   - Coordinate flood-related systems (water controller, overlays, messaging) via events.
     *
     * DEPENDENCIES:
     *   - Unity APIs: MonoBehaviour lifecycle, Coroutines, Random, Color.
     *   - Project Scripts: DisasterTypeManager (selected disaster routing), FloodWaterController (subscribes to events), MessageNotificationController.
     *   - External Packages: None (URP water shader driven through FloodWaterController).
     *
     * DATA FLOW:
     *   - Input: DisasterType changes from DisasterTypeManager, BeginScenarioIfReady() requests from loading flow.
     *   - Processing: Generate flood depth, duration phases, visual multipliers; run coroutine to broadcast progress phases.
     *   - Output: FloodScenarioParameters via OnParametersUpdated, FloodScenarioProgress via OnProgressUpdated, debug logs.
     *
     * INTEGRATION POINTS:
     *   - ARSafeLoadingIntegration waits on BeginScenarioIfReady to start flood timeline after onboarding.
     *   - FloodWaterController listens to events to animate water height and shader parameters.
     *   - UI systems can read CurrentParameters to present messaging (future overlay hook).
     *
     * PERFORMANCE CONSIDERATIONS:
     *   - Single coroutine drives timeline; negligible cost.
     *   - Avoid allocations inside coroutine loop beyond struct refresh.
     *
     * DEBUG LOGGING:
     *   - Prefix logs with [FloodScenario] and color per severity for ARDebugLogger filtering.
     */

    /// <summary>
    /// PAGASA (Philippine Atmospheric, Geophysical and Astronomical Services Administration) rainfall warning levels.
    /// Official 3-tier color-coded system used for flood warnings in the Philippines.
    /// </summary>
    public enum RainfallWarningLevel
    {
        None = 0,
        Yellow = 1,  // 7.5-15 mm/hour - Slight flooding in low-lying areas
        Orange = 2,  // 15-30 mm/hour - Flooding in low-lying areas & near rivers
        Red = 3      // 30+ mm/hour - Serious flooding, coastal towns at risk
    }

    [DefaultExecutionOrder(-240)]
    public class FloodScenarioManager : MonoBehaviour
    {
        private static readonly FloodScenarioParameters InactiveParameters = new FloodScenarioParameters(
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            Color.black,
            Color.black,
            Color.white,
            RainfallWarningLevel.None,
            string.Empty,
            string.Empty,
            Color.white,
            false);

        private static readonly FloodScenarioProgress InactiveProgress = new FloodScenarioProgress(
            0f,
            0f,
            FloodScenarioPhase.Inactive,
            0f,
            false);

        [Header("Water Depth")]
        [Tooltip("Range of flood depth (in meters above baseline) randomly selected when the scenario starts. Chest level ≈ 1.2-1.5m for adults.")]
        [SerializeField] private Vector2 depthRange = new Vector2(1.2f, 1.5f);

        [Header("Phase Durations (seconds)")]
        [Tooltip("Rise phase duration range. Water height eases in over this period - SLOWER = more dramatic/realistic.")]
        [SerializeField] private Vector2 riseDurationRange = new Vector2(25f, 40f);
        [Tooltip("Sustained peak duration - Water remains at max height indefinitely (flood does NOT recede).")]
        [SerializeField] private Vector2 holdDurationRange = new Vector2(999999f, 999999f); // Infinite sustain
        [Tooltip("DISABLED - Flood does not recede, water stays at peak level.")]
        [SerializeField] private Vector2 recedeDurationRange = new Vector2(0f, 0f);

        [Header("Water Visual Modifiers")]
        [Tooltip("Multiplier applied to shader wave amplitude (1 = base amplitude).")]
        [SerializeField] private Vector2 waveAmplitudeMultiplierRange = new Vector2(0.8f, 1.35f);
        [Tooltip("Multiplier applied to shader flow speed (1 = base speed).")]
        [SerializeField] private Vector2 flowSpeedMultiplierRange = new Vector2(0.75f, 1.35f);
        [Tooltip("Range of turbidity (muddy appearance) applied to shader.")]
        [SerializeField] private Vector2 turbidityRange = new Vector2(0.55f, 0.85f);

        [Header("Color Palette")]
        [SerializeField] private Color deepWaterColor = new Color(0.22f, 0.2f, 0.16f, 0.85f);
        [SerializeField] private Color shallowWaterColor = new Color(0.45f, 0.36f, 0.28f, 0.7f);
        [SerializeField] private Color foamColor = new Color(0.81f, 0.75f, 0.64f, 1f);

        [Header("Rain Audio")]
        [Tooltip("AudioSource component for rain ambient sound. Assign AudioSource with rain clip already set. Should be 2D (not spatialized).")]
        [SerializeField] private AudioSource rainAudioSource;
        [Tooltip("Rain audio volume (0-1).")]
        [SerializeField, Range(0f, 1f)] private float rainVolume = 0.7f;
        [Tooltip("Rain fade-out duration when scenario ends (seconds).")]
        [SerializeField] private float rainFadeOutDuration = 2f;

        [Header("Notifications")]
        [SerializeField] private bool logSelectedParameters = true;
        [SerializeField] private bool notifyOnScenarioStart = true;

        [Header("Testing (Editor Only)")]
        [Tooltip("Quick test parameters for editor testing.")]
        [SerializeField] private float testDepthMeters = 1.5f;
        [SerializeField] private float testRiseDuration = 10f;
        [SerializeField] private float testHoldDuration = 5f;
        [SerializeField] private float testRecedeDuration = 8f;

        public static event Action<FloodScenarioParameters> OnParametersUpdated;
        public static event Action<FloodScenarioProgress> OnProgressUpdated;
        public static event Action<RainfallWarningLevel, RainfallWarningLevel> OnWarningLevelChanged; // (previousLevel, newLevel)
        public static event Action<FloodWarningProgression> OnProgressionGenerated; // Fired once at scenario start

        public static FloodScenarioParameters CurrentParameters { get; private set; } = InactiveParameters;
        public static FloodScenarioProgress CurrentProgress { get; private set; } = InactiveProgress;

        public static FloodScenarioManager Instance { get; private set; }

        private Coroutine scenarioRoutine;
        private bool pendingScenarioStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            CurrentParameters = InactiveParameters;
            CurrentProgress = InactiveProgress;
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
                Debug.Log($"[FloodScenarioManager] Not in MainScene (currently in '{activeScene.name}'), skipping initialization until MainScene loads.");
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<FloodScenarioManager>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Debug.LogWarning("[FloodScenarioManager] No manually-placed FloodScenarioManager found in MainScene. Creating new instance. For best results, add FloodScenarioManager GameObject to MainScene manually.");
                var managerObject = new GameObject(nameof(FloodScenarioManager));
                existing = managerObject.AddComponent<FloodScenarioManager>();
            }
            else
            {
                Debug.Log($"[FloodScenarioManager] Using manually-placed instance: {existing.gameObject.name}");
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

            // Setup rain audio source (if not manually configured in Inspector)
            SetupRainAudioSource();
        }

        /// <summary>
        /// Configure rain AudioSource for looping ambient rain sound.
        /// NOTE: AudioSource and AudioClip must be assigned manually in Inspector.
        /// </summary>
        private void SetupRainAudioSource()
        {
            if (rainAudioSource == null)
            {
                if (logSelectedParameters)
                {
                    Debug.LogWarning("[FloodScenarioManager] Rain AudioSource not assigned in Inspector. Rain audio will be disabled.");
                }
                return;
            }

            // Configure for looping ambient rain sound (2D audio, not spatialized)
            rainAudioSource.loop = true;
            rainAudioSource.playOnAwake = false;
            rainAudioSource.spatialBlend = 0f; // 2D audio
            rainAudioSource.volume = 0f; // Start muted (will fade in when scenario starts)
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
            BroadcastParameters(InactiveParameters);
            BroadcastProgress(InactiveProgress);
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Flood)
            {
                pendingScenarioStart = false;
                StopScenarioRoutine();
                StopRain(); // Stop rain audio when switching away from flood
                BroadcastParameters(InactiveParameters);
                BroadcastProgress(InactiveProgress);
                return;
            }

            pendingScenarioStart = true;
            StopScenarioRoutine();
            BroadcastParameters(InactiveParameters);
            BroadcastProgress(InactiveProgress);
            FloodAlertOverlayController.EnsureInstance();

            // NOTE: Notification moved to GenerateScenarioParameters() to show AFTER user clicks "Start Simulation"
        }

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
                StopScenarioRoutine();
                BroadcastProgress(InactiveProgress);
            }

            GenerateScenarioParameters();
            return true;
        }

        private void GenerateScenarioParameters()
        {
            // Generate randomized peak level (20% Yellow, 40% Orange, 40% Red)
            RainfallWarningLevel peakLevel = GenerateRandomWarningLevel();

            // Build linear progression timeline (Yellow → Orange → Red)
            FloodWarningProgression progression = BuildProgression(peakLevel);

            // Broadcast progression timeline to UI systems
            OnProgressionGenerated?.Invoke(progression);

            // Start with Yellow warning level parameters
            var firstPhase = progression.Phases[0];
            UpdateParametersForLevel(firstPhase, progression);

            if (logSelectedParameters)
            {
                Debug.Log($"<color=cyan>[FloodScenario] ★★★ Flood progression → Peak: {peakLevel}, Phases: {progression.Phases.Count}, Total Duration: {GetTotalDuration(progression):F1}s</color>");
            }

            // Start rain ambient sound with fade-in
            PlayRain();

            // Show initial scenario start notification (after user clicks "Start Simulation")
            if (notifyOnScenarioStart && MessageNotificationController.Instance != null)
            {
                MessageNotificationController.Instance.ShowMessage(
                    "FLOOD ALERT: Water will begin rising soon! Move to higher ground!",
                    MessageType.Warning,
                    6f);
            }

            // CRITICAL: Enable wrong-way navigation warnings during flood scenario
            if (ARSafe.UI.ARSafeWrongWayWarning.Instance != null)
            {
                ARSafe.UI.ARSafeWrongWayWarning.Instance.EnableWarnings();

                if (logSelectedParameters)
                {
                    Debug.Log("[FloodScenarioManager] Wrong-way navigation warnings ENABLED");
                }
            }

            // Start progression coroutine
            StartProgressionRoutine(progression);
        }

        /// <summary>
        /// Updates flood parameters for a specific warning level phase
        /// </summary>
        private void UpdateParametersForLevel(WarningLevelPhase phase, FloodWarningProgression progression)
        {
            string warningLevelLabel = GetWarningLevelLabel(phase.Level);
            string warningMessage = GetWarningMessage(phase.Level);
            Color warningColor = GetWarningColor(phase.Level);

            // Visual parameters correlate with warning level intensity
            float intensityFactor = phase.Level == RainfallWarningLevel.Red ? 1.0f :
                                   phase.Level == RainfallWarningLevel.Orange ? 0.7f : 0.4f;

            float waveMultiplier = Mathf.Lerp(waveAmplitudeMultiplierRange.x, waveAmplitudeMultiplierRange.y, intensityFactor + UnityEngine.Random.value * 0.2f);
            float flowMultiplier = Mathf.Lerp(flowSpeedMultiplierRange.x, flowSpeedMultiplierRange.y, intensityFactor + UnityEngine.Random.value * 0.2f);
            float turbidity = Mathf.Lerp(turbidityRange.x, turbidityRange.y, intensityFactor + UnityEngine.Random.value * 0.2f);

            // Hold duration is infinite (water stays at peak level forever)
            float holdDuration = Mathf.Max(4f, UnityEngine.Random.Range(holdDurationRange.x, holdDurationRange.y));

            var parameters = new FloodScenarioParameters(
                phase.TargetDepth,
                phase.Duration,
                holdDuration,
                0f, // No recede
                waveMultiplier,
                flowMultiplier,
                turbidity,
                deepWaterColor,
                shallowWaterColor,
                foamColor,
                phase.Level,
                warningLevelLabel,
                warningMessage,
                warningColor,
                true);

            BroadcastParameters(parameters);

            // NOTE: Notifications and wrong-way warnings moved to GenerateScenarioParameters()
            // to only show once at scenario start, not on every level transition
        }

        private void StartScenarioRoutine(FloodScenarioParameters parameters)
        {
            StopScenarioRoutine();
            scenarioRoutine = StartCoroutine(RunScenario(parameters));
        }

        private void StopScenarioRoutine()
        {
            if (scenarioRoutine != null)
            {
                StopCoroutine(scenarioRoutine);
                scenarioRoutine = null;
            }

            // NOTE: Wrong-way warnings are NOT disabled here
            // They stay active until the user reaches a safe zone (handled by FloodSafeZoneController)
            // or reaches an exit (handled by ARSafeWrongWayWarning.OnExitReached)
            // This ensures navigation guidance continues even after water stops rising
        }

        /// <summary>
        /// Start progression-based scenario with linear warning level escalation.
        /// </summary>
        private void StartProgressionRoutine(FloodWarningProgression progression)
        {
            StopScenarioRoutine();
            scenarioRoutine = StartCoroutine(RunProgressionScenario(progression));
        }

        private IEnumerator RunScenario(FloodScenarioParameters parameters)
        {
            float elapsed = 0f;

            // Track which messages have been shown to avoid duplicates
            bool risingStartMessageShown = false;
            bool risingMidMessageShown = false;
            bool sustainedMessageShown = false;

            BroadcastProgress(new FloodScenarioProgress(elapsed, 0f, FloodScenarioPhase.Rising, 0f, false));

            // Infinite loop - flood scenario runs forever until disaster type changes
            while (DisasterTypeManager.SelectedDisasterType == DisasterType.Flood)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var phase = ResolvePhase(parameters, elapsed, out float phaseNormalized);
                BroadcastProgress(new FloodScenarioProgress(elapsed, 0f, phase, phaseNormalized, false));

                // Show phase-specific safety warnings
                if (phase == FloodScenarioPhase.Rising)
                {
                    // Initial warning when water starts rising
                    if (!risingStartMessageShown && phaseNormalized >= 0.05f)
                    {
                        risingStartMessageShown = true;
                        MessageNotificationController.Instance?.ShowMessage(
                            "FLOOD WARNING: Water rising! Move to higher ground immediately!",
                            MessageType.Warning,
                            7f
                        );
                    }

                    // Urgent warning mid-way through rising phase
                    if (!risingMidMessageShown && phaseNormalized >= 0.5f)
                    {
                        risingMidMessageShown = true;
                        MessageNotificationController.Instance?.ShowMessage(
                            "DANGER: Water rising rapidly! Evacuate to upper floors NOW!",
                            MessageType.Warning,
                            6f
                        );
                    }
                }
                else if (phase == FloodScenarioPhase.Sustained)
                {
                    // Warning when water reaches peak level (water stays here FOREVER)
                    if (!sustainedMessageShown && phaseNormalized >= 0.01f)
                    {
                        sustainedMessageShown = true;
                        MessageNotificationController.Instance?.ShowMessage(
                            "FLOOD EMERGENCY: Water at peak level! Stay on upper floors! Water will NOT recede!",
                            MessageType.Warning,
                            10f
                        );
                    }
                }
            }

            // Only reaches here if user switches disaster type
            BroadcastProgress(new FloodScenarioProgress(elapsed, 0f, FloodScenarioPhase.Complete, 1f, true));
            BroadcastParameters(InactiveParameters);

            // NOTE: Warnings are disabled in StopScenarioRoutine when user switches disaster type

            scenarioRoutine = null;
        }

        /// <summary>
        /// Runs the progression-based flood scenario with linear warning level escalation.
        /// CRITICAL: This replaces RunScenario() for progression system.
        /// Handles time-based level transitions (Yellow → Orange → Red) with notifications.
        /// </summary>
        private IEnumerator RunProgressionScenario(FloodWarningProgression progression)
        {
            float scenarioStartTime = Time.time;
            int currentPhaseIndex = 0;
            RainfallWarningLevel previousLevel = RainfallWarningLevel.None;

            // Track notification messages to avoid duplicates
            bool initialWarningShown = false;

            if (logSelectedParameters)
            {
                Debug.Log($"<color=cyan>[FloodScenario] ★★★ Progression started → {progression.Phases.Count} phases, peak: {progression.PeakLevel}</color>");
            }

            // Infinite loop - progression runs until disaster type changes
            while (DisasterTypeManager.SelectedDisasterType == DisasterType.Flood && currentPhaseIndex < progression.Phases.Count)
            {
                yield return null;

                float elapsed = Time.time - scenarioStartTime;
                var currentPhase = progression.Phases[currentPhaseIndex];

                // Show initial warning once when scenario starts
                if (!initialWarningShown && elapsed >= 0.5f)
                {
                    initialWarningShown = true;
                    MessageNotificationController.Instance?.ShowMessage(
                        $"FLASH FLOOD WARNING: {GetWarningLevelLabel(currentPhase.Level)} - Water will rise gradually!",
                        MessageType.Warning,
                        7f
                    );
                }

                // Check if we need to transition to next phase
                float phaseEndTime = currentPhase.StartTime + currentPhase.Duration;
                if (elapsed >= phaseEndTime && currentPhaseIndex < progression.Phases.Count - 1)
                {
                    // Transition to next level
                    previousLevel = currentPhase.Level;
                    currentPhaseIndex++;
                    var nextPhase = progression.Phases[currentPhaseIndex];

                    if (logSelectedParameters)
                    {
                        Debug.Log($"<color=yellow>[FloodScenario] ⚠ Level transition → {previousLevel} to {nextPhase.Level} at {elapsed:F1}s</color>");
                    }

                    // Fire level change event
                    OnWarningLevelChanged?.Invoke(previousLevel, nextPhase.Level);

                    // Update parameters for new level (triggers water depth/speed changes)
                    UpdateParametersForLevel(nextPhase, progression);

                    // Show escalation notification
                    MessageNotificationController.Instance?.ShowMessage(
                        $"WARNING ESCALATED: {GetWarningLevelLabel(nextPhase.Level)} - Water rising faster!",
                        MessageType.Warning,
                        8f
                    );

                    // Wait a frame before continuing
                    yield return null;
                    continue;
                }

                // Broadcast progress (for potential UI widgets showing countdown)
                float phaseProgress = (elapsed - currentPhase.StartTime) / Mathf.Max(0.001f, currentPhase.Duration);
                phaseProgress = Mathf.Clamp01(phaseProgress);

                // Use existing progress system for compatibility
                var progress = new FloodScenarioProgress(
                    elapsed,
                    GetTotalDuration(progression),
                    FloodScenarioPhase.Rising,
                    phaseProgress,
                    false
                );
                BroadcastProgress(progress);
            }

            // Reached peak level - continue infinite sustain phase
            if (logSelectedParameters)
            {
                Debug.Log($"<color=cyan>[FloodScenario] Peak level reached: {progression.PeakLevel} - Sustaining indefinitely</color>");
            }

            // Show peak level notification
            MessageNotificationController.Instance?.ShowMessage(
                $"PEAK FLOOD LEVEL: {GetWarningLevelLabel(progression.PeakLevel)} - Water will remain at this level!",
                MessageType.Warning,
                10f
            );

            // Infinite sustain loop - water stays at peak level forever
            float peakStartTime = Time.time;
            while (DisasterTypeManager.SelectedDisasterType == DisasterType.Flood)
            {
                yield return null;

                // Broadcast sustained phase progress
                float sustainedElapsed = Time.time - peakStartTime;
                var sustainedProgress = new FloodScenarioProgress(
                    sustainedElapsed,
                    0f,
                    FloodScenarioPhase.Sustained,
                    1f, // 100% - at peak
                    false
                );
                BroadcastProgress(sustainedProgress);
            }

            // Only reaches here if user switches disaster type
            BroadcastProgress(new FloodScenarioProgress(0f, 0f, FloodScenarioPhase.Complete, 1f, true));
            BroadcastParameters(InactiveParameters);
            scenarioRoutine = null;
        }

        private FloodScenarioPhase ResolvePhase(FloodScenarioParameters parameters, float elapsed, out float phaseNormalized)
        {
            float riseEnd = parameters.RiseDurationSeconds;

            // Rising phase - water rising to peak level
            if (elapsed < riseEnd)
            {
                phaseNormalized = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, parameters.RiseDurationSeconds));

                if (logSelectedParameters && (phaseNormalized < 0.1f || phaseNormalized > 0.9f))
                {
                    Debug.Log($"<color=green>[FloodScenario] RISING: elapsed={elapsed:F2}s / {riseEnd:F2}s, normalized={phaseNormalized:F3}</color>");
                }

                return FloodScenarioPhase.Rising;
            }

            // Sustained phase - water stays at peak level FOREVER (no receding, no completion)
            // phaseNormalized = 1.0 indicates water is at peak and staying there
            phaseNormalized = 1f;

            if (logSelectedParameters && elapsed < riseEnd + 1f)
            {
                Debug.Log($"<color=cyan>[FloodScenario] SUSTAINED: Water at peak level (elapsed={elapsed:F2}s, riseEnd={riseEnd:F2}s)</color>");
            }

            return FloodScenarioPhase.Sustained;
        }

        private void BroadcastParameters(FloodScenarioParameters parameters)
        {
            CurrentParameters = parameters;
            OnParametersUpdated?.Invoke(parameters);
        }

        private void BroadcastProgress(FloodScenarioProgress progress)
        {
            CurrentProgress = progress;
            OnProgressUpdated?.Invoke(progress);
        }

        // ========== RAIN AUDIO CONTROL ==========

        /// <summary>
        /// Start playing rain ambient sound with fade-in.
        /// Called when flood scenario starts.
        /// </summary>
        private void PlayRain()
        {
            if (rainAudioSource == null)
            {
                return; // Silently skip if no AudioSource assigned
            }

            if (rainAudioSource.clip == null)
            {
                if (logSelectedParameters)
                {
                    Debug.LogWarning("[FloodScenarioManager] Rain AudioSource has no AudioClip assigned. Assign clip in Inspector.");
                }
                return;
            }

            if (!rainAudioSource.isPlaying)
            {
                rainAudioSource.volume = 0f;
                rainAudioSource.Play();

                if (logSelectedParameters)
                {
                    Debug.Log("<color=cyan>[FloodScenarioManager] Rain audio started</color>");
                }
            }

            // Fade in rain volume over 2 seconds
            StopAllCoroutines();
            StartCoroutine(FadeRainVolume(rainVolume, 2f));
        }

        /// <summary>
        /// Stop playing rain ambient sound with fade-out.
        /// Called when flood scenario ends or disaster type changes.
        /// </summary>
        private void StopRain()
        {
            if (rainAudioSource == null || !rainAudioSource.isPlaying)
            {
                return;
            }

            if (logSelectedParameters)
            {
                Debug.Log("<color=yellow>[FloodScenarioManager] Rain audio stopping (fade-out)</color>");
            }

            // Fade out rain volume, then stop
            StopAllCoroutines();
            StartCoroutine(FadeRainVolumeAndStop(rainFadeOutDuration));
        }

        /// <summary>
        /// Fade rain volume to target value over duration.
        /// </summary>
        private IEnumerator FadeRainVolume(float targetVolume, float duration)
        {
            if (rainAudioSource == null) yield break;

            float startVolume = rainAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rainAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                yield return null;
            }

            rainAudioSource.volume = targetVolume;
        }

        /// <summary>
        /// Fade out rain volume to zero, then stop audio.
        /// </summary>
        private IEnumerator FadeRainVolumeAndStop(float duration)
        {
            if (rainAudioSource == null) yield break;

            float startVolume = rainAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rainAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            rainAudioSource.volume = 0f;
            rainAudioSource.Stop();
        }

        // ========== TESTING UTILITIES ==========

        /// <summary>
        /// Start a test flood with custom parameters (useful for editor testing)
        /// </summary>
        public static void StartFloodWithParams(float depthMeters, float riseDuration, float holdDuration, float recedeDuration)
        {
            if (Instance == null) return;

            Instance.StopScenarioRoutine();

            // Generate warning level for test scenarios
            RainfallWarningLevel warningLevel = Instance.GenerateRandomWarningLevel();
            string warningLevelLabel = Instance.GetWarningLevelLabel(warningLevel);
            string warningMessage = Instance.GetWarningMessage(warningLevel);
            Color warningColor = Instance.GetWarningColor(warningLevel);

            var parameters = new FloodScenarioParameters(
                depthMeters,
                riseDuration,
                holdDuration,
                recedeDuration,
                1f, // wave amplitude multiplier
                1f, // flow speed multiplier
                0.7f, // turbidity
                Instance.deepWaterColor,
                Instance.shallowWaterColor,
                Instance.foamColor,
                warningLevel,
                warningLevelLabel,
                warningMessage,
                warningColor,
                true);

            Debug.Log($"<color=cyan>[FloodScenario] ★★★ TEST FLOOD STARTED → depth {depthMeters:F2}m, PAGASA {warningLevel} warning, rise {riseDuration:F1}s, hold {holdDuration:F1}s, recede {recedeDuration:F1}s</color>");

            Instance.BroadcastParameters(parameters);
            Instance.StartScenarioRoutine(parameters);
        }

        /// <summary>
        /// Start test flood using inspector test parameters
        /// </summary>
        [ContextMenu("Start Test Flood")]
        public void StartTestFlood()
        {
            StartFloodWithParams(testDepthMeters, testRiseDuration, testHoldDuration, testRecedeDuration);
        }

        /// <summary>
        /// Stop current flood scenario
        /// </summary>
        [ContextMenu("Stop Flood")]
        public void StopFlood()
        {
            StopScenarioRoutine();
            BroadcastParameters(InactiveParameters);
            BroadcastProgress(InactiveProgress);
            Debug.Log("<color=yellow>[FloodScenario] Flood scenario stopped manually</color>");
        }

        /// <summary>
        /// Quick test: 2m flood, fast timing
        /// </summary>
        [ContextMenu("Quick Test (2m, Fast)")]
        public void QuickTestFast()
        {
            StartFloodWithParams(2f, 5f, 3f, 4f);
        }

        /// <summary>
        /// Quick test: 1m flood, slow timing
        /// </summary>
        [ContextMenu("Quick Test (1m, Slow)")]
        public void QuickTestSlow()
        {
            StartFloodWithParams(1f, 20f, 10f, 15f);
        }

        /// <summary>
        /// Generate random PAGASA rainfall warning level with realistic distribution.
        /// Weighted: 20% Yellow, 40% Orange, 40% Red (for emergency scenarios in Bicol Region).
        /// </summary>
        private RainfallWarningLevel GenerateRandomWarningLevel()
        {
            float roll = UnityEngine.Random.value;
            if (roll < 0.20f) return RainfallWarningLevel.Yellow;
            if (roll < 0.60f) return RainfallWarningLevel.Orange;
            return RainfallWarningLevel.Red;
        }

        /// <summary>
        /// Get PAGASA warning level label
        /// </summary>
        private string GetWarningLevelLabel(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow:
                    return "YELLOW RAINFALL WARNING";
                case RainfallWarningLevel.Orange:
                    return "ORANGE RAINFALL WARNING";
                case RainfallWarningLevel.Red:
                    return "RED RAINFALL WARNING";
                default:
                    return "NO WARNING";
            }
        }

        /// <summary>
        /// Get contextual warning message for Iriga City / Bicol Region context (typhoon-prone area).
        /// Messages reference PAGASA official rainfall thresholds and USANT SGO Building context.
        /// Correlated with actual flood depths: Yellow (0.05-0.1m), Orange (0.1-0.2m), Red (0.2-0.3m)
        /// </summary>
        private string GetWarningMessage(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow:
                    return "PAGASA: 7.5-15mm/hr rainfall. Minor flooding in low-lying areas. Stay alert!";

                case RainfallWarningLevel.Orange:
                    return "PAGASA: 15-30mm/hr rainfall. Ankle-deep flooding expected. Move to 2nd floor NOW!";

                case RainfallWarningLevel.Red:
                    return "PAGASA: 30+mm/hr TORRENTIAL RAIN! Low shin-deep flooding! EVACUATE TO 2ND FLOOR IMMEDIATELY!";

                default:
                    return "Monitor weather conditions.";
            }
        }

        /// <summary>
        /// Get official PAGASA warning color (Yellow, Orange, Red)
        /// </summary>
        private Color GetWarningColor(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow:
                    return new Color(1f, 0.92f, 0.016f, 1f); // Bright yellow #FFEB04

                case RainfallWarningLevel.Orange:
                    return new Color(1f, 0.6f, 0f, 1f); // Orange #FF9900

                case RainfallWarningLevel.Red:
                    return new Color(0.9f, 0.1f, 0.1f, 1f); // Bright red #E61A1A

                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// Calculate total duration of all phases in a progression.
        /// </summary>
        private float GetTotalDuration(FloodWarningProgression progression)
        {
            float total = 0f;
            foreach (var phase in progression.Phases)
            {
                total += phase.Duration;
            }
            return total;
        }

        /// <summary>
        /// Builds a linear progression timeline from Yellow to the specified peak level.
        /// Each phase has randomized duration and correlates water depth/rise speed with warning level.
        /// </summary>
        private FloodWarningProgression BuildProgression(RainfallWarningLevel peakLevel)
        {
            var progression = new FloodWarningProgression { PeakLevel = peakLevel };

            // Phase 1: Yellow (always starts here)
            // 7.5-15mm/hr rainfall, barely ankle-deep flooding, very slow rise
            progression.Phases.Add(new WarningLevelPhase
            {
                Level = RainfallWarningLevel.Yellow,
                StartTime = 0f,
                Duration = UnityEngine.Random.Range(30f, 45f), // Yellow lasts 30-45s
                TargetDepth = UnityEngine.Random.Range(0.05f, 0.1f), // Barely ankle (0.05-0.1m / 5-10cm)
                RiseSpeed = 0.002f // Very slow rise (2mm/s)
            });

            // Phase 2: Orange (if peak is Orange or Red)
            // 15-30mm/hr rainfall, ankle-deep flooding, slow rise
            if (peakLevel >= RainfallWarningLevel.Orange)
            {
                var prevPhase = progression.Phases[progression.Phases.Count - 1];
                progression.Phases.Add(new WarningLevelPhase
                {
                    Level = RainfallWarningLevel.Orange,
                    StartTime = prevPhase.StartTime + prevPhase.Duration,
                    Duration = UnityEngine.Random.Range(30f, 45f), // Orange lasts 30-45s
                    TargetDepth = UnityEngine.Random.Range(0.1f, 0.2f), // Ankle-deep (0.1-0.2m / 10-20cm)
                    RiseSpeed = 0.003f // Slow rise (3mm/s)
                });
            }

            // Phase 3: Red (if peak is Red)
            // 30+mm/hr torrential rainfall, low shin-deep flooding, medium rise
            if (peakLevel == RainfallWarningLevel.Red)
            {
                var prevPhase = progression.Phases[progression.Phases.Count - 1];
                progression.Phases.Add(new WarningLevelPhase
                {
                    Level = RainfallWarningLevel.Red,
                    StartTime = prevPhase.StartTime + prevPhase.Duration,
                    Duration = UnityEngine.Random.Range(40f, 60f), // Red lasts 40-60s
                    TargetDepth = UnityEngine.Random.Range(0.2f, 0.3f), // Low shin (0.2-0.3m / 20-30cm)
                    RiseSpeed = 0.005f // Medium rise (5mm/s)
                });
            }

            return progression;
        }
    }

    /// <summary>
    /// Defines the progression path for a flood scenario with linear warning level escalation.
    /// Always starts at Yellow and escalates to a randomized peak level (Yellow, Orange, or Red).
    /// </summary>
    public class FloodWarningProgression
    {
        public RainfallWarningLevel StartLevel = RainfallWarningLevel.Yellow; // Always Yellow
        public RainfallWarningLevel PeakLevel; // Randomized: Yellow, Orange, or Red
        public List<WarningLevelPhase> Phases = new List<WarningLevelPhase>(); // Timeline of level transitions
    }

    /// <summary>
    /// Represents a single phase in the flood warning progression timeline.
    /// Each phase corresponds to one warning level (Yellow, Orange, or Red).
    /// </summary>
    public struct WarningLevelPhase
    {
        public RainfallWarningLevel Level;
        public float StartTime; // Seconds since scenario start
        public float Duration; // How long this level lasts (seconds)
        public float TargetDepth; // Water depth at end of this phase (meters)
        public float RiseSpeed; // Meters per second
    }

    public enum FloodScenarioPhase
    {
        Inactive = 0,
        Rising = 1,
        Sustained = 2,
        Receding = 3,
        Complete = 4,
    }

    public readonly struct FloodScenarioParameters
    {
        public FloodScenarioParameters(
            float targetDepthMeters,
            float riseDurationSeconds,
            float holdDurationSeconds,
            float recedeDurationSeconds,
            float waveAmplitudeMultiplier,
            float flowSpeedMultiplier,
            float turbidity,
            Color deepWaterColor,
            Color shallowWaterColor,
            Color foamColor,
            RainfallWarningLevel warningLevel,
            string warningLevelLabel,
            string warningMessage,
            Color warningColor,
            bool isActive)
        {
            TargetDepthMeters = Mathf.Max(0f, targetDepthMeters);
            RiseDurationSeconds = Mathf.Max(0f, riseDurationSeconds);
            HoldDurationSeconds = Mathf.Max(0f, holdDurationSeconds);
            RecedeDurationSeconds = Mathf.Max(0f, recedeDurationSeconds);
            WaveAmplitudeMultiplier = Mathf.Max(0f, waveAmplitudeMultiplier);
            FlowSpeedMultiplier = Mathf.Max(0f, flowSpeedMultiplier);
            Turbidity = Mathf.Clamp01(turbidity);
            DeepWaterColor = deepWaterColor;
            ShallowWaterColor = shallowWaterColor;
            FoamColor = foamColor;
            WarningLevel = warningLevel;
            WarningLevelLabel = warningLevelLabel ?? string.Empty;
            WarningMessage = warningMessage ?? string.Empty;
            WarningColor = warningColor;
            IsActive = isActive && TargetDepthMeters > 0f;
        }

        public float TargetDepthMeters { get; }
        public float RiseDurationSeconds { get; }
        public float HoldDurationSeconds { get; }
        public float RecedeDurationSeconds { get; }
        public float TotalDurationSeconds => RiseDurationSeconds + HoldDurationSeconds + RecedeDurationSeconds;
        public float WaveAmplitudeMultiplier { get; }
        public float FlowSpeedMultiplier { get; }
        public float Turbidity { get; }
        public Color DeepWaterColor { get; }
        public Color ShallowWaterColor { get; }
        public Color FoamColor { get; }
        public RainfallWarningLevel WarningLevel { get; }
        public string WarningLevelLabel { get; }
        public string WarningMessage { get; }
        public Color WarningColor { get; }
        public bool IsActive { get; }
    }

    public readonly struct FloodScenarioProgress
    {
        public FloodScenarioProgress(
            float elapsedSeconds,
            float durationSeconds,
            FloodScenarioPhase phase,
            float phaseNormalized,
            bool completed)
        {
            DurationSeconds = Mathf.Max(0f, durationSeconds);
            ElapsedSeconds = Mathf.Clamp(elapsedSeconds, 0f, DurationSeconds);
            Phase = phase;
            PhaseNormalized = Mathf.Clamp01(phaseNormalized);
            OverallNormalized = DurationSeconds > Mathf.Epsilon ? Mathf.Clamp01(ElapsedSeconds / DurationSeconds) : 0f;
            IsComplete = completed || phase == FloodScenarioPhase.Complete;
            IsActive = !IsComplete && phase != FloodScenarioPhase.Inactive;
        }

        public float DurationSeconds { get; }
        public float ElapsedSeconds { get; }
        public float PhaseNormalized { get; }
        public float OverallNormalized { get; }
        public FloodScenarioPhase Phase { get; }
        public bool IsActive { get; }
        public bool IsComplete { get; }
    }
}
