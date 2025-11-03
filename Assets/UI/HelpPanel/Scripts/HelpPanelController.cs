using UnityEngine;
using UnityEngine.UIElements;

namespace ARSafe.UI
{
    /// <summary>
    /// Controls the Help &amp; Support overlay panel with FAQ, troubleshooting tips, and safety information.
    /// Mobile-optimized design with touch-friendly navigation.
    /// </summary>
    public class HelpPanelController : MonoBehaviour
    {
        [Header("UI Document Settings")]
        [Tooltip("Leave empty to auto-create at runtime")]
        public PanelSettings panelSettings;

        [Tooltip("Sorting order for the help overlay (higher = on top)")]
        public int sortingOrder = 50;

        [Header("Behavior")]
        [Tooltip("If true, allows Android back button to close the help panel")]
        public bool enableBackButtonClose = true;

        [Header("Debug")]
        [Tooltip("Verbose debug logging")]
        public bool verbose = true;

        private UIDocument uiDocument;
        private VisualElement root;
        private VisualElement overlay;
        private Button closeButton;
        private bool isVisible = false;

        private void Awake()
        {
            EnsureUIDocument();
            BuildUI();
        }

        private void Start()
        {
            // Hide after UI is fully built and first frame rendered
            Hide();
        }

        private void OnEnable()
        {
            if (root != null)
            {
                RegisterCallbacks();
            }
        }

        private void OnDisable()
        {
            UnregisterCallbacks();
        }

        private void Update()
        {
            // Handle Android back button to close panel
            if (enableBackButtonClose && isVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                if (verbose)
                    Debug.Log("<color=cyan>[HelpPanel] Back button pressed - hiding panel</color>");
                Hide();
            }
        }

        private void EnsureUIDocument()
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                Debug.LogError("<color=red>[HelpPanel] No UIDocument component found! Add UIDocument and assign HelpPanel.uxml as Source Asset.</color>");
                return;
            }

            if (verbose)
                Debug.Log("<color=green>[HelpPanel] ✓ Found UIDocument component</color>");

            // Ensure panel settings exist
            if (uiDocument.panelSettings == null)
            {
                if (panelSettings == null)
                {
                    panelSettings = CreateRuntimePanelSettings(sortingOrder);
                    if (verbose)
                        Debug.Log("<color=green>[HelpPanel] ✓ Created runtime PanelSettings</color>");
                }
                uiDocument.panelSettings = panelSettings;
            }

            uiDocument.sortingOrder = sortingOrder;

