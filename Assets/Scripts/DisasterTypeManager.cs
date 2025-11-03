using UnityEngine;

public enum DisasterType
{
    None,
    Fire,
    Earthquake,
    Flood,
    GeneralSafety,
}

public class DisasterTypeManager : MonoBehaviour
{
    public static DisasterType SelectedDisasterType { get; private set; } = DisasterType.None;
    public static event System.Action<DisasterType> OnDisasterTypeChanged;

    // Public static instance property for safer access
    public static DisasterTypeManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton pattern to keep this object alive between scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Cleanup on destroy
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void SetDisasterType(DisasterType disasterType)
    {
        if (SelectedDisasterType != disasterType)
        {
            SelectedDisasterType = disasterType;
            Debug.Log($"[DisasterManager] Selected disaster type: {disasterType}");
            OnDisasterTypeChanged?.Invoke(disasterType);
        }
    }
}
