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
                existing = managerObject.AddComponent<FloodAlertOverlayController>();
                managerObject.AddComponent<UIDocument>();
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
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterChanged;

            // Subscribe to welcome screen completion
            var welcomeManager = WelcomeScreenManager.Instance;
            if (welcomeManager != null)
            {
                welcomeManager.OnWelcomeCompleted += HandleWelcomeCompleted;
                welcomeScreenActive = welcomeManager.IsShowing;
            }
            else
            {
                welcomeScreenActive = false;
            }

            HandleScenarioParameters(FloodScenarioManager.CurrentParameters);
            HandleScenarioProgress(FloodScenarioManager.CurrentProgress);
        }

        private void OnDisable()
        {
            FloodScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
            FloodScenarioManager.OnProgressUpdated -= HandleScenarioProgress;
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
            Debug.Log($"[FloodAlertOverlayController] Received parameters: IsActive={parameters.IsActive}, TargetDepth={parameters.TargetDepthMeters}m, uiBuilt={uiBuilt}");

            if (!uiBuilt)
            {
                CacheElements();

                if (!uiBuilt)
                {
                    if (parameters.IsActive)
                    {
                        pendingParameters = parameters;
                        hasPendingParameters = true;
                    }
                    return;
                }
            }

            // Wait for welcome screen if active
            if (welcomeScreenActive && parameters.IsActive)
            {
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

        private void HandleScenarioProgress(FloodScenarioProgress progress)
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
            welcomeScreenActive = false;

            if (hasPendingParameters && pendingParameters.IsActive)
            {
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

        private void UpdateContent(FloodScenarioParameters parameters)
        {
            if (responseLabel != null)
            {
                // Generate response message based on water depth
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
