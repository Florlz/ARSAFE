using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ARSafe.UI
{
    /// <summary>
    /// Controls the main menu About overlay rendered with Unity UI Toolkit.
    /// Loads the About UXML/USS from Resources, injects team/app metadata, and exposes
    /// show/hide helpers that menu buttons can trigger.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class AboutPanelController : MonoBehaviour
    {
        [Header("Content References")]
        [Tooltip("Local override for the UXML layout. When empty the controller loads the default from Resources/UI/AboutPanel/AboutPanel.")]
        [SerializeField] private VisualTreeAsset layoutAsset;

        [Tooltip("Local override for the stylesheet. When empty the controller loads the default from Resources/UI/AboutPanel/AboutPanelStyles.")]
        [SerializeField] private StyleSheet styleSheet;

        [Tooltip("Short mission statement displayed below the title.")]
        [TextArea]
        [SerializeField] private string appSubtitle = "Augmented reality guidance for campus evacuation drills.";

        [Tooltip("Body copy describing the app. Supports multi-line text.")]
        [TextArea(3, 6)]
        [SerializeField] private string appDescription = "ARSAFE pairs Vuforia area targets with real-world safety content to help trainees practice faster, safer decisions during drills.";

        [Header("Team Roster")]
        [SerializeField] private List<TeamMember> teamMembers = new List<TeamMember>();

        [Header("Footer")]
        [SerializeField] private string versionLabel = "Version 1.0";

        [SerializeField] private string contactLabel = "Contact: arsafe@usant.edu";

        [Header("Overlay Settings")]
        [SerializeField] private PanelSettings panelSettingsOverride;

        [SerializeField] private int sortingOrder = 50;

        [SerializeField] private bool showOnStart;

        // Animation settings removed - instant show/hide for better mobile performance

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement overlay;
        private VisualElement card;
        private Button closeButton;
        private Label titleLabel;
        private Label subtitleLabel;
        private Label descriptionLabel;
        private VisualElement teamList;
        private Label versionLabelElement;
        private Label contactLabelElement;
        private StyleSheet runtimeStyleSheet;
        private VisualTreeAsset runtimeLayout;
        private bool isVisible;
        private PanelSettings runtimePanelSettings;

        private const string DEFAULT_LAYOUT_RESOURCE = "UI/AboutPanel/AboutPanel";
        private const string DEFAULT_STYLE_RESOURCE = "UI/AboutPanel/AboutPanelStyles";
        private const string OVERLAY_VISIBLE_CLASS = "about-overlay--visible";
        private const string DEFAULT_THEME_RESOURCE = "UI/AboutPanel/AboutPanelTheme";
        private static ThemeStyleSheet runtimeTheme;

        [System.Serializable]
        private class TeamMember
        {
            public string name = "Team Member";
            public string role = "Role";
            [Tooltip("Developer profile image (square recommended: 512x512px)")]
            public Texture2D avatarImage;
        }

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            EnsurePanelSettings();
            
            // If Source Asset is assigned in UIDocument, it's already rendered
            // Just cache the elements and hide the overlay
            if (uiDocument != null && uiDocument.visualTreeAsset != null)
            {
                root = uiDocument.rootVisualElement;
                if (root != null && root.childCount > 0)
                {
                    // Visual tree already loaded from Source Asset, just cache elements
                    CacheUIElements();
                    
                    // Force hide overlay immediately
                    if (overlay != null)
                    {
                        overlay.style.display = DisplayStyle.None;
                        overlay.style.opacity = 0f;
                        overlay.pickingMode = PickingMode.Ignore;
                        overlay.RemoveFromClassList(OVERLAY_VISIBLE_CLASS);
                    }
                    
                    PopulateContent();
                    AttachEvents();
                    
                    if (showOnStart)
                    {
                        Show();
                    }
                    return;
                }
            }
            
            // Otherwise, build visual tree from Resources
            EnsureVisualTree();
            
            // Always start hidden
            if (overlay != null)
            {
                overlay.style.display = DisplayStyle.None;
                overlay.style.opacity = 0f;
                overlay.pickingMode = PickingMode.Ignore;
                overlay.RemoveFromClassList(OVERLAY_VISIBLE_CLASS);
            }
            
            if (showOnStart)
            {
                Show();
            }
        }

        private void OnDisable()
        {
            DetachEvents();
            ClearCachedElements();
        }

        public void Show()
        {
            Debug.Log("<color=cyan>[AboutPanel] Show() called</color>");

            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument != null && !uiDocument.enabled) uiDocument.enabled = true;

            EnsurePanelSettings();
            Debug.Log($"<color=cyan>[AboutPanel] After EnsurePanelSettings, uiDocument.panelSettings: {(uiDocument?.panelSettings != null ? "OK" : "NULL")}</color>");

            if (!EnsureVisualTree())
            {
                Debug.LogError("<color=red>[AboutPanel] EnsureVisualTree() returned false - cannot show panel</color>");
                return;
            }

            Debug.Log($"<color=cyan>[AboutPanel] Visual tree ensured. Overlay: {(overlay != null ? "OK" : "NULL")}</color>");

            // Instant show without animations
            overlay.pickingMode = PickingMode.Position;
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.opacity = 1f;
            overlay.AddToClassList(OVERLAY_VISIBLE_CLASS);

            // Reset card position and opacity
            if (card != null)
            {
                card.style.opacity = 1f;
                card.style.translate = new Translate(0, 0);
            }

            isVisible = true;
            Debug.Log("<color=green>[AboutPanel] ✓ Show() completed successfully</color>");
        }

        public void Hide(bool immediate = false)
        {
            if (overlay == null) return;

            // Instant hide without animations
            overlay.RemoveFromClassList(OVERLAY_VISIBLE_CLASS);
            overlay.pickingMode = PickingMode.Ignore;
            overlay.style.opacity = 0f;
            overlay.style.display = DisplayStyle.None;

            isVisible = false;
        }

        public void Toggle()
        {
            if (isVisible) Hide();
            else Show();
        }

        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            EnsurePanelSettings();
        }

        public static PanelSettings CreateRuntimePanelSettings(int order)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            settings.sortingOrder = order;
            settings.name = "AboutPanelPanelSettings (Runtime)";
            settings.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            settings.themeStyleSheet = GetRuntimeTheme();
            return settings;
        }

        private void CacheUIElements()
        {
            if (root == null) return;
            
            // Query all UI elements from the existing visual tree
            overlay = root.Q<VisualElement>("about-overlay");
            card = root.Q<VisualElement>("about-card");
            closeButton = root.Q<Button>("about-close-button");
            titleLabel = root.Q<Label>("about-title");
            subtitleLabel = root.Q<Label>("about-subtitle");
            descriptionLabel = root.Q<Label>("about-description");
            teamList = root.Q<VisualElement>("about-team-list");
            versionLabelElement = root.Q<Label>("about-version");
            contactLabelElement = root.Q<Label>("about-contact");
            
            Debug.Log($"<color=cyan>[AboutPanel] Elements cached - Overlay: {(overlay != null ? "OK" : "NULL")}, Card: {(card != null ? "OK" : "NULL")}</color>");
        }

        private bool EnsureVisualTree()
        {
            Debug.Log("<color=cyan>[AboutPanel] EnsureVisualTree() started</color>");
            
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("[AboutPanel] UIDocument missing.", this);
                return false;
            }

            if (overlay != null && root != null && root.childCount > 0)
            {
                Debug.Log("<color=green>[AboutPanel] Visual tree already exists, skipping rebuild</color>");
                return true;
            }

            runtimeLayout = layoutAsset != null ? layoutAsset : LoadResource<VisualTreeAsset>(DEFAULT_LAYOUT_RESOURCE);
            runtimeStyleSheet = styleSheet != null ? styleSheet : LoadResource<StyleSheet>(DEFAULT_STYLE_RESOURCE);

            Debug.Log($"<color=cyan>[AboutPanel] Layout: {(runtimeLayout != null ? "OK" : "NULL")}, StyleSheet: {(runtimeStyleSheet != null ? "OK" : "NULL")}</color>");

            if (runtimeLayout == null)
            {
                Debug.LogError("[AboutPanel] Layout asset missing.", this);
                return false;
            }

            root = uiDocument.rootVisualElement;
            root.Clear();
            root.styleSheets.Clear();
            root.pickingMode = PickingMode.Ignore;

            if (runtimeStyleSheet != null) root.styleSheets.Add(runtimeStyleSheet);

            VisualElement tree = runtimeLayout.CloneTree();
            tree.style.flexGrow = 1f;
            root.Add(tree);

            Debug.Log("<color=cyan>[AboutPanel] Tree cloned and added to root</color>");

            CacheUIElements();

            Debug.Log($"<color=cyan>[AboutPanel] Elements queried - Overlay: {(overlay != null ? "OK" : "NULL")}, Card: {(card != null ? "OK" : "NULL")}, CloseButton: {(closeButton != null ? "OK" : "NULL")}</color>");

            PopulateContent();
            AttachEvents();

            overlay.style.display = DisplayStyle.None;
            overlay.style.opacity = 0f;
            overlay.pickingMode = PickingMode.Ignore;
            overlay.RemoveFromClassList(OVERLAY_VISIBLE_CLASS);

            Debug.Log("<color=green>[AboutPanel] ✓ Visual tree built successfully</color>");
            return true;
        }

        private void PopulateContent()
        {
            if (titleLabel != null) titleLabel.text = "USANT ARSAFE";
            if (subtitleLabel != null) subtitleLabel.text = string.IsNullOrWhiteSpace(appSubtitle) ? "Augmented Reality Safety & Emergency Training" : appSubtitle;
            if (descriptionLabel != null) descriptionLabel.text = string.IsNullOrWhiteSpace(appDescription) ? "ARSAFE transforms campus emergency preparedness through cutting-edge augmented reality technology." : appDescription;
            if (versionLabelElement != null) versionLabelElement.text = string.IsNullOrWhiteSpace(versionLabel) ? $"Version {Application.version}" : versionLabel;
            if (contactLabelElement != null)
            {
                string emailOnly = contactLabel.Replace("Contact: ", "").Replace("contact: ", "");
                contactLabelElement.text = emailOnly;
            }
            PopulateTeamList();
        }

        private void PopulateTeamList()
        {
            if (teamList == null) return;
            teamList.Clear();

            if (teamMembers == null || teamMembers.Count == 0)
            {
                AddTeamEntry("USANT ARSAFE Team", "Multidisciplinary Response & Safety Lab", null);
                return;
            }

            foreach (var member in teamMembers)
            {
                if (member != null) AddTeamEntry(member.name, member.role, member.avatarImage);
            }
        }

        /*
         * PURPOSE: Creates a team member card with optional avatar image
         * DEPENDENCIES: UI Toolkit (VisualElement, Label, StyleBackground)
         * DATA FLOW: name/role/avatarImage → VisualElement hierarchy → team list
         * PERFORMANCE: Non-hot path, called once during UI population
         * EDGE CASES: Null avatar gracefully handled (fallback icon shown)
         */
        private void AddTeamEntry(string name, string role, Texture2D avatarImage)
        {
            var item = new VisualElement();
            item.AddToClassList("about-team-item");

            var contentWrapper = new VisualElement();
            contentWrapper.style.flexDirection = FlexDirection.Row;
            contentWrapper.style.alignItems = Align.Center;
            contentWrapper.style.flexGrow = 1;
            contentWrapper.AddToClassList("about-team-item__content-wrapper");

            var avatar = new VisualElement();
            avatar.AddToClassList("about-team-item__avatar");

            // Set avatar image if provided
            if (avatarImage != null)
            {
                avatar.style.backgroundImage = new StyleBackground(avatarImage);
                avatar.AddToClassList("about-team-item__avatar--has-image");
            }
            else
            {
                // Fallback: Use default user icon (👤)
                var iconLabel = new Label("👤");
                iconLabel.AddToClassList("about-team-item__avatar-icon");
                iconLabel.style.fontSize = 50;
                iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                iconLabel.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.5f));
                avatar.Add(iconLabel);
            }

            contentWrapper.Add(avatar);

            var info = new VisualElement();
            info.AddToClassList("about-team-item__info");
            info.style.flexGrow = 1;
            contentWrapper.Add(info);

            var nameLabel = new Label(string.IsNullOrWhiteSpace(name) ? "Team Member" : name);
            nameLabel.AddToClassList("about-team-item__name");
            info.Add(nameLabel);

            if (!string.IsNullOrWhiteSpace(role))
            {
                var roleLabel = new Label(role);
                roleLabel.AddToClassList("about-team-item__role");
                info.Add(roleLabel);
            }

            item.Add(contentWrapper);
            ((VisualElement)teamList).Add(item);
        }

        private void ClearCachedElements()
        {
            root = null;
            overlay = null;
            card = null;
            closeButton = null;
            titleLabel = null;
            subtitleLabel = null;
            descriptionLabel = null;
            teamList = null;
            versionLabelElement = null;
            contactLabelElement = null;
        }

        private void AttachEvents()
        {
            DetachEvents();
            if (closeButton != null) closeButton.clicked += OnCloseClicked;
            if (overlay != null) overlay.RegisterCallback<ClickEvent>(OnOverlayClicked);
        }

        private void DetachEvents()
        {
            if (closeButton != null) closeButton.clicked -= OnCloseClicked;
            if (overlay != null) overlay.UnregisterCallback<ClickEvent>(OnOverlayClicked);
        }

        private void OnCloseClicked() => Hide();
        private void OnOverlayClicked(ClickEvent evt)
        {
            if (evt.target == overlay)
            {
                Hide();
                evt.StopPropagation();
            }
        }

        private void EnsurePanelSettings()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            if (panelSettingsOverride != null)
            {
                uiDocument.panelSettings = panelSettingsOverride;
            }
            else if (uiDocument.panelSettings == null)
            {
                runtimePanelSettings ??= CreateRuntimePanelSettings(sortingOrder);
                uiDocument.panelSettings = runtimePanelSettings;
            }
            else
            {
                uiDocument.panelSettings.sortingOrder = sortingOrder;
                if (uiDocument.panelSettings.themeStyleSheet == null)
                {
                    uiDocument.panelSettings.themeStyleSheet = GetRuntimeTheme();
                }
            }
            uiDocument.sortingOrder = sortingOrder;
        }

        private T LoadResource<T>(string path) where T : Object
        {
            T asset = Resources.Load<T>(path);
            if (asset == null) Debug.LogWarning($"[AboutPanel] Resource '{path}' not found.", this);
            return asset;
        }

        private static ThemeStyleSheet GetRuntimeTheme()
        {
            if (runtimeTheme != null) return runtimeTheme;
            runtimeTheme = Resources.Load<ThemeStyleSheet>(DEFAULT_THEME_RESOURCE) ?? ScriptableObject.CreateInstance<ThemeStyleSheet>();
            if (runtimeTheme != null)
            {
                runtimeTheme.name = "AboutPanelTheme (Runtime)";
                runtimeTheme.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
            return runtimeTheme;
        }

        // Animation coroutines removed for instant show/hide and better mobile performance
    }
}