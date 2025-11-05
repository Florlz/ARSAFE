using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;
using ARSafe.Modular.Welcome;

namespace ARSafe.UI
{
    /// <summary>
    /// Displays a UI Toolkit overlay for fire emergency with smoke and heat effects.
    /// Shows fire severity, smoke density, and evacuation guidance.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class FireAlertOverlayController : MonoBehaviour
    {
        private const string VisibleClass = "fire-alert__root--visible";
        private const string OverlayRootName = "fire-alert-overlay";
        private const string AlarmLevelValueName = "alarm-level-value";
        private const string AlarmLevelLabelName = "alarm-level-label";
        private const string ResponseTypeLabelName = "response-type-label";
        private const string ResponseLabelName = "response-label";
        private const string AcknowledgeButtonName = "acknowledge-button";
        private const string StartSectionName = "fire-alert-start";
        private const string CompleteSectionName = "fire-alert-complete";
        private const string CompleteButtonName = "complete-button";
        private const string HiddenSectionClass = "fire-alert__section--hidden";
        private const string TemplateResourcePath = "UI/FireAlert/FireAlertOverlay";
        private const string StyleResourcePath = "UI/FireAlert/FireAlertStyles";

        [SerializeField, Tooltip("Optional PanelSettings override. Null uses the scene default.")]
        private PanelSettings panelSettings;

        [SerializeField, Tooltip("Automatically hide the alert after this many seconds (0 keeps it visible until acknowledged).")]
        private float autoHideDelay = 0f;

        public static FireAlertOverlayController Instance { get; private set; }

        private UIDocument uiDocument;
        private VisualElement overlayRoot;
        private Label alarmLevelValue;
        private Label alarmLevelLabel;
        private Label responseTypeLabel;
        private Label responseLabel;
        private Button acknowledgeButton;
        private VisualElement startSection;
        private VisualElement completeSection;
        private Button completeButton;

        private bool isVisible;
        private float hideTimer;
        private bool uiBuilt;
        private IVisualElementScheduledItem cacheRetryItem;
        private bool allowAutoHide;
        private bool showingCompletion;
        private bool welcomeScreenActive;
        private FireScenarioParameters pendingParameters;
        private bool hasPendingParameters;
        private bool scenarioActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>
        /// Ensures an instance exists. Finds manually-placed GameObject first, creates new one only if none found.
        /// Call this from FireScenarioManager when Fire disaster is selected.
        /// </summary>
        public static void EnsureInstance()
        {
            if (Instance != null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindFirstObjectByType<FireAlertOverlayController>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Debug.Log("[FireAlertOverlayController] No manually-placed instance found, creating new GameObject.");
                var managerObject = new GameObject(nameof(FireAlertOverlayController));
                managerObject.transform.SetParent(null); // Ensure root-level for DontDestroyOnLoad

                // CRITICAL: Add UIDocument BEFORE FireAlertOverlayController
                // Otherwise Awake() runs before UIDocument exists, causing GetComponent<UIDocument>() to return null
                managerObject.AddComponent<UIDocument>();
                existing = managerObject.AddComponent<FireAlertOverlayController>();
            }
            else
            {
                Debug.Log($"[FireAlertOverlayController] Using manually-placed instance: {existing.gameObject.name}");
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
                Debug.LogWarning($"[FireAlertOverlayController] Moving {gameObject.name} to scene root for DontDestroyOnLoad");
                transform.SetParent(null);
            }

            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[FireAlertOverlayController] Missing UIDocument component.");
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

            FireScenarioManager.OnParametersUpdated += HandleScenarioParameters;
            FireScenarioManager.OnProgressUpdated += HandleScenarioProgress;
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
                    Debug.Log("[FireAlertOverlayController] Welcome screen exists but not showing - proceeding immediately");
                }
            }
            else
            {
                // No welcome screen in scene - proceed immediately
                welcomeScreenActive = false;
                Debug.Log("[FireAlertOverlayController] No welcome screen found - proceeding immediately");
            }

