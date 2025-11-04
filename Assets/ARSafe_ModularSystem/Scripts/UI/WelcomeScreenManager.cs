using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace ARSafe.Modular.Welcome
{
    /// <summary>
    /// Welcome flow implemented with Unity UI Toolkit. Presents a disaster-specific overlay
    /// that replaces the legacy Modern UI Pack modal prefab.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class WelcomeScreenManager : MonoBehaviour
    {
    [Serializable]
    private class DisasterModalContent
    {
        public DisasterType disasterType = DisasterType.None;
        public Sprite bannerOverride;
        public Sprite iconOverride;
        public string titleOverride;
        [TextArea(3, 10)] public string descriptionOverride;
        public string confirmButtonOverride;
        public string skipButtonOverride;
    }

    private const string DONT_SHOW_AGAIN_KEY = "ARSAFE_WelcomeSuppressed";
    private const string TEMPLATE_RESOURCE_PATH = "UI/Welcome/WelcomeScreen";
    private const string STYLE_RESOURCE_PATH = "UI/Welcome/WelcomeScreenStyles";

    [Header("UI Toolkit")]
    [Tooltip("Optional override for the welcome screen UXML template. If null, the manager loads it from Resources/UI/Welcome/WelcomeScreen.")]
    [SerializeField] private VisualTreeAsset welcomeScreenTemplate;

    [Tooltip("Optional override for the welcome stylesheet. If null, loads Resources/UI/Welcome/WelcomeScreenStyles.")]
    [SerializeField] private StyleSheet welcomeScreenStyles;

    [Tooltip("PanelSettings asset required for UI Toolkit rendering. Assign 'Assets/UI Toolkit/PanelSettings.asset' from the Inspector.")]
    [SerializeField] private PanelSettings panelSettingsAsset;

    [Tooltip("Sorting order for the welcome overlay UIDocument so it renders above other UI Toolkit panels.")]
    [SerializeField] private int documentSortingOrder = 500;

    [Header("Content")]
    [SerializeField] public bool forceShowWelcome = false;
    [SerializeField] private Sprite defaultBannerImage;
    [SerializeField] private Sprite modalIcon;
    [SerializeField] private string modalTitle = "Welcome to ARSAFE Training";
    [TextArea(3, 10)]
    [SerializeField] private string descriptionTemplate =
        "You're about to begin the {0} training scenario.\n\n" +
        "1. Review the safety card in front of you and confirm your physical surroundings are clear.\n" +
        "2. Point your device at the floor markers until the AR environment anchors in place.\n" +
        "3. Follow the on-screen prompts, complete each task, and acknowledge hazards before proceeding.\n\n" +
        "Tap Begin when you're ready to enter the simulation.";
    [SerializeField] private string confirmButtonText = "Begin Simulation";
    [SerializeField] private string skipButtonText = "Skip for now";
    [SerializeField] private bool allowSkip = false;
    [SerializeField] private bool markDontShowWhenSkipped = false;
    [SerializeField] private bool allowDontShowToggle = true;
    [SerializeField] private string dontShowToggleLabel = "Don't show this again";
    [SerializeField] private List<DisasterModalContent> disasterContentOverrides = new();

    public event UnityAction OnWelcomeCompleted;

    private static WelcomeScreenManager _instance;
    private static bool _runtimeInstanceCreated;
    private static bool applicationIsQuitting;
    private UIDocument welcomeDocument;
    private TemplateContainer activeTemplate;
    private VisualElement overlayElement;
    private VisualElement cardElement;
    private VisualElement bannerElement;
    private VisualElement iconElement;
    private Label titleLabel;
    private Label scenarioLabel;
    private Label introLabel;
    private VisualElement instructionsContainer;
    private Label footerLabel;
    private Toggle dontShowToggle;
    private Button confirmButton;
    private Button skipButton;
    private bool completionDispatched;
    private DisasterType currentSimulation;
    private DisasterModalContent currentOverride;
    public bool IsShowing { get; private set; }

    public static WelcomeScreenManager Instance
    {
        get
        {
            if (_instance == null && !applicationIsQuitting)
            {
                // Only look for instance in MainScene (where it should be manually placed)
                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.name == "MainScene")
                {
                    _instance = FindFirstObjectByType<WelcomeScreenManager>(FindObjectsInactive.Include);
                    // Do NOT auto-create - user must place WelcomeScreenManager in scene manually
                    // This ensures Panel Settings and other Inspector fields are properly configured
                    if (_instance == null)
                    {
                        Debug.LogWarning("[WelcomeScreenManager] No WelcomeScreenManager found in MainScene. Please add one manually to MainScene.");
                    }
                }
                // If not in MainScene (e.g., MainMenu), return null silently - will be available once MainScene loads
            }

            return _instance;
        }
        private set => _instance = value;
    }

    public static bool TryGetInstance(out WelcomeScreenManager manager)
    {
        manager = _instance;
        if (manager != null)
        {
            return true;
        }

        if (applicationIsQuitting)
        {
            manager = null;
            return false;
        }

        manager = FindFirstObjectByType<WelcomeScreenManager>(FindObjectsInactive.Include);
        if (manager != null)
        {
            _instance = manager;
            return true;
        }

        manager = null;
        return false;
    }

    private void Awake()
    {
        applicationIsQuitting = false;

        if (_instance != null && _instance != this)
        {
            // If another instance already exists (likely DontDestroyOnLoad), attempt to adopt
            // serialized content from this scene instance before destroying it.
            TryAdoptContentFrom(this, _instance);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }

        welcomeDocument = GetComponent<UIDocument>();
        if (welcomeDocument == null)
        {
            Debug.LogError("[WelcomeScreen] UIDocument component is required on the WelcomeScreenManager GameObject.");
        }
        else
        {
            // Simple PanelSettings assignment - matches overlay controller pattern
            // Use whatever is assigned in Inspector (UIDocument or panelSettingsAsset field)
            // If both are null, Unity will show clear error instead of confusing fallback behavior
            if (panelSettingsAsset != null)
            {
                welcomeDocument.panelSettings = panelSettingsAsset;
                Debug.Log($"<color=green>[WelcomeScreen] ✓ Using assigned PanelSettings: {panelSettingsAsset.name}</color>");
            }
            else if (welcomeDocument.panelSettings != null)
            {
                Debug.Log($"<color=green>[WelcomeScreen] ✓ Using UIDocument's PanelSettings: {welcomeDocument.panelSettings.name}</color>");
            }
            else
            {
                Debug.LogWarning("<color=yellow>[WelcomeScreen] ⚠️ No PanelSettings assigned! Please assign 'Assets/UI Toolkit/PanelSettings.asset' in Inspector.</color>");
            }

            // Keep UIDocument enabled - prevents race conditions with rootVisualElement availability
            // Visibility is controlled via DisplayStyle.None instead of disabling the component
        }

        DontDestroyOnLoad(gameObject);

        // Listen to scene changes to hide welcome screen when returning to main menu
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Wrap entire cleanup in try-catch to prevent Unity Editor crashes during shutdown
        try
        {
            // CRITICAL: Unsubscribe from scene changes to prevent memory leaks
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // CRITICAL: Clean up UI element references and event callbacks
            // This prevents null reference errors when accessing UI elements after destruction
            try
            {
                DetachUIEvents();
            }
            catch { /* Ignore errors during shutdown */ }

            // Clear the visual tree to prevent lingering references
            try
            {
                if (welcomeDocument != null && welcomeDocument.rootVisualElement != null)
                {
                    welcomeDocument.rootVisualElement.Clear();
                }
            }
            catch { /* Ignore errors during shutdown */ }

            // Clear template reference
            activeTemplate = null;

            // Clear singleton reference
            if (_instance == this)
            {
                _instance = null;
            }

            // Clear UIDocument reference
            welcomeDocument = null;

            // Mark as not showing
            IsShowing = false;
        }
        catch
        {
            // Silent catch - don't log during shutdown to avoid crashes
        }
    }

    /// <summary>
    /// If a runtime-created instance exists with empty/default content, and a scene-provided
    /// instance (donor) has serialized overrides (banner/icon/content lists), copy those into
    /// the existing instance before destroying the donor. This preserves Inspector overrides
    /// when entering MainScene from MainMenu.
    /// </summary>
    private static void TryAdoptContentFrom(WelcomeScreenManager donor, WelcomeScreenManager target)
    {
        if (donor == null || target == null || donor == target)
            return;

        // Prefer donor PanelSettings/policy if target has none or we came from a runtime creation
        if (target.panelSettingsAsset == null && donor.panelSettingsAsset != null)
        {
            target.panelSettingsAsset = donor.panelSettingsAsset;
        }
        // Content images and strings
        if (target.defaultBannerImage == null && donor.defaultBannerImage != null)
            target.defaultBannerImage = donor.defaultBannerImage;
        if (target.modalIcon == null && donor.modalIcon != null)
            target.modalIcon = donor.modalIcon;
        if (string.IsNullOrWhiteSpace(target.modalTitle) && !string.IsNullOrWhiteSpace(donor.modalTitle))
            target.modalTitle = donor.modalTitle;
        if (string.IsNullOrWhiteSpace(target.descriptionTemplate) && !string.IsNullOrWhiteSpace(donor.descriptionTemplate))
            target.descriptionTemplate = donor.descriptionTemplate;
        if (string.IsNullOrWhiteSpace(target.confirmButtonText) && !string.IsNullOrWhiteSpace(donor.confirmButtonText))
            target.confirmButtonText = donor.confirmButtonText;
        if (string.IsNullOrWhiteSpace(target.skipButtonText) && !string.IsNullOrWhiteSpace(donor.skipButtonText))
            target.skipButtonText = donor.skipButtonText;

        // Toggles and behavior
        if (_runtimeInstanceCreated)
        {
            target.allowSkip = donor.allowSkip;
            target.markDontShowWhenSkipped = donor.markDontShowWhenSkipped;
            target.allowDontShowToggle = donor.allowDontShowToggle;
            if (string.IsNullOrWhiteSpace(target.dontShowToggleLabel) && !string.IsNullOrWhiteSpace(donor.dontShowToggleLabel))
                target.dontShowToggleLabel = donor.dontShowToggleLabel;
        }

        // Disaster content overrides: adopt when target has none
        if ((target.disasterContentOverrides == null || target.disasterContentOverrides.Count == 0) &&
            donor.disasterContentOverrides != null && donor.disasterContentOverrides.Count > 0)
        {
            target.disasterContentOverrides = new List<DisasterModalContent>(donor.disasterContentOverrides);
        }
    }

    /// <summary>
    /// Hide welcome screen when returning to main menu
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If we're back in the main menu, hide the welcome screen
        if (scene.name.Equals("MainMenu", StringComparison.OrdinalIgnoreCase))
        {
            HideWelcomeUI();

            // UIDocument stays enabled - visibility controlled via clearing rootVisualElement
            Debug.Log("<color=yellow>[WelcomeScreen] Main menu loaded - hiding welcome screen</color>");
        }
    }

    private void OnApplicationQuit()
    {
        applicationIsQuitting = true;
    }

    public static bool IsFirstSimulationSelection()
    {
        if (TryGetInstance(out var manager) && manager.forceShowWelcome)
        {
            return true;
        }

        return !HasUserSuppressedWelcome();
    }

    public static bool HasUserSuppressedWelcome()
    {
#if UNITY_EDITOR
        // In editor mode, always allow welcome screen for testing
        return false;
#else
        return PlayerPrefs.GetInt(DONT_SHOW_AGAIN_KEY, 0) == 1;
#endif
    }

    public void ShowWelcomeScreen(DisasterType selectedSimulation = DisasterType.None)
    {
        Debug.Log($"<color=cyan>[WelcomeScreen] ShowWelcomeScreen called for {selectedSimulation}</color>");
        
        if (welcomeDocument == null)
        {
            welcomeDocument = GetComponent<UIDocument>();
        }

        if (welcomeDocument == null)
        {
            Debug.LogError("[WelcomeScreen] UIDocument component missing. Skipping welcome flow.");
            CompleteWelcome(false);
            return;
        }

        // Get template and stylesheet from Inspector fields or Resources
        VisualTreeAsset template = ResolveTemplate();
        if (template == null)
        {
            Debug.LogError("[WelcomeScreen] No template found! Check Inspector or Resources/UI/Welcome/WelcomeScreen");
            CompleteWelcome(false);
            return;
        }

        StyleSheet stylesheet = ResolveStyleSheet();

        // UIDocument is kept enabled throughout lifecycle (no toggling)
        // Set sorting order for proper layering
        welcomeDocument.sortingOrder = documentSortingOrder;

        if (welcomeDocument.panelSettings == null)
        {
            Debug.LogWarning("<color=yellow>[WelcomeScreen] ⚠️ No PanelSettings assigned! UI may not render properly.</color>");
        }
        else
        {
            Debug.Log($"<color=green>[WelcomeScreen] ✓ Using PanelSettings: {welcomeDocument.panelSettings.name}</color>");
        }

        completionDispatched = false;
        currentSimulation = selectedSimulation;
        currentOverride = ResolveContentOverride(selectedSimulation);

        // Build the visual tree from template
        BuildVisualTree(template, stylesheet);

        // Configure content
        ConfigureContent(selectedSimulation, currentOverride);

        IsShowing = true;

        if (overlayElement != null)
        {
            Debug.Log("<color=green>[WelcomeScreen] Setting overlay to VISIBLE (DisplayStyle.Flex)</color>");
            overlayElement.style.display = DisplayStyle.Flex;
            overlayElement.style.opacity = 1f;
            overlayElement.Focus();
            Debug.Log($"<color=green>[WelcomeScreen] ✓ Overlay display set. Current display: {overlayElement.style.display.value}</color>");
        }
        else
        {
            Debug.LogError("<color=red>[WelcomeScreen] ✗ CRITICAL: overlayElement is NULL! Cannot show UI!</color>");
        }
    }

    public void ShowWelcomeModal(DisasterType selectedSimulation)
    {
        ShowWelcomeScreen(selectedSimulation);
    }

    public void ShowWelcomeModal()
    {
        ShowWelcomeScreen(DisasterType.None);
    }

    public void ResetWelcomeStatus()
    {
        PlayerPrefs.DeleteKey(DONT_SHOW_AGAIN_KEY);
        PlayerPrefs.Save();
    }

    private void BuildVisualTree(VisualTreeAsset template, StyleSheet stylesheet)
    {
        Debug.Log("<color=cyan>[WelcomeScreen] BuildVisualTree() - Starting UI construction...</color>");

        // CRITICAL: Null checks to prevent errors after destruction
        if (welcomeDocument == null)
        {
            Debug.LogError("<color=red>[WelcomeScreen] welcomeDocument is NULL!</color>");
            return;
        }

        if (welcomeDocument.rootVisualElement == null)
        {
            Debug.LogError("<color=red>[WelcomeScreen] rootVisualElement is NULL!</color>");
            return;
        }

        VisualElement root = welcomeDocument.rootVisualElement;

        // Clean up any existing UI before building new one
        DetachUIEvents();

        root.Clear();
        root.styleSheets.Clear();
        root.pickingMode = PickingMode.Ignore;

        if (stylesheet != null)
        {
            root.styleSheets.Add(stylesheet);
            Debug.Log($"<color=green>[WelcomeScreen] ✓ Stylesheet added to root: {stylesheet.name}</color>");
        }
        else
        {
            Debug.LogError("<color=red>[WelcomeScreen] ✗ No stylesheet provided! UI will be unstyled.</color>");
        }

        activeTemplate = template.CloneTree();
        activeTemplate.style.flexGrow = 1f;
        root.Add(activeTemplate);
        
        Debug.Log("<color=cyan>[WelcomeScreen] Template cloned and added to root</color>");

        overlayElement = activeTemplate.Q<VisualElement>("welcome-overlay");
        cardElement = activeTemplate.Q<VisualElement>("welcome-card");
        bannerElement = activeTemplate.Q<VisualElement>("welcome-banner");
        iconElement = activeTemplate.Q<VisualElement>("welcome-icon");
        titleLabel = activeTemplate.Q<Label>("title-label");
        scenarioLabel = activeTemplate.Q<Label>("scenario-label");
        introLabel = activeTemplate.Q<Label>("intro-label");
        instructionsContainer = activeTemplate.Q<VisualElement>("instructions-container");
        footerLabel = activeTemplate.Q<Label>("footer-label");
        dontShowToggle = activeTemplate.Q<Toggle>("dont-show-toggle");
        confirmButton = activeTemplate.Q<Button>("confirm-button");
        skipButton = activeTemplate.Q<Button>("skip-button");

        if (overlayElement != null)
        {
            overlayElement.pickingMode = PickingMode.Position;
            overlayElement.focusable = true;
            overlayElement.tabIndex = 0;
            // Don't set display here - let ShowWelcomeScreen() control visibility
            overlayElement.RegisterCallback<ClickEvent>(HandleOverlayClicked);
            overlayElement.RegisterCallback<KeyDownEvent>(HandleOverlayKeyDown, TrickleDown.TrickleDown);
            
            Debug.Log("<color=cyan>[WelcomeScreen] ✓ Overlay element configured and ready</color>");
        }
        else
        {
            Debug.LogError("<color=red>[WelcomeScreen] ✗ Overlay element NOT FOUND! Check UXML for 'welcome-overlay'</color>");
        }

        if (cardElement != null)
        {
            cardElement.pickingMode = PickingMode.Position;
        }

        if (confirmButton != null)
        {
            confirmButton.clicked += HandleConfirmClicked;
        }

        if (skipButton != null)
        {
            skipButton.clicked += HandleSkipClicked;
        }

        if (dontShowToggle != null)
        {
            dontShowToggle.value = false;
            dontShowToggle.label = dontShowToggleLabel;
            dontShowToggle.style.display = allowDontShowToggle ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void ConfigureContent(DisasterType simulation, DisasterModalContent contentOverride)
    {
        Debug.Log("<color=cyan>[WelcomeScreen] ConfigureContent - Configuring UI elements</color>");
        
        // Elements already queried in BuildVisualTree - just configure content
        string title = GetTitle(simulation, contentOverride);
        string description = GetDescription(simulation, contentOverride);
        string confirmLabel = GetConfirmLabel(contentOverride);
        string skipLabel = GetSkipLabel(contentOverride);

        if (titleLabel != null)
        {
            titleLabel.text = title;
        }

        if (scenarioLabel != null)
        {
            scenarioLabel.text = GetScenarioLabel(simulation);
            scenarioLabel.style.display = simulation == DisasterType.None ? DisplayStyle.None : DisplayStyle.Flex;
        }

        ConfigureBanner(contentOverride != null && contentOverride.bannerOverride != null
            ? contentOverride.bannerOverride
            : defaultBannerImage);

        ConfigureIcon(contentOverride != null && contentOverride.iconOverride != null
            ? contentOverride.iconOverride
            : modalIcon);

        PopulateDescription(description);

        if (confirmButton != null)
        {
            confirmButton.text = confirmLabel;
        }

        if (skipButton != null)
        {
            skipButton.text = skipLabel;
            skipButton.style.display = allowSkip ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (dontShowToggle != null)
        {
            dontShowToggle.value = false;
        }
        
        Debug.Log($"<color=green>[WelcomeScreen] ✓ Content configured - overlay: {overlayElement != null}, title: {titleLabel != null}</color>");
    }

    private void ConfigureBanner(Sprite sprite)
    {
        if (bannerElement == null)
        {
            Debug.LogWarning("[WelcomeScreen] Banner element not found in UXML template");
            return;
        }

        if (sprite == null)
        {
            bannerElement.style.display = DisplayStyle.None;
            bannerElement.style.backgroundImage = StyleKeyword.None;
            return;
        }

        bannerElement.style.display = DisplayStyle.Flex;
        bannerElement.style.backgroundImage = new StyleBackground(sprite);
    }

    private void ConfigureIcon(Sprite sprite)
    {
        if (iconElement == null)
        {
            return;
        }

        if (sprite == null)
        {
            iconElement.style.display = DisplayStyle.None;
            iconElement.style.backgroundImage = StyleKeyword.None;
            return;
        }

        iconElement.style.display = DisplayStyle.Flex;
        iconElement.style.backgroundImage = new StyleBackground(sprite);
        iconElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
    }

    private void PopulateDescription(string description)
    {
        if (instructionsContainer != null)
        {
            instructionsContainer.Clear();
        }

        string intro = string.Empty;
        string footer = string.Empty;
        List<string> steps = new List<string>();

        if (!string.IsNullOrWhiteSpace(description))
        {
            string[] sections = description.Split(new[] { "\n\n" }, StringSplitOptions.None);
            if (sections.Length > 0)
            {
                intro = sections[0].Trim();
            }

            for (int i = 1; i < sections.Length; i++)
            {
                bool isLastSection = i == sections.Length - 1;
                string block = sections[i].Trim();
                if (string.IsNullOrWhiteSpace(block))
                {
                    continue;
                }

                if (isLastSection)
                {
                    footer = block;
                    continue;
                }

                string[] lines = block.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string trimmed = lines[lineIndex].Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        steps.Add(trimmed);
                    }
                }
            }
        }

        if (introLabel != null)
        {
            introLabel.text = intro;
            introLabel.style.display = string.IsNullOrWhiteSpace(intro) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (footerLabel != null)
        {
            footerLabel.text = footer;
            footerLabel.style.display = string.IsNullOrWhiteSpace(footer) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (instructionsContainer != null)
        {
            if (steps.Count == 0)
            {
                instructionsContainer.style.display = DisplayStyle.None;
            }
            else
            {
                instructionsContainer.style.display = DisplayStyle.Flex;
                for (int i = 0; i < steps.Count; i++)
                {
                    var label = new Label(steps[i])
                    {
                        pickingMode = PickingMode.Ignore
                    };
                    label.AddToClassList("welcome-instructions__item");
                    label.style.marginBottom = i == steps.Count - 1 ? 0f : 10f;
                    instructionsContainer.Add(label);
                }
            }
        }
    }

    private void HandleConfirmClicked()
    {
        bool dontShowAgain = dontShowToggle != null && dontShowToggle.value;
        CompleteWelcome(dontShowAgain);
    }

    private void HandleSkipClicked()
    {
        if (!allowSkip)
        {
            return;
        }

        bool dontShowAgain = markDontShowWhenSkipped || (dontShowToggle != null && dontShowToggle.value);
        CompleteWelcome(dontShowAgain);
    }

    private void HandleOverlayClicked(ClickEvent evt)
    {
        if (!allowSkip)
        {
            return;
        }

        if (overlayElement != null && evt.target == overlayElement)
        {
            HandleSkipClicked();
            evt.StopPropagation();
        }
    }

    private void HandleOverlayKeyDown(KeyDownEvent evt)
    {
        if (!allowSkip || evt == null)
        {
            return;
        }

        if (evt.keyCode == KeyCode.Escape)
        {
            HandleSkipClicked();
            evt.StopPropagation();
        }
    }

    private void DetachUIEvents()
    {
        if (overlayElement != null)
        {
            overlayElement.UnregisterCallback<ClickEvent>(HandleOverlayClicked);
            overlayElement.UnregisterCallback<KeyDownEvent>(HandleOverlayKeyDown, TrickleDown.TrickleDown);
        }

        if (confirmButton != null)
        {
            confirmButton.clicked -= HandleConfirmClicked;
        }

        if (skipButton != null)
        {
            skipButton.clicked -= HandleSkipClicked;
        }

        overlayElement = null;
        cardElement = null;
        bannerElement = null;
        iconElement = null;
        titleLabel = null;
        scenarioLabel = null;
        introLabel = null;
        instructionsContainer = null;
        footerLabel = null;
        dontShowToggle = null;
        confirmButton = null;
        skipButton = null;
    }

    private DisasterModalContent ResolveContentOverride(DisasterType simulation)
    {
        if (simulation == DisasterType.None || disasterContentOverrides == null)
        {
            return null;
        }

        for (int i = 0; i < disasterContentOverrides.Count; i++)
        {
            DisasterModalContent item = disasterContentOverrides[i];
            if (item != null && item.disasterType == simulation)
            {
                return item;
            }
        }

        return null;
    }

    private string GetTitle(DisasterType simulation, DisasterModalContent contentOverride)
    {
        if (contentOverride != null && !string.IsNullOrWhiteSpace(contentOverride.titleOverride))
        {
            return contentOverride.titleOverride;
        }

        if (string.IsNullOrWhiteSpace(modalTitle))
        {
            return "Welcome";
        }

        if (simulation == DisasterType.None)
        {
            return modalTitle.Replace("{SIMULATION}", "the simulation");
        }

        return modalTitle.Replace("{SIMULATION}", FormatSimulationName(simulation));
    }

    private string GetDescription(DisasterType simulation, DisasterModalContent contentOverride)
    {
        if (contentOverride != null && !string.IsNullOrWhiteSpace(contentOverride.descriptionOverride))
        {
            return contentOverride.descriptionOverride;
        }

        string simText = simulation == DisasterType.None ? "the simulation" : FormatSimulationName(simulation);
        try
        {
            return string.Format(descriptionTemplate, simText);
        }
        catch (FormatException)
        {
            return descriptionTemplate;
        }
    }

    private string GetScenarioLabel(DisasterType simulation)
    {
        if (simulation == DisasterType.None)
        {
            return string.Empty;
        }

        return $"Scenario: {FormatSimulationName(simulation)}";
    }

    private string GetConfirmLabel(DisasterModalContent contentOverride)
    {
        if (contentOverride != null && !string.IsNullOrWhiteSpace(contentOverride.confirmButtonOverride))
        {
            return contentOverride.confirmButtonOverride;
        }

        return string.IsNullOrWhiteSpace(confirmButtonText) ? "Begin" : confirmButtonText;
    }

    private string GetSkipLabel(DisasterModalContent contentOverride)
    {
        if (contentOverride != null && !string.IsNullOrWhiteSpace(contentOverride.skipButtonOverride))
        {
            return contentOverride.skipButtonOverride;
        }

        return string.IsNullOrWhiteSpace(skipButtonText) ? "Skip" : skipButtonText;
    }

    private string FormatSimulationName(DisasterType simulation)
    {
        string raw = simulation.ToString();
        return raw.Replace('_', ' ');
    }

    private VisualTreeAsset ResolveTemplate()
    {
        if (welcomeScreenTemplate != null)
        {
            return welcomeScreenTemplate;
        }

        if (welcomeDocument != null && welcomeDocument.visualTreeAsset != null)
        {
            return welcomeDocument.visualTreeAsset;
        }

        return Resources.Load<VisualTreeAsset>(TEMPLATE_RESOURCE_PATH);
    }

    private StyleSheet ResolveStyleSheet()
    {
        if (welcomeScreenStyles != null)
        {
            Debug.Log("<color=cyan>[WelcomeScreen] Using assigned stylesheet from Inspector</color>");
            return welcomeScreenStyles;
        }

        if (welcomeDocument != null)
        {
            var root = welcomeDocument.rootVisualElement;
            if (root != null && root.styleSheets != null && root.styleSheets.count > 0)
            {
                Debug.Log("<color=cyan>[WelcomeScreen] Reusing existing stylesheet from root element</color>");
                return root.styleSheets[0];
            }
        }

        StyleSheet loadedSheet = Resources.Load<StyleSheet>(STYLE_RESOURCE_PATH);
        if (loadedSheet != null)
        {
            Debug.Log($"<color=green>[WelcomeScreen] ✓ Loaded stylesheet from Resources: {STYLE_RESOURCE_PATH}</color>");
        }
        else
        {
            Debug.LogError($"<color=red>[WelcomeScreen] ✗ Failed to load stylesheet from Resources: {STYLE_RESOURCE_PATH}</color>");
        }
        
        return loadedSheet;
    }

    private void CompleteWelcome(bool dontShowAgain)
    {
        HideWelcomeUI();

        if (completionDispatched)
        {
            return;
        }

        completionDispatched = true;

        if (dontShowAgain)
        {
            PlayerPrefs.SetInt(DONT_SHOW_AGAIN_KEY, 1);
        }
        else
        {
            PlayerPrefs.SetInt(DONT_SHOW_AGAIN_KEY, 0);
        }
        PlayerPrefs.Save();

        OnWelcomeCompleted?.Invoke();

    }

    private void HideWelcomeUI()
    {
        // CRITICAL: Null check before accessing welcomeDocument
        if (welcomeDocument != null && welcomeDocument.rootVisualElement != null)
        {
            welcomeDocument.rootVisualElement.Clear();
        }

        DetachUIEvents();
        activeTemplate = null;
        IsShowing = false;

        // UIDocument stays enabled - clearing rootVisualElement above is sufficient to hide UI
    }

    public void OnARTrackingAchieved()
    {
        // Localization guidance removed; method retained for binary compatibility.
    }
}
}
