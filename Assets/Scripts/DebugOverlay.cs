using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Creates and manages a runtime debug UI overlay.
/// This is a singleton that can be accessed from any script.
/// It automatically builds its own UI elements.
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    // --- Singleton Instance ---
    public static DebugOverlay Instance { get; private set; }

    public enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Header("Style / Layout")]
    [SerializeField] private Corner anchorCorner = Corner.TopRight;
    [SerializeField] private float panelWidth = 450f;
    [SerializeField] private float maxPanelHeight = 600f;
    [SerializeField] private int baseFontSize = 14;
    [SerializeField] private int headerFontSize = 16;
    [SerializeField] private int subHeaderFontSize = 15;
    [SerializeField] private int padding = 12;
    [SerializeField] private float backgroundAlpha = 0.75f;
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.09f, 0.12f, 0.75f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.94f, 0.97f, 1f);
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.8f, 0.3f, 1f); // For important status
    [SerializeField] private Color warningColor = new Color(0.9f, 0.7f, 0.2f, 1f); // For warnings
    [SerializeField] private Color errorColor = new Color(0.9f, 0.3f, 0.3f, 1f); // For errors
    [SerializeField] private Vector2 toggleButtonSize = new Vector2(176f, 72f);
    [SerializeField] private int toggleButtonFontSize = 28;

    [Header("Behavior")]
    [SerializeField] private bool startVisible = true;
    [SerializeField] private bool compactMode = false; // DISABLED - let integration control layout
    [SerializeField] private int compactMaxTargetLines = 25; // Increased from 8
    [SerializeField] private bool autoHideWhenEmpty = false;
    [SerializeField] private KeyCode editorToggleKey = KeyCode.BackQuote; // dev convenience

    [Header("Scene Visibility")]
    [Tooltip("Overlay will be visible only in these scenes when 'Hide In Other Scenes' is enabled.")]
    [SerializeField] private List<string> visibleInScenes = new List<string> { "MainScene" };
    [SerializeField] private bool hideInOtherScenes = true;
    
    [Header("Display Options")]
    [SerializeField] private bool showPerformanceStats = true;
    [SerializeField] private bool useColorCoding = true;
    [SerializeField] private bool showTimestamp = false;

    [Header("Advanced")] 
    [SerializeField] private bool clampHeight = true;
    [SerializeField] private bool shrinkToContentWidth = false;
    [SerializeField] private float widthShrinkPadding = 40f;

    // --- UI Elements ---
    private Text m_StatusText;
    private Text m_AnchorDetailsText;  // NEW: Anchor metadata section
    private Text m_TargetListText;
    private RectTransform m_BackgroundPanel;
    private Canvas m_Canvas;
    private Button m_ToggleButton;
    private Text m_ToggleButtonLabel;
    private RectTransform m_ToggleButtonRect;

    // --- State ---
    private StringBuilder m_StringBuilder;
    private bool m_DirtyStyle;
    private float m_LastUpdateTime;
    private int m_FrameCount;
    private float m_FpsUpdateInterval = 0.5f;
    private float m_CurrentFps;
    private float m_DeltaTimeAccumulator;
    private bool m_AllowInCurrentScene = true;
    private bool m_IsCollapsed;
    private string m_LastStatus = string.Empty;
    private string m_LastAnchorDetails = string.Empty;
    private string m_LastTargetList = string.Empty;

    #region Initialization

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Persist across scene loads if needed
        if (transform.parent != null)
        {
            transform.SetParent(null, true);
        }
        DontDestroyOnLoad(gameObject);

        m_StringBuilder = new StringBuilder(512);
        SetupUI();
        if (!startVisible) Hide(); else Show();

        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EvaluateScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }
    }

    private void SetupUI()
    {
        // --- Create Canvas ---
        GameObject canvasGO = new GameObject("DebugCanvas");
        m_Canvas = canvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_Canvas.sortingOrder = 1000; // Ensure it's on top of everything
        
        // IMPROVED: Better canvas scaling setup
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f; // Balance between width and height matching
        
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // --- Create Background Panel ---
        GameObject panelGO = new GameObject("BackgroundPanel");
        panelGO.transform.SetParent(m_Canvas.transform, false);
        Image panelImage = panelGO.AddComponent<Image>();
        panelImage.color = AdjustAlpha(backgroundColor, backgroundAlpha);
        panelImage.raycastTarget = false;

        m_BackgroundPanel = panelGO.GetComponent<RectTransform>();
        ApplyCorner();
        m_BackgroundPanel.sizeDelta = new Vector2(panelWidth, 200f); // will auto-resize

        // --- Create Status Text (system status header) ---
        m_StatusText = CreateTextElement("StatusText", m_BackgroundPanel, FontStyle.Bold);
        RectTransform statusRect = m_StatusText.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0, 1);
        statusRect.anchorMax = new Vector2(1, 1);
        statusRect.pivot = new Vector2(0.5f, 1);
        statusRect.anchoredPosition = new Vector2(0, -padding);
        statusRect.sizeDelta = new Vector2(-padding * 2, 50);

        // --- NEW: Create Anchor Details Text (rich anchor metadata) ---
        m_AnchorDetailsText = CreateTextElement("AnchorDetailsText", m_BackgroundPanel, FontStyle.Normal);
        RectTransform anchorRect = m_AnchorDetailsText.GetComponent<RectTransform>();
        anchorRect.anchorMin = new Vector2(0, 1);
        anchorRect.anchorMax = new Vector2(1, 1);
        anchorRect.pivot = new Vector2(0.5f, 1);
        anchorRect.anchoredPosition = new Vector2(0, -padding - 50); // below status
        anchorRect.sizeDelta = new Vector2(-padding * 2, 100);

        // --- Create Target List Text ---
        m_TargetListText = CreateTextElement("TargetListText", m_BackgroundPanel, FontStyle.Normal);
        RectTransform listRect = m_TargetListText.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 0);
        listRect.anchorMax = new Vector2(1, 1);
        listRect.pivot = new Vector2(0.5f, 1);
        listRect.anchoredPosition = new Vector2(0, -padding - 150); // below anchor details
        listRect.sizeDelta = new Vector2(-padding * 2, -padding * 2 - 150);

        // Initial font sizing
        m_StatusText.fontSize = headerFontSize;
        m_AnchorDetailsText.fontSize = subHeaderFontSize;
        m_TargetListText.fontSize = baseFontSize;
        m_StatusText.color = textColor;
        m_AnchorDetailsText.color = textColor;
        m_TargetListText.color = textColor;

        // Toggle button allowing compact display
        GameObject buttonGO = new GameObject("OverlayToggleButton");
        buttonGO.transform.SetParent(m_BackgroundPanel, false);
        m_ToggleButtonRect = buttonGO.AddComponent<RectTransform>();
        m_ToggleButtonRect.anchorMin = new Vector2(1f, 1f);
        m_ToggleButtonRect.anchorMax = new Vector2(1f, 1f);
        m_ToggleButtonRect.pivot = new Vector2(1f, 1f);
        m_ToggleButtonRect.anchoredPosition = new Vector2(-padding, -padding);
    Vector2 buttonSize = toggleButtonSize;
    if (buttonSize.x < 96f) buttonSize.x = 96f;
    if (buttonSize.y < 48f) buttonSize.y = 48f;
    m_ToggleButtonRect.sizeDelta = buttonSize;

        Image buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = AdjustAlpha(highlightColor, 0.65f);
    buttonImage.raycastTarget = true;

        m_ToggleButton = buttonGO.AddComponent<Button>();
        m_ToggleButton.targetGraphic = buttonImage;
        m_ToggleButton.onClick.AddListener(ToggleCollapsedState);

        GameObject buttonLabelGO = new GameObject("Label");
        buttonLabelGO.transform.SetParent(buttonGO.transform, false);
        RectTransform buttonLabelRect = buttonLabelGO.AddComponent<RectTransform>();
        buttonLabelRect.anchorMin = Vector2.zero;
        buttonLabelRect.anchorMax = Vector2.one;
        buttonLabelRect.offsetMin = Vector2.zero;
        buttonLabelRect.offsetMax = Vector2.zero;

        m_ToggleButtonLabel = buttonLabelGO.AddComponent<Text>();
        m_ToggleButtonLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    m_ToggleButtonLabel.fontSize = Mathf.Max(toggleButtonFontSize, headerFontSize);
        m_ToggleButtonLabel.alignment = TextAnchor.MiddleCenter;
        m_ToggleButtonLabel.color = textColor;
        m_ToggleButtonLabel.text = "Hide";
        m_ToggleButtonLabel.raycastTarget = false;

        RefreshLayout();
    }

    private Text CreateTextElement(string name, Transform parent, FontStyle style)
    {
        GameObject textGO = new GameObject(name);
        textGO.transform.SetParent(parent, false);

        Text textComponent = textGO.AddComponent<Text>();
        textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // fallback font
        textComponent.fontSize = baseFontSize;
        textComponent.fontStyle = style;
        textComponent.color = textColor;
        textComponent.alignment = TextAnchor.UpperLeft;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.raycastTarget = false;
        textComponent.supportRichText = true; // Enable color tags

        return textComponent;
    }

    #endregion

    #region Public Update Methods

    /// <summary>
    /// Updates the entire debug display with the latest information from the manager.
    /// </summary>
    public void UpdateDisplay(string status, string targetList)
    {
        UpdateDisplay(status, "", targetList);
    }

    /// <summary>
    /// NEW: Enhanced update with separate anchor details section
    /// </summary>
    public void UpdateDisplay(string status, string anchorDetails, string targetList)
    {
        if (!m_AllowInCurrentScene)
        {
            return;
        }

        if (!m_Canvas.enabled) m_Canvas.enabled = true;

        string formattedStatus = status;
        string formattedTargets = targetList;
        string formattedAnchor = anchorDetails;

        // Augment status with performance stats if enabled
        if (showPerformanceStats)
        {
            UpdatePerformanceStats();
            m_StringBuilder.Clear();
            if (showTimestamp)
            {
                m_StringBuilder.AppendLine($"[{System.DateTime.Now:HH:mm:ss}]");
            }
            m_StringBuilder.AppendLine(formattedStatus);
            m_StringBuilder.AppendLine($"FPS: {m_CurrentFps:F1} | Frame: {Time.frameCount}");
            formattedStatus = m_StringBuilder.ToString().TrimEnd();
        }

        if (compactMode && compactMaxTargetLines > 0)
        {
            var lines = formattedTargets.Split('\n');
            if (lines.Length > compactMaxTargetLines)
            {
                int extra = lines.Length - compactMaxTargetLines;
                var sb = new StringBuilder();
                for (int i = 0; i < compactMaxTargetLines; i++) sb.AppendLine(lines[i]);
                sb.Append($"... (+{extra} more)");
                formattedTargets = sb.ToString();
            }
        }

        if (autoHideWhenEmpty && string.IsNullOrEmpty(formattedTargets) && string.IsNullOrEmpty(formattedStatus) && string.IsNullOrEmpty(formattedAnchor))
        {
            Hide();
            return;
        }
        else if (!m_Canvas.enabled && startVisible) Show();

        m_LastStatus = formattedStatus;
        m_LastAnchorDetails = ApplyColorCoding(formattedAnchor);
        m_LastTargetList = ApplyColorCoding(formattedTargets);

        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (m_BackgroundPanel == null || m_StatusText == null)
        {
            return;
        }

        string displayStatus = m_IsCollapsed ? ExtractSummary(m_LastStatus) : m_LastStatus;
        if (displayStatus == null)
        {
            displayStatus = string.Empty;
        }

        m_StatusText.text = displayStatus;

        bool showAnchor = !m_IsCollapsed && !string.IsNullOrEmpty(m_LastAnchorDetails);
        bool showTargets = !m_IsCollapsed && !string.IsNullOrEmpty(m_LastTargetList);

        if (m_AnchorDetailsText != null)
        {
            m_AnchorDetailsText.gameObject.SetActive(showAnchor);
            m_AnchorDetailsText.text = showAnchor ? m_LastAnchorDetails : string.Empty;
        }

        if (m_TargetListText != null)
        {
            m_TargetListText.gameObject.SetActive(showTargets);
            m_TargetListText.text = showTargets ? m_LastTargetList : string.Empty;
        }

        float statusHeight = Mathf.Clamp(m_StatusText.preferredHeight, 28f, 140f);
        float anchorHeight = showAnchor && m_AnchorDetailsText != null ? Mathf.Clamp(m_AnchorDetailsText.preferredHeight, 0f, 150f) : 0f;
        float listHeight = showTargets && m_TargetListText != null ? Mathf.Clamp(m_TargetListText.preferredHeight, 0f, Mathf.Max(0f, maxPanelHeight - statusHeight - anchorHeight - padding * 3)) : 0f;

        float topOffset = padding;
        if (m_ToggleButtonRect != null)
        {
            topOffset += m_ToggleButtonRect.sizeDelta.y + padding * 0.5f;
        }

        RectTransform statusRect = m_StatusText.GetComponent<RectTransform>();
        if (statusRect != null)
        {
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(0.5f, 1);
            statusRect.anchoredPosition = new Vector2(0, -topOffset);
            statusRect.sizeDelta = new Vector2(-padding * 2, statusHeight);
        }

        if (m_AnchorDetailsText != null)
        {
            RectTransform anchorRect = m_AnchorDetailsText.GetComponent<RectTransform>();
            anchorRect.anchoredPosition = new Vector2(0, -topOffset - statusHeight);
            anchorRect.sizeDelta = new Vector2(-padding * 2, anchorHeight);
        }

        if (m_TargetListText != null)
        {
            RectTransform listRect = m_TargetListText.GetComponent<RectTransform>();
            listRect.anchoredPosition = new Vector2(0, -topOffset - statusHeight - anchorHeight - padding);
            listRect.sizeDelta = new Vector2(-padding * 2, listHeight);
        }

        float width = panelWidth;
        if (shrinkToContentWidth)
        {
            float contentWidth = m_StatusText.preferredWidth;
            if (showAnchor && m_AnchorDetailsText != null)
            {
                contentWidth = Mathf.Max(contentWidth, m_AnchorDetailsText.preferredWidth);
            }
            if (showTargets && m_TargetListText != null)
            {
                contentWidth = Mathf.Max(contentWidth, m_TargetListText.preferredWidth);
            }
            width = Mathf.Min(panelWidth, contentWidth + widthShrinkPadding);
        }

        float totalHeight = topOffset + statusHeight + anchorHeight + listHeight + padding;
        if (clampHeight)
        {
            totalHeight = Mathf.Min(totalHeight, maxPanelHeight);
        }

        m_BackgroundPanel.sizeDelta = new Vector2(width, totalHeight);

        if (m_ToggleButtonLabel != null)
        {
            m_ToggleButtonLabel.text = m_IsCollapsed ? "Show" : "Hide";
        }

        m_DirtyStyle = false;
    }

    /// <summary>
    /// Hides the debug overlay completely.
    /// </summary>
    public void Hide() { if (m_Canvas != null && m_Canvas.enabled) m_Canvas.enabled = false; }
    public void Show()
    {
        if (!m_AllowInCurrentScene)
        {
            return;
        }

        if (m_Canvas != null && !m_Canvas.enabled)
        {
            m_Canvas.enabled = true;
        }
    }
    public void ToggleVisible()
    {
        if (m_Canvas == null) return;

        if (!m_AllowInCurrentScene)
        {
            return;
        }

        if (m_Canvas.enabled) Hide(); else Show();
    }
    public void SetCompact(bool compact) { compactMode = compact; m_DirtyStyle = true; }
    public void SetCorner(Corner c) { anchorCorner = c; ApplyCorner(); m_DirtyStyle = true; }
    
    /// <summary>
    /// Runtime control for performance stats display
    /// </summary>
    public void SetShowPerformanceStats(bool show) { showPerformanceStats = show; }
    
    /// <summary>
    /// Runtime control for color coding
    /// </summary>
    public void SetUseColorCoding(bool use) { useColorCoding = use; }
    
    /// <summary>
    /// Clear all text content
    /// </summary>
    public void Clear()
    {
        if (m_StatusText != null) m_StatusText.text = string.Empty;
        if (m_AnchorDetailsText != null) m_AnchorDetailsText.text = string.Empty;
        if (m_TargetListText != null) m_TargetListText.text = string.Empty;
    }

    private void ToggleCollapsedState()
    {
        m_IsCollapsed = !m_IsCollapsed;
        RefreshLayout();
    }

    private string ExtractSummary(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lines = value.Split('\n');
        int captured = 0;
        m_StringBuilder.Clear();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (captured > 0)
            {
                m_StringBuilder.Append('\n');
            }
            m_StringBuilder.Append(line);
            captured++;
            if (captured >= 2)
            {
                break;
            }
        }

        return m_StringBuilder.ToString();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(editorToggleKey)) ToggleVisible();