            HandleScenarioParameters(FireScenarioManager.CurrentParameters);
            HandleScenarioProgress(FireScenarioManager.CurrentProgress);
        }

        private void OnDisable()
        {
            // Wrap in try-catch to prevent crashes during Unity shutdown
            try
            {
                FireScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
                FireScenarioManager.OnProgressUpdated -= HandleScenarioProgress;
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
                Debug.LogError($"[FireAlertOverlayController] Failed to load UXML template from Resources/{TemplateResourcePath}");
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
                Debug.LogWarning("[FireAlertOverlayController] Root visual element is null, retrying...");
                return;
            }

            overlayRoot = root.Q<VisualElement>(OverlayRootName);
            alarmLevelValue = root.Q<Label>(AlarmLevelValueName);
            alarmLevelLabel = root.Q<Label>(AlarmLevelLabelName);
            responseTypeLabel = root.Q<Label>(ResponseTypeLabelName);
            responseLabel = root.Q<Label>(ResponseLabelName);
            acknowledgeButton = root.Q<Button>(AcknowledgeButtonName);
            startSection = root.Q<VisualElement>(StartSectionName);
            completeSection = root.Q<VisualElement>(CompleteSectionName);
            completeButton = root.Q<Button>(CompleteButtonName);

            if (overlayRoot == null)
            {
                Debug.LogWarning($"[FireAlertOverlayController] Could not find '{OverlayRootName}' element.");
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

            Debug.Log($"[FireAlertOverlayController] Successfully cached UI elements.");
            uiBuilt = true;
            HideImmediate();

            if (FireScenarioManager.CurrentParameters.IsActive)
            {
                HandleScenarioParameters(FireScenarioManager.CurrentParameters);
            }
        }

        private void HandleScenarioParameters(FireScenarioParameters parameters)
        {
            Debug.Log($"[FireAlertOverlayController] Received parameters: IsActive={parameters.IsActive}, Intensity={parameters.IntensityLabel}, uiBuilt={uiBuilt}, welcomeScreenActive={welcomeScreenActive}");

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
                        Debug.LogWarning("[FireAlertOverlayController] UI not ready, scheduling retry in 100ms");

                        // Schedule retry after a short delay to give UI time to build
                        if (uiDocument != null && uiDocument.rootVisualElement != null)
                        {
                            uiDocument.rootVisualElement.schedule.Execute(() =>
                            {
                                if (hasPendingParameters && !uiBuilt)
                                {
                                    Debug.Log("[FireAlertOverlayController] Retrying UI build after scheduled delay");
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
                Debug.Log("[FireAlertOverlayController] Welcome screen showing - deferring overlay until welcome completes");
                pendingParameters = parameters;
                hasPendingParameters = true;
                return;
            }

            if (parameters.IsActive)
            {
                UpdateContent(parameters);
                ShowStartOverlay();
                hasPendingParameters = false;
                scenarioActive = true;
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

        private void HandleScenarioProgress(FireScenarioProgress progress)
        {
            if (!uiBuilt || !scenarioActive)
            {
                return;
            }

            if (progress.IsComplete)
            {
                ShowCompleteOverlay();
            }
        }

        private void HandleWelcomeCompleted()
        {
            Debug.Log("[FireAlertOverlayController] Welcome screen completed");
            welcomeScreenActive = false;

            // Show pending parameters if we have them
            if (hasPendingParameters && pendingParameters.IsActive)
            {
                Debug.Log("[FireAlertOverlayController] Showing deferred fire overlay");
                HandleScenarioParameters(pendingParameters);
                if (!hasPendingParameters)
                {
                    pendingParameters = default;
                }
            }
        }

        private void HandleDisasterChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Fire)
            {
                HideOverlay();
                scenarioActive = false;
            }
        }

        private void UpdateContent(FireScenarioParameters parameters)
        {
            // Update alarm level stat card
            if (alarmLevelValue != null)
            {
                alarmLevelValue.text = parameters.AlarmLevel.ToString();
            }

            if (alarmLevelLabel != null)
            {
                alarmLevelLabel.text = parameters.AlarmLevelLabel;
            }

            if (responseTypeLabel != null)
            {
                responseTypeLabel.text = parameters.ResponseType;
            }

            // Update response message
            if (responseLabel != null)
            {
                responseLabel.text = parameters.ResponseMessage;
            }
        }

        private void ShowStartOverlay()
        {
            if (!uiBuilt || overlayRoot == null)
            {
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

            Debug.Log("[FireAlertOverlayController] Fire alert overlay shown (START)");
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

            Debug.Log("[FireAlertOverlayController] Fire alert overlay shown (COMPLETE)");
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

            Debug.Log("[FireAlertOverlayController] Fire alert overlay hidden");
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
