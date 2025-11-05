using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using ARSafe.Modular;
using ARSafe.Modular.Welcome;
using ARSafe.UI; // UI Toolkit support (LocalizationInstructionsController)

namespace ARSafe.Modular.Integration
{
    /// <summary>
    /// Integrates the new ARSafeActivationController with the existing ARLoadingScreenManager.
    /// Handles loading screen flow, welcome screen display, and initial AR setup.
    /// 
    /// REPLACES: ARLoadingScreenManager's SequentiallyActivateAreaTargets() logic
    /// WORKS WITH: New modular activation system (ARSafeActivationController)
    /// 
    /// USAGE:
    /// 1. Attach to same GameObject as ARLoadingScreenManager
    /// 2. Assign ARSafeActivationController reference
    /// 3. Configure welcome screen settings
    /// 4. Disable "sequentiallyActivateAreaTargets" in ARLoadingScreenManager
    /// </summary>
    [RequireComponent(typeof(ARLoadingScreenManager))]
    public class ARSafeLoadingIntegration : MonoBehaviour
    {
        [Header("Required References")]
        [Tooltip("The new modular activation controller")]
        public ARSafeActivationController activationController;
        
        [Tooltip("Loading screen manager (auto-assigned)")]
        private ARLoadingScreenManager loadingManager;
        
        [Header("Loading Behavior")]
        [Tooltip("Wait for Vuforia initialization before hiding loading screen")]
        public bool waitForVuforiaReady = true;
        
        [Tooltip("Wait for at least one target to start tracking")]
        public bool waitForInitialTracking = true;
        
        [Tooltip("Maximum time to wait for tracking before continuing (seconds)")]
        [Range(5f, 60f)]
        public float maxTrackingWaitTime = 5f; // Reduced from 20s for faster mobile startup

        [Tooltip("Minimum display time for loading screen (seconds)")]
        [Range(0f, 5f)]
        public float minimumDisplayTime = 0.5f; // Reduced from 2s for faster mobile startup
        
        [Header("Welcome Screen")]
        [Tooltip("Show welcome screen after loading completes")]
        public bool showWelcomeScreen = true;
        
        [Tooltip("Use selected disaster type for welcome content")]
        public bool useDisasterTypeForWelcome = true;
        
        [Header("Location Selection")]
        [Tooltip("NOTE: Location selection now happens in MainMenu scene via MainMenuLocationController")]
        public bool showLocationSelection = false; // Disabled - happens in MainMenu now
        
    [Header("Localization Gate")]
    [Tooltip("Wait for ARSafeActivationController to confirm localization before continuing onboarding.")]
    public bool requireLocalizationBeforeProceeding = true;

    [Tooltip("Maximum time to wait for localization confirmation once tracking starts (seconds)")]
    [Range(1f, 30f)]
    public float localizationConfirmationTimeout = 5f; // Reduced from 10s for faster mobile startup
        
        [Header("Events")]
        public UnityEvent onLoadingStarted;
        public UnityEvent onLoadingCompleted;
        public UnityEvent onWelcomeCompleted;
        
        [Header("Debug")]
        public bool enableDebugLogs = true;
        
        private ARSafeTrackingManager trackingManager;
        private bool isIntegrationActive = false;
        private bool localizationReadyVisualsShown = false;

        // FIX: Track coroutine for proper cleanup
        private Coroutine initializeARCoroutine;

        // FIX: Track shutdown to exit waiting loops early
        private bool isShuttingDown = false;
        
        void Awake()
        {
            loadingManager = GetComponent<ARLoadingScreenManager>();
            
            if (activationController != null)
            {
                trackingManager = activationController.GetComponent<ARSafeTrackingManager>();
            }
        }
        
        void Start()
        {
            // Subscribe to loading manager events
            if (loadingManager != null)
            {
                // Hook into loading manager's lifecycle
                loadingManager.OnLoadingCompleted.AddListener(OnLoadingManagerCompleted);
            }
            
            ValidateSetup();
        }
        
