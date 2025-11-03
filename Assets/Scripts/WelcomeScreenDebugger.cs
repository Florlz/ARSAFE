using UnityEngine;
using UnityEngine.UI;
using ARSafe.Modular.Welcome;

/// <summary>
/// Simple test script to debug and force show the welcome screen.
/// Add this to any GameObject in your scene for testing.
/// </summary>
public class WelcomeScreenDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private bool resetPlayerPrefsOnStart = false;
    
    [Header("UI Controls (Optional)")]
    [SerializeField] private Button showWelcomeButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button debugStateButton;
    
    private void Start()
    {
        // Setup UI buttons if provided
        if (showWelcomeButton != null)
            showWelcomeButton.onClick.AddListener(ForceShowWelcome);
            
        if (resetButton != null)
            resetButton.onClick.AddListener(ResetAndShow);
            
        if (debugStateButton != null)
            debugStateButton.onClick.AddListener(DebugState);
        
        // Auto actions
        if (resetPlayerPrefsOnStart)
        {
            ResetPlayerPrefs();
        }
        
        if (showOnStart)
        {
            Invoke(nameof(ForceShowWelcome), 1f); // Small delay
        }
    }
    
    [ContextMenu("Force Show Welcome")]
    public void ForceShowWelcome()
    {
        Debug.Log("[WelcomeDebugger] Force showing welcome screen");
        
        var manager = WelcomeScreenManager.Instance;
        if (manager != null)
        {
            Debug.Log("[WelcomeDebugger] WelcomeScreenManager found, calling ShowWelcomeScreen");
            manager.ShowWelcomeScreen(DisasterType.Fire);
        }
        else
        {
            Debug.LogError("[WelcomeDebugger] WelcomeScreenManager.Instance is null!");
        }
    }
    
    [ContextMenu("Reset PlayerPrefs and Show")]
    public void ResetAndShow()
    {
        Debug.Log("[WelcomeDebugger] Resetting PlayerPrefs and showing welcome");
        ResetPlayerPrefs();
        ForceShowWelcome();
    }
    
    [ContextMenu("Debug PlayerPrefs State")]
    public void DebugState()
    {
        Debug.Log("=== WELCOME SCREEN DEBUG STATE ===");
        Debug.Log($"ARSAFE_WelcomeShown: {PlayerPrefs.GetInt("ARSAFE_WelcomeShown", 0)}");
        Debug.Log($"ARSAFE_WelcomeSuppressed: {PlayerPrefs.GetInt("ARSAFE_WelcomeSuppressed", 0)}");
        Debug.Log($"ARSAFE_FirstTimeUser: {PlayerPrefs.GetInt("ARSAFE_FirstTimeUser", 0)}");
        Debug.Log($"HasUserSuppressedWelcome(): {WelcomeScreenManager.HasUserSuppressedWelcome()}");
        Debug.Log($"WelcomeScreenManager.Instance exists: {WelcomeScreenManager.Instance != null}");
        Debug.Log("==================================");
    }
    
    private void ResetPlayerPrefs()
    {
        PlayerPrefs.DeleteKey("ARSAFE_WelcomeShown");
        PlayerPrefs.DeleteKey("ARSAFE_WelcomeSuppressed");
        PlayerPrefs.DeleteKey("ARSAFE_FirstTimeUser");
        PlayerPrefs.Save();
        Debug.Log("[WelcomeDebugger] PlayerPrefs reset");
    }
}