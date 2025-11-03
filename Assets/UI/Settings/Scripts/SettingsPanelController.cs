/*
 * ARCHITECTURE PLAN: SettingsPanelController
 *
 * PURPOSE:
 *   - Control the Settings Panel UI (show/hide, update values)
 *   - Connect UI elements to ARSafeSettings persistence system
 *   - Provide public API for showing settings from anywhere (UGUI button, etc.)
 *   - Apply mobile-first interaction patterns (large touch targets)
 *
 * DEPENDENCIES:
 *   - Unity APIs: UIToolkit (UIDocument, VisualElement, Button, Slider, Toggle)
 *   - Project Scripts: ARSafeSettings
 *
 * DATA FLOW:
 *   - ShowSettings() → Make overlay visible → Load current values
 *   - User changes slider/toggle → Update ARSafeSettings → Save to PlayerPrefs
 *   - Apply button → Save all → Hide panel
 *   - Close button → Discard changes → Hide panel
 *
 * PERFORMANCE:
 *   - Cache all UI element queries in OnEnable
 *   - Update UI only when values change (not every frame)
 *   - Debounce slider changes (apply after release, not continuous)
 */

using UnityEngine;
using UnityEngine.UIElements;

namespace ARSafe.UI
{
    /// <summary>
    /// Controls the Settings Panel UI Toolkit overlay.
    /// Can be shown from UGUI buttons or other UI systems.
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument uiDocument;

        [Header("Settings")]
        [SerializeField] private bool enableDebugLogs = true;

    [Header("Build Gating")]
    [Tooltip("Hide the entire Settings panel in non-development builds (kept visible in Editor).")]
    [SerializeField] private bool developmentOnlyEntirePanel = false;
    [Tooltip("Hide the Developer section in non-development builds (kept visible in Editor).")]
    [SerializeField] private bool developmentOnlyDebugSection = true;

        // Root elements
        private VisualElement root;
        private VisualElement overlay;
        private Button closeButton;

        // Audio elements
        private Slider masterVolumeSlider;
        private Label masterVolumeValue;
        private Slider sfxVolumeSlider;
        private Label sfxVolumeValue;
        private Slider uiVolumeSlider;
        private Label uiVolumeValue;

        // Graphics elements
        private Button qualityLowButton;
        private Button qualityMediumButton;

        // AR elements
        private Slider driftFPSSlider;
        private Label driftFPSValue;
        private Toggle hapticToggle;
        private Toggle wrongWayWarningsToggle;

        // Debug elements
        private Toggle debugOverlayToggle;
        private Toggle performanceMonitoringToggle;

        // Footer elements
        private Button resetButton;
        private Button applyButton;
    private VisualElement debugSection;
    private VisualElement devBadge;

        private bool isInitialized = false;

        #region Unity Lifecycle