        void OnDisable()
        {
            // FIX: Set shutdown flag when component is disabled (scene exit, etc.)
            isShuttingDown = true;

            if (enableDebugLogs)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Component disabled - stopping all waiting loops</color>");
            }
        }

        void OnDestroy()
        {
            // FIX: Set shutdown flag
            isShuttingDown = true;

            if (loadingManager != null)
            {
                loadingManager.OnLoadingCompleted.RemoveListener(OnLoadingManagerCompleted);
            }

            // FIX: Stop coroutine to prevent operations on destroyed objects
            if (initializeARCoroutine != null)
            {
                StopCoroutine(initializeARCoroutine);
                initializeARCoroutine = null;
            }

            if (enableDebugLogs)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Component destroyed - cleanup complete</color>");
            }
        }
        
        /// <summary>
        /// Validate that everything is configured correctly
        /// </summary>
        private void ValidateSetup()
        {
            if (activationController == null)
            {
                Debug.LogError("[ARSafeLoadingIntegration] ARSafeActivationController is not assigned! Loading integration will not work.");
                return;
            }
            
            if (trackingManager == null)
            {
                Debug.LogWarning("[ARSafeLoadingIntegration] ARSafeTrackingManager not found on activation controller.");
            }
            
            if (loadingManager == null)
            {
                Debug.LogError("[ARSafeLoadingIntegration] ARLoadingScreenManager not found on this GameObject!");
                return;
            }
            
            // Check if old system is still enabled
            if (loadingManager.GetType().GetField("sequentiallyActivateAreaTargets", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
            {
                var field = loadingManager.GetType().GetField("sequentiallyActivateAreaTargets",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool oldSystemEnabled = (bool)field.GetValue(loadingManager);
                
                if (oldSystemEnabled)
                {
                    Debug.LogWarning("[ARSafeLoadingIntegration] Old sequential activation is still enabled in ARLoadingScreenManager! " +
                        "Disable 'sequentiallyActivateAreaTargets' to avoid conflicts.");
                }
            }
            
            isIntegrationActive = true;
            
            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] Integration validated and ready.");
            }
        }
        
        /// <summary>
        /// Called when ARLoadingScreenManager completes its loading sequence
        /// </summary>
        private void OnLoadingManagerCompleted()
        {
            if (!isIntegrationActive) return;
            
            localizationReadyVisualsShown = false;
            
            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] Loading manager completed. Starting AR initialization...");
            }
            
            // Store coroutine reference for cleanup
            initializeARCoroutine = StartCoroutine(InitializeARSystem());
        }
        
        /// <summary>
        /// Initialize the new AR system after loading completes
        /// </summary>
        private IEnumerator InitializeARSystem()
        {
            float startTime = Time.time;

            onLoadingStarted?.Invoke();

            while (Time.time - startTime < minimumDisplayTime && !isShuttingDown)
            {
                yield return null;
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Minimum display wait cancelled - component shutting down</color>");
                yield break;
            }

            if (waitForVuforiaReady)
            {
                yield return WaitForVuforiaInitialization();
            }

            onLoadingCompleted?.Invoke();

            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 1: Loading completed event invoked</color>");

            // CRITICAL: Show localization instructions BEFORE waiting for tracking
            // Instructions guide the user on how to establish tracking
            var instructionsController = FindFirstObjectByType<LocalizationInstructionsController>();
            if (instructionsController != null)
            {
                instructionsController.ShowInitialLocalization();
                Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ Localization instructions shown IMMEDIATELY (guiding user to establish tracking)</color>");
            }
            else
            {
                Debug.Log("<color=yellow>[ARSafeLoadingIntegration] ▶ LocalizationInstructionsController not found - skipping localization guidance</color>");
            }

            if (waitForInitialTracking)
            {
                yield return WaitForInitialTargetTracking();
            }

            yield return WaitForLocalizationConfirmation("Waiting for localization before starting simulations...");

            // Instructions will auto-hide when tracking is stable
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 2: Localization wait completed</color>");

            // CRITICAL: Force refresh all disaster filters to ensure arrows show up
            if (activationController != null)
            {
                activationController.ForceRefreshAllDisasterFilters();
                Debug.Log("<color=green>[ARSafeLoadingIntegration] ▶ Forced disaster filter refresh after localization</color>");
            }

            // NOTE: Location selection now happens in MainMenu via MainMenuLocationController
            // No need to show location overlay here - user already selected in MainMenu
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 2.5: Location selection handled in MainMenu (skipping)</color>");

            if (showWelcomeScreen)
            {
                Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 3: About to show welcome screen</color>");
                yield return ShowWelcomeScreen();
                Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 4: Welcome screen completed</color>");
            }
            else
            {
                Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 3-SKIP: Welcome screen disabled</color>");
            }

            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 5: About to ensure earthquake scenario</color>");
            yield return EnsureScenarioStarted();
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ▶ CHECKPOINT 6: Earthquake scenario check completed</color>");

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] AR initialization complete.");
            }
        }
        
        /// <summary>
        /// Wait for Vuforia to initialize
        /// </summary>
        private IEnumerator WaitForVuforiaInitialization()
        {
            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] Waiting for Vuforia initialization...");
            }
            
            // Update loading UI
            if (loadingManager != null)
            {
                loadingManager.ReportAreaTargetProgress(0.1f, "Initializing Vuforia AR Engine...");
            }
            
            // Wait for Vuforia to be running
            while ((Vuforia.VuforiaApplication.Instance == null ||
                   Vuforia.VuforiaApplication.Instance.IsRunning == false) && !isShuttingDown)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Vuforia initialization wait cancelled - component shutting down</color>");
                yield break;
            }
            
            // Additional small delay for Vuforia to fully stabilize
            yield return new WaitForSeconds(0.5f);
            
            if (loadingManager != null)
            {
                loadingManager.ReportAreaTargetProgress(0.3f, "Vuforia AR Engine ready");
            }
            
            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] Vuforia is ready.");
            }
        }
        
        /// <summary>
        /// Wait for at least one target to start tracking
        /// </summary>
        private IEnumerator WaitForInitialTargetTracking()
        {
#if UNITY_EDITOR
            // EDITOR MODE: Skip tracking wait in Unity Editor (Vuforia doesn't work in editor)
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] EDITOR MODE: Skipping tracking wait (Vuforia doesn't run in editor)</color>");
            yield break;
#else
            if (trackingManager == null)
            {
                Debug.LogWarning("[ARSafeLoadingIntegration] Cannot wait for tracking - TrackingManager not found.");
                yield break;
            }

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeLoadingIntegration] Waiting for initial target tracking...");
            }
            
            // Get total activated targets from activation controller
            int totalTargets = 0;
            if (activationController != null)
            {
                // Count all area targets in the controller's list
                var allTargetsField = activationController.GetType().GetField("allAreaTargets",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (allTargetsField != null)
                {
                    var targetsList = allTargetsField.GetValue(activationController);
                    if (targetsList != null)
                    {
                        var count = targetsList.GetType().GetProperty("Count");
                        if (count != null)
                        {
                            totalTargets = (int)count.GetValue(targetsList);
                        }
                    }
                }
            }
            
            if (loadingManager != null)
            {
                loadingManager.ReportAreaTargetProgress(0.5f, "Scanning for area targets...");
                loadingManager.UpdateAreaActivationStatus("Waiting for tracking...", 0, totalTargets);
            }
            
            float startTime = Time.time;
            bool trackingAchieved = false;
            
            while (Time.time - startTime < maxTrackingWaitTime && !isShuttingDown)
            {
                int trackingCount = trackingManager.GetTrackingCount();

                // Update UI with current tracking count
                if (loadingManager != null)
                {
                    float progress = 0.5f + (0.4f * Mathf.Clamp01((Time.time - startTime) / maxTrackingWaitTime));
                    loadingManager.ReportAreaTargetProgress(progress, $"Tracking {trackingCount} target(s)...");
                    loadingManager.UpdateAreaActivationCounts(trackingCount, totalTargets);
                }
                
                if (trackingCount > 0)
                {
                    trackingAchieved = true;
                    break;
                }
                
                yield return new WaitForSeconds(0.2f);
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Tracking wait cancelled - component shutting down</color>");
                yield break;
            }

            if (trackingAchieved)
            {
                int finalCount = trackingManager.GetTrackingCount();
                
                if (loadingManager != null)
                {
                    loadingManager.ReportAreaTargetProgress(0.95f, $"Tracking established! {finalCount} target(s) active");
                    loadingManager.UpdateAreaActivationStatus("Tracking established!", finalCount, totalTargets);
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[ARSafeLoadingIntegration] Initial tracking achieved! {finalCount} target(s) tracking.");
                }
                
                // Wait a bit longer for stability
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                if (loadingManager != null)
                {
                    loadingManager.ReportAreaTargetProgress(1.0f, "Ready to scan environment");
                    loadingManager.UpdateAreaActivationStatus("Ready to scan", 0, totalTargets);
                }
                
                Debug.LogWarning($"[ARSafeLoadingIntegration] No tracking achieved within {maxTrackingWaitTime}s. Continuing anyway...");
            }
#endif
        }
        
        /// <summary>
        /// Show welcome screen using WelcomeScreenManager
        /// </summary>
        private IEnumerator ShowWelcomeScreen()
        {
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] ShowWelcomeScreen() ENTRY</color>");

            if (WelcomeScreenManager.Instance == null)
            {
                Debug.Log("<color=yellow>[ARSafeLoadingIntegration] WelcomeScreenManager not found. Skipping welcome screen.</color>");
                yield break;
            }

            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] WelcomeScreenManager found</color>");

            bool shouldShow = ShouldDisplayWelcome();
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] ShouldDisplayWelcome() returned: {shouldShow}</color>");

            if (!shouldShow)
            {
                Debug.Log("<color=yellow>[ARSafeLoadingIntegration] Welcome screen skipped (not first time user or disabled).</color>");

                // CRITICAL: Fire OnWelcomeCompleted even when skipped so earthquake scenario can start
                onWelcomeCompleted?.Invoke();
                Debug.Log("<color=cyan>[ARSafeLoadingIntegration] OnWelcomeCompleted event fired (skipped path)</color>");

                yield break;
            }
            
            Debug.Log("<color=green>[ARSafeLoadingIntegration] Preparing to show welcome screen...</color>");
            
            bool welcomeCompleted = false;
            
            // Subscribe to completion
            UnityEngine.Events.UnityAction completionHandler = () => 
            { 
                Debug.Log("<color=green>[ARSafeLoadingIntegration] ★ OnWelcomeCompleted EVENT FIRED! ★</color>");
                welcomeCompleted = true; 
            };
            WelcomeScreenManager.Instance.OnWelcomeCompleted += completionHandler;
            
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] Event handler subscribed</color>");
            
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] Event handler subscribed</color>");
            
            // Get current disaster type if configured
            DisasterType currentType = DisasterType.None;
            if (useDisasterTypeForWelcome && DisasterTypeManager.Instance != null)
            {
                currentType = DisasterTypeManager.SelectedDisasterType;

                // Fallback: if nothing was persisted yet (e.g., scene started directly),
                // adopt the last menu selection so disaster-specific banners/icons are used
                if (currentType == DisasterType.None && MenuButtonHandler.LastSelectedDisasterType != DisasterType.None)
                {
                    currentType = MenuButtonHandler.LastSelectedDisasterType;
                    Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] Using LastSelectedDisasterType fallback for welcome: {currentType}</color>");
                }
            }
            
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] Disaster type for welcome: {currentType}</color>");
            
            // Show welcome directly through the manager API
            try
            {
                if (useDisasterTypeForWelcome)
                {
                    Debug.Log($"<color=green>[ARSafeLoadingIntegration] Calling ShowWelcomeScreen({currentType})...</color>");
                    WelcomeScreenManager.Instance.ShowWelcomeScreen(currentType);
                }
                else
                {
                    Debug.Log("<color=green>[ARSafeLoadingIntegration] Calling ShowWelcomeScreen(None)...</color>");
                    WelcomeScreenManager.Instance.ShowWelcomeScreen(DisasterType.None);
                }
                Debug.Log("<color=green>[ARSafeLoadingIntegration] ShowWelcomeScreen() call completed successfully</color>");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"<color=red>[ARSafeLoadingIntegration] ✗ Failed to show welcome screen: {ex.Message}\n{ex.StackTrace}</color>");
                welcomeCompleted = true;
            }
            
            // Wait for welcome to complete
            float timeout = 120f; // 2 minutes max
            float elapsed = 0f;
            float lastLogTime = 0f;
            
            Debug.Log("<color=yellow>[ARSafeLoadingIntegration] Now waiting for welcome screen to be dismissed by user...</color>");

            while (!welcomeCompleted && elapsed < timeout && !isShuttingDown)
            {
                // Log every 5 seconds to show we're waiting
                if (elapsed - lastLogTime >= 5f)
                {
                    Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] Still waiting for welcome dismissal... ({elapsed:F1}s / {timeout:F0}s)</color>");
                    lastLogTime = elapsed;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Welcome wait cancelled - component shutting down</color>");
                yield break;
            }
            
            if (!welcomeCompleted)
            {
                Debug.LogError($"<color=red>[ARSafeLoadingIntegration] ✗ Welcome screen TIMEOUT after {timeout}s! Event never fired.</color>");
            }
            
            WelcomeScreenManager.Instance.OnWelcomeCompleted -= completionHandler;
            
            Debug.Log("<color=cyan>[ARSafeLoadingIntegration] Event handler unsubscribed</color>");
            
            onWelcomeCompleted?.Invoke();
            
            Debug.Log("<color=green>[ARSafeLoadingIntegration] Welcome screen sequence completed.</color>");
        }

        private IEnumerator WaitForLocalizationConfirmation(string context)
        {
            Debug.Log($"<color=magenta>[ARSafeLoadingIntegration] WaitForLocalizationConfirmation ENTRY - requireLocalization={requireLocalizationBeforeProceeding}, activationController={activationController != null}</color>");

#if UNITY_EDITOR
            // EDITOR MODE: Skip localization requirement in Unity Editor (Vuforia doesn't work in editor)
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] EDITOR MODE: Skipping localization wait (Vuforia doesn't run in editor)</color>");
            HandleLocalizationReady();
            yield break;
#else
            // CRITICAL: Verify activation controller is valid and not destroyed
            if (activationController == null || activationController.gameObject == null)
            {
                Debug.LogError($"<color=red>[ARSafeLoadingIntegration] CRITICAL: activationController is null or destroyed! Cannot wait for localization.</color>");
                yield break;
            }

            if (!requireLocalizationBeforeProceeding)
            {
                Debug.Log($"<color=magenta>[ARSafeLoadingIntegration] Localization NOT required - skipping wait</color>");
                if (activationController.HasLocalized)
                {
                    HandleLocalizationReady();
                }
                yield break;
            }

            // CRITICAL: Check if controller is still valid before accessing HasLocalized
            try
            {
                if (activationController.HasLocalized)
                {
                    Debug.Log($"<color=green>[ARSafeLoadingIntegration] Already localized - proceeding immediately</color>");
                    HandleLocalizationReady();
                    yield break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"<color=red>[ARSafeLoadingIntegration] Exception checking HasLocalized: {ex.Message}. Skipping localization wait.</color>");
                yield break;
            }

            Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] {context}</color>");

            float startTime = Time.time;
            float lastLogTime = startTime;

            // CRITICAL: Wait indefinitely for localization (NO TIMEOUT)
            // Welcome screen must ONLY appear after proper localization
            while (!isShuttingDown)
            {
                // CRITICAL: Verify controller is still valid during wait loop
                if (activationController == null || activationController.gameObject == null)
                {
                    Debug.LogError($"<color=red>[ARSafeLoadingIntegration] Activation controller became null during localization wait! Aborting.</color>");
                    yield break;
                }

                // Check localization status
                try
                {
                    if (activationController.HasLocalized)
                    {
                        break; // Localization confirmed
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"<color=red>[ARSafeLoadingIntegration] Exception during localization wait: {ex.Message}. Aborting.</color>");
                    yield break;
                }

                // Log every 2 seconds to track progress
                if (Time.time - lastLogTime >= 2f)
                {
                    Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] Still waiting for localization... ({Time.time - startTime:F1}s elapsed)</color>");
                    lastLogTime = Time.time;
                }
                yield return null;
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Localization wait cancelled - component shutting down</color>");
                yield break;
            }

            // Localization confirmed - proceed
            Debug.Log("<color=green>[ARSafeLoadingIntegration] ✓ Localization confirmed by activation controller.</color>");
            HandleLocalizationReady();
