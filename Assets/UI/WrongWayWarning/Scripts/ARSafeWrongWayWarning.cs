using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;

namespace ARSafe.UI
{
    /*
     * PURPOSE: Visual and audio warning when user moves away from nearest exit
     * DEPENDENCIES: ARSafeNavigationValidator, UIDocument, AudioSource, ARSafeSettings
     * DATA FLOW: Navigator event → Show border + play audio → Auto-hide when corrected
     * PERFORMANCE: Instant UI updates, audio cooldown prevents spam
     * EDGE CASES: No UIDocument, no AudioSource, scenarios disabled
     */

    /// <summary>
    /// Displays red pulsing screen border and plays audio alert when user is moving
    /// away from the nearest exit during emergency scenarios.
    /// Integrates with ARSafeNavigationValidator for wrong-way detection.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ARSafeWrongWayWarning : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("UIDocument component (auto-assigned)")]
        private UIDocument uiDocument;

        [Tooltip("Navigation validator (auto-found)")]
        private ARSafeNavigationValidator navigationValidator;

        [Header("Audio Settings")]
        [Tooltip("Audio source for wrong-way alert sound")]
        public AudioSource wrongWayAudioSource;

        [Tooltip("Cooldown between audio alerts (seconds)")]
        [Range(1f, 10f)]
        public float audioCooldown = 5f;

        [Header("Visual Settings")]
        [Tooltip("Border fade in duration (seconds)")]
        [Range(0.1f, 1f)]
        public float fadeInDuration = 0.3f;

        [Tooltip("Border fade out duration (seconds)")]
        [Range(0.1f, 1f)]
        public float fadeOutDuration = 0.5f;

        [Header("Grace Period")]
        [Tooltip("Grace period after scenario starts (seconds) - warnings won't trigger during this time. Lower = faster warning.")]
        [Range(2f, 30f)]
        public float scenarioGracePeriod = 5f;

        [Header("Debug")]
        public bool enableDebugLogs = false;

        // UI Elements
        private VisualElement rootElement;
        private VisualElement warningOverlay;
        private VisualElement wrongWayMessage;
        private List<VisualElement> warningBorders;

        // State
        private bool isWarningActive = false;
        private bool isEnabled = false;
        private float lastAudioPlayTime = -999f;
        private Coroutine pulseAnimationRoutine;
        private float scenarioStartTime = -999f;

        // Debounce to prevent rapid flickering when user near threshold angle
        private float lastStateChangeTime = -999f;
        private const float stateChangeDebounce = 0.5f;

        // Singleton for easy access from scenario managers
        public static ARSafeWrongWayWarning Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            uiDocument = GetComponent<UIDocument>();
            navigationValidator = FindFirstObjectByType<ARSafeNavigationValidator>();

            // Defensive audio initialization - force correct settings regardless of Inspector configuration
            if (wrongWayAudioSource != null)
            {
                wrongWayAudioSource.playOnAwake = false;  // Prevent auto-play on scene load
                wrongWayAudioSource.loop = false;         // One-shot alert sound
                wrongWayAudioSource.Stop();               // Ensure stopped initially

                if (enableDebugLogs)
                {
                    Debug.Log("[ARSafeWrongWayWarning] AudioSource initialized: playOnAwake=false, loop=false, stopped");
                }
            }

