using UnityEngine;

/// <summary>
/// Automatically adjusts UI elements to respect device safe areas (notches, system bars)
/// Perfect for Android phones with various screen shapes
/// </summary>
public class SafeAreaAdjuster : MonoBehaviour
{
    [Header("Settings")]
    public bool adjustOnStart = true;
    public bool debugSafeArea = false;
    
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        if (adjustOnStart)
        {
            ApplySafeArea();
        }
    }
    
    void Update()
    {
        // Check if safe area has changed (device rotation, etc.)
        if (Screen.safeArea != lastSafeArea)
        {
            ApplySafeArea();
        }
    }
    
    void ApplySafeArea()
    {
        if (rectTransform == null) return;
        
        Rect safeArea = Screen.safeArea;
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        
        // Convert safe area to anchor coordinates (0-1 range)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= screenSize.x;
        anchorMin.y /= screenSize.y;
        anchorMax.x /= screenSize.x;
        anchorMax.y /= screenSize.y;
        
        // Apply to RectTransform
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        lastSafeArea = safeArea;
        
        if (debugSafeArea)
        {
            Debug.Log($"[SafeArea] Applied safe area: {safeArea} on screen {screenSize}");
            Debug.Log($"[SafeArea] Anchors: min={anchorMin}, max={anchorMax}");
        }
    }
    
    /// <summary>
    /// Manually trigger safe area adjustment
    /// </summary>
    [ContextMenu("Apply Safe Area")]
    public void ApplySafeAreaManually()
    {
        ApplySafeArea();
    }
}