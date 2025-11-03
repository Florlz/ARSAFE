using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;
using ARSafe.Modular.Welcome;

namespace ARSafe.UI
{
    /// <summary>
    /// Displays a UI Toolkit overlay announcing the procedurally generated earthquake magnitude/intensity.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EarthquakeAlertOverlayController : MonoBehaviour
    {
        private const string VisibleClass = "earthquake-alert__root--visible";
        private const string OverlayRootName = "earthquake-alert-overlay";
        private const string MagnitudeLabelName = "magnitude-label";
        private const string IntensityLabelName = "intensity-label";
        private const string ResponseLabelName = "response-label";
        private const string AcknowledgeButtonName = "acknowledge-button";
        private const string StartSectionName = "earthquake-alert-start";
        private const string CompleteSectionName = "earthquake-alert-complete";
        private const string CompleteButtonName = "complete-button";
        private const string HiddenSectionClass = "earthquake-alert__section--hidden";
    private const string TemplateResourcePath = "UI/EarthquakeAlert/EarthquakeAlertOverlay";
    private const string StyleResourcePath = "UI/EarthquakeAlert/EarthquakeAlertStyles";

        [SerializeField, Tooltip("Optional PanelSettings override. Null uses the scene default.")]
        private PanelSettings panelSettings;

        [SerializeField, Tooltip("Automatically hide the alert after this many seconds (0 keeps it visible until acknowledged).")]
        private float autoHideDelay = 0f;

    public static EarthquakeAlertOverlayController Instance { get; private set; }
    private static PanelSettings runtimePanelSettings;

        private UIDocument uiDocument;
        private VisualElement overlayRoot;
        private Label magnitudeLabel;
        private Label intensityLabel;
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
    private EarthquakeScenarioParameters pendingParameters;
    private bool hasPendingParameters;
    private bool styleSheetApplied;
    private bool scenarioActive;

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
                Debug.LogError("[EarthquakeAlertOverlayController] Missing UIDocument component.");
                return;
            }

