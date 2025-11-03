using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;
using System.Collections.Generic;

namespace ARSafe.UI
{
    /// <summary>
    /// Displays customizable notification messages at the top-left of the screen.
    /// Supports message queue with stacking animations and sequential dismissal.
    /// </summary>
    public class MessageNotificationController : MonoBehaviour
    {
        public static MessageNotificationController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private UIDocument uiDocument;
        [SerializeField, Tooltip("Optional override UXML for the notification entry template.")]
        private VisualTreeAsset notificationTemplate;
        [SerializeField, Tooltip("Optional override stylesheet for notifications.")]
        private StyleSheet notificationStyles;

        [Header("Message Settings")]
        [Tooltip("Default duration to show messages (seconds). 0 = indefinite")]
        [SerializeField] private float defaultDisplayDuration = 6f;

        [Tooltip("Minimum display time before message can be dismissed (seconds)")]
        [SerializeField] private float minimumDisplayTime = 3f;

        [Tooltip("Vertical spacing between stacked messages (pixels)")]
        [SerializeField] private float messageSpacing = 20f;

        [Tooltip("Maximum number of visible messages at once")]
        [SerializeField] private int maxVisibleMessages = 3;

        [Header("Animation Settings")]
        [Tooltip("Time for message to slide in/out (seconds)")]
        [SerializeField] private float animationDuration = 0.4f;
        
        [Tooltip("Maximum lifetime for any message (seconds). Messages auto-dismiss after this time even if duration=0")]
        [SerializeField] private float maxMessageLifetime = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        // UI Container
        private VisualElement root;
        private VisualElement messageContainer;
        private VisualTreeAsset runtimeTemplate;
        private StyleSheet runtimeStyles;

        // Message Queue System
        private List<MessageInstance> activeMessages = new List<MessageInstance>();
        private Queue<QueuedMessage> messageQueue = new Queue<QueuedMessage>();
        private bool isProcessingQueue = false;

        public enum MessageType
        {
            Info,       // Blue - General information
            Success,    // Green - Success/completion
            Warning,    // Yellow - Warning/caution
            Error,      // Red - Error/problem
            ARHint      // Purple - AR-specific hints
        }

        private class MessageInstance
        {
            public VisualElement rootElement;
            public VisualElement cardElement;
            public Label messageLabel;
            public Label iconLabel;
            public float createTime;
            public float duration;
            public int index;
            public Coroutine dismissCoroutine;
        }

        private class QueuedMessage
        {
            public string text;
            public MessageType type;
            public float duration;
        }

        private void Awake()
        {
            // Singleton pattern
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            InitializeUI();
            
            // Start periodic cleanup check for stuck messages
            StartCoroutine(PeriodicCleanupCheck());
        }
        
        private IEnumerator PeriodicCleanupCheck()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f); // Check every 5 seconds
                
                // Check for messages that have exceeded max lifetime
                var stuckMessages = new List<MessageInstance>();
                foreach (var msg in activeMessages)
                {
                    float lifetime = Time.time - msg.createTime;
                    if (lifetime > maxMessageLifetime)
                    {
                        stuckMessages.Add(msg);
                        
                        if (showDebugLogs)
                        {
                            Debug.LogWarning($"<color=orange>[MessageNotificationController] ⚠️ Message stuck (lifetime={lifetime:F1}s): \"{msg.messageLabel?.text}\"</color>", this);
                        }
                    }
                }
                
                // Dismiss stuck messages
                foreach (var msg in stuckMessages)
                {
                    DismissMessage(msg);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            // Clean up all active messages
            foreach (var msg in activeMessages)
            {
                if (msg.dismissCoroutine != null)
                {
                    StopCoroutine(msg.dismissCoroutine);
                }
            }
            activeMessages.Clear();
            messageQueue.Clear();
        }

