// You may need to add this 'using' directive for the asset's code to be visible.
// If you get a compile error, try removing your .asmdef files as we discussed before.
using Lovatto.SceneLoader;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;
using UnityEngine.UIElements;
using ARSAFE.UI; // MainMenuLocationController
using ARSafe.UI; // AboutPanelController

public class MenuButtonHandler : MonoBehaviour
{
    public static DisasterType LastSelectedDisasterType { get; private set; } = DisasterType.None;

    [Header("Buttons")]
    public UIButton fireButton;
    public UIButton earthquakeButton;
    public UIButton floodButton;

    [Header("Auxiliary Buttons")]
    public UIButton aboutButton;
    public UIButton settingsButton;
    public UIButton helpButton;

    [Header("Location Selection")]
    [Tooltip("MainMenuLocationController for location selection. Leave empty to auto-find.")]
    public MainMenuLocationController locationController;

    [Tooltip("If true, show location selection after disaster button click. If false, load scene directly.")]
    public bool showLocationSelection = true;

    [Header("About Overlay")]
    [Tooltip("Optional existing AboutPanelController in the scene. Leave empty to spawn one automatically.")]
    public AboutPanelController aboutPanelController;

    [Tooltip("If true and no controller is assigned, a runtime AboutPanelController will be created automatically.")]
    public bool autoCreateAboutPanel = true;

    [Tooltip("Optional location for an auto-created AboutPanel GameObject.")]
    public Transform aboutPanelParent;

    [Tooltip("Sorting order for the About overlay.")]
    public int aboutPanelSortingOrder = 60;

    [Header("Settings Panel")]
    [Tooltip("Optional existing SettingsPanelController in the scene. Leave empty to auto-find.")]
    public SettingsPanelController settingsPanelController;

    [Tooltip("If true and no controller is assigned, auto-find SettingsPanelController in scene.")]
    public bool autoFindSettingsPanel = true;

    [Header("Help Panel")]
    [Tooltip("Optional existing HelpPanelController in the scene. Leave empty to auto-create.")]
    public HelpPanelController helpPanelController;

    [Tooltip("If true and no controller is assigned, a runtime HelpPanelController will be created automatically.")]
    public bool autoCreateHelpPanel = true;

    [Tooltip("Optional location for an auto-created HelpPanel GameObject.")]
    public Transform helpPanelParent;

    [Tooltip("Sorting order for the Help overlay.")]
    public int helpPanelSortingOrder = 55;

    [Header("Target Scene (must exist in SceneLoaderManager list)")]
    public string mainSceneName = "MainScene";

    [Header("Debug")]
    public bool verbose = true;

    private void Start()
    {
        if (fireButton)
        {
            fireButton.onClick.AddListener(() => Load(DisasterType.Fire));
        }
        if (earthquakeButton)
        {
            earthquakeButton.onClick.AddListener(() => Load(DisasterType.Earthquake));
        }
        if (floodButton)
        {
            floodButton.onClick.AddListener(() => Load(DisasterType.Flood));
        }

        // Auto-find location controller if not assigned
        if (locationController == null && showLocationSelection)
        {
            locationController = FindFirstObjectByType<MainMenuLocationController>();
            if (locationController == null && verbose)
            {
                Debug.LogWarning("[MenuButtons] MainMenuLocationController not found in scene. Location selection will be skipped.");
            }
        }

        EnsureAboutPanel();
        if (aboutButton)
        {
            aboutButton.onClick.AddListener(HandleAboutClicked);
        }

        EnsureSettingsPanel();
        if (settingsButton)
        {
            settingsButton.onClick.AddListener(HandleSettingsClicked);
        }

        EnsureHelpPanel();
        if (helpButton)
        {
            helpButton.onClick.AddListener(HandleHelpClicked);
        }
    }

    private void Load(DisasterType type)
    {
        LastSelectedDisasterType = type;
        DisasterTypeManager.SetDisasterType(type);

        if (verbose)
            Debug.Log($"[MenuButtons] Disaster selected: {type}");

        // Show location selection if enabled and controller exists
        if (showLocationSelection && locationController != null)
        {
            if (verbose)
                Debug.Log($"[MenuButtons] Showing location selection overlay");

            locationController.ShowOverlay();
        }
        else
        {
            // Skip location selection - load scene directly
            if (verbose)
                Debug.Log($"[MenuButtons] Skipping location selection - loading '{mainSceneName}' directly");

            ProceedWithLoad();
        }
    }

    private void ProceedWithLoad()
    {
        // Doc: load by code -> bl_SceneLoaderManager.LoadScene("SCENE_NAME");
        if (verbose)
            Debug.Log($"[MenuButtons] Request load '{mainSceneName}' (Disaster={LastSelectedDisasterType})");

        // One frame delay prevents the click from triggering an immediate skip if SkipType = AnyKey accidentally.
        StartCoroutine(DelayedLoad());
    }

    private System.Collections.IEnumerator DelayedLoad()
    {
        yield return null;

        // Call documented API
        bl_SceneLoaderManager.LoadScene(mainSceneName);
    }

    private void OnDestroy()
    {
        fireButton?.onClick.RemoveAllListeners();
        earthquakeButton?.onClick.RemoveAllListeners();
        floodButton?.onClick.RemoveAllListeners();
        aboutButton?.onClick.RemoveListener(HandleAboutClicked);
        settingsButton?.onClick.RemoveListener(HandleSettingsClicked);
        helpButton?.onClick.RemoveListener(HandleHelpClicked);
    }

