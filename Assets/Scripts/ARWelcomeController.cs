using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using ARSafe.Modular.Welcome;

/// <summary>
/// Shows welcome screen immediately when the main AR scene loads,
/// before AR tracking begins, to guide the user.
/// </summary>
public class ARWelcomeController : MonoBehaviour
{
    [Header("Welcome Timing")]
    [SerializeField] private float delayBeforeWelcome = 0.5f; // Small delay for scene initialization
    
    [Header("AR Components")]
    [SerializeField] private GameObject[] arComponents; // AR objects to disable during welcome
    [SerializeField] private bool disableARDuringWelcome = true;
    
    [Header("Debug")]
    [SerializeField] private bool verbose = true;
    
    private bool welcomeShown = false;

    private void OnEnable()
    {
        welcomeShown = false;
    }
    
    private void Start()
    {
        // Show welcome screen immediately when scene loads
        StartCoroutine(ShowWelcomeAfterDelay());
    }
    
    private IEnumerator ShowWelcomeAfterDelay()
    {
        // Small delay to ensure scene is fully loaded
        yield return new WaitForSeconds(delayBeforeWelcome);

        while (ARLoadingScreenManager.Instance != null && ARLoadingScreenManager.Instance.IsLoading)
        {
            yield return null;
        }

        if (ARLoadingScreenManager.Instance != null && ARLoadingScreenManager.Instance.ManagesWelcomeFlow)
        {
            if (verbose)
                Debug.Log("[ARWelcome] Loading manager is handling welcome flow; skipping duplicate welcome display.");
            yield break;
        }
        
        // Debug PlayerPrefs state
        if (verbose)
        {
            Debug.Log($"[ARWelcome] ARSAFE_WelcomeShown: {PlayerPrefs.GetInt("ARSAFE_WelcomeShown", 0)}");
            Debug.Log($"[ARWelcome] ARSAFE_WelcomeSuppressed: {PlayerPrefs.GetInt("ARSAFE_WelcomeSuppressed", 0)}");
            Debug.Log($"[ARWelcome] ARSAFE_FirstTimeUser: {PlayerPrefs.GetInt("ARSAFE_FirstTimeUser", 0)}");
        }
        
        bool forceShow = WelcomeScreenManager.TryGetInstance(out var manager) && manager.forceShowWelcome;
        bool suppressed = WelcomeScreenManager.HasUserSuppressedWelcome();

        if (verbose)
        {
            Debug.Log($"[ARWelcome] Welcome suppressed: {suppressed}");
        }

        if (!forceShow && suppressed)
        {
            if (verbose)
                Debug.Log("[ARWelcome] User opted out of the welcome screen, enabling AR immediately");

            EnableARTracking();
            yield break;
        }

        if (verbose)
            Debug.Log("[ARWelcome] Showing welcome screen before AR tracking");

        ShowWelcome();
    }
    
    private void ShowWelcome()
    {
        if (welcomeShown) return;
        
        welcomeShown = true;
        
        if (verbose)
            Debug.Log("[ARWelcome] ShowWelcome() called - attempting to show welcome screen");
        
        // Disable AR components during welcome if configured
        if (disableARDuringWelcome)
        {
            SetARComponentsEnabled(false);
        }
        
        // Get current disaster type for context
        DisasterType currentDisaster = DisasterTypeManager.SelectedDisasterType;
        if (verbose)
            Debug.Log($"[ARWelcome] Current disaster type: {currentDisaster}");
        
        // Show welcome screen
        var welcomeManager = WelcomeScreenManager.Instance;
        if (welcomeManager != null)
        {
            if (verbose)
                Debug.Log("[ARWelcome] WelcomeScreenManager instance found, setting up callback and showing screen");
            
            // Set up callback to enable AR after welcome is completed
            UnityAction handler = null;
            handler = () =>
            {
                if (verbose)
                    Debug.Log("[ARWelcome] Welcome completed, enabling AR tracking");

                EnableARTracking();

                welcomeManager.OnWelcomeCompleted -= handler;
            };

            welcomeManager.OnWelcomeCompleted += handler;
            
            welcomeManager.ShowWelcomeScreen(currentDisaster);
            
            if (verbose)
                Debug.Log("[ARWelcome] ShowWelcomeScreen() method called");
        }
        else
        {
            Debug.LogError("[ARWelcome] Could not create WelcomeScreenManager");
            EnableARTracking(); // Fallback to normal AR flow
        }
    }
    
    private void EnableARTracking()
    {
        // Re-enable AR components
        if (disableARDuringWelcome)
        {
            SetARComponentsEnabled(true);
        }
        
        if (verbose)
            Debug.Log("[ARWelcome] AR tracking enabled - user can now track targets");
    }
    
    private void SetARComponentsEnabled(bool enabled)
    {
        if (arComponents == null) return;
        
        foreach (var component in arComponents)
        {
            if (component != null)
            {
                component.SetActive(enabled);
            }
        }
        
        if (verbose)
            Debug.Log($"[ARWelcome] AR components {(enabled ? "enabled" : "disabled")} ({arComponents.Length} components)");
    }
    
    /// <summary>
    /// Force show welcome screen (for testing)
    /// </summary>
    [System.Obsolete("Only use for testing")]
    public void ForceShowWelcome()
    {
        welcomeShown = false;
        ShowWelcome();
    }
    
    /// <summary>
    /// Reset PlayerPrefs and force show welcome screen (for testing)
    /// </summary>
    public void ResetAndShowWelcome()
    {
        if (verbose)
            Debug.Log("[ARWelcome] Resetting PlayerPrefs and forcing welcome screen");
            
        // Reset welcome-related PlayerPrefs
        PlayerPrefs.DeleteKey("ARSAFE_WelcomeShown");
        PlayerPrefs.DeleteKey("ARSAFE_WelcomeSuppressed");
        PlayerPrefs.DeleteKey("ARSAFE_FirstTimeUser");
        PlayerPrefs.Save();
        
        // Reset state and force show
        welcomeShown = false;
        ShowWelcome();
    }
    
    /// <summary>
    /// Check current PlayerPrefs state (for debugging)
    /// </summary>
    [ContextMenu("Debug PlayerPrefs State")]
    public void DebugPlayerPrefsState()
    {
        Debug.Log($"[ARWelcome] ARSAFE_WelcomeShown: {PlayerPrefs.GetInt("ARSAFE_WelcomeShown", 0)}");
        Debug.Log($"[ARWelcome] ARSAFE_WelcomeSuppressed: {PlayerPrefs.GetInt("ARSAFE_WelcomeSuppressed", 0)}");
        Debug.Log($"[ARWelcome] ARSAFE_FirstTimeUser: {PlayerPrefs.GetInt("ARSAFE_FirstTimeUser", 0)}");
        Debug.Log($"[ARWelcome] HasUserSuppressedWelcome(): {WelcomeScreenManager.HasUserSuppressedWelcome()}");
    }
}