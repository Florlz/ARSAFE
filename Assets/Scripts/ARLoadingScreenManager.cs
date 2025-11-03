using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using ARSafe.Modular.Welcome;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Vuforia;

public class ARLoadingScreenManager : MonoBehaviour
{
    public static ARLoadingScreenManager Instance { get; private set; }

    [Header("UI Toolkit (Optional)")]
    [SerializeField] private UIDocument loadingDocument;
    [SerializeField] private string loadingRootElementName = "loading-overlay-root";
    [SerializeField] private string progressElementName = "progress-bar";
    [SerializeField] private string primaryLabelElementName = "primary-status";
    [SerializeField] private string secondaryLabelElementName = "secondary-status";
    [SerializeField] private string progressPercentElementName = "progress-percent";
    [SerializeField] private string areaActivationStatusElementName = "area-activation-status";
    [SerializeField] private string areaActivationPercentElementName = "area-activation-percent";

    [Header("Behaviour")]
    [SerializeField] private bool simulateAssetLoading = true;
    [SerializeField] private float simulatedAssetLoadSeconds = 0.5f; // Reduced from 1.5s for faster mobile startup
    [Range(0.1f, 0.9f)]
    [SerializeField] private float assetStageWeight = 0.4f;
    [SerializeField] private float minimumDisplaySeconds = 0.5f; // Reduced from 2s for faster mobile startup
    [SerializeField] private bool autoStart = true;

    [Header("Scene Restart")]
    [Tooltip("Scene names that should automatically restart the loading flow when loaded again (e.g. returning from MainMenu).")]
    [SerializeField] private List<string> autoRestartSceneNames = new List<string> { "MainScene" };

    [Header("Sequential Activation (Area Targets)")]
    [Tooltip("If true, area targets will be activated sequentially during the loading phase to avoid overloading Vuforia.")]
    [SerializeField] private bool sequentiallyActivateAreaTargets = false;

    [Header("Localization & Welcome")]
    [Tooltip("If true, the modern welcome flow is shown after loading finishes.")]
    [SerializeField] private bool showWelcomeScreenAfterLoading = true;
    [Tooltip("Pass the currently selected disaster type to the welcome screen.")]
    [SerializeField] private bool useSelectedDisasterType = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onLoadingCompleted;

    public bool IsLoading { get; private set; }
    public bool HasCompleted { get; private set; }
    public UnityEvent OnLoadingCompleted => onLoadingCompleted;

    public bool ManagesWelcomeFlow => showWelcomeScreenAfterLoading;

    private const string DefaultDisplayName = "the simulation";
    private const string DefaultGuidanceTemplate = "Aim at the starting markers to localize the {SIMULATION} scene.";

    [System.Serializable]
    private class SimulationVisualProfile
    {
        public DisasterType disasterType = DisasterType.None;
        [Tooltip("Friendly label inserted into loading copy, e.g. 'Fire response'.")]
        public string displayName = DefaultDisplayName;
        [Tooltip("Guidance message displayed when prompting the user to localize. Use {SIMULATION} to insert the display name.")]
        [TextArea(2, 4)] public string guidanceMessage = DefaultGuidanceTemplate;
        [Tooltip("Optional logo override for the loading card.")]
        public Texture2D overlayLogo;
    }

    [Header("Simulation Copy")]
    [SerializeField] private SimulationVisualProfile defaultProfile = new SimulationVisualProfile();
    [SerializeField] private List<SimulationVisualProfile> simulationProfiles = new List<SimulationVisualProfile>();

    private SimulationVisualProfile activeProfile;
    private string activeDisplayName = DefaultDisplayName;
    private string activeGuidanceMessage = DefaultGuidanceTemplate;

    private Coroutine loadingRoutine;

    private VisualElement toolkitRoot;
    private ProgressBar toolkitProgressBar;
    private Label toolkitPrimaryLabel;
    private Label toolkitSecondaryLabel;
    private Label toolkitProgressPercentLabel;
    private Label toolkitAreaActivationStatusLabel;
    private Label toolkitAreaActivationPercentLabel;
    private List<VisualElement> toolkitMeterCells;
    private VisualElement toolkitLogoElement;
    private Texture2D originalLogoTexture;
    private VectorImage originalLogoVector;
    private bool hasOriginalLogoImage;

    private static readonly StyleColor ActiveCellColor = new StyleColor(new Color(0.36f, 0.86f, 1f, 0.95f));
    private static readonly StyleColor InactiveCellColor = new StyleColor(new Color(0.55f, 0.48f, 0.72f, 0.25f));

    // UI runtime helpers for illustrative mockup
    private VisualElement toolkitActivationStepsContainer;
    private bool reduceMotion = false;
    private float targetProgress = 0f;
    private float displayedProgress = 0f;
    private Coroutine progressAnimationRoutine;

    private void OnEnable()
    {
        if (Instance != null && Instance != this)
        {
            return;
        }

    DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
    SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        DontDestroyOnLoad(gameObject);

        PrepareToolkit();
        RefreshActiveProfile(DisasterTypeManager.SelectedDisasterType);
        SetOverlayVisible(false);
    }

    private void Start()
    {
        if (autoStart)
        {
            BeginLoadingSequence();
        }
    }

    public void BeginLoadingSequence()
    {
        RefreshActiveProfile(DisasterTypeManager.SelectedDisasterType);

        HasCompleted = false;

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
        }

        loadingRoutine = StartCoroutine(RunLoadingSequence());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoStart)
        {
            return;
        }

        if (autoRestartSceneNames == null || autoRestartSceneNames.Count == 0)
        {
            return;
        }

        if (!autoRestartSceneNames.Contains(scene.name))
        {
            return;
        }

        if (IsLoading || !HasCompleted)
        {
            return;
        }

        BeginLoadingSequence();
    }

    private IEnumerator RunLoadingSequence()
    {
        IsLoading = true;
        float startTime = Time.time;

        SetOverlayVisible(true);

        SetPrimaryStatus($"Loading {activeDisplayName} environment");
        SetSecondaryStatus($"Preparing {activeDisplayName} content...");

        yield return SimulateAssetLoading();

        bool usingModularIntegration = HasActiveModularIntegration();
        GameObject activationManager = null;

        if (!usingModularIntegration)
        {
            activationManager = FindAreaTargetManager();

            if (sequentiallyActivateAreaTargets && activationManager != null)
            {
                SetSecondaryStatus("Activating area targets...");
                yield return SequentiallyActivateAreaTargets(activationManager);
            }

            yield return HandleAreaTargetWarmup(activationManager);
        }
        else
        {
            ReportAreaTargetProgress(1f, "Handing off to ARSafe integration");
            UpdateAreaActivationStatus("Awaiting modular activation", 0, 0);
        }

        float elapsed = Time.time - startTime;
        if (elapsed < minimumDisplaySeconds)
        {
            yield return new WaitForSeconds(minimumDisplaySeconds - elapsed);
        }

        SetOverlayVisible(false);

        IsLoading = false;
        HasCompleted = true;

        // Restored original behavior: do not explicitly notify the AreaTargetActivationManager here.
        // Localization will proceed according to the manager's original logic.

        yield return ShowWelcomeFlow();

        onLoadingCompleted?.Invoke();
    }

    private IEnumerator SimulateAssetLoading()
    {
        float assetsTarget = assetStageWeight * 100f;

        if (!simulateAssetLoading)
        {
            UpdateProgress(0f);
            SetSecondaryStatus($"Waiting for {activeDisplayName} content...");
            yield break;
        }

        float duration = Mathf.Max(0f, simulatedAssetLoadSeconds);

        UpdateProgress(0f);

        if (duration <= Mathf.Epsilon)
        {
            UpdateProgress(assetsTarget);
            SetSecondaryStatus($"{activeDisplayName} content ready");
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float weighted = Mathf.Lerp(0f, assetsTarget, normalized);
            UpdateProgress(weighted);
            SetSecondaryStatus($"Preparing {activeDisplayName} content ({normalized * 100f:0}%)");
            yield return null;
        }

        UpdateProgress(assetsTarget);
        SetSecondaryStatus($"{activeDisplayName} content ready");
    }

    private IEnumerator SequentiallyActivateAreaTargets(GameObject managerStub)
    {
        // LEGACY: This method is deprecated. ARSafeLoadingIntegration handles activation.
        Debug.LogWarning("[ARLoadingScreenManager] SequentiallyActivateAreaTargets is deprecated. Use ARSafeLoadingIntegration instead.", this);
        ReportAreaTargetProgress(1f, "Using modular activation");
        yield break;
    }

    private IEnumerator HandleAreaTargetWarmup(GameObject managerStub)
    {
        // LEGACY: This method is deprecated. ARSafeLoadingIntegration handles warmup.
        Debug.LogWarning("[ARLoadingScreenManager] HandleAreaTargetWarmup is deprecated. Use ARSafeLoadingIntegration instead.", this);
        
        SetPrimaryStatus($"Preparing {activeDisplayName} tracking");
        UpdateProgress(100f);
        SetSecondaryStatus(activeGuidanceMessage);
        yield break;
    }

    private IEnumerator ShowWelcomeFlow()
    {
        if (HasActiveModularIntegration())
        {
            yield break;
        }

        if (!showWelcomeScreenAfterLoading)
        {
            yield break;
        }

        var welcomeManager = WelcomeScreenManager.Instance;
        if (welcomeManager == null)
        {
            Debug.LogWarning("[LoadingScreen] WelcomeScreenManager not found; skipping welcome flow.");
            yield break;
        }

        bool completed = false;
        UnityAction handler = null;
        handler = () =>
        {
            welcomeManager.OnWelcomeCompleted -= handler;
            completed = true;
        };

        welcomeManager.OnWelcomeCompleted += handler;

        var disaster = useSelectedDisasterType ? DisasterTypeManager.SelectedDisasterType : DisasterType.None;
        welcomeManager.ShowWelcomeScreen(disaster);

        while (!completed)
        {
            yield return null;
        }
    }

    private void UpdateProgress(float value)
    {
        UpdateToolkitProgress(value);
    }

    private void PrepareToolkit()
    {
        if (loadingDocument == null)
        {
            return;
        }

        var root = loadingDocument.rootVisualElement;
        toolkitRoot = string.IsNullOrEmpty(loadingRootElementName)
            ? root
            : root.Q<VisualElement>(loadingRootElementName) ?? root;

        toolkitProgressBar = toolkitRoot?.Q<ProgressBar>(progressElementName);
        toolkitPrimaryLabel = toolkitRoot?.Q<Label>(primaryLabelElementName);
        toolkitSecondaryLabel = toolkitRoot?.Q<Label>(secondaryLabelElementName);
        toolkitMeterCells = toolkitRoot?.Query<VisualElement>(className: "meter-cell").ToList();
        toolkitProgressPercentLabel = toolkitRoot?.Q<Label>(progressPercentElementName);
        toolkitAreaActivationStatusLabel = toolkitRoot?.Q<Label>(areaActivationStatusElementName);
        toolkitAreaActivationPercentLabel = toolkitRoot?.Q<Label>(areaActivationPercentElementName);
        toolkitLogoElement = toolkitRoot?.Q<VisualElement>("logo-slot");

        // activation steps container used by illustrative mockup
        toolkitActivationStepsContainer = toolkitRoot?.Q<VisualElement>("activation-steps");

        if (toolkitLogoElement != null)
        {
            originalLogoTexture = null;
            originalLogoVector = null;
            hasOriginalLogoImage = false;

            var background = toolkitLogoElement.resolvedStyle.backgroundImage;
            originalLogoTexture = background.texture;
            originalLogoVector = background.vectorImage;
            hasOriginalLogoImage = originalLogoTexture != null || originalLogoVector != null;
        }

        if (toolkitProgressBar != null)
        {
            toolkitProgressBar.lowValue = 0f;
            toolkitProgressBar.highValue = 100f;
            toolkitProgressBar.value = 0f;
            toolkitProgressBar.title = string.Empty;
        }

        if (toolkitRoot != null)
        {
            toolkitRoot.style.display = DisplayStyle.None;
        }

        // initialize progress state
        targetProgress = 0f;
        displayedProgress = 0f;
        progressAnimationRoutine = null;

        // clear any existing activation step children
        toolkitActivationStepsContainer?.Clear();

        UpdateToolkitProgress(0f);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (toolkitRoot != null)
        {
            toolkitRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void UpdateToolkitProgress(float value)
    {
        if (toolkitProgressBar == null)
        {
            return;
        }

        // set animation target; actual displayed value animates in AnimateProgressCoroutine
        targetProgress = Mathf.Clamp(value, toolkitProgressBar.lowValue, toolkitProgressBar.highValue);

        // instant update when reduce motion is requested
        if (reduceMotion)
        {
            displayedProgress = targetProgress;
            ApplyProgressToUI(displayedProgress);
            return;
        }

        // start coroutine to animate towards target if not already running
        if (progressAnimationRoutine == null)
        {
            progressAnimationRoutine = StartCoroutine(AnimateProgressCoroutine());
        }
    }

    private void ApplyProgressToUI(float clamped)
    {
        if (toolkitProgressBar != null)
            toolkitProgressBar.value = clamped;

        if (toolkitProgressPercentLabel != null)
            toolkitProgressPercentLabel.text = $"{clamped:0}%";

        if (toolkitMeterCells != null && toolkitMeterCells.Count > 0)
        {
            float normalized = Mathf.Approximately(toolkitProgressBar.highValue, toolkitProgressBar.lowValue)
                ? 1f
                : Mathf.InverseLerp(toolkitProgressBar.lowValue, toolkitProgressBar.highValue, clamped);
            int activeCount = Mathf.Clamp(Mathf.RoundToInt(normalized * toolkitMeterCells.Count), 0, toolkitMeterCells.Count);

            for (int i = 0; i < toolkitMeterCells.Count; i++)
            {
                var cell = toolkitMeterCells[i];
                if (cell == null) continue;
                cell.style.backgroundColor = i < activeCount ? ActiveCellColor : InactiveCellColor;
            }
        }
    }

    private IEnumerator AnimateProgressCoroutine()
    {
        // speed controls how fast the visible progress catches up (percentage points per second)
        const float speed = 120f;
        while (!Mathf.Approximately(displayedProgress, targetProgress))
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, speed * Time.deltaTime);
            ApplyProgressToUI(displayedProgress);
            yield return null;
        }

        // ensure final value applied
        ApplyProgressToUI(targetProgress);
        progressAnimationRoutine = null;
        yield break;
    }

    private void SetPrimaryStatus(string message)
    {
        if (toolkitPrimaryLabel != null)
        {
            toolkitPrimaryLabel.text = message;
        }
    }

    private void SetSecondaryStatus(string message)
    {
        if (toolkitSecondaryLabel != null)
        {
            toolkitSecondaryLabel.text = message;
        }
    }

    private void RefreshActiveProfile(DisasterType type)
    {
        var profile = ResolveProfile(type);
        activeProfile = profile;

        activeDisplayName = string.IsNullOrWhiteSpace(profile.displayName) ? DefaultDisplayName : profile.displayName;

        string guidanceTemplate = string.IsNullOrWhiteSpace(profile.guidanceMessage)
            ? DefaultGuidanceTemplate
            : profile.guidanceMessage;

        activeGuidanceMessage = guidanceTemplate
            .Replace("{SIMULATION}", activeDisplayName)
            .Replace("{simulation}", activeDisplayName.ToLowerInvariant());

        ApplyProfileToUI();
    }

    private SimulationVisualProfile ResolveProfile(DisasterType type)
    {
        if (simulationProfiles != null)
        {
            for (int i = 0; i < simulationProfiles.Count; i++)
            {
                var profile = simulationProfiles[i];
                if (profile != null && profile.disasterType == type)
                {
                    return profile;
                }
            }
        }

        return defaultProfile ?? new SimulationVisualProfile();
    }

    private void ApplyProfileToUI()
    {
        if (toolkitLogoElement == null)
        {
            return;
        }

        if (activeProfile != null && activeProfile.overlayLogo != null)
        {
            toolkitLogoElement.style.backgroundImage = new StyleBackground(activeProfile.overlayLogo);
            toolkitLogoElement.style.unityBackgroundImageTintColor = Color.white;
        }
        else if (hasOriginalLogoImage)
        {
            if (originalLogoTexture != null)
            {
                toolkitLogoElement.style.backgroundImage = new StyleBackground(originalLogoTexture);
            }
            else if (originalLogoVector != null)
            {
                toolkitLogoElement.style.backgroundImage = new StyleBackground(originalLogoVector);
            }
            else
            {
                toolkitLogoElement.style.backgroundImage = StyleKeyword.Null;
            }
        }
        else
        {
            toolkitLogoElement.style.backgroundImage = StyleKeyword.Null;
        }
    }

    private void HandleDisasterTypeChanged(DisasterType type)
    {
        RefreshActiveProfile(type);
    }

    private GameObject FindAreaTargetManager()
    {
        // LEGACY: Stubbed for backward compatibility
        Debug.LogWarning("[ARLoadingScreenManager] FindAreaTargetManager is deprecated.", this);
        return null;
    }

    /// <summary>
    /// LEGACY: This method is deprecated. ARSafeLoadingIntegration handles fallback logic.
    /// </summary>
    private IEnumerator AttemptEmergencyFallback(GameObject managerStub, GameObject[] handlersStub, int activatedCount, int totalCount)
    {
        Debug.LogWarning("[ARLoadingScreenManager] AttemptEmergencyFallback is deprecated. Use ARSafeLoadingIntegration instead.", this);
        SetSecondaryStatus("Legacy fallback skipped");
        yield break;
    }

    /// <summary>
    /// Waits for Vuforia to be fully initialized and ready before proceeding with area target activation.
    /// This prevents observer activation errors that can occur when activating targets too early.
    /// </summary>
    private IEnumerator WaitForVuforiaReady()
    {
        SetSecondaryStatus("Initializing Vuforia...");
        
        float timeout = 10f; // Maximum wait time
        float elapsed = 0f;
        
        // Wait for VuforiaApplication to exist and be running
        while (elapsed < timeout)
        {
            var vuforiaApp = VuforiaApplication.Instance;
            if (vuforiaApp != null && vuforiaApp.IsRunning)
            {
                // Additional wait to ensure observers are ready for registration
                SetSecondaryStatus("Vuforia ready, preparing observers...");
                yield return new WaitForSeconds(0.5f);
                break;
            }
            
            elapsed += Time.deltaTime;
            SetSecondaryStatus($"Waiting for Vuforia initialization... ({elapsed:F1}s)");
            yield return new WaitForSeconds(0.1f);
        }
        
        if (elapsed >= timeout)
        {
            Debug.LogWarning("[LoadingScreen] Vuforia initialization timeout - proceeding with caution");
            SetSecondaryStatus("Vuforia timeout - attempting activation...");
        }
        else
        {
            Debug.Log("[LoadingScreen] Vuforia is ready for area target activation");
            SetSecondaryStatus("Vuforia ready");
        }
    }

    public void ReportAssetProgress(float normalized, string status = null)
    {
        float weighted = Mathf.Lerp(0f, assetStageWeight * 100f, Mathf.Clamp01(normalized));
        UpdateProgress(weighted);
        if (!string.IsNullOrEmpty(status))
        {
            SetSecondaryStatus(status);
        }
    }

    public void ReportAreaTargetProgress(float normalized, string status = null)
    {
        float weighted = Mathf.Lerp(assetStageWeight * 100f, 100f, Mathf.Clamp01(normalized));
        UpdateProgress(weighted);
        if (!string.IsNullOrEmpty(status))
        {
            SetSecondaryStatus(status);
        }
    }

    /// <summary>
    /// Update the area activation status display in the loading UI.
    /// Integrates with ARSafeActivationController to show real-time activation progress.
    /// </summary>
    /// <param name="statusMessage">Status message to display (e.g., "Activating targets...")</param>
    /// <param name="activatedCount">Number of targets activated</param>
    /// <param name="totalCount">Total number of targets</param>
    public void UpdateAreaActivationStatus(string statusMessage, int activatedCount, int totalCount)
    {
        if (toolkitAreaActivationStatusLabel != null)
        {
            toolkitAreaActivationStatusLabel.text = statusMessage;
        }

        if (toolkitAreaActivationPercentLabel != null)
        {
            toolkitAreaActivationPercentLabel.text = $"{activatedCount}/{totalCount}";
        }
    }

    /// <summary>
    /// Simplified method to update just the activation counts.
    /// </summary>
    public void UpdateAreaActivationCounts(int activatedCount, int totalCount)
    {
        if (toolkitAreaActivationPercentLabel != null)
        {
            toolkitAreaActivationPercentLabel.text = $"{activatedCount}/{totalCount}";
        }
    }

    public void SetStageLabel(string message)
    {
        SetPrimaryStatus(message);
    }

    private bool HasActiveModularIntegration()
    {
        var integration = GetComponent<ARSafe.Modular.Integration.ARSafeLoadingIntegration>();
        return integration != null && integration.isActiveAndEnabled;
    }
}
