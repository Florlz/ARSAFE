using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;
using ARSafe.UI;

namespace ARSAFE.UI
{
    /*
     * PURPOSE: Show "You're Safe!" UI when user reaches high ground (2nd floor+) during flood simulation
     *
     * DEPENDENCIES:
     *   - UIDocument (UI Toolkit)
     *   - ARSafeActivationController (triggers this UI)
     *   - ARSafeNavigationValidator (disables warnings)
     *   - ARSafeWrongWayWarning (auto-disabled when shown)
     *
     * DATA FLOW:
     *   ARSafeActivationController detects floor >= 2 → Show() → Display UI → User clicks Finish → Hide()
     *
     * PERFORMANCE:
     *   - UI Toolkit (lightweight)
     *   - No Update() loop
     *   - Event-driven only
     *
     * EDGE CASES:
     *   - Only shown during Flood disaster
     *   - Auto-hides if user leaves 2nd floor area
     *   - Gracefully handles missing UI elements
     */

    [RequireComponent(typeof(UIDocument))]
    public class FloodSafeZoneController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("UI Document component (auto-found if not set)")]
        [SerializeField] private UIDocument uiDocument;

        [Header("UI Element Names")]
        [Tooltip("Root container name in UXML")]
        [SerializeField] private string rootContainerName = "flood-safe-zone-root";

        [Tooltip("Title label name")]
        [SerializeField] private string titleLabelName = "title-label";

        [Tooltip("Message label name")]
        [SerializeField] private string messageLabelName = "message-label";

        [Tooltip("Floor info label name")]
        [SerializeField] private string floorLabelName = "floor-label";

        [Tooltip("Finish button name")]
        [SerializeField] private string finishButtonName = "finish-button";

        [Header("Content")]
        [TextArea(2, 4)]
        [SerializeField] private string safeZoneTitle = "You're Safe!";

        [TextArea(3, 6)]
        [SerializeField] private string safeZoneMessage = "You've reached high ground. You are now safe from the flood.\n\nStay on upper floors until authorities give the all-clear.";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // UI Elements
        private VisualElement rootContainer;
        private Label titleLabel;
        private Label messageLabel;
        private Label floorLabel;
        private Button finishButton;

        // State
        private bool isVisible = false;
        private int currentFloorLevel = 2;

        // Singleton
        public static FloodSafeZoneController Instance { get; private set; }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Get UIDocument
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[FloodSafeZoneController] UIDocument component not found!", this);
                enabled = false;
                return;
            }
        }

        private void Start()
        {
            InitializeUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Unregister button callback
            if (finishButton != null)
            {
                finishButton.clicked -= OnFinishButtonClicked;
            }
        }

        private void InitializeUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[FloodSafeZoneController] UI Document or root element is null!", this);
                return;
            }

            var root = uiDocument.rootVisualElement;

            // Find UI elements
            rootContainer = root.Q<VisualElement>(rootContainerName);
            titleLabel = root.Q<Label>(titleLabelName);
            messageLabel = root.Q<Label>(messageLabelName);
            floorLabel = root.Q<Label>(floorLabelName);
            finishButton = root.Q<Button>(finishButtonName);

            // Validate elements
            if (rootContainer == null)
            {
                Debug.LogWarning($"[FloodSafeZoneController] Root container '{rootContainerName}' not found in UXML. UI will not be visible.", this);
            }

            if (finishButton != null)
            {
                finishButton.clicked += OnFinishButtonClicked;
            }
            else
            {
                Debug.LogWarning($"[FloodSafeZoneController] Finish button '{finishButtonName}' not found in UXML.", this);
            }

            // Hide initially
            Hide();

            if (enableDebugLogs)
            {
                Debug.Log("[FloodSafeZoneController] UI initialized and hidden");
            }
        }

        /// <summary>
        /// Show flood safe zone UI (user reached 2nd floor or higher)
        /// </summary>
        public void Show()
        {
            Show(2); // Default to floor 2
        }

        /// <summary>
        /// Show flood safe zone UI with specific floor level
        /// </summary>
        public void Show(int floorLevel)
        {
            if (rootContainer == null)
            {
                Debug.LogWarning("[FloodSafeZoneController] Cannot show UI - root container not found!");
                return;
            }

            currentFloorLevel = floorLevel;

            // Update content
            if (titleLabel != null)
            {
                titleLabel.text = safeZoneTitle;
            }

            if (messageLabel != null)
            {
                messageLabel.text = safeZoneMessage;
            }

            if (floorLabel != null)
            {
                floorLabel.text = $"Floor {floorLevel}";
            }

            // Show UI
            rootContainer.style.display = DisplayStyle.Flex;
            isVisible = true;

            // Disable wrong-way warnings
            DisableWrongWayWarnings();

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>★★★ [FloodSafeZoneController]</color> Showing flood safe zone UI (Floor {floorLevel})");
            }
        }

        /// <summary>
        /// Hide flood safe zone UI
        /// </summary>
        public void Hide()
        {
            if (rootContainer == null)
            {
                return;
            }

            rootContainer.style.display = DisplayStyle.None;
            isVisible = false;

            if (enableDebugLogs)
            {
                Debug.Log("[FloodSafeZoneController] Hiding flood safe zone UI");
            }
        }

        /// <summary>
        /// Check if UI is currently visible
        /// </summary>
        public bool IsVisible => isVisible;

        /// <summary>
        /// Get current floor level shown in UI
        /// </summary>
        public int CurrentFloorLevel => currentFloorLevel;

        private void OnFinishButtonClicked()
        {
            if (enableDebugLogs)
            {
                Debug.Log("[FloodSafeZoneController] Finish button clicked");
            }

            // TODO: Add finish simulation logic here
            // For now, just hide the UI
            Hide();

            // Optionally return to main menu or show completion screen
            // You can add custom logic here based on your app flow
        }

        private void DisableWrongWayWarnings()
        {
            // Find and disable wrong-way warning system
            var wrongWayWarning = FindFirstObjectByType<ARSafeWrongWayWarning>();
            if (wrongWayWarning != null)
            {
                wrongWayWarning.DisableWarnings();

                if (enableDebugLogs)
                {
                    Debug.Log("[FloodSafeZoneController] Disabled wrong-way warnings");
                }
            }
        }

        #if UNITY_EDITOR
        [ContextMenu("Test: Show Safe Zone UI")]
        private void TestShow()
        {
            Show(2);
        }

        [ContextMenu("Test: Hide Safe Zone UI")]
        private void TestHide()
        {
            Hide();
        }
        #endif
    }
}
