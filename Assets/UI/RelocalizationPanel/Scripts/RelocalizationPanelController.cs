using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Vuforia;
using ARSafe.Modular;
using ARSafe.UI;

namespace ARSAFE.UI
{
    /// <summary>
    /// Relocalization UI Panel - Shows recent anchors and allows manual Area Target selection.
    /// Helps users relocalize when tracking is lost or they're unsure of their location.
    /// </summary>
    public class RelocalizationPanelController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("UIDocument component (auto-finds if not set)")]
        public UIDocument uiDocument;

        [Header("Settings")]
        [Tooltip("Enable debug logging")]
        public bool enableDebugLogs = false;

        // UI Elements
        private VisualElement root;
        private VisualElement panel;
        private VisualElement recentAnchorsContainer;
        private VisualElement allTargetsContainer;
        private Button closeButton;
        private ScrollView allTargetsScrollView;

        // References
        private ARSafeActivationController activationController;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            activationController = FindFirstObjectByType<ARSafeActivationController>();
        }

        private void OnEnable()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[RelocalizationPanel] UIDocument or root visual element is null!");
                return;
            }

            root = uiDocument.rootVisualElement;
            SetupUI();
        }

        private void SetupUI()
        {
            // Get main panel
            panel = root.Q<VisualElement>("relocalization-panel");
            if (panel == null)
            {
                Debug.LogError("[RelocalizationPanel] relocalization-panel not found in UXML!");
                return;
            }

            // Get containers
            recentAnchorsContainer = root.Q<VisualElement>("recent-anchors-container");
            allTargetsContainer = root.Q<VisualElement>("all-targets-container");
            allTargetsScrollView = root.Q<ScrollView>("all-targets-scroll");

            // Get close button
            closeButton = root.Q<Button>("close-button");
            if (closeButton != null)
            {
                closeButton.clicked += HidePanel;
            }

            // Initially hidden
            HidePanel();
        }

        /// <summary>
        /// Show the relocalization panel with recent anchors and all available targets
        /// </summary>
        public void ShowPanel()
        {
            if (panel == null)
            {
                Debug.LogWarning("[RelocalizationPanel] Panel not initialized!");
                return;
            }

            if (activationController == null)
            {
                Debug.LogError("[RelocalizationPanel] ARSafeActivationController not found!");
                return;
            }

            // Populate recent anchors
            PopulateRecentAnchors();

            // Populate all targets (grouped by floor)
            PopulateAllTargets();

            // Update subtitle to show current floor
            UpdateCurrentFloorContext();

            // Show panel
            panel.style.display = DisplayStyle.Flex;
            panel.style.opacity = 0;

            // Fade in
            panel.schedule.Execute(() =>
            {
                panel.style.opacity = 1;
            }).StartingIn(50);

            if (enableDebugLogs)
            {
                Debug.Log("[RelocalizationPanel] Panel shown");
            }
        }

        /// <summary>
        /// Update the subtitle to show current floor context
        /// </summary>
        private void UpdateCurrentFloorContext()
        {
            var currentAnchor = activationController.GetCurrentAnchor();
            if (currentAnchor == null) return;

            var currentInfo = currentAnchor.GetComponent<ARSafeTargetInfo>();
            if (currentInfo == null) return;

            // Find subtitle label and update it
            var subtitle = root.Q<Label>("all-targets-subtitle");
            if (subtitle != null)
            {
                subtitle.text = $"You are currently on Floor {currentInfo.floorLevel}";
            }
        }

        /// <summary>
        /// Hide the relocalization panel
        /// </summary>
        public void HidePanel()
        {
            if (panel == null) return;

            // Fade out
            panel.style.opacity = 0;

            panel.schedule.Execute(() =>
            {
                panel.style.display = DisplayStyle.None;
            }).StartingIn(300);

            if (enableDebugLogs)
            {
                Debug.Log("[RelocalizationPanel] Panel hidden");
            }
        }

        private void PopulateRecentAnchors()
        {
            if (recentAnchorsContainer == null) return;

            recentAnchorsContainer.Clear();

            var history = activationController.GetAnchorHistory();

            if (history == null || history.Count == 0)
            {
                // No history - show message
                var noHistoryLabel = new Label("No recent locations");
                noHistoryLabel.AddToClassList("no-history-label");
                recentAnchorsContainer.Add(noHistoryLabel);
                return;
            }

            // Add recent anchor buttons (max 5)
            int count = 0;
            foreach (var anchor in history)
            {
                if (anchor == null) continue;

                var button = CreateTargetButton(anchor, true);
                recentAnchorsContainer.Add(button);

                count++;
                if (count >= 5) break;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[RelocalizationPanel] Populated {count} recent anchors");
            }
        }

        private void PopulateAllTargets()
        {
            if (allTargetsContainer == null) return;

            allTargetsContainer.Clear();

            var allTargets = activationController.allAreaTargets;

            if (allTargets == null || allTargets.Count == 0)
            {
                var noTargetsLabel = new Label("No Area Targets found");
                noTargetsLabel.AddToClassList("no-targets-label");
                allTargetsContainer.Add(noTargetsLabel);
                return;
            }

            // Get all targets with their info components
            var targetsWithInfo = allTargets
                .Where(t => t != null)
                .Select(t => new { Target = t, Info = t.GetComponent<ARSafeTargetInfo>() })
                .Where(x => x.Info != null)
                .ToList();

            if (targetsWithInfo.Count == 0)
            {
                var noInfoLabel = new Label("No valid targets found");
                noInfoLabel.AddToClassList("no-targets-label");
                allTargetsContainer.Add(noInfoLabel);
                return;
            }

            // Group by floor level
            var floorGroups = targetsWithInfo
                .GroupBy(x => x.Info.floorLevel)
                .OrderBy(g => g.Key);

            int totalTargets = 0;

            bool isFirstFloorHeader = true;

            foreach (var floorGroup in floorGroups)
            {
                // Create floor header
                var floorHeader = new Label($"Floor {floorGroup.Key}");
                floorHeader.AddToClassList("floor-header");
                if (!isFirstFloorHeader)
                {
                    floorHeader.AddToClassList("floor-header--spaced");
                }
                allTargetsContainer.Add(floorHeader);

                // Sort targets within this floor by name
                var floorTargets = floorGroup.OrderBy(x => x.Target.name).ToList();

                foreach (var item in floorTargets)
                {
                    var button = CreateTargetButton(item.Target, false);
                    allTargetsContainer.Add(button);
                    totalTargets++;
                }

                isFirstFloorHeader = false;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[RelocalizationPanel] Populated {totalTargets} targets across {floorGroups.Count()} floors");
            }
        }

        private Button CreateTargetButton(ObserverBehaviour target, bool isRecent)
        {
            var button = new Button();
            button.AddToClassList("target-button");

            if (isRecent)
            {
                button.AddToClassList("recent-button");
            }

            // Use GameObject name as display name
            string displayName = target.name;

            // Clean up common prefixes/suffixes for better readability
            displayName = displayName.Replace("AreaTarget_", "").Replace("_AreaTarget", "");

            button.text = displayName;

            // Click handler
            button.clicked += () => OnTargetSelected(target);

            return button;
        }

        private void OnTargetSelected(ObserverBehaviour target)
        {
            if (target == null) return;

            if (enableDebugLogs)
            {
                Debug.Log($"[RelocalizationPanel] User selected: {target.name}");
            }

            // Hide this panel first
            HidePanel();

            // Close simulation menu if open
            var simulationMenu = FindFirstObjectByType<SimulationBackButtonController>();
            if (simulationMenu != null)
            {
                simulationMenu.CloseMenu();
            }

            // Show localization instructions
            var instructionsController = FindFirstObjectByType<LocalizationInstructionsController>();
            if (instructionsController != null)
            {
                instructionsController.ShowRelocalization();
            }

            // Call relocalization (this will switch the anchor)
            activationController.RelocalizeTo(target);

            if (enableDebugLogs)
            {
                Debug.Log($"[RelocalizationPanel] Relocalization initiated for: {target.name}");
            }
        }
    }
}