        void Awake()
        {
            // Ensure settings manager exists
            if (ARSafeSettings.Instance == null)
            {
                Debug.LogWarning("[SettingsPanelController] ARSafeSettings not found in scene!");
            }

            // Auto-find UIDocument if not assigned
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
                if (uiDocument == null)
                {
                    Debug.LogError("[SettingsPanelController] UIDocument component not found!");
                    return;
                }
            }
        }

        void OnEnable()
        {
            if (!isInitialized)
            {
                InitializeUI();
            }
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        private void InitializeUI()
        {
            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[SettingsPanelController] Root visual element is null!");
                return;
            }

            // Get elements
            overlay = root.Q<VisualElement>("settings-overlay");
            closeButton = root.Q<Button>("close-button");

            // Audio
            masterVolumeSlider = root.Q<Slider>("master-volume-slider");
            masterVolumeValue = root.Q<Label>("master-volume-value");
            sfxVolumeSlider = root.Q<Slider>("sfx-volume-slider");
            sfxVolumeValue = root.Q<Label>("sfx-volume-value");
            uiVolumeSlider = root.Q<Slider>("ui-volume-slider");
            uiVolumeValue = root.Q<Label>("ui-volume-value");

            // Graphics
            qualityLowButton = root.Q<Button>("quality-low");
            qualityMediumButton = root.Q<Button>("quality-medium");

            // AR
            driftFPSSlider = root.Q<Slider>("drift-fps-slider");
            driftFPSValue = root.Q<Label>("drift-fps-value");
            hapticToggle = root.Q<Toggle>("haptic-toggle");
            wrongWayWarningsToggle = root.Q<Toggle>("wrong-way-warnings-toggle");

            // Debug
            debugOverlayToggle = root.Q<Toggle>("debug-overlay-toggle");
            performanceMonitoringToggle = root.Q<Toggle>("performance-monitoring-toggle");
            debugSection = root.Q<VisualElement>("debug-section");
            devBadge = root.Q<VisualElement>("dev-badge");

            // Footer
            resetButton = root.Q<Button>("reset-button");
            applyButton = root.Q<Button>("apply-button");

            // Subscribe to events
            SubscribeToEvents();

            // Hide panel by default
            HideSettings();

            // Apply build gating (development vs release)
            ApplyBuildGating();

            isInitialized = true;

            if (enableDebugLogs)
            {
                Debug.Log("<color=green>[SettingsPanelController] UI initialized successfully</color>");
            }
        }

        #endregion

        #region Event Subscription

        private void SubscribeToEvents()
        {
            // Close/Apply
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (applyButton != null) applyButton.clicked += OnApplyClicked;
            if (resetButton != null) resetButton.clicked += OnResetClicked;

            // Audio sliders
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.RegisterValueChangedCallback(OnMasterVolumeChanged);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.RegisterValueChangedCallback(OnSFXVolumeChanged);
            }
            if (uiVolumeSlider != null)
            {
                uiVolumeSlider.RegisterValueChangedCallback(OnUIVolumeChanged);
            }

            // Graphics quality buttons
            if (qualityLowButton != null) qualityLowButton.clicked += () => OnGraphicsQualityClicked(0);
            if (qualityMediumButton != null) qualityMediumButton.clicked += () => OnGraphicsQualityClicked(1);

            // AR settings
            if (driftFPSSlider != null)
            {
                driftFPSSlider.RegisterValueChangedCallback(OnDriftFPSChanged);
            }
            if (hapticToggle != null)
            {
                hapticToggle.RegisterValueChangedCallback(evt => OnHapticToggled(evt.newValue));
            }
            if (wrongWayWarningsToggle != null)
            {
                wrongWayWarningsToggle.RegisterValueChangedCallback(evt => OnWrongWayWarningsToggled(evt.newValue));
            }

            // Debug toggles
            if (debugOverlayToggle != null)
            {
                debugOverlayToggle.RegisterValueChangedCallback(evt => OnDebugOverlayToggled(evt.newValue));
            }

            if (performanceMonitoringToggle != null)
            {
                performanceMonitoringToggle.RegisterValueChangedCallback(evt => OnPerformanceMonitoringToggled(evt.newValue));
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (closeButton != null) closeButton.clicked -= OnCloseClicked;
            if (applyButton != null) applyButton.clicked -= OnApplyClicked;
            if (resetButton != null) resetButton.clicked -= OnResetClicked;

            // Note: ValueChanged callbacks don't need manual unsubscription in UI Toolkit
        }

        #endregion

        #region Public API

        /// <summary>
        /// Show the settings panel and load current values
        /// Call this from UGUI button or other scripts
        /// </summary>
        public void ShowSettings()
        {
            // Respect build gating – prevent opening the panel in release if requested
            if (developmentOnlyEntirePanel && !IsDevelopment())
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[SettingsPanelController] Settings panel hidden in non-development build.");
                }
                return;
            }

            if (overlay == null)
            {
                Debug.LogError("[SettingsPanelController] Cannot show settings - overlay is null!");
                return;
            }

            LoadCurrentSettings();
            // Show overlay with proper interaction properties (mobile pattern)
            overlay.AddToClassList("overlay--visible"); // keep class for CSS display
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.opacity = 0f;
            overlay.pickingMode = PickingMode.Position;

            // Fade in and focus for immediate input
            overlay.schedule.Execute(() =>
            {
                overlay.style.opacity = 1f;
                overlay.Focus();
            }).StartingIn(50);

            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[SettingsPanelController] Settings panel shown</color>");
            }
        }

        /// <summary>
        /// Hide the settings panel
        /// </summary>
        public void HideSettings()
        {
            if (overlay == null) return;

            overlay.RemoveFromClassList("overlay--visible");
            overlay.style.opacity = 0f;
            overlay.schedule.Execute(() =>
            {
                overlay.style.display = DisplayStyle.None;
                overlay.pickingMode = PickingMode.Ignore;
            }).StartingIn(200);

            if (enableDebugLogs)
            {
                Debug.Log("<color=gray>[SettingsPanelController] Settings panel hidden</color>");
            }
        }

        #endregion

        #region Build Gating

        private bool IsDevelopment()
        {
#if UNITY_EDITOR
            return true;
#else
            return Debug.isDebugBuild;
#endif
        }

        private void ApplyBuildGating()
        {
            bool isDev = IsDevelopment();

            // Developer section visibility
            if (debugSection != null)
            {
                if (developmentOnlyDebugSection && !isDev)
                {
                    debugSection.style.display = DisplayStyle.None;
                    debugSection.style.opacity = 0f;
                    debugSection.pickingMode = PickingMode.Ignore;
                }
                else
                {
                    // Default: visible
                    debugSection.style.display = DisplayStyle.Flex;
                    debugSection.style.opacity = 1f;
                    debugSection.pickingMode = PickingMode.Position;
                }
            }

            // DEV badge in header (visible only in development/editor)
            if (devBadge != null)
            {
                if (isDev)
                {
                    devBadge.style.display = DisplayStyle.Flex;
                    devBadge.style.opacity = 1f;
                    devBadge.pickingMode = PickingMode.Ignore; // decorative only
                }
                else
                {
                    devBadge.style.display = DisplayStyle.None;
                    devBadge.style.opacity = 0f;
                    devBadge.pickingMode = PickingMode.Ignore;
                }
            }
        }

        #endregion

        #region Load Settings

        private void LoadCurrentSettings()
        {
            if (ARSafeSettings.Instance == null) return;

            var settings = ARSafeSettings.Instance;

            // Audio
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(settings.MasterVolume);
                UpdateVolumeLabel(masterVolumeValue, settings.MasterVolume);
            }
            if (sfxVolumeSlider != null)
            {
                sfxVolumeSlider.SetValueWithoutNotify(settings.SFXVolume);
                UpdateVolumeLabel(sfxVolumeValue, settings.SFXVolume);
            }
            if (uiVolumeSlider != null)
            {
                uiVolumeSlider.SetValueWithoutNotify(settings.UIVolume);
                UpdateVolumeLabel(uiVolumeValue, settings.UIVolume);
            }

            // Graphics
            UpdateGraphicsQualityButtons(settings.GraphicsQuality);

            // AR
            if (driftFPSSlider != null)
            {
                driftFPSSlider.SetValueWithoutNotify(settings.DriftCorrectionFPS);
                UpdateFPSLabel(driftFPSValue, settings.DriftCorrectionFPS);
            }
            if (hapticToggle != null)
            {
                hapticToggle.SetValueWithoutNotify(settings.HapticFeedback);
            }
            if (wrongWayWarningsToggle != null)
            {
                wrongWayWarningsToggle.SetValueWithoutNotify(settings.WrongWayWarnings);
            }

            // Debug
            if (debugOverlayToggle != null)
            {
                debugOverlayToggle.SetValueWithoutNotify(settings.DebugOverlay);
            }

            if (performanceMonitoringToggle != null)
            {
                performanceMonitoringToggle.SetValueWithoutNotify(ARSafeSettings.PerformanceMonitoring);
            }
        }

        #endregion

        #region Event Handlers

        private void OnCloseClicked()
        {
            HideSettings();
        }

        private void OnApplyClicked()
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SaveSettings();

                if (enableDebugLogs)
                {
                    Debug.Log("<color=green>[SettingsPanelController] Settings applied and saved</color>");
                }
            }

            HideSettings();
        }

        private void OnResetClicked()
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.ResetToDefaults();
                LoadCurrentSettings(); // Refresh UI

                if (enableDebugLogs)
                {
                    Debug.Log("<color=yellow>[SettingsPanelController] Settings reset to defaults</color>");
                }
            }
        }

        // Audio
        private void OnMasterVolumeChanged(ChangeEvent<float> evt)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetMasterVolume(evt.newValue);
                UpdateVolumeLabel(masterVolumeValue, evt.newValue);
            }
        }

        private void OnSFXVolumeChanged(ChangeEvent<float> evt)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetSFXVolume(evt.newValue);
                UpdateVolumeLabel(sfxVolumeValue, evt.newValue);
            }
        }

        private void OnUIVolumeChanged(ChangeEvent<float> evt)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetUIVolume(evt.newValue);
                UpdateVolumeLabel(uiVolumeValue, evt.newValue);
            }
        }

        // Graphics
        private void OnGraphicsQualityClicked(int level)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetGraphicsQuality(level);
                UpdateGraphicsQualityButtons(level);
            }
        }

        // AR
        private void OnDriftFPSChanged(ChangeEvent<float> evt)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetDriftCorrectionFPS(evt.newValue);
                UpdateFPSLabel(driftFPSValue, evt.newValue);
            }
        }

        private void OnHapticToggled(bool enabled)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetHapticFeedback(enabled);
            }
        }

        private void OnWrongWayWarningsToggled(bool enabled)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetWrongWayWarnings(enabled);
            }
        }

        // Debug
        private void OnDebugOverlayToggled(bool enabled)
        {
            if (ARSafeSettings.Instance != null)
            {
                ARSafeSettings.Instance.SetDebugOverlay(enabled);
            }
        }

        private void OnPerformanceMonitoringToggled(bool enabled)
        {
            ARSafeSettings.PerformanceMonitoring = enabled;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[SettingsPanelController]</color> Performance monitoring: {(enabled ? "ON" : "OFF")}");
            }
        }

        #endregion

        #region UI Update Helpers

        private void UpdateVolumeLabel(Label label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
        }

        private void UpdateFPSLabel(Label label, float value)
        {
            if (label != null)
            {
                label.text = $"{Mathf.RoundToInt(value)} FPS";
            }
        }

        private void UpdateGraphicsQualityButtons(int level)
        {
            qualityLowButton?.RemoveFromClassList("quality-selected");
            qualityMediumButton?.RemoveFromClassList("quality-selected");

            if (level == 0) qualityLowButton?.AddToClassList("quality-selected");
            else if (level == 1) qualityMediumButton?.AddToClassList("quality-selected");
        }

        #endregion
    }
}
