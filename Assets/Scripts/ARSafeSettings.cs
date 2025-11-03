/*
 * ARCHITECTURE PLAN: ARSafeSettings
 *
 * PURPOSE:
 *   - Centralized settings management with PlayerPrefs persistence
 *   - Provides global access to user preferences
 *   - Applies settings to AR system and other components
 *   - Mobile-optimized defaults for AR performance
 *
 * DEPENDENCIES:
 *   - Unity APIs: PlayerPrefs, QualitySettings, AudioListener
 *   - Project Scripts: ARSafeActivationController, ARDebugLogger
 *
 * DATA FLOW:
 *   - OnEnable → Load settings from PlayerPrefs
 *   - User changes setting → Save to PlayerPrefs → Apply immediately
 *   - Other scripts → Access via ARSafeSettings.Instance
 *
 * PERFORMANCE:
 *   - Settings applied once on load and on change (not every frame)
 *   - PlayerPrefs I/O minimized (batch saves)
 */

using UnityEngine;

namespace ARSafe
{
    /// <summary>
    /// Singleton settings manager for ARSAFE application.
    /// Handles persistence, loading, and applying of user preferences.
    /// </summary>
    public class ARSafeSettings : MonoBehaviour
    {
        private static ARSafeSettings instance;
        public static ARSafeSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ARSafeSettings>();
                    if (instance == null)
                    {
                        GameObject settingsObj = new GameObject("ARSafeSettings");
                        instance = settingsObj.AddComponent<ARSafeSettings>();
                        DontDestroyOnLoad(settingsObj);
                    }
                }
                return instance;
            }
        }

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;

        // Settings Keys
        private const string KEY_MASTER_VOLUME = "Settings_MasterVolume";
        private const string KEY_SFX_VOLUME = "Settings_SFXVolume";
        private const string KEY_UI_VOLUME = "Settings_UIVolume";
        private const string KEY_GRAPHICS_QUALITY = "Settings_GraphicsQuality";
        private const string KEY_DEBUG_OVERLAY = "Settings_DebugOverlay";
        private const string KEY_DEBUG_LOGS = "Settings_DebugLogs";
        private const string KEY_HAPTIC_FEEDBACK = "Settings_HapticFeedback";
        private const string KEY_DRIFT_CORRECTION_FPS = "Settings_DriftCorrectionFPS";
        private const string KEY_WRONG_WAY_WARNINGS = "Settings_WrongWayWarnings";
        private const string KEY_PERFORMANCE_MONITORING = "Settings_PerformanceMonitoring";

        // Default Values (Mobile-optimized)
        private const float DEFAULT_MASTER_VOLUME = 0.8f;
        private const float DEFAULT_SFX_VOLUME = 0.7f;
        private const float DEFAULT_UI_VOLUME = 0.6f;
        private const int DEFAULT_GRAPHICS_QUALITY = 1; // Medium (0=Low, 1=Medium)
        private const bool DEFAULT_DEBUG_OVERLAY = false;
        private const bool DEFAULT_DEBUG_LOGS = false;
        private const bool DEFAULT_HAPTIC_FEEDBACK = true;
        private const float DEFAULT_DRIFT_CORRECTION_FPS = 15f;
        private const bool DEFAULT_WRONG_WAY_WARNINGS = true;

        // Current Settings (cached)
        private float masterVolume;
        private float sfxVolume;
        private float uiVolume;
        private int graphicsQuality;
        private bool debugOverlay;
        private bool debugLogs;
        private bool hapticFeedback;
        private float driftCorrectionFPS;
        private bool wrongWayWarnings;

        // Events for settings changes
        public System.Action<float> OnMasterVolumeChanged;
        public System.Action<float> OnSFXVolumeChanged;
        public System.Action<float> OnUIVolumeChanged;
        public System.Action<int> OnGraphicsQualityChanged;
        public System.Action<bool> OnDebugOverlayChanged;
        public System.Action<bool> OnDebugLogsChanged;
        public System.Action<bool> OnHapticFeedbackChanged;
        public System.Action<float> OnDriftCorrectionFPSChanged;
        public System.Action<bool> OnWrongWayWarningsChanged;
        public static System.Action<bool> OnPerformanceMonitoringChanged;

        #region Unity Lifecycle

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
            ApplyAllSettings();
        }

        #endregion

        #region Settings Load/Save

        /// <summary>
        /// Load all settings from PlayerPrefs
        /// </summary>
        public void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, DEFAULT_MASTER_VOLUME);
            sfxVolume = PlayerPrefs.GetFloat(KEY_SFX_VOLUME, DEFAULT_SFX_VOLUME);
            uiVolume = PlayerPrefs.GetFloat(KEY_UI_VOLUME, DEFAULT_UI_VOLUME);
            graphicsQuality = PlayerPrefs.GetInt(KEY_GRAPHICS_QUALITY, DEFAULT_GRAPHICS_QUALITY);
            debugOverlay = PlayerPrefs.GetInt(KEY_DEBUG_OVERLAY, DEFAULT_DEBUG_OVERLAY ? 1 : 0) == 1;
            debugLogs = PlayerPrefs.GetInt(KEY_DEBUG_LOGS, DEFAULT_DEBUG_LOGS ? 1 : 0) == 1;
            hapticFeedback = PlayerPrefs.GetInt(KEY_HAPTIC_FEEDBACK, DEFAULT_HAPTIC_FEEDBACK ? 1 : 0) == 1;
            driftCorrectionFPS = PlayerPrefs.GetFloat(KEY_DRIFT_CORRECTION_FPS, DEFAULT_DRIFT_CORRECTION_FPS);
            wrongWayWarnings = PlayerPrefs.GetInt(KEY_WRONG_WAY_WARNINGS, DEFAULT_WRONG_WAY_WARNINGS ? 1 : 0) == 1;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Settings loaded from PlayerPrefs</color>");
            }
        }

        /// <summary>
        /// Save all settings to PlayerPrefs
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, masterVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOLUME, sfxVolume);
            PlayerPrefs.SetFloat(KEY_UI_VOLUME, uiVolume);
            PlayerPrefs.SetInt(KEY_GRAPHICS_QUALITY, graphicsQuality);
            PlayerPrefs.SetInt(KEY_DEBUG_OVERLAY, debugOverlay ? 1 : 0);
            PlayerPrefs.SetInt(KEY_DEBUG_LOGS, debugLogs ? 1 : 0);
            PlayerPrefs.SetInt(KEY_HAPTIC_FEEDBACK, hapticFeedback ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_DRIFT_CORRECTION_FPS, driftCorrectionFPS);
            PlayerPrefs.SetInt(KEY_WRONG_WAY_WARNINGS, wrongWayWarnings ? 1 : 0);

            PlayerPrefs.Save();

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeSettings] Settings saved to PlayerPrefs</color>");
            }
        }

        /// <summary>
        /// Reset all settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            SetMasterVolume(DEFAULT_MASTER_VOLUME);
            SetSFXVolume(DEFAULT_SFX_VOLUME);
            SetUIVolume(DEFAULT_UI_VOLUME);
            SetGraphicsQuality(DEFAULT_GRAPHICS_QUALITY);
            SetDebugOverlay(DEFAULT_DEBUG_OVERLAY);
            SetDebugLogs(DEFAULT_DEBUG_LOGS);
            SetHapticFeedback(DEFAULT_HAPTIC_FEEDBACK);
            SetDriftCorrectionFPS(DEFAULT_DRIFT_CORRECTION_FPS);

            SaveSettings();

            if (enableDebugLogs)
            {
                Debug.Log($"<color=yellow>[ARSafeSettings] Settings reset to defaults</color>");
            }
        }

        #endregion

        #region Audio Settings

        public float MasterVolume => masterVolume;
        public float SFXVolume => sfxVolume;
        public float UIVolume => uiVolume;

        public void SetMasterVolume(float value)
        {
            masterVolume = Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            OnMasterVolumeChanged?.Invoke(masterVolume);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Master volume set to {masterVolume:F2}</color>");
            }
        }

        public void SetSFXVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplySFXVolumeToAllAudioSources();
            OnSFXVolumeChanged?.Invoke(sfxVolume);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] SFX volume set to {sfxVolume:F2}</color>");
            }
        }

        public void SetUIVolume(float value)
        {
            uiVolume = Mathf.Clamp01(value);
            ApplyUIVolumeToAllAudioSources();
            OnUIVolumeChanged?.Invoke(uiVolume);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] UI volume set to {uiVolume:F2}</color>");
            }
        }

        /// <summary>
        /// Apply SFX volume to all AudioSources tagged as "SFX" or untagged in the scene
        /// </summary>
        private void ApplySFXVolumeToAllAudioSources()
        {
            AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            int appliedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource == null) continue;

                // Apply to AudioSources tagged as "SFX" or untagged (default to SFX)
                // Also apply if tag is "UI" doesn't exist (safe check)
                bool isSFXSource = audioSource.gameObject.tag == "Untagged" ||
                                   audioSource.gameObject.tag == "SFX";

                if (isSFXSource)
                {
                    // Store original volume if not already stored
                    if (!audioSource.gameObject.TryGetComponent<AudioSourceVolumeCache>(out var cache))
                    {
                        cache = audioSource.gameObject.AddComponent<AudioSourceVolumeCache>();
                        cache.originalVolume = audioSource.volume;
                    }

                    // Apply SFX volume multiplier
                    audioSource.volume = cache.originalVolume * sfxVolume;
                    appliedCount++;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeSettings] Applied SFX volume to {appliedCount} AudioSources</color>");
            }
        }

        /// <summary>
        /// Apply UI volume to all AudioSources tagged as "UI"
        /// </summary>
        private void ApplyUIVolumeToAllAudioSources()
        {
            AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            int appliedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource == null) continue;

                // Apply to AudioSources tagged as "UI" (safe check - tag may not exist)
                bool isUISource = audioSource.gameObject.tag == "UI";

                if (isUISource)
                {
                    // Store original volume if not already stored
                    if (!audioSource.gameObject.TryGetComponent<AudioSourceVolumeCache>(out var cache))
                    {
                        cache = audioSource.gameObject.AddComponent<AudioSourceVolumeCache>();
                        cache.originalVolume = audioSource.volume;
                    }

                    // Apply UI volume multiplier
                    audioSource.volume = cache.originalVolume * uiVolume;
                    appliedCount++;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeSettings] Applied UI volume to {appliedCount} AudioSources</color>");
            }
        }

        #endregion

        #region Graphics Settings

        public int GraphicsQuality => graphicsQuality;

        public void SetGraphicsQuality(int level)
        {
            graphicsQuality = Mathf.Clamp(level, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(graphicsQuality);
            OnGraphicsQualityChanged?.Invoke(graphicsQuality);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Graphics quality set to: {QualitySettings.names[graphicsQuality]}</color>");
            }
        }

        #endregion

        #region Debug Settings

        public bool DebugOverlay => debugOverlay;
        public bool DebugLogs => debugLogs;

        public void SetDebugOverlay(bool enabled)
        {
            debugOverlay = enabled;
            OnDebugOverlayChanged?.Invoke(debugOverlay);

            // Also control the actual runtime debug overlay if present
            var overlay = global::DebugOverlay.Instance;
            if (overlay == null)
            {
                overlay = FindFirstObjectByType<global::DebugOverlay>();
            }

            if (overlay == null && enabled)
            {
                // Create one on demand when enabling
                var go = new GameObject("DebugOverlay");
                overlay = go.AddComponent<global::DebugOverlay>();
            }

            if (overlay != null)
            {
                if (enabled) overlay.Show(); else overlay.Hide();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Debug overlay: {(enabled ? "ON" : "OFF")}</color>");
            }
        }

        public void SetDebugLogs(bool enabled)
        {
            debugLogs = enabled;
            OnDebugLogsChanged?.Invoke(debugLogs);

            // Apply to activation controller
            var activationController = FindFirstObjectByType<ARSafe.Modular.ARSafeActivationController>();
            if (activationController != null)
            {
                activationController.enableDebugLogs = debugLogs;
            }

            // Apply to debug logger
            var debugLogger = FindFirstObjectByType<ARDebugLogger>();
            if (debugLogger != null)
            {
                debugLogger.captureLogsToFile = debugLogs;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Debug logs: {(enabled ? "ON" : "OFF")}</color>");
            }
        }

        #endregion

        #region AR Settings

        public bool HapticFeedback => hapticFeedback;
        public float DriftCorrectionFPS => driftCorrectionFPS;
        public bool WrongWayWarnings => wrongWayWarnings;

        public void SetHapticFeedback(bool enabled)
        {
            hapticFeedback = enabled;
            OnHapticFeedbackChanged?.Invoke(hapticFeedback);
        }

        public void SetDriftCorrectionFPS(float fps)
        {
            driftCorrectionFPS = Mathf.Clamp(fps, 5f, 60f);
            OnDriftCorrectionFPSChanged?.Invoke(driftCorrectionFPS);

            // Apply to activation controller if in scene
            var activationController = FindFirstObjectByType<ARSafe.Modular.ARSafeActivationController>();
            if (activationController != null)
            {
                activationController.multiAreaPoseUpdateFPS = driftCorrectionFPS;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Drift correction FPS: {driftCorrectionFPS:F0}</color>");
            }
        }

        public void SetWrongWayWarnings(bool enabled)
        {
            wrongWayWarnings = enabled;
            PlayerPrefs.SetInt(KEY_WRONG_WAY_WARNINGS, enabled ? 1 : 0);
            OnWrongWayWarningsChanged?.Invoke(wrongWayWarnings);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeSettings] Wrong-way warnings: {(enabled ? "ON" : "OFF")}</color>");
            }
        }

        /// <summary>
        /// Get or set performance monitoring state (static for easy access)
        /// </summary>
        public static bool PerformanceMonitoring
        {
            get => PlayerPrefs.GetInt(KEY_PERFORMANCE_MONITORING, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KEY_PERFORMANCE_MONITORING, value ? 1 : 0);
                PlayerPrefs.Save();

                // CRITICAL: Force initialization of performance monitor singleton
                // This ensures the singleton is created and subscribes to the event BEFORE we invoke it
                if (value)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    var monitor = ARSafe.Performance.ARSafePerformanceMonitor.Instance;
#endif
                }

                OnPerformanceMonitoringChanged?.Invoke(value);

                if (Instance != null && Instance.enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[ARSafeSettings]</color> Performance monitoring: {(value ? "ON" : "OFF")}");
                }
            }
        }

        #endregion

        #region Apply Settings

        /// <summary>
        /// Apply all settings to the application
        /// </summary>
        private void ApplyAllSettings()
        {
            SetMasterVolume(masterVolume);
            SetSFXVolume(sfxVolume);
            SetUIVolume(uiVolume);
            SetGraphicsQuality(graphicsQuality);
            SetDebugOverlay(debugOverlay);
            SetDebugLogs(debugLogs);
            SetHapticFeedback(hapticFeedback);
            SetDriftCorrectionFPS(driftCorrectionFPS);
            OnWrongWayWarningsChanged?.Invoke(wrongWayWarnings);
        }

        #endregion
    }
}
