/*
 * ARCHITECTURE PLAN: SelectedLocationManager
 *
 * PURPOSE:
 *   - Persist user's area target location selection between scenes
 *   - Bridge MainMenu scene (where selection happens) and MainScene (where it's applied)
 *   - Provide singleton access pattern with DontDestroyOnLoad
 *
 * DEPENDENCIES:
 *   - Unity APIs: MonoBehaviour, DontDestroyOnLoad
 *   - None (pure data storage)
 *
 * DATA FLOW:
 *   - MainMenu → User selects location → SetSelectedLocation(string)
 *   - MainScene → ARSafeActivationController.Start() → GetSelectedLocation()
 *   - After application → ClearSelectedLocation()
 *
 * PERFORMANCE CONSIDERATIONS:
 *   - Minimal memory footprint (single string)
 *   - No per-frame updates
 *   - Singleton pattern prevents duplicates
 */

using UnityEngine;

/// <summary>
/// Singleton manager that persists the user's selected Area Target location
/// between the MainMenu scene and the MainScene.
/// </summary>
public class SelectedLocationManager : MonoBehaviour
{
    private static SelectedLocationManager instance;

    /// <summary>
    /// Singleton instance access
    /// </summary>
    public static SelectedLocationManager Instance
    {
        get
        {
            if (instance == null)
            {
                // Auto-create instance if it doesn't exist
                GameObject go = new GameObject("SelectedLocationManager");
                instance = go.AddComponent<SelectedLocationManager>();
                DontDestroyOnLoad(go);
                Debug.Log("<color=cyan>[SelectedLocationManager]</color> Auto-created singleton instance");
            }
            return instance;
        }
    }

    // Stored data
    private string selectedLocationName = null;
    private bool hasSelection = false;

    #region Unity Lifecycle

    private void Awake()
    {
        // Enforce singleton pattern
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[SelectedLocationManager] Duplicate instance detected - destroying duplicate");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("<color=cyan>[SelectedLocationManager]</color> Initialized");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Set the selected location name (called from MainMenu scene)
    /// </summary>
    /// <param name="locationName">The GameObject name of the selected Area Target</param>
    public static void SetSelectedLocation(string locationName)
    {
        if (string.IsNullOrEmpty(locationName))
        {
            Debug.LogWarning("[SelectedLocationManager] Cannot set null or empty location name");
            return;
        }

        Instance.selectedLocationName = locationName;
        Instance.hasSelection = true;
        Debug.Log($"<color=cyan>[SelectedLocationManager]</color> Selected location: {locationName}");
    }

    /// <summary>
    /// Get the selected location name (called from MainScene)
    /// </summary>
    /// <returns>Location name, or null if no selection was made</returns>
    public static string GetSelectedLocation()
    {
        if (!Instance.hasSelection)
        {
            Debug.Log("<color=yellow>[SelectedLocationManager]</color> No location selected - using auto-detection");
            return null;
        }

        Debug.Log($"<color=cyan>[SelectedLocationManager]</color> Retrieved selected location: {Instance.selectedLocationName}");
        return Instance.selectedLocationName;
    }

    /// <summary>
    /// Check if a location has been selected
    /// </summary>
    public static bool HasSelectedLocation()
    {
        return Instance.hasSelection;
    }

    /// <summary>
    /// Clear the selected location (e.g., when returning to main menu or after use)
    /// </summary>
    public static void ClearSelectedLocation()
    {
        Instance.selectedLocationName = null;
        Instance.hasSelection = false;
        Debug.Log("<color=cyan>[SelectedLocationManager]</color> Cleared selected location");
    }

    #endregion

    #region Debug Tools

    /// <summary>
    /// Debug context menu to inspect current state
    /// </summary>
    [ContextMenu("Debug: Log Current Selection")]
    private void DebugLogSelection()
    {
        Debug.Log(
            $"<color=cyan>=== SelectedLocationManager State ===</color>\n" +
            $"Has Selection: {hasSelection}\n" +
            $"Selected Location: {(hasSelection ? selectedLocationName : "None")}"
        );
    }

    #endregion
}