        private void InitializeUI()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument == null)
            {
                Debug.LogError("[MessageNotificationController] No UIDocument found! Attach this script to a GameObject with UIDocument component.", this);
                return;
            }

            // Ensure panel settings with HIGHEST sorting order for notifications
            if (uiDocument.panelSettings == null)
            {
                var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "MessageNotificationPanelSettings (Runtime)";
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.match = 0.5f;
                panelSettings.sortingOrder = 9999;
                uiDocument.panelSettings = panelSettings;

                if (showDebugLogs)
                {
                    Debug.Log("<color=cyan>[MessageNotificationController] Created panel settings with sorting order 9999</color>", this);
                }
            }
            else if (uiDocument.panelSettings.sortingOrder < 9999)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"<color=yellow>[MessageNotificationController] Increasing sorting order from {uiDocument.panelSettings.sortingOrder} to 9999</color>", this);
                }
                uiDocument.panelSettings.sortingOrder = 9999;
            }

            root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[MessageNotificationController] UIDocument root is null!", this);
                return;
            }

            root.Clear();

            LoadAssets();

            // Always bind stylesheet first so classes render even when template fallback is used
            if (runtimeStyles != null)
            {
                if (!root.styleSheets.Contains(runtimeStyles))
                {
                    root.styleSheets.Add(runtimeStyles);
                }
            }
            else if (showDebugLogs)
            {
                Debug.LogWarning("<color=yellow>[MessageNotificationController] Missing stylesheet — notifications will use default visual styling.</color>", this);
            }

            // Create container used for stacking messages
            messageContainer = new VisualElement { name = "message-container" };
            messageContainer.AddToClassList("message-container");
            messageContainer.pickingMode = PickingMode.Ignore;
            root.Add(messageContainer);

            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[MessageNotificationController] ✓ Initialized with message queue system (sorting order: {uiDocument.panelSettings.sortingOrder})</color>", this);
            }
        }

        /// <summary>
        /// Show a message with default settings (Info type, default duration)
        /// </summary>
        public void ShowMessage(string message)
        {
            ShowMessage(message, MessageType.Info, defaultDisplayDuration);
        }

        /// <summary>
        /// Show a message with specific type and default duration
        /// </summary>
        public void ShowMessage(string message, MessageType type)
        {
            ShowMessage(message, type, defaultDisplayDuration);
        }

        /// <summary>
        /// Show a message with full customization
        /// </summary>
        /// <param name="message">The message text to display</param>
        /// <param name="type">Message type (affects color and icon)</param>
        /// <param name="duration">How long to show (seconds). 0 = indefinite (must hide manually)</param>
        public void ShowMessage(string message, MessageType type, float duration)
        {
            if (messageContainer == null)
            {
                Debug.LogError("[MessageNotificationController] UI not initialized!", this);
                return;
            }

            // Queue the message if we're at max capacity
            if (activeMessages.Count >= maxVisibleMessages)
            {
                messageQueue.Enqueue(new QueuedMessage { text = message, type = type, duration = duration });
                
                if (showDebugLogs)
                {
                    Debug.Log($"<color=yellow>[MessageNotificationController] Message queued (max reached): \"{message}\"</color>", this);
                }
                return;
            }

            CreateAndShowMessage(message, type, duration);
        }

        private void CreateAndShowMessage(string message, MessageType type, float duration)
        {
            // Create message instance
            var msgInstance = new MessageInstance
            {
                createTime = Time.time,
                duration = duration,
                index = activeMessages.Count
            };

            // Create root element from template when available
            if (runtimeTemplate != null)
            {
                var templateRoot = runtimeTemplate.CloneTree();
                msgInstance.rootElement = templateRoot.Q<VisualElement>("message-notification-root") ?? templateRoot;
                msgInstance.cardElement = msgInstance.rootElement.Q<VisualElement>("message-card") ?? msgInstance.rootElement;
                msgInstance.iconLabel = msgInstance.rootElement.Q<Label>("icon-text") ?? new Label();
                msgInstance.messageLabel = msgInstance.rootElement.Q<Label>("message-text") ?? new Label();

                msgInstance.rootElement.AddToClassList("hidden");

                // Ensure elements exist even if template is missing parts
                if (msgInstance.iconLabel.parent == null)
                {
                    var iconContainer = new VisualElement();
                    iconContainer.AddToClassList("message-icon");
                    iconContainer.Add(msgInstance.iconLabel);
                    msgInstance.cardElement.Insert(0, iconContainer);
                }

                if (msgInstance.messageLabel.parent == null)
                {
                    var contentContainer = new VisualElement();
                    contentContainer.AddToClassList("message-content");
                    contentContainer.Add(msgInstance.messageLabel);
                    msgInstance.cardElement.Add(contentContainer);
                }
            }
            else
            {
                msgInstance.rootElement = new VisualElement
                {
                    name = $"message-notification-{msgInstance.index}"
                };
                msgInstance.rootElement.AddToClassList("message-notification");
                msgInstance.rootElement.AddToClassList("hidden");

                msgInstance.cardElement = new VisualElement();
                msgInstance.cardElement.AddToClassList("message-card");

                var iconContainer = new VisualElement();
                iconContainer.AddToClassList("message-icon");
                msgInstance.iconLabel = new Label();
                msgInstance.iconLabel.AddToClassList("icon-text");
                iconContainer.Add(msgInstance.iconLabel);

                var contentContainer = new VisualElement();
                contentContainer.AddToClassList("message-content");
                msgInstance.messageLabel = new Label(message);
                msgInstance.messageLabel.AddToClassList("message-label");
                contentContainer.Add(msgInstance.messageLabel);

                msgInstance.cardElement.Add(iconContainer);
                msgInstance.cardElement.Add(contentContainer);
                msgInstance.rootElement.Add(msgInstance.cardElement);
            }

            if (msgInstance.messageLabel != null)
            {
                msgInstance.messageLabel.text = message;
            }

            // Update styling
            UpdateMessageStyle(msgInstance, type);

            // Add to container
            messageContainer.Add(msgInstance.rootElement);
            activeMessages.Add(msgInstance);

            // Position all messages
            UpdateMessagePositions();

            // Trigger show animation on next frame
            StartCoroutine(ShowMessageDelayed(msgInstance));

            if (showDebugLogs)
            {
                Debug.Log($"<color=cyan>[MessageNotificationController] Created message #{msgInstance.index}: \"{message}\" ({type}, {duration}s)</color>", this);
            }

            // Auto-dismiss after duration
            if (duration > 0)
            {
                msgInstance.dismissCoroutine = StartCoroutine(AutoDismissMessage(msgInstance, duration));
            }
        }

        private void LoadAssets()
        {
            runtimeTemplate = notificationTemplate != null
                ? notificationTemplate
                : Resources.Load<VisualTreeAsset>("UI/MessageNotification/MessageNotification");

            runtimeStyles = notificationStyles != null
                ? notificationStyles
                : Resources.Load<StyleSheet>("UI/MessageNotification/MessageNotification");

            if (runtimeTemplate == null && showDebugLogs)
            {
                Debug.LogWarning("<color=yellow>[MessageNotificationController] No notification template found. Using procedural element generation.</color>", this);
            }

            if (runtimeStyles == null && showDebugLogs)
            {
                Debug.LogWarning("<color=yellow>[MessageNotificationController] No stylesheet found for notifications. Styling may be degraded.</color>", this);
            }
        }

        private IEnumerator ShowMessageDelayed(MessageInstance msg)
        {
            yield return null; // Wait one frame for CSS to apply
            msg.rootElement.RemoveFromClassList("hidden");
        }

        private void UpdateMessagePositions()
        {
            float currentTop = 0;
            
            for (int i = 0; i < activeMessages.Count; i++)
            {
                var msg = activeMessages[i];
                msg.index = i;
                
                // Animate to new position
                msg.rootElement.style.top = currentTop;
                
                // Calculate next position using fixed height estimate
                // Average message height is ~60-70px, use 70 + spacing for safety
                float estimatedHeight = 70f;
                currentTop += estimatedHeight + messageSpacing;
            }
        }

        private void UpdateMessageStyle(MessageInstance msg, MessageType type)
        {
            if (msg.cardElement == null || msg.iconLabel == null) return;

            // Remove all type classes
            msg.cardElement.RemoveFromClassList("info");
            msg.cardElement.RemoveFromClassList("success");
            msg.cardElement.RemoveFromClassList("warning");
            msg.cardElement.RemoveFromClassList("error");
            msg.cardElement.RemoveFromClassList("ar-hint");

            // Apply new type class and icon
            switch (type)
            {
                case MessageType.Info:
                    msg.cardElement.AddToClassList("info");
                    msg.iconLabel.text = "i";
                    break;

                case MessageType.Success:
                    msg.cardElement.AddToClassList("success");
                    msg.iconLabel.text = "✓";
                    break;

                case MessageType.Warning:
                    msg.cardElement.AddToClassList("warning");
                    msg.iconLabel.text = "!";
                    break;

                case MessageType.Error:
                    msg.cardElement.AddToClassList("error");
                    msg.iconLabel.text = "✕";
                    break;

                case MessageType.ARHint:
                    msg.cardElement.AddToClassList("ar-hint");
                    msg.iconLabel.text = "AR";
                    break;
            }
        }

        private IEnumerator AutoDismissMessage(MessageInstance msg, float delay)
        {
            // If duration is 0 (indefinite), use max lifetime instead
            float displayTime = delay > 0 ? Mathf.Max(delay, minimumDisplayTime) : maxMessageLifetime;
            
            if (showDebugLogs && delay == 0)
            {
                Debug.Log($"<color=cyan>[MessageNotificationController] Indefinite message will auto-dismiss after {maxMessageLifetime}s max lifetime</color>", this);
            }
            
            yield return new WaitForSeconds(displayTime);

            DismissMessage(msg);
        }

        private void DismissMessage(MessageInstance msg)
        {
            if (msg == null || !activeMessages.Contains(msg)) return;

            // Stop auto-dismiss coroutine if running
            if (msg.dismissCoroutine != null)
            {
                StopCoroutine(msg.dismissCoroutine);
                msg.dismissCoroutine = null;
            }

            if (showDebugLogs)
            {
                Debug.Log($"<color=yellow>[MessageNotificationController] Dismissing message #{msg.index}: \"{msg.messageLabel?.text}\"</color>", this);
            }

            // Start hide animation
            if (msg.rootElement != null)
            {
                msg.rootElement.AddToClassList("hidden");
            }

            // Remove after animation completes
            StartCoroutine(RemoveMessageAfterAnimation(msg));
        }

        private IEnumerator RemoveMessageAfterAnimation(MessageInstance msg)
        {
            // Wait for animation to complete
            yield return new WaitForSeconds(animationDuration);

            // Remove from active list
            activeMessages.Remove(msg);

            // Remove from UI
            if (msg.rootElement != null && msg.rootElement.parent != null)
            {
                messageContainer.Remove(msg.rootElement);
            }

            // Update positions of remaining messages with animation
            UpdateMessagePositions();

            // Process queued messages
            ProcessMessageQueue();
        }

        private void ProcessMessageQueue()
        {
            if (messageQueue.Count == 0 || isProcessingQueue) return;

            isProcessingQueue = true;

            while (messageQueue.Count > 0 && activeMessages.Count < maxVisibleMessages)
            {
                var queuedMsg = messageQueue.Dequeue();
                CreateAndShowMessage(queuedMsg.text, queuedMsg.type, queuedMsg.duration);
            }

            isProcessingQueue = false;
        }

        /// <summary>
        /// Hide all messages immediately
        /// </summary>
        public void HideAllMessages()
        {
            foreach (var msg in new List<MessageInstance>(activeMessages))
            {
                DismissMessage(msg);
            }
        }

        /// <summary>
        /// Clear all messages immediately without animation
        /// </summary>
        public void ClearAllMessages()
        {
            foreach (var msg in activeMessages)
            {
                if (msg.dismissCoroutine != null)
                {
                    StopCoroutine(msg.dismissCoroutine);
                }
                
                if (msg.rootElement != null && msg.rootElement.parent != null)
                {
                    messageContainer.Remove(msg.rootElement);
                }
            }

            activeMessages.Clear();
            messageQueue.Clear();
        }

        /// <summary>
        /// Quick helper for showing "Point phone at Area Target" message
        /// </summary>
        public void ShowARTargetPrompt()
        {
            ShowMessage("Point your phone at an Area Target", MessageType.ARHint, 0); // Indefinite
        }

        /// <summary>
        /// Quick helper for showing tracking success
        /// </summary>
        public void ShowTrackingSuccess()
        {
            ShowMessage("Area Target detected!", MessageType.Success, 4f);
        }

        /// <summary>
        /// Quick helper for showing tracking lost
        /// </summary>
        public void ShowTrackingLost()
        {
            ShowMessage("Tracking lost - move closer to target", MessageType.Warning, 0);
        }

        /// <summary>
        /// Helper for showing evacuation point completion
        /// </summary>
        public void ShowEvacuationReached(float duration = 6f)
        {
            ShowMessage("Evacuation point reached. Proceed to the safe zone.", MessageType.Success, duration);
        }
    }
}