#endif

        if (!m_AllowInCurrentScene && m_Canvas != null && m_Canvas.enabled)
        {
            m_Canvas.enabled = false;
        }

        if (m_DirtyStyle && m_BackgroundPanel != null) // allow deferred update if changed at runtime
        {
            // Force a minimal refresh (no need to rebuild)
            ApplyCorner();
            m_DirtyStyle = false;
        }
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        EvaluateScene(next);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EvaluateScene(SceneManager.GetActiveScene());
    }

    private void EvaluateScene(Scene scene)
    {
        if (!hideInOtherScenes)
        {
            m_AllowInCurrentScene = true;
            if (startVisible) Show();
            return;
        }

        string sceneName = scene.name;
        bool allowed = false;

        if (visibleInScenes != null && visibleInScenes.Count > 0)
        {
            for (int i = 0; i < visibleInScenes.Count; i++)
            {
                if (string.Equals(visibleInScenes[i], sceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    allowed = true;
                    break;
                }
            }
        }

        m_AllowInCurrentScene = allowed;

        if (!allowed)
        {
            Hide();
        }
        else if (startVisible)
        {
            Show();
        }
    }

    private void ApplyCorner()
    {
        if (m_BackgroundPanel == null) return;
        Vector2 anchor;
        Vector2 pivot;
        Vector2 offset;
        switch (anchorCorner)
        {
            case Corner.TopLeft:
                anchor = new Vector2(0, 1); pivot = new Vector2(0, 1); offset = new Vector2(padding, -padding); break;
            case Corner.BottomLeft:
                anchor = new Vector2(0, 0); pivot = new Vector2(0, 0); offset = new Vector2(padding, padding); break;
            case Corner.BottomRight:
                anchor = new Vector2(1, 0); pivot = new Vector2(1, 0); offset = new Vector2(-padding, padding); break;
            default: // TopRight
                anchor = new Vector2(1, 1); pivot = new Vector2(1, 1); offset = new Vector2(-padding, -padding); break;
        }
        m_BackgroundPanel.anchorMin = anchor;
        m_BackgroundPanel.anchorMax = anchor;
        m_BackgroundPanel.pivot = pivot;
        m_BackgroundPanel.anchoredPosition = offset;
    }

    private static Color AdjustAlpha(Color c, float a) { c.a = a; return c; }

    #endregion

    #region Performance & Formatting

    private void UpdatePerformanceStats()
    {
        m_FrameCount++;
        m_DeltaTimeAccumulator += Time.unscaledDeltaTime;

        if (Time.realtimeSinceStartup - m_LastUpdateTime >= m_FpsUpdateInterval)
        {
            m_CurrentFps = m_FrameCount / m_DeltaTimeAccumulator;
            m_FrameCount = 0;
            m_DeltaTimeAccumulator = 0f;
            m_LastUpdateTime = Time.realtimeSinceStartup;
        }
    }

    private string ApplyColorCoding(string text)
    {
        if (!useColorCoding || string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Apply color tags for common patterns
        m_StringBuilder.Clear();
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                m_StringBuilder.AppendLine();
                continue;
            }

            string processedLine = line;

            // Priority order: Most specific patterns first
            
            // Color anchor switch events (highest priority)
            if (line.Contains("★★★") || line.Contains("ANCHOR SWITCHED"))
            {
                processedLine = WrapWithColor(line, new Color(0.4f, 0.8f, 1f, 1f)); // Cyan for anchor switch
            }
            // Color localization complete (green success)
            else if (line.Contains("✓") || line.Contains("LOCALIZATION COMPLETE"))
            {
                processedLine = WrapWithColor(line, highlightColor); // Green
            }
            // Color deactivation events (yellow warning)
            else if (line.Contains("Deactivated") && line.Contains("starting targets"))
            {
                processedLine = WrapWithColor(line, warningColor); // Yellow
            }
            // Color priority +200 boost (gold)
            else if (line.Contains("Priority=") && (line.Contains("+200") || line.Contains("250")))
            {
                processedLine = WrapWithColor(line, new Color(1f, 0.84f, 0f, 1f)); // Gold
            }
            // Color priority +50 boost (light blue)
            else if (line.Contains("Priority=") && line.Contains("+50"))
            {
                processedLine = WrapWithColor(line, new Color(0.5f, 0.9f, 1f, 1f)); // Light blue
            }
            // Color directly connected indicator
            else if (line.Contains("directlyConnected=True"))
            {
                processedLine = WrapWithColor(line, new Color(1f, 0.84f, 0f, 1f)); // Gold
            }
            // Color approaching indicator
            else if (line.Contains("approaching=True"))
            {
                processedLine = WrapWithColor(line, new Color(0.5f, 0.9f, 1f, 1f)); // Light blue
            }
            // Color INSIDE boundary (green)
            else if (line.Contains("INSIDE") || line.Contains("(in)"))
            {
                processedLine = WrapWithColor(line, highlightColor); // Green
            }
            // Color outside boundary (dimmed gray)
            else if (line.Contains("outside"))
            {
                processedLine = WrapWithColor(line, new Color(0.6f, 0.6f, 0.6f, 1f)); // Gray
            }
            // Color anchored targets (contains [A])
            else if (line.Contains("[A]"))
            {
                processedLine = WrapWithColor(line, new Color(0.4f, 0.8f, 1f, 1f)); // Cyan
            }
            // Color tracking targets (contains [T])
            else if (line.Contains("[T]"))
            {
                processedLine = WrapWithColor(line, warningColor); // Yellow
            }
            // Color Room/Hallway type indicators
            else if (line.Contains("| Room") || line.Contains("| Hallway"))
            {
                processedLine = WrapWithColor(line, new Color(0.8f, 0.8f, 1f, 1f)); // Light purple
            }

            m_StringBuilder.AppendLine(processedLine);
        }

        return m_StringBuilder.ToString().TrimEnd();
    }

    private string WrapWithColor(string text, Color color)
    {
        string hexColor = ColorUtility.ToHtmlStringRGB(color);
        return $"<color=#{hexColor}>{text}</color>";
    }

    #endregion
}
