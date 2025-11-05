using System;
using System.Collections;
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
            float depth = Mathf.Max(0.1f, UnityEngine.Random.Range(depthRange.x, depthRange.y));
            float riseDuration = Mathf.Max(4f, UnityEngine.Random.Range(riseDurationRange.x, riseDurationRange.y));
            float holdDuration = Mathf.Max(4f, UnityEngine.Random.Range(holdDurationRange.x, holdDurationRange.y));
            float recedeDuration = Mathf.Max(4f, UnityEngine.Random.Range(recedeDurationRange.x, recedeDurationRange.y));

            float waveMultiplier = Mathf.Lerp(waveAmplitudeMultiplierRange.x, waveAmplitudeMultiplierRange.y, UnityEngine.Random.value);
            float flowMultiplier = Mathf.Lerp(flowSpeedMultiplierRange.x, flowSpeedMultiplierRange.y, UnityEngine.Random.value);
            float turbidity = Mathf.Lerp(turbidityRange.x, turbidityRange.y, UnityEngine.Random.value);

            // Generate PAGASA rainfall warning level
            RainfallWarningLevel warningLevel = GenerateRandomWarningLevel();
            string warningLevelLabel = GetWarningLevelLabel(warningLevel);
            string warningMessage = GetWarningMessage(warningLevel);
            Color warningColor = GetWarningColor(warningLevel);

            var parameters = new FloodScenarioParameters(
                depth,
                riseDuration,
                holdDuration,
                recedeDuration,
                waveMultiplier,
                flowMultiplier,
                turbidity,
                deepWaterColor,
                shallowWaterColor,
                foamColor,
                warningLevel,
                warningLevelLabel,
                warningMessage,
                warningColor,
                true);

            if (logSelectedParameters)
            {
                Debug.Log($"<color=cyan>[FloodScenario] ★★★ Flood params → depth {depth:F2}m, PAGASA {warningLevel} warning, rise {riseDuration:F1}s, hold {holdDuration:F1}s, recede {recedeDuration:F1}s</color>");
            }

            BroadcastParameters(parameters);

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

            StartScenarioRoutine(parameters);
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
        /// </summary>
        private string GetWarningMessage(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow:
                    return "PAGASA: 7.5-15mm/hr rainfall. Slight flooding possible in low-lying areas. Stay alert and monitor conditions!";

                case RainfallWarningLevel.Orange:
                    return "PAGASA: 15-30mm/hr rainfall. Flooding expected near rivers. Prepare to evacuate to upper floors!";

                case RainfallWarningLevel.Red:
                    return "PAGASA: 30+mm/hr TORRENTIAL RAIN! Serious flooding imminent in SGO Building area. EVACUATE TO 2ND FLOOR NOW!";

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