            if (verbose)
                Debug.Log($"<color=green>[HelpPanel] ✓ UIDocument configured (SortingOrder: {sortingOrder})</color>");
        }

        private void BuildUI()
        {
            // Use the UIDocument's root directly (source asset assigned in inspector)
            root = uiDocument.rootVisualElement;

            if (verbose)
                Debug.Log($"<color=green>[HelpPanel] ✓ Using UIDocument root (children: {root.childCount})</color>");

            // Query elements from the existing visual tree
            overlay = root.Q<VisualElement>("help-overlay");
            closeButton = root.Q<Button>("help-close-button");

            if (overlay == null)
            {
                Debug.LogError("<color=red>[HelpPanel] Failed to find 'help-overlay' element in UXML. Make sure UIDocument Source Asset is assigned!</color>");
                return;
            }

            if (verbose)
                Debug.Log($"<color=green>[HelpPanel] ✓ Found overlay element (current display: {overlay.style.display.value})</color>");

            if (closeButton == null)
            {
                Debug.LogWarning("<color=yellow>[HelpPanel] Close button not found in UXML</color>");
            }
            else if (verbose)
            {
                Debug.Log("<color=green>[HelpPanel] ✓ Found close button</color>");
            }

            if (verbose)
                Debug.Log("<color=green>[HelpPanel] ✓ UI built successfully</color>");
        }

        private void RegisterCallbacks()
        {
            if (closeButton != null)
            {
                closeButton.clicked += Hide;
            }

            // Click overlay background to close
            if (overlay != null)
            {
                overlay.RegisterCallback<PointerDownEvent>(OnOverlayClicked);
            }
        }

        private void UnregisterCallbacks()
        {
            if (closeButton != null)
            {
                closeButton.clicked -= Hide;
            }

            if (overlay != null)
            {
                overlay.UnregisterCallback<PointerDownEvent>(OnOverlayClicked);
            }
        }

        private void OnOverlayClicked(PointerDownEvent evt)
        {
            // Only close if clicking directly on overlay background (not the card)
            if (evt.target == overlay)
            {
                if (verbose)
                    Debug.Log("<color=cyan>[HelpPanel] Overlay background clicked - hiding panel</color>");
                Hide();
            }
        }

        /// <summary>
        /// Show the help panel overlay.
        /// </summary>
        public void Show()
        {
            if (verbose)
                Debug.Log($"<color=cyan>[HelpPanel] Show() called - overlay null? {overlay == null}</color>");

            if (overlay == null)
            {
                Debug.LogError("<color=red>[HelpPanel] Cannot show - overlay not initialized! Rebuilding UI...</color>");
                BuildUI();
                if (overlay == null)
                {
                    Debug.LogError("<color=red>[HelpPanel] Rebuild failed - overlay still null!</color>");
                    return;
                }
            }

            if (verbose)
                Debug.Log($"<color=cyan>[HelpPanel] Before Show - display: {overlay.style.display.value}, opacity: {overlay.style.opacity.value}, pickingMode: {overlay.pickingMode}</color>");

            // CRITICAL: Set all three properties for UI Toolkit visibility
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.opacity = 1f;
            overlay.pickingMode = PickingMode.Position;
            
            // Force layout update
            overlay.MarkDirtyRepaint();
            
            isVisible = true;

            if (verbose)
                Debug.Log($"<color=green>[HelpPanel] ✓ After Show - display: {overlay.style.display.value}, opacity: {overlay.style.opacity.value}, pickingMode: {overlay.pickingMode}, visible: {overlay.visible}, worldBound: {overlay.worldBound}</color>");
        }

        /// <summary>
        /// Hide the help panel overlay.
        /// </summary>
        public void Hide()
        {
            if (overlay == null)
            {
                // Don't warn during initialization
                return;
            }

            // CRITICAL: Set all three properties for UI Toolkit hiding
            overlay.style.display = DisplayStyle.None;
            overlay.style.opacity = 0f;
            overlay.pickingMode = PickingMode.Ignore;
            isVisible = false;

            if (verbose)
                Debug.Log("<color=cyan>[HelpPanel] Help panel hidden (display=None, opacity=0, pickingMode=Ignore)</color>");
        }

        /// <summary>
        /// Toggle help panel visibility.
        /// </summary>
        public void Toggle()
        {
            if (isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        /// <summary>
        /// Check if help panel is currently visible.
        /// </summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Update sorting order at runtime.
        /// </summary>
        public void SetSortingOrder(int order)
        {
            sortingOrder = order;
            if (uiDocument != null)
            {
                uiDocument.sortingOrder = order;
            }
            if (panelSettings != null)
            {
                panelSettings.sortingOrder = order;
            }
        }

        /// <summary>
        /// Create runtime PanelSettings for mobile AR UI.
        /// </summary>
        public static PanelSettings CreateRuntimePanelSettings(int sortOrder = 50)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "HelpPanel_RuntimeSettings";
            settings.sortingOrder = sortOrder;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f; // Balance between width and height
            settings.referenceResolution = new Vector2Int(1080, 1920); // Mobile portrait

            // Theme stylesheet is embedded in UXML via <Style> tag
            // No need to load it separately here

            return settings;
        }

        /// <summary>
        /// Context menu: Show help panel (Editor testing).
        /// </summary>
        [ContextMenu("Show Help Panel")]
        private void DebugShow()
        {
            Debug.Log("<color=yellow>[HelpPanel] ContextMenu: Show called</color>");
            Show();
        }

        /// <summary>
        /// Context menu: Hide help panel (Editor testing).
        /// </summary>
        [ContextMenu("Hide Help Panel")]
        private void DebugHide()
        {
            Debug.Log("<color=yellow>[HelpPanel] ContextMenu: Hide called</color>");
            Hide();
        }

        /// <summary>
        /// Context menu: Toggle help panel (Editor testing).
        /// </summary>
        [ContextMenu("Toggle Help Panel")]
        private void DebugToggle()
        {
            Debug.Log("<color=yellow>[HelpPanel] ContextMenu: Toggle called</color>");
            Toggle();
        }

        /// <summary>
        /// Context menu: Check panel state (Editor testing).
        /// </summary>
        [ContextMenu("Check Panel State")]
        private void DebugCheckState()
        {
            Debug.Log($"<color=yellow>[HelpPanel] === PANEL STATE ===</color>");
            Debug.Log($"<color=white>UIDocument: {uiDocument != null}</color>");
            Debug.Log($"<color=white>Root: {root != null}</color>");
            Debug.Log($"<color=white>Overlay: {overlay != null}</color>");
            Debug.Log($"<color=white>CloseButton: {closeButton != null}</color>");
            Debug.Log($"<color=white>IsVisible Flag: {isVisible}</color>");
            
            if (overlay != null)
            {
                Debug.Log($"<color=white>Overlay Display: {overlay.style.display.value}</color>");
                Debug.Log($"<color=white>Overlay Opacity: {overlay.style.opacity.value}</color>");
                Debug.Log($"<color=white>Overlay PickingMode: {overlay.pickingMode}</color>");
                Debug.Log($"<color=white>Overlay Visible: {overlay.visible}</color>");
                Debug.Log($"<color=white>Overlay WorldBound: {overlay.worldBound}</color>");
                Debug.Log($"<color=white>Overlay Parent: {overlay.parent?.name}</color>");
            }
            
            if (uiDocument != null)
            {
                Debug.Log($"<color=white>UIDocument PanelSettings: {uiDocument.panelSettings != null}</color>");
                Debug.Log($"<color=white>UIDocument SortingOrder: {uiDocument.sortingOrder}</color>");
                Debug.Log($"<color=white>UIDocument Root Children: {uiDocument.rootVisualElement.childCount}</color>");
            }
            
            Debug.Log($"<color=yellow>==================</color>");
        }
    }
}
