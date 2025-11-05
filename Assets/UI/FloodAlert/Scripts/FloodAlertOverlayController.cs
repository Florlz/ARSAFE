using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;
using ARSafe.Modular.Welcome;

namespace ARSafe.UI
{
    /// <summary>
    /// Displays a UI Toolkit overlay for flood emergency with water rising effects.
    /// Shows flood level, water rise rate, and evacuation guidance to higher ground.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FloodAlertOverlayController : MonoBehaviour
    {
        private const string VisibleClass = "flood-alert__root--visible";
        private const string OverlayRootName = "flood-alert-overlay";
        private const string ResponseLabelName = "response-label";
        private const string AcknowledgeButtonName = "acknowledge-button";
        private const string StartSectionName = "flood-alert-start";
        private const string CompleteSectionName = "flood-alert-complete";
        private const string CompleteButtonName = "complete-button";
        private const string HiddenSectionClass = "flood-alert__section--hidden";
        private const string TemplateResourcePath = "UI/FloodAlert/FloodAlertOverlay";
        private const string StyleResourcePath = "UI/FloodAlert/FloodAlertStyles";

        [SerializeField, Tooltip("Optional PanelSettings override. Null uses the scene default.")]
        private PanelSettings panelSettings;

        [SerializeField, Tooltip("Automatically hide the alert after this many seconds (0 keeps it visible until acknowledged).")]
        private float autoHideDelay = 0f;

        public static FloodAlertOverlayController Instance { get; private set; }

        private UIDocument uiDocument;
        private VisualElement overlayRoot;
        private Label responseLabel;
        private Button acknowledgeButton;
        private VisualElement startSection;
        private VisualElement completeSection;
        private Button completeButton;
        private Label rainfallWarningValue;
        private Label rainfallWarningLabel;
        private Label warningMessageLabel;
        private Label progressionInfoLabel;

        private bool isVisible;
        private float hideTimer;
        private bool uiBuilt;
        private IVisualElementScheduledItem cacheRetryItem;
        private bool allowAutoHide;
        private bool showingCompletion;
        private bool welcomeScreenActive;
        private FloodScenarioParameters pendingParameters;
        private bool hasPendingParameters;
        private bool scenarioActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>
        /// Ensures an instance exists. Finds manually-placed GameObject first, creates new one only if none found.
        /// Call this from FloodScenarioManager when Flood disaster is selected.
        /// </summary>
        public static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<FloodAlertOverlayController>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Debug.Log("[FloodAlertOverlayController] No manually-placed instance found, creating new GameObject.");
                var managerObject = new GameObject(nameof(FloodAlertOverlayController));
                managerObject.transform.SetParent(null); // Ensure root-level for DontDestroyOnLoad

                // CRITICAL: Add UIDocument BEFORE FloodAlertOverlayController
                // Otherwise Awake() runs before UIDocument exists, causing GetComponent<UIDocument>() to return null
                managerObject.AddComponent<UIDocument>();
                existing = managerObject.AddComponent<FloodAlertOverlayController>();
            }
            else
            {
                Debug.Log($"[FloodAlertOverlayController] Using manually-placed instance: {existing.gameObject.name}");
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

            // Ensure this GameObject is a root GameObject for DontDestroyOnLoad
            if (transform.parent != null)
            {
                Debug.LogWarning($"[FloodAlertOverlayController] Moving {gameObject.name} to scene root for DontDestroyOnLoad");
                transform.SetParent(null);
            }

            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[FloodAlertOverlayController] Missing UIDocument component.");
                return;
            }

            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }

            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                return;
            }

            EnsureDocumentTemplate();
            CacheElements();

            FloodScenarioManager.OnParametersUpdated += HandleScenarioParameters;
            FloodScenarioManager.OnProgressUpdated += HandleScenarioProgress;
            FloodScenarioManager.OnProgressionGenerated += HandleProgressionGenerated;
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterChanged;

            // Subscribe to welcome screen completion
            // Only wait for welcome screen if it exists AND is currently showing
            var welcomeManager = WelcomeScreenManager.Instance;
            if (welcomeManager != null)
            {
                welcomeManager.OnWelcomeCompleted += HandleWelcomeCompleted;
                welcomeScreenActive = welcomeManager.IsShowing;

                // Safety: If welcome screen exists but isn't showing, don't wait for it
                if (!welcomeScreenActive)
                {
                    Debug.Log("[FloodAlertOverlayController] Welcome screen exists but not showing - proceeding immediately");
                }
            }
            else
            {
                // No welcome screen in scene - proceed immediately
                welcomeScreenActive = false;
                Debug.Log("[FloodAlertOverlayController] No welcome screen found - proceeding immediately");
            }

            HandleScenarioParameters(FloodScenarioManager.CurrentParameters);
            HandleScenarioProgress(FloodScenarioManager.CurrentProgress);
        }

        private void OnDisable()
        {
            // Wrap in try-catch to prevent crashes during Unity shutdown
            try
            {
                FloodScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
                FloodScenarioManager.OnProgressUpdated -= HandleScenarioProgress;
                FloodScenarioManager.OnProgressionGenerated -= HandleProgressionGenerated;
                DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterChanged;

                if (WelcomeScreenManager.TryGetInstance(out var welcomeManager))
                {
                    welcomeManager.OnWelcomeCompleted -= HandleWelcomeCompleted;
                }

                if (acknowledgeButton != null)
                {
                    acknowledgeButton.clicked -= HideOverlay;
                }
                if (completeButton != null)
                {
                    completeButton.clicked -= HideOverlay;
                }

                cacheRetryItem?.Pause();
                cacheRetryItem = null;
                overlayRoot = null;
                responseLabel = null;
                acknowledgeButton = null;
                startSection = null;
                completeSection = null;
                completeButton = null;
                rainfallWarningValue = null;
                rainfallWarningLabel = null;
                warningMessageLabel = null;
                progressionInfoLabel = null;
                uiBuilt = false;
                showingCompletion = false;
                welcomeScreenActive = false;
                pendingParameters = default;
                hasPendingParameters = false;
                scenarioActive = false;
            }
            catch
            {
                // Silent catch during shutdown
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!isVisible || !allowAutoHide)
            {
                return;
            }

            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                HideOverlay();
            }
        }

        private void EnsureDocumentTemplate()
        {
            if (uiDocument.visualTreeAsset != null)
            {
                return;
            }

            var template = Resources.Load<VisualTreeAsset>(TemplateResourcePath);
            if (template == null)
            {
                Debug.LogError($"[FloodAlertOverlayController] Failed to load UXML template from Resources/{TemplateResourcePath}");
                return;
            }

            uiDocument.visualTreeAsset = template;

            // Apply stylesheet
            var styleSheet = Resources.Load<StyleSheet>(StyleResourcePath);
            if (styleSheet != null && uiDocument.rootVisualElement != null)
            {
                if (!uiDocument.rootVisualElement.styleSheets.Contains(styleSheet))
                {
                    uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
                }
            }
        }

        private void CacheElements()
        {
            if (uiBuilt)
            {
                return;
            }

            EnsureDocumentTemplate();
            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("[FloodAlertOverlayController] Root visual element is null, retrying...");
                return;
            }

            overlayRoot = root.Q<VisualElement>(OverlayRootName);
            responseLabel = root.Q<Label>(ResponseLabelName);
            acknowledgeButton = root.Q<Button>(AcknowledgeButtonName);
            startSection = root.Q<VisualElement>(StartSectionName);
            completeSection = root.Q<VisualElement>(CompleteSectionName);
            completeButton = root.Q<Button>(CompleteButtonName);
            rainfallWarningValue = root.Q<Label>("rainfall-warning-value");
            rainfallWarningLabel = root.Q<Label>("rainfall-warning-label");
            warningMessageLabel = root.Q<Label>("warning-message-label");
            progressionInfoLabel = root.Q<Label>("progression-info-label");

            if (overlayRoot == null)
            {
                Debug.LogWarning($"[FloodAlertOverlayController] Could not find '{OverlayRootName}' element.");
                if (cacheRetryItem == null)
                {
                    cacheRetryItem = root.schedule.Execute(CacheElements).StartingIn(100);
                }
                return;
            }
            cacheRetryItem = null;

            if (acknowledgeButton != null)
            {
                acknowledgeButton.clicked -= HideOverlay;
                acknowledgeButton.clicked += HideOverlay;
            }

            if (completeButton != null)
            {
                completeButton.clicked -= HideOverlay;
                completeButton.clicked += HideOverlay;
            }

            Debug.Log($"[FloodAlertOverlayController] Successfully cached UI elements.");
            uiBuilt = true;
            HideImmediate();

            if (FloodScenarioManager.CurrentParameters.IsActive)
            {
                HandleScenarioParameters(FloodScenarioManager.CurrentParameters);
            }
        }

        private void HandleScenarioParameters(FloodScenarioParameters parameters)
        {
            Debug.Log($"[FloodAlertOverlayController] Received parameters: IsActive={parameters.IsActive}, TargetDepth={parameters.TargetDepthMeters}m, uiBuilt={uiBuilt}, welcomeScreenActive={welcomeScreenActive}");

            if (!uiBuilt)
            {
                CacheElements();

                // If still not built, schedule retry
                if (!uiBuilt)
                {
                    if (parameters.IsActive)
                    {
                        pendingParameters = parameters;
                        hasPendingParameters = true;
                        Debug.LogWarning("[FloodAlertOverlayController] UI not ready, scheduling retry in 100ms");

                        // Schedule retry after a short delay to give UI time to build
                        if (uiDocument != null && uiDocument.rootVisualElement != null)
                        {
                            uiDocument.rootVisualElement.schedule.Execute(() =>
                            {
                                if (hasPendingParameters && !uiBuilt)
                                {
                                    Debug.Log("[FloodAlertOverlayController] Retrying UI build after scheduled delay");
                                    HandleScenarioParameters(pendingParameters);
                                }
                            }).StartingIn(100);
                        }
                    }
                    return;
                }
            }

            // If welcome screen is active, wait until it's dismissed
            // Check welcome screen state in real-time instead of using cached flag
            // This prevents race conditions on 2nd+ scenario cycles
            var welcomeManager = WelcomeScreenManager.TryGetInstance(out var mgr) ? mgr : null;
            if (welcomeManager != null && welcomeManager.IsShowing && parameters.IsActive)
            {
                Debug.Log("[FloodAlertOverlayController] Welcome screen showing - deferring overlay until welcome completes");
                pendingParameters = parameters;
                hasPendingParameters = true;
                return;
            }

            if (parameters.IsActive)
            {
                UpdateContent(parameters);

                // Only show overlay on INITIAL scenario start, not on level transitions (Yellow→Orange→Red)
                if (!scenarioActive)
                {
                    ShowStartOverlay();
                    scenarioActive = true;
                }
                // Otherwise, just update content without re-showing overlay

                hasPendingParameters = false;
            }
            else
            {
                hasPendingParameters = false;
                scenarioActive = false;

                if (!showingCompletion)
                {
                    HideOverlay();
                }
            }
        }

        private void HandleScenarioProgress(FloodScenarioProgress progress)
        {
            if (!uiBuilt || !scenarioActive)
            {
                return;
            }

            // NOTE: Completion overlay disabled for Flood - ExitOverlayController handles 2nd floor exit display
            // When user reaches 2nd floor, ARSafeActivationController shows ExitOverlayController instead
            if (progress.IsComplete)
            {
                // Hide flood warning overlay when scenario completes (user reached 2nd floor)
                HideOverlay();
            }
        }

        private void HandleWelcomeCompleted()
        {
            Debug.Log("[FloodAlertOverlayController] Welcome screen completed");
            welcomeScreenActive = false;

            // Show pending parameters if we have them
            if (hasPendingParameters && pendingParameters.IsActive)
            {
                Debug.Log("[FloodAlertOverlayController] Showing deferred flood overlay");
                HandleScenarioParameters(pendingParameters);
                if (!hasPendingParameters)
                {
                    pendingParameters = default;
                }
            }
        }

        private void HandleDisasterChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Flood)
            {
                HideOverlay();
                scenarioActive = false;
            }
        }

        private void HandleProgressionGenerated(FloodWarningProgression progression)
        {
            if (progressionInfoLabel == null)
            {
                return;
            }

            // Build progression info message
            string startLevel = progression.Phases[0].Level.ToString().ToUpper();
            string peakLevel = progression.PeakLevel.ToString().ToUpper();

            string message;
            if (progression.PeakLevel == progression.Phases[0].Level)
            {
                // Peak is same as start (Yellow only)
                message = $"Starting at {startLevel} warning - Will remain at this level";
            }
            else
            {
                // Will escalate to higher level
                message = $"Starting at {startLevel} - Will escalate to {peakLevel} warning level";
            }

            progressionInfoLabel.text = message;

            // Color based on peak level
            Color peakColor = GetWarningColorForLevel(progression.PeakLevel);
            progressionInfoLabel.style.color = new StyleColor(peakColor);

            Debug.Log($"[FloodAlertOverlayController] Progression info updated: {message}");
        }

        private Color GetWarningColorForLevel(RainfallWarningLevel level)
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

        private void UpdateContent(FloodScenarioParameters parameters)
        {
            // Update PAGASA rainfall warning stat card
            if (rainfallWarningValue != null)
            {
                rainfallWarningValue.text = parameters.WarningLevel.ToString().ToUpper();
                rainfallWarningValue.style.color = new StyleColor(parameters.WarningColor);
            }

            if (rainfallWarningLabel != null)
            {
                rainfallWarningLabel.text = parameters.WarningLevelLabel;
            }

            if (warningMessageLabel != null)
            {
                warningMessageLabel.text = parameters.WarningMessage;
            }

            // Update main response message based on water depth
            if (responseLabel != null)
            {
                string message = "Water rising rapidly! Move to higher floors immediately!";
                if (parameters.TargetDepthMeters >= 2f)
                {
                    message = "CRITICAL FLOOD LEVEL! Evacuate to 2nd floor or higher NOW!";
                }
                else if (parameters.TargetDepthMeters >= 1f)
                {
                    message = "DANGEROUS WATER LEVELS! Move upstairs immediately!";
                }

                responseLabel.text = message;
            }
        }

        private void ShowStartOverlay()
        {
            if (!uiBuilt || overlayRoot == null)
            {
                Debug.LogWarning("[FloodAlertOverlayController] ShowStartOverlay called but UI not built or overlayRoot is null");
                return;
            }

            showingCompletion = false;

            if (startSection != null)
            {
                startSection.RemoveFromClassList(HiddenSectionClass);
            }

            if (completeSection != null)
            {
                completeSection.AddToClassList(HiddenSectionClass);
            }

            overlayRoot.AddToClassList(VisibleClass);
            isVisible = true;

            if (autoHideDelay > 0f)
            {
                allowAutoHide = true;
                hideTimer = autoHideDelay;
            }
            else
            {
                allowAutoHide = false;
            }

            Debug.Log("[FloodAlertOverlayController] Flood alert overlay shown (START)");
        }

        private void ShowCompleteOverlay()
        {
            if (!uiBuilt || overlayRoot == null || showingCompletion)
            {
                return;
            }

            showingCompletion = true;

            if (startSection != null)
            {
                startSection.AddToClassList(HiddenSectionClass);
            }

            if (completeSection != null)
            {
                completeSection.RemoveFromClassList(HiddenSectionClass);
            }

            overlayRoot.AddToClassList(VisibleClass);
            isVisible = true;
            allowAutoHide = false;

            Debug.Log("[FloodAlertOverlayController] Flood alert overlay shown (COMPLETE)");
        }

        private void HideOverlay()
        {
            if (!uiBuilt || overlayRoot == null)
            {
                return;
            }

            overlayRoot.RemoveFromClassList(VisibleClass);
            isVisible = false;
            showingCompletion = false;
            allowAutoHide = false;

            Debug.Log("[FloodAlertOverlayController] Flood alert overlay hidden");
        }

        private void HideImmediate()
        {
            if (overlayRoot != null)
            {
                overlayRoot.RemoveFromClassList(VisibleClass);
                isVisible = false;
                showingCompletion = false;
            }
        }
    }
}
