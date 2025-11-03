using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;

namespace ARSafe.UI
{
    /// <summary>
    /// Shows localization instructions in the center-top of screen.
    /// Used during initial localization and relocalization flows.
    /// Waits for tracking to be established before hiding.
    /// </summary>
    public class LocalizationInstructionsController : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("UIDocument component (auto-finds if not set)")]
        public UIDocument uiDocument;

        [Header("Messages")]
        [Tooltip("Message shown during initial localization")]
        [TextArea(2, 4)]
        public string initialLocalizationMessage = "Finding your location using AR tracking\n\nPoint your camera at the floor or nearby walls";

        [Tooltip("Message shown during relocalization")]
        [TextArea(2, 4)]
        public string relocalizationMessage = "Point your camera at the selected location\n\nLook at the floor and walls - AR will recognize the space";

        [Header("Tracking Settings")]
        [Tooltip("Minimum time tracking must be active before hiding instructions (seconds)")]
        [Range(0.5f, 5f)]
        public float minTrackingDuration = 1.5f;

        [Tooltip("Auto-hide instructions after timeout if tracking not established (0 = never)")]
        [Range(0f, 120f)]
        public float timeoutDuration = 60f;

        [Header("Debug")]
        public bool enableDebugLogs = false;

        // UI Elements
        private VisualElement root;
        private VisualElement instructionsPanel;
    private VisualElement compactBar;
    private Label compactText;
    private Label expandIcon;
    private VisualElement instructionsBody;
        private Label instructionsLabel;
        private VisualElement loadingSpinner;
        private VisualElement spinnerRing;
    private VisualElement miniLoadingSpinner;
    private VisualElement miniSpinnerRing;

        // State
        private bool isVisible = false;
        private bool isWaitingForTracking = false;
    private bool isExpanded = false;
        private float trackingStartTime = -1f;
        private float showTime = -1f;
        private ARSafeActivationController activationController;
        private ARSafeTrackingManager trackingManager;

        // Spinner animation
        private IVisualElementScheduledItem spinnerAnimation;
        private float spinnerRotation = 0f;
        private const float SPINNER_SPEED = 360f; // degrees per second
    private IVisualElementScheduledItem miniSpinnerAnimation;
    private float miniSpinnerRotation = 0f;

        private void Awake()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            activationController = FindFirstObjectByType<ARSafeActivationController>();
            trackingManager = FindFirstObjectByType<ARSafeTrackingManager>();
        }

        private void OnEnable()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null)
            {
                Debug.LogError("[LocalizationInstructions] UIDocument or root visual element is null!");
                return;
            }

            root = uiDocument.rootVisualElement;
            SetupUI();
        }

        private void SetupUI()
        {
            // Get main panel
            instructionsPanel = root.Q<VisualElement>("localization-instructions-panel");
            if (instructionsPanel == null)
            {
                Debug.LogError("[LocalizationInstructions] localization-instructions-panel not found in UXML!");
                return;
            }

            // Compact bar and expandable body
            compactBar = root.Q<VisualElement>("compact-bar");
            compactText = root.Q<Label>("compact-text");
            expandIcon = root.Q<Label>("expand-icon");
            instructionsBody = root.Q<VisualElement>("instructions-body");

            // Get elements
            instructionsLabel = root.Q<Label>("instructions-label");
            loadingSpinner = root.Q<VisualElement>("loading-spinner");
            spinnerRing = root.Q<VisualElement>("spinner-ring");
            miniLoadingSpinner = root.Q<VisualElement>("mini-loading-spinner");
            miniSpinnerRing = root.Q<VisualElement>("mini-spinner-ring");

            // Initially hidden
            Hide(true);

            // Wire compact bar toggle
            if (compactBar != null)
            {
                compactBar.RegisterCallback<PointerDownEvent>(_ => ToggleExpanded());
                compactBar.pickingMode = PickingMode.Position;
            }
        }

        private void Update()
        {
            if (!isWaitingForTracking || !isVisible)
            {
                return;
            }

            // Check timeout
            if (timeoutDuration > 0 && Time.time - showTime > timeoutDuration)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[LocalizationInstructions] Timeout reached ({timeoutDuration}s) - hiding instructions");
                }
                Hide();
                return;
            }

            // Check if tracking is established
            bool hasTracking = CheckTrackingEstablished();

            if (hasTracking)
            {
                // Tracking just started
                if (trackingStartTime < 0)
                {
                    trackingStartTime = Time.time;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"[LocalizationInstructions] Tracking established - waiting {minTrackingDuration}s for stability...");
                    }
                }
                else
                {
                    // Check if tracking has been stable long enough
                    float trackingDuration = Time.time - trackingStartTime;
                    if (trackingDuration >= minTrackingDuration)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=green>[LocalizationInstructions] Tracking stable for {trackingDuration:F1}s - hiding instructions</color>");
                        }
                        Hide();
                    }
                }
            }
            else
            {
                // Lost tracking - reset timer
                if (trackingStartTime >= 0)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log("[LocalizationInstructions] Tracking lost - resetting stability timer");
                    }
                    trackingStartTime = -1f;
                }
            }
        }

        /// <summary>
        /// Show instructions for initial localization (first time entering AR scene)
        /// </summary>
        public void ShowInitialLocalization()
        {
            ShowInstructions(initialLocalizationMessage, true);

            if (enableDebugLogs)
            {
                Debug.Log("[LocalizationInstructions] Showing initial localization instructions");
            }
        }

        /// <summary>
        /// Show instructions for relocalization (user manually relocalizing)
        /// </summary>
        public void ShowRelocalization()
        {
            ShowInstructions(relocalizationMessage, true);

            if (enableDebugLogs)
            {
                Debug.Log("[LocalizationInstructions] Showing relocalization instructions");
            }
        }

        /// <summary>
        /// Show custom instructions with optional tracking wait
        /// </summary>
        public void ShowInstructions(string message, bool waitForTracking = false)
        {
            if (instructionsPanel == null || instructionsLabel == null)
            {
                Debug.LogWarning("[LocalizationInstructions] UI not initialized!");
                return;
            }

            // Set message
            instructionsLabel.text = message;

            // Show panel (collapsed by default)
            instructionsPanel.style.display = DisplayStyle.Flex;
            instructionsPanel.style.opacity = 0;
            instructionsPanel.pickingMode = PickingMode.Position;

            // Start collapsed
            Collapse(immediate: true);

            // Fade in
            instructionsPanel.schedule.Execute(() =>
            {
                instructionsPanel.style.opacity = 1;
                // Prime input on the compact bar for mobile
                if (compactBar != null) compactBar.Focus();
            }).StartingIn(50);

            isVisible = true;
            isWaitingForTracking = waitForTracking;
            trackingStartTime = -1f;
            showTime = Time.time;

            // Show/hide loading spinner
            if (loadingSpinner != null)
            {
                // Body spinner only visible when expanded
                loadingSpinner.style.display = DisplayStyle.None;
            }

            // Compact bar: update status and mini-spinner
            if (compactText != null)
            {
                compactText.text = waitForTracking ? "Localizing… Tap for tips" : "Tips available – Tap to view";
            }

            if (miniLoadingSpinner != null)
            {
                miniLoadingSpinner.style.display = waitForTracking ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (waitForTracking)
            {
                StartMiniSpinnerAnimation();
            }
            else
            {
                StopMiniSpinnerAnimation();
            }
        }

        /// <summary>
        /// Hide instructions panel
        /// </summary>
        public void Hide(bool immediate = false)
        {
            if (instructionsPanel == null)
            {
                return;
            }

            isVisible = false;
            isWaitingForTracking = false;
            isExpanded = false;
            trackingStartTime = -1f;

            // Stop spinner animation
            StopSpinnerAnimation();
            StopMiniSpinnerAnimation();

            if (immediate)
            {
                instructionsPanel.style.display = DisplayStyle.None;
                instructionsPanel.style.opacity = 0;
                if (instructionsBody != null)
                {
                    instructionsBody.style.display = DisplayStyle.None;
                    instructionsBody.style.opacity = 0;
                    instructionsBody.pickingMode = PickingMode.Ignore;
                }
                return;
            }

            // Fade out
            instructionsPanel.style.opacity = 0;

            instructionsPanel.schedule.Execute(() =>
            {
                instructionsPanel.style.display = DisplayStyle.None;
            }).StartingIn(300);

            if (enableDebugLogs)
            {
                Debug.Log("[LocalizationInstructions] Instructions hidden");
            }
        }

        private void ToggleExpanded()
        {
            if (!isVisible || instructionsBody == null)
                return;

            if (isExpanded)
            {
                Collapse();
            }
            else
            {
                Expand();
            }
        }

        private void Expand(bool immediate = false)
        {
            if (instructionsBody == null)
                return;

            isExpanded = true;

            // Update icon
            if (expandIcon != null) expandIcon.text = "˄";

            // Body spinner visibility when waiting
            if (loadingSpinner != null)
            {
                loadingSpinner.style.display = isWaitingForTracking ? DisplayStyle.Flex : DisplayStyle.None;
                if (isWaitingForTracking) StartSpinnerAnimation(); else StopSpinnerAnimation();
            }

            // Show body
            instructionsBody.style.display = DisplayStyle.Flex;
            instructionsBody.pickingMode = PickingMode.Position;
            if (immediate)
            {
                instructionsBody.style.opacity = 1f;
            }
            else
            {
                instructionsBody.style.opacity = 0f;
                instructionsBody.schedule.Execute(() => { instructionsBody.style.opacity = 1f; }).StartingIn(50);
            }

            if (enableDebugLogs)
            {
                Debug.Log("[LocalizationInstructions] Expanded (dropdown open)");
            }
        }

        private void Collapse(bool immediate = false)
        {
            if (instructionsBody == null)
                return;

            isExpanded = false;

            // Update icon
            if (expandIcon != null) expandIcon.text = "˅";

            // Hide body spinner
            StopSpinnerAnimation();
            if (loadingSpinner != null)
            {
                loadingSpinner.style.display = DisplayStyle.None;
            }

            // Hide body
            if (immediate)
            {
                instructionsBody.style.opacity = 0f;
                instructionsBody.style.display = DisplayStyle.None;
                instructionsBody.pickingMode = PickingMode.Ignore;
            }
            else
            {
                instructionsBody.style.opacity = 1f;
                instructionsBody.style.display = DisplayStyle.Flex;
                instructionsBody.schedule.Execute(() =>
                {
                    instructionsBody.style.opacity = 0f;
                    instructionsBody.style.display = DisplayStyle.None;
                    instructionsBody.pickingMode = PickingMode.Ignore;
                }).StartingIn(150);
            }

            if (enableDebugLogs)
            {
                Debug.Log("[LocalizationInstructions] Collapsed (dropdown closed)");
            }
        }

        /// <summary>
        /// Start animating the loading spinner
        /// </summary>
        private void StartSpinnerAnimation()
        {
            if (spinnerRing == null)
            {
                return;
            }

            // Stop existing animation if any
            StopSpinnerAnimation();

            // Reset rotation
            spinnerRotation = 0f;

            // Start continuous rotation animation
            // UI Toolkit doesn't support CSS animations, so we use schedule.Execute
            spinnerAnimation = spinnerRing.schedule.Execute(() =>
            {
                // Increment rotation
                spinnerRotation += SPINNER_SPEED * Time.deltaTime;
                if (spinnerRotation >= 360f)
                {
                    spinnerRotation -= 360f;
                }

                // Apply rotation using UI Toolkit's rotate transform
                spinnerRing.style.rotate = new Rotate(new Angle(spinnerRotation));
            }).Every(16); // ~60 FPS (16ms per frame)
        }

        /// <summary>
        /// Stop the loading spinner animation
        /// </summary>
        private void StopSpinnerAnimation()
        {
            if (spinnerAnimation != null)
            {
                spinnerAnimation.Pause();
                spinnerAnimation = null;
            }

            if (spinnerRing != null)
            {
                spinnerRing.style.rotate = new Rotate(new Angle(0f));
                spinnerRotation = 0f;
            }
        }

        private void StartMiniSpinnerAnimation()
        {
            if (miniSpinnerRing == null)
            {
                return;
            }

            StopMiniSpinnerAnimation();
            miniSpinnerRotation = 0f;
            miniSpinnerAnimation = miniSpinnerRing.schedule.Execute(() =>
            {
                miniSpinnerRotation += SPINNER_SPEED * Time.deltaTime;
                if (miniSpinnerRotation >= 360f)
                {
                    miniSpinnerRotation -= 360f;
                }
                miniSpinnerRing.style.rotate = new Rotate(new Angle(miniSpinnerRotation));
            }).Every(16);
        }

        private void StopMiniSpinnerAnimation()
        {
            if (miniSpinnerAnimation != null)
            {
                miniSpinnerAnimation.Pause();
                miniSpinnerAnimation = null;
            }

            if (miniSpinnerRing != null)
            {
                miniSpinnerRing.style.rotate = new Rotate(new Angle(0f));
                miniSpinnerRotation = 0f;
            }
        }

        /// <summary>
        /// Check if tracking is currently established
        /// </summary>
        private bool CheckTrackingEstablished()
        {
            // Check if we have a current anchor
            if (activationController != null)
            {
                var currentAnchor = activationController.GetCurrentAnchor();
                if (currentAnchor == null)
                {
                    return false;
                }

                // Check if anchor is tracking
                if (trackingManager != null)
                {
                    return trackingManager.IsTracking(currentAnchor);
                }

                return true; // Assume tracking if we have anchor but no tracking manager
            }

            return false;
        }

        /// <summary>
        /// Public accessor for visibility state
        /// </summary>
        public bool IsVisible => isVisible;
        public bool IsExpanded => isExpanded;
    }
}