    private void EnsureAboutPanel()
    {
        Debug.Log("<color=cyan>[MenuButtons] EnsureAboutPanel() started</color>");
        
        if (aboutPanelController != null || !autoCreateAboutPanel)
        {
            Debug.Log($"<color=cyan>[MenuButtons] Skipping auto-create: controller={aboutPanelController != null}, autoCreate={autoCreateAboutPanel}</color>");
            return;
        }

        var existingController = FindFirstObjectByType<AboutPanelController>(FindObjectsInactive.Include);
        if (existingController != null)
        {
            aboutPanelController = existingController;
            Debug.Log("<color=green>[MenuButtons] ✓ Found existing AboutPanelController in scene</color>");
            return;
        }

        Debug.Log("<color=yellow>[MenuButtons] Creating new AboutPanel GameObject...</color>");
        
        Transform parent = aboutPanelParent != null ? aboutPanelParent : transform;
        var panelRoot = new GameObject("AboutPanel");
        panelRoot.SetActive(false);
        panelRoot.transform.SetParent(parent, worldPositionStays: false);

        var document = panelRoot.AddComponent<UIDocument>();
        document.panelSettings = AboutPanelController.CreateRuntimePanelSettings(aboutPanelSortingOrder);
        document.sortingOrder = aboutPanelSortingOrder;

        aboutPanelController = panelRoot.AddComponent<AboutPanelController>();
        aboutPanelController.SetSortingOrder(aboutPanelSortingOrder);

        panelRoot.SetActive(true);
        
        Debug.Log("<color=green>[MenuButtons] ✓ AboutPanel created successfully</color>");
    }

    private void HandleAboutClicked()
    {
        Debug.Log("<color=cyan>[MenuButtons] About button clicked</color>");
        
        if (aboutPanelController == null)
        {
            Debug.LogWarning("[MenuButtons] About button clicked but no AboutPanelController is available.");
            return;
        }

        Debug.Log($"<color=cyan>[MenuButtons] Calling Toggle() on AboutPanelController</color>");
        aboutPanelController.Toggle();
    }

    private void EnsureSettingsPanel()
    {
        if (settingsPanelController != null || !autoFindSettingsPanel)
        {
            return;
        }

        var existingController = FindFirstObjectByType<SettingsPanelController>(FindObjectsInactive.Include);
        if (existingController != null)
        {
            settingsPanelController = existingController;
            if (verbose)
                Debug.Log("[MenuButtons] Found SettingsPanelController in scene.");
        }
        else if (verbose)
        {
            Debug.LogWarning("[MenuButtons] SettingsPanelController not found in scene. Settings button will not work.");
        }
    }

    private void HandleSettingsClicked()
    {
        if (settingsPanelController == null)
        {
            Debug.LogWarning("[MenuButtons] Settings button clicked but no SettingsPanelController is available.");
            return;
        }

        settingsPanelController.ShowSettings();
    }

    private void EnsureHelpPanel()
    {
        if (verbose)
            Debug.Log("<color=cyan>[MenuButtons] EnsureHelpPanel() started</color>");
        
        if (helpPanelController != null || !autoCreateHelpPanel)
        {
            if (verbose)
                Debug.Log($"<color=cyan>[MenuButtons] Skipping help panel auto-create: controller={helpPanelController != null}, autoCreate={autoCreateHelpPanel}</color>");
            return;
        }

        var existingController = FindFirstObjectByType<HelpPanelController>(FindObjectsInactive.Include);
        if (existingController != null)
        {
            helpPanelController = existingController;
            if (verbose)
                Debug.Log("<color=green>[MenuButtons] ✓ Found existing HelpPanelController in scene</color>");
            return;
        }

        if (verbose)
            Debug.Log("<color=yellow>[MenuButtons] Creating new HelpPanel GameObject...</color>");
        
        Transform parent = helpPanelParent != null ? helpPanelParent : transform;
        var panelRoot = new GameObject("HelpPanel");
        panelRoot.SetActive(false);
        panelRoot.transform.SetParent(parent, worldPositionStays: false);

        var document = panelRoot.AddComponent<UIDocument>();
        document.panelSettings = HelpPanelController.CreateRuntimePanelSettings(helpPanelSortingOrder);
        document.sortingOrder = helpPanelSortingOrder;

        helpPanelController = panelRoot.AddComponent<HelpPanelController>();
        helpPanelController.SetSortingOrder(helpPanelSortingOrder);

        panelRoot.SetActive(true);
        
        if (verbose)
            Debug.Log("<color=green>[MenuButtons] ✓ HelpPanel created successfully</color>");
    }

    private void HandleHelpClicked()
    {
        if (verbose)
            Debug.Log("<color=cyan>[MenuButtons] Help button clicked</color>");
        
        if (helpPanelController == null)
        {
            Debug.LogWarning("[MenuButtons] Help button clicked but no HelpPanelController is available.");
            return;
        }

        if (verbose)
            Debug.Log($"<color=cyan>[MenuButtons] Calling Toggle() on HelpPanelController</color>");
        helpPanelController.Toggle();
    }
}