            if (panelSettings != null)
            {
                uiDocument.panelSettings = panelSettings;
            }
        }

        private void OnEnable()
        {
            if (uiDocument == null)
            {
                return;
            }

            EnsureDocumentTemplate();
            CacheElements();

            EarthquakeScenarioManager.OnParametersUpdated += HandleScenarioParameters;
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterChanged;
            EarthquakeScenarioManager.OnProgressUpdated += HandleScenarioProgress;
            
            // Subscribe to welcome screen completion and capture current visibility state
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
            
            HandleScenarioParameters(EarthquakeScenarioManager.CurrentParameters);
            HandleScenarioProgress(EarthquakeScenarioManager.CurrentProgress);
        }

        private void OnDisable()
        {
            EarthquakeScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterChanged;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleScenarioProgress;
            
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
            magnitudeLabel = null;
            intensityLabel = null;
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
                Debug.LogWarning("[EarthquakeAlertOverlayController] Root visual element is null, retrying...");
                return;
            }

            overlayRoot = root.Q<VisualElement>(OverlayRootName);
            magnitudeLabel = root.Q<Label>(MagnitudeLabelName);
            intensityLabel = root.Q<Label>(IntensityLabelName);
            responseLabel = root.Q<Label>(ResponseLabelName);
            acknowledgeButton = root.Q<Button>(AcknowledgeButtonName);
            startSection = root.Q<VisualElement>(StartSectionName);
            completeSection = root.Q<VisualElement>(CompleteSectionName);
            completeButton = root.Q<Button>(CompleteButtonName);

            if (overlayRoot == null)
            {
                Debug.LogWarning($"[EarthquakeAlertOverlayController] Could not find '{OverlayRootName}' element. Ensure UIDocument Source Asset is set to EarthquakeAlertOverlay.uxml");
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

            Debug.Log($"[EarthquakeAlertOverlayController] Successfully cached UI elements. Root found: {overlayRoot != null}");
            uiBuilt = true;
            HideImmediate();
            
            // If parameters were received while UI was building, show them now
            if (EarthquakeScenarioManager.CurrentParameters.IsActive)
            {
                Debug.Log("[EarthquakeAlertOverlayController] UI now ready - showing cached parameters");
                HandleScenarioParameters(EarthquakeScenarioManager.CurrentParameters);
            }
        }

        private void HandleScenarioParameters(EarthquakeScenarioParameters parameters)
        {
            Debug.Log($"[EarthquakeAlertOverlayController] Received parameters: IsActive={parameters.IsActive}, Magnitude={parameters.Magnitude:F1}, uiBuilt={uiBuilt}, welcomeScreenActive={welcomeScreenActive}");
            
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
                        Debug.LogWarning("[EarthquakeAlertOverlayController] UI not ready, caching parameters for retry");
                    }
                    return;
                }
            }

            // If welcome screen is active, wait until it's dismissed
            if (welcomeScreenActive && parameters.IsActive)
            {
                Debug.Log("[EarthquakeAlertOverlayController] Welcome screen active - deferring overlay until welcome completes");
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
        
        private void HandleWelcomeCompleted()
        {
            Debug.Log("[EarthquakeAlertOverlayController] Welcome screen completed");
            welcomeScreenActive = false;
            
            // Show pending parameters if we have them
            if (hasPendingParameters && pendingParameters.IsActive)
            {
                Debug.Log("[EarthquakeAlertOverlayController] Showing deferred earthquake overlay");
                HandleScenarioParameters(pendingParameters);
                if (!hasPendingParameters)
                {
                    pendingParameters = default;
                }
            }
        }

        private void HandleDisasterChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Earthquake)
            {
                HideOverlay();
                showingCompletion = false;
            }
        }

        private void HandleScenarioProgress(EarthquakeScenarioProgress progress)
        {
            if (!uiBuilt)
            {
                return;
            }

            if (!scenarioActive)
            {
                return;
            }

            Debug.Log($"[EarthquakeAlertOverlayController] Progress update - IsComplete: {progress.IsComplete}, Elapsed: {progress.ElapsedSeconds:F1}/{progress.DurationSeconds:F1}s, showingCompletion: {showingCompletion}");

            if (progress.IsComplete && !showingCompletion)
            {
                Debug.Log("[EarthquakeAlertOverlayController] Showing completion overlay");
                ShowCompletionOverlay();
            }
        }

        private void UpdateContent(EarthquakeScenarioParameters parameters)
        {
            if (magnitudeLabel != null)
            {
                magnitudeLabel.text = $"{parameters.Magnitude:F1}";
            }

            if (intensityLabel != null)
            {
                intensityLabel.text = parameters.IntensityLabel;
            }

            if (responseLabel != null)
            {
                responseLabel.text = parameters.ResponseMessage;
            }
        }

        private void ShowOverlay(bool enableAutoHide)
        {
            if (overlayRoot == null)
            {
                Debug.LogWarning("[EarthquakeAlertOverlayController] Cannot show overlay - overlayRoot is null");
                return;
            }

            Debug.Log($"[EarthquakeAlertOverlayController] Showing earthquake alert overlay");
            overlayRoot.AddToClassList(VisibleClass);
            allowAutoHide = enableAutoHide && autoHideDelay > 0f;
            hideTimer = allowAutoHide ? autoHideDelay : 0f;
            isVisible = true;
        }

        private void ShowStartOverlay()
        {
            if (!uiBuilt)
            {
                return;
            }

            Debug.Log("[EarthquakeAlertOverlayController] Displaying start overlay");
            ToggleSections(showStart: true);
            showingCompletion = false;
            ShowOverlay(enableAutoHide: true);
        }

        private void ShowCompletionOverlay()
        {
            if (!uiBuilt)
            {
                Debug.LogWarning("[EarthquakeAlertOverlayController] Cannot show completion - UI not built");
                return;
            }

            Debug.Log("[EarthquakeAlertOverlayController] Displaying completion overlay");
            ToggleSections(showStart: false);
            showingCompletion = true;
            ShowOverlay(enableAutoHide: false);
        }

        private void ToggleSections(bool showStart)
        {
            if (startSection != null)
            {
                if (showStart)
                {
                    startSection.RemoveFromClassList(HiddenSectionClass);
                }
                else
                {
                    startSection.AddToClassList(HiddenSectionClass);
                }
            }

            if (completeSection != null)
            {
                if (showStart)
                {
                    completeSection.AddToClassList(HiddenSectionClass);
                }
                else
                {
                    completeSection.RemoveFromClassList(HiddenSectionClass);
                }
            }
        }

        private void HideOverlay()
        {
            if (overlayRoot == null)
            {
                return;
            }

            overlayRoot.RemoveFromClassList(VisibleClass);
            isVisible = false;
            hideTimer = 0f;
            allowAutoHide = false;
            showingCompletion = false;
        }

        private void HideImmediate()
        {
            if (overlayRoot == null)
            {
                return;
            }

            overlayRoot.RemoveFromClassList(VisibleClass);
            isVisible = false;
            hideTimer = 0f;
            allowAutoHide = false;
            showingCompletion = false;
        }

        private void EnsureDocumentTemplate()
        {
            if (uiDocument == null)
            {
                return;
            }

            if (uiDocument.visualTreeAsset == null)
            {
                var template = Resources.Load<VisualTreeAsset>(TemplateResourcePath);
                if (template != null)
                {
                    uiDocument.visualTreeAsset = template;
                }
                else
                {
                    Debug.LogError($"[EarthquakeAlertOverlayController] Failed to load template at Resources/{TemplateResourcePath}");
                }
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                return;
            }

            if (!styleSheetApplied)
            {
                var style = Resources.Load<StyleSheet>(StyleResourcePath);
                if (style != null)
                {
                    if (!HasStyleSheet(root, style))
                    {
                        root.styleSheets.Add(style);
                    }
                    styleSheetApplied = true;
                }
                else
                {
                    Debug.LogError($"[EarthquakeAlertOverlayController] Failed to load stylesheet at Resources/{StyleResourcePath}");
                }
            }
        }

        private static bool HasStyleSheet(VisualElement root, StyleSheet styleSheet)
        {
            if (root == null || styleSheet == null)
            {
                return false;
            }

            for (int i = 0; i < root.styleSheets.count; i++)
            {
                if (root.styleSheets[i] == styleSheet)
                {
                    return true;
                }
            }

            return false;
        }

        public static EarthquakeAlertOverlayController EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<EarthquakeAlertOverlayController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("EarthquakeAlertOverlay");
            DontDestroyOnLoad(go);

            var panelSettings = ResolvePanelSettings();
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 450;

            go.AddComponent<EarthquakeAlertOverlayController>();
            return Instance;
        }

        private static PanelSettings ResolvePanelSettings()
        {
            if (Instance != null && Instance.uiDocument != null && Instance.uiDocument.panelSettings != null)
            {
                return Instance.uiDocument.panelSettings;
            }

            if (runtimePanelSettings != null)
            {
                return runtimePanelSettings;
            }

            var existingDoc = FindFirstObjectByType<UIDocument>(FindObjectsInactive.Include);
            if (existingDoc != null && existingDoc.panelSettings != null)
            {
                runtimePanelSettings = existingDoc.panelSettings;
                return runtimePanelSettings;
            }

            runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            runtimePanelSettings.name = "EarthquakeAlertPanelSettings (Runtime)";
            runtimePanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            runtimePanelSettings.referenceResolution = new Vector2Int(1920, 1080);
            runtimePanelSettings.match = 0.5f;
            runtimePanelSettings.sortingOrder = 0;
            runtimePanelSettings.targetTexture = null;
            runtimePanelSettings.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            // Unity 6+ requires a theme stylesheet - try multiple sources
            PanelSettings defaultSettings = Resources.Load<PanelSettings>("DefaultPanelSettings");
            if (defaultSettings != null && defaultSettings.themeStyleSheet != null)
            {
                runtimePanelSettings.themeStyleSheet = defaultSettings.themeStyleSheet;
            }
            else
            {
                var defaultTheme = Resources.Load<ThemeStyleSheet>("unity-default-runtime-theme");
                if (defaultTheme != null)
                {
                    runtimePanelSettings.themeStyleSheet = defaultTheme;
                }
            }

            return runtimePanelSettings;
        }
    }
}