#endif
        }
        
        /// <summary>
        /// Check if welcome should be shown for first-time users
        /// </summary>
        private bool ShouldDisplayWelcome()
        {
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] ShouldDisplayWelcome() CHECK - showWelcomeScreen field = {showWelcomeScreen}</color>");

            if (!showWelcomeScreen)
            {
                Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] showWelcomeScreen is FALSE - welcome will be skipped!</color>");
                return false;
            }

            bool hasManager = WelcomeScreenManager.TryGetInstance(out var manager);
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] WelcomeScreenManager exists = {hasManager}, forceShowWelcome = {(hasManager ? manager.forceShowWelcome.ToString() : "N/A")}</color>");

            if (hasManager && manager.forceShowWelcome)
            {
                Debug.Log($"<color=green>[ARSafeLoadingIntegration] forceShowWelcome is TRUE - showing welcome!</color>");
                return true;
            }

            bool suppressed = WelcomeScreenManager.HasUserSuppressedWelcome();
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] HasUserSuppressedWelcome() = {suppressed}</color>");

            bool result = !suppressed;
            Debug.Log($"<color=cyan>[ARSafeLoadingIntegration] ShouldDisplayWelcome() RESULT = {result}</color>");
            return result;
        }
        
        /// <summary>
        /// Public API: Force show loading screen
        /// </summary>
        public void ShowLoadingScreen()
        {
            if (loadingManager != null && loadingManager.GetType().GetMethod("SetOverlayVisible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
            {
                var method = loadingManager.GetType().GetMethod("SetOverlayVisible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(loadingManager, new object[] { true });
            }
        }
        
        /// <summary>
        /// Public API: Force hide loading screen
        /// </summary>
        public void HideLoadingScreen()
        {
            if (loadingManager != null && loadingManager.GetType().GetMethod("SetOverlayVisible",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) != null)
            {
                var method = loadingManager.GetType().GetMethod("SetOverlayVisible",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method.Invoke(loadingManager, new object[] { false });
            }
        }
        
        /// <summary>
        /// Ensure localization-ready visuals run once.
        /// </summary>
        private void HandleLocalizationReady()
        {
            if (localizationReadyVisualsShown)
            {
                return;
            }

            localizationReadyVisualsShown = true;

            if (enableDebugLogs)
            {
                Debug.Log("<color=green>[ARSafeLoadingIntegration] ✓ Localization confirmed</color>");
            }
        }

        private IEnumerator EnsureScenarioStarted()
        {
            const float waitTimeout = 6f;
            float waitStart = Time.time;
            bool scenarioStarted = false;
            DisasterType currentType = DisasterTypeManager.SelectedDisasterType;

            if (currentType == DisasterType.None && MenuButtonHandler.LastSelectedDisasterType != DisasterType.None)
            {
                currentType = MenuButtonHandler.LastSelectedDisasterType;
                DisasterTypeManager.SetDisasterType(currentType);
                Debug.Log($"[ARSafeLoadingIntegration] Adopted menu selection '{currentType}' prior to scenario startup.");
            }

            if (currentType == DisasterType.None)
            {
                Debug.LogWarning("[ARSafeLoadingIntegration] No disaster type selected. Scenario startup skipped.");
                yield break;
            }

            Debug.Log($"<color=yellow>[ARSafeLoadingIntegration] Attempting to start scenario '{currentType}'...</color>");

            if (currentType != DisasterType.Earthquake && currentType != DisasterType.Flood && currentType != DisasterType.Fire)
            {
                Debug.Log("[ARSafeLoadingIntegration] Current disaster type has no runtime scenario to start.");
                yield break;
            }

            while (!scenarioStarted && Time.time - waitStart < waitTimeout && !isShuttingDown)
            {
                switch (currentType)
                {
                    case DisasterType.Earthquake when EarthquakeScenarioManager.Instance != null:
                        scenarioStarted = EarthquakeScenarioManager.BeginScenarioIfReady() || EarthquakeScenarioManager.CurrentParameters.IsActive;
                        break;
                    case DisasterType.Flood when FloodScenarioManager.Instance != null:
                        scenarioStarted = FloodScenarioManager.BeginScenarioIfReady() || FloodScenarioManager.CurrentParameters.IsActive;
                        break;
                    case DisasterType.Fire when FireScenarioManager.Instance != null:
                        scenarioStarted = FireScenarioManager.BeginScenarioIfReady() || FireScenarioManager.CurrentParameters.IsActive;
                        break;
                }

                if (scenarioStarted)
                {
                    Debug.Log($"<color=green>[ARSafeLoadingIntegration] ✓ Scenario '{currentType}' started successfully!</color>");
                    break;
                }

                Debug.Log($"[ARSafeLoadingIntegration] Waiting for scenario manager... (elapsed: {Time.time - waitStart:F2}s)");
                yield return new WaitForSeconds(0.5f);
            }

            // FIX: Check if we exited due to shutdown
            if (isShuttingDown)
            {
                Debug.Log("<color=gray>[ARSafeLoadingIntegration] Scenario wait cancelled - component shutting down</color>");
                yield break;
            }

            if (!scenarioStarted)
            {
                if (currentType == DisasterType.Earthquake)
                {
                    Debug.LogError($"<color=red>[ARSafeLoadingIntegration] ✗ Scenario '{currentType}' FAILED to start within {waitTimeout:F1}s!</color>\n" +
                        $"EarthquakeScenarioManager.Instance: {(EarthquakeScenarioManager.Instance != null ? "EXISTS" : "NULL")}\n" +
                        $"CurrentParameters.IsActive: {EarthquakeScenarioManager.CurrentParameters.IsActive}\n" +
                        $"DisasterTypeManager.SelectedDisasterType: {DisasterTypeManager.SelectedDisasterType}");

                    Debug.LogWarning("[ARSafeLoadingIntegration] Forcing explicit earthquake scenario trigger...");
                    EarthquakeScenarioManager.BeginScenarioIfReady();
                }
                else if (currentType == DisasterType.Flood)
                {
                    Debug.LogError($"<color=red>[ARSafeLoadingIntegration] ✗ Scenario '{currentType}' FAILED to start within {waitTimeout:F1}s!</color>\n" +
                        $"FloodScenarioManager.Instance: {(FloodScenarioManager.Instance != null ? "EXISTS" : "NULL")}\n" +
                        $"CurrentParameters.IsActive: {FloodScenarioManager.CurrentParameters.IsActive}\n" +
                        $"DisasterTypeManager.SelectedDisasterType: {DisasterTypeManager.SelectedDisasterType}");

                    Debug.LogWarning("[ARSafeLoadingIntegration] Attempting explicit flood scenario trigger...");
                    FloodScenarioManager.BeginScenarioIfReady();
                }
                else if (currentType == DisasterType.Fire)
                {
                    Debug.LogError($"<color=red>[ARSafeLoadingIntegration] ✗ Scenario '{currentType}' FAILED to start within {waitTimeout:F1}s!</color>\n" +
                        $"FireScenarioManager.Instance: {(FireScenarioManager.Instance != null ? "EXISTS" : "NULL")}\n" +
                        $"CurrentParameters.IsActive: {FireScenarioManager.CurrentParameters.IsActive}\n" +
                        $"DisasterTypeManager.SelectedDisasterType: {DisasterTypeManager.SelectedDisasterType}");

                    Debug.LogWarning("[ARSafeLoadingIntegration] Attempting explicit fire scenario trigger...");
                    FireScenarioManager.BeginScenarioIfReady();
                }
            }
        }
    }
}