            if (navigationValidator == null)
            {
                Debug.LogError("[ARSafeWrongWayWarning] No ARSafeNavigationValidator found in scene!");
            }
        }

        void OnEnable()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[ARSafeWrongWayWarning] UIDocument or root visual element is null!");
                return;
            }

            SetupUI();
            SubscribeToNavigator();
        }

        void OnDisable()
        {
            UnsubscribeFromNavigator();

            // Hide warning when component is disabled (scenario ends, disaster type changes, etc.)
            HideWarning();
        }

        /// <summary>
        /// Setup UI elements
        /// </summary>
        private void SetupUI()
        {
            rootElement = uiDocument.rootVisualElement;
            warningOverlay = rootElement.Q<VisualElement>("wrong-way-warning-overlay");

            if (warningOverlay == null)
            {
                Debug.LogError("[ARSafeWrongWayWarning] 'wrong-way-warning-overlay' not found in UXML!");
                return;
            }

            // Collect all border elements for pulsing animation
            warningBorders = new List<VisualElement>();
            warningBorders.Add(warningOverlay.Q<VisualElement>(null, "warning-border--top"));
            warningBorders.Add(warningOverlay.Q<VisualElement>(null, "warning-border--right"));
            warningBorders.Add(warningOverlay.Q<VisualElement>(null, "warning-border--bottom"));
            warningBorders.Add(warningOverlay.Q<VisualElement>(null, "warning-border--left"));

            // Get center warning message
            wrongWayMessage = warningOverlay.Q<VisualElement>("wrong-way-message");
            if (wrongWayMessage == null)
            {
                Debug.LogWarning("[ARSafeWrongWayWarning] 'wrong-way-message' not found in UXML! Center message will not be shown.");
            }

            // Initially hidden
            warningOverlay.style.display = DisplayStyle.None;
            warningOverlay.style.opacity = 0f;
        }

        /// <summary>
        /// Subscribe to navigation validator events
        /// </summary>
        private void SubscribeToNavigator()
        {
            if (navigationValidator != null)
            {
                navigationValidator.OnWrongWayStatusChanged += OnWrongWayStatusChanged;
                navigationValidator.OnExitReached += OnExitReached;
                navigationValidator.OnVirtualExitReached += OnVirtualExitReached;

                if (enableDebugLogs)
                {
                    Debug.Log("[ARSafeWrongWayWarning] Subscribed to navigation validator (including virtual exits)");
                }
            }
        }

        /// <summary>
        /// Unsubscribe from navigation validator events
        /// </summary>
        private void UnsubscribeFromNavigator()
        {
            if (navigationValidator != null)
            {
                navigationValidator.OnWrongWayStatusChanged -= OnWrongWayStatusChanged;
                navigationValidator.OnExitReached -= OnExitReached;
                navigationValidator.OnVirtualExitReached -= OnVirtualExitReached;
            }
        }

        /// <summary>
        /// Handle wrong-way status change event from navigator
        /// </summary>
        private void OnWrongWayStatusChanged(bool movingWrongWay)
        {
            // Check if warnings are enabled in settings
            if (ARSafeSettings.Instance != null && !ARSafeSettings.Instance.WrongWayWarnings)
            {
                // Settings disabled - hide any active warning and return
                if (isWarningActive)
                {
                    HideWarning();
                }
                return;
            }

            if (!isEnabled)
            {
                // Warnings disabled - don't show
                return;
            }

            // Check if still in grace period
            float timeSinceStart = Time.time - scenarioStartTime;
            if (timeSinceStart < scenarioGracePeriod)
            {
                if (enableDebugLogs && movingWrongWay)
                {
                    Debug.Log($"<color=gray>[ARSafeWrongWayWarning] Grace period active ({timeSinceStart:F1}s / {scenarioGracePeriod}s) - warnings suppressed</color>");
                }
                return;
            }

            if (movingWrongWay)
            {
                ShowWarning();
            }
            else
            {
                HideWarning();
            }
        }

        /// <summary>
        /// Handle exit reached event from navigator
        /// </summary>
        private void OnExitReached(ARSafe.Modular.ARSafeTargetInfo exitTarget)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeWrongWayWarning] ✓ User reached exit: {(exitTarget != null ? exitTarget.name : "Virtual Exit")} - Disabling warnings</color>");
            }

            // User successfully evacuated - disable warnings
            DisableWarnings();
        }

        /// <summary>
        /// Handle virtual exit reached event from navigator
        /// </summary>
        private void OnVirtualExitReached(ARSafe.Content.VirtualExitMarker exitMarker)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeWrongWayWarning] ✓ User reached virtual exit: {exitMarker.gameObject.name} - Disabling warnings</color>");
            }

            // User successfully reached virtual exit - disable warnings
            DisableWarnings();
        }

        /// <summary>
        /// Show red border warning and play audio
        /// </summary>
        private void ShowWarning()
        {
            // Debounce check - prevent rapid show/hide cycles
            if (Time.time - lastStateChangeTime < stateChangeDebounce)
            {
                return;
            }

            if (isWarningActive)
            {
                // Already showing
                return;
            }

            if (warningOverlay == null)
            {
                return;
            }

            lastStateChangeTime = Time.time;
            isWarningActive = true;

            // Show border with fade in
            warningOverlay.style.display = DisplayStyle.Flex;
            warningOverlay.style.opacity = 0f;

            // Show center warning message
            if (wrongWayMessage != null)
            {
                wrongWayMessage.style.display = DisplayStyle.Flex;
            }

            // Fade in animation
            warningOverlay.schedule.Execute(() =>
            {
                warningOverlay.style.opacity = 1f;
            }).StartingIn((long)(fadeInDuration * 1000f));

            // Start pulsing animation
            if (pulseAnimationRoutine != null)
            {
                StopCoroutine(pulseAnimationRoutine);
            }
            pulseAnimationRoutine = StartCoroutine(PulseAnimation());

            // Play audio alert (with cooldown)
            PlayAudioAlert();

            if (enableDebugLogs)
            {
                Debug.Log("<color=red>[ARSafeWrongWayWarning] ⚠ WRONG WAY WARNING SHOWN!</color>");
            }
        }

        /// <summary>
        /// Hide red border warning
        /// </summary>
        private void HideWarning()
        {
            // Debounce check - prevent rapid show/hide cycles
            if (Time.time - lastStateChangeTime < stateChangeDebounce)
            {
                return;
            }

            if (!isWarningActive)
            {
                // Already hidden
                return;
            }

            if (warningOverlay == null)
            {
                return;
            }

            lastStateChangeTime = Time.time;
            isWarningActive = false;

            // Hide center warning message
            if (wrongWayMessage != null)
            {
                wrongWayMessage.style.display = DisplayStyle.None;
            }

            // Stop pulsing animation
            if (pulseAnimationRoutine != null)
            {
                StopCoroutine(pulseAnimationRoutine);
                pulseAnimationRoutine = null;
            }

            // Fade out animation
            warningOverlay.style.opacity = 1f;
            warningOverlay.schedule.Execute(() =>
            {
                warningOverlay.style.opacity = 0f;
                warningOverlay.schedule.Execute(() =>
                {
                    warningOverlay.style.display = DisplayStyle.None;
                }).StartingIn((long)(fadeOutDuration * 1000f));
            });

            if (enableDebugLogs)
            {
                Debug.Log("<color=green>[ARSafeWrongWayWarning] ✓ Warning hidden - back on track</color>");
            }
        }

        /// <summary>
        /// Play audio alert with cooldown
        /// </summary>
        private void PlayAudioAlert()
        {
            if (wrongWayAudioSource == null || wrongWayAudioSource.clip == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[ARSafeWrongWayWarning] No audio source or clip assigned!");
                }
                return;
            }

            // Check cooldown
            float timeSinceLastPlay = Time.time - lastAudioPlayTime;
            if (timeSinceLastPlay < audioCooldown)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ARSafeWrongWayWarning] Audio on cooldown ({timeSinceLastPlay:F1}s / {audioCooldown}s)");
                }
                return;
            }

            // Apply SFX volume from settings
            if (ARSafe.ARSafeSettings.Instance != null)
            {
                // Audio source volume should already be managed by ARSafeSettings
                // Just play the sound
            }

            wrongWayAudioSource.Play();
            lastAudioPlayTime = Time.time;

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeWrongWayWarning] 🔊 Audio alert played");
            }
        }

        /// <summary>
        /// Enable wrong-way warnings (call from scenario managers)
        /// </summary>
        public void EnableWarnings()
        {
            isEnabled = true;
            scenarioStartTime = Time.time;

            // Reset exit state for new scenario
            if (navigationValidator != null)
            {
                navigationValidator.ResetExitState();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeWrongWayWarning] Warnings ENABLED (exit state reset, {scenarioGracePeriod}s grace period started)</color>");
            }
        }

        /// <summary>
        /// Disable wrong-way warnings (call from scenario managers)
        /// </summary>
        public void DisableWarnings()
        {
            isEnabled = false;

            // Hide any active warning
            if (isWarningActive)
            {
                HideWarning();
            }

            // CRITICAL: Reset grace period timer so next scenario starts fresh
            scenarioStartTime = -999f;

            if (enableDebugLogs)
            {
                Debug.Log("<color=gray>[ARSafeWrongWayWarning] Warnings DISABLED (grace period timer reset)</color>");
            }
        }

        /// <summary>
        /// Check if warnings are currently enabled
        /// </summary>
        public bool IsEnabled => isEnabled;

        /// <summary>
        /// Check if warning is currently showing
        /// </summary>
        public bool IsWarningActive => isWarningActive;

        /// <summary>
        /// Coroutine to animate border pulsing (bright/dim cycle)
        /// Unity UI Toolkit doesn't support CSS @keyframes, so we animate in C#
        /// </summary>
        private IEnumerator PulseAnimation()
        {
            if (warningBorders == null || warningBorders.Count == 0)
            {
                yield break;
            }

            // Animation parameters
            Color dimColor = new Color(255f / 255f, 50f / 255f, 50f / 255f); // rgb(255, 50, 50)
            Color brightColor = new Color(255f / 255f, 80f / 255f, 80f / 255f); // rgb(255, 80, 80)
            float dimOpacity = 0.7f;
            float brightOpacity = 1.0f;
            float pulseDuration = 0.75f; // Time for half cycle (dim→bright or bright→dim)

            bool isPulsing = true; // true = going to bright, false = going to dim

            while (true)
            {
                // Determine start and end states
                Color startColor = isPulsing ? dimColor : brightColor;
                Color endColor = isPulsing ? brightColor : dimColor;
                float startOpacity = isPulsing ? dimOpacity : brightOpacity;
                float endOpacity = isPulsing ? brightOpacity : dimOpacity;

                // Smooth lerp over pulseDuration using Time.deltaTime
                float elapsed = 0f;
                while (elapsed < pulseDuration)
                {
                    float t = elapsed / pulseDuration;
                    Color lerpedColor = Color.Lerp(startColor, endColor, t);
                    float lerpedOpacity = Mathf.Lerp(startOpacity, endOpacity, t);

                    // Apply to all borders EVERY FRAME for smooth animation
                    foreach (var border in warningBorders)
                    {
                        if (border != null)
                        {
                            border.style.backgroundColor = lerpedColor;
                            border.style.opacity = lerpedOpacity;
                        }
                    }

                    elapsed += Time.deltaTime;
                    yield return null; // Wait one frame
                }

                // Ensure final state is exactly reached
                foreach (var border in warningBorders)
                {
                    if (border != null)
                    {
                        border.style.backgroundColor = endColor;
                        border.style.opacity = endOpacity;
                    }
                }

                // Toggle state for next cycle
                isPulsing = !isPulsing;
            }
        }
    }
}
