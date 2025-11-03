using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Captures debug logs from AreaTargetActivationManager and ProximityAndPoseEventHandler
/// and writes them to a text file for easy sharing and analysis.
/// 
/// Usage:
/// 1. Attach this to any GameObject in your scene (or the AreaTargetActivationManager)
/// 2. Set captureLogsToFile = true
/// 3. Run your AR scene
/// 4. Logs will be written to: Documents/ARSAFE_Logs/ARDebug_[timestamp].txt
/// 5. Right-click in Hierarchy and select "AR Debug Logger > Open Log Folder" to find the file
/// </summary>
public class ARDebugLogger : MonoBehaviour
{
    [Header("Log Capture Settings")]
    [Tooltip("Enable to capture logs to file")]
    public bool captureLogsToFile = true;

    [Tooltip("Only capture logs containing these keywords (leave empty to capture all)")]
    public string[] filterKeywords = new string[]
    {
        // UI: Localization Instructions dropdown
        "[LocalizationInstructions]",
        // UI: Settings
        "[SettingsPanelController]",
        "[ARSafeSettings]",

        // Modular system components (all debug output from these)
        "[ARSafeActivationController]",
        "[ARSafeTrackingManager]",
        "[ARSafeProximityDisplay]",
        "[ARSafeDisasterFilter]",
        "[ARSafeDebugHelper]",
        "[ARSafeLoadingIntegration]",
        "[ARSafeDebugOverlayIntegration]",
        "[ARSafeTargetInfo]",
        "[EarthquakeCrack]",
        "EarthquakeCrack",
    // Earthquake crack billboard controller (PNG sprite-based)
    "[EarthquakeCrackBillboard]",
    "EarthquakeCrackBillboard",
    // Earthquake crack quad controller (3D quad + material-based)
    "[EarthquakeCrackQuad]",
    "EarthquakeCrackQuad",
        "DecalProjector",
        "NOW VISIBLE",
    "FORCE ALWAYS VISIBLE",
    "Validate Decal Setup",
        
        // Flood water system (position-based animation with GPU shader waves)
        "[FloodWater]",
        "FloodWater",
        "FLOOD WATER",
        "Flood water",
        "Rising to",
        "Receding to",
        "Arrows shown",
        "Arrows hidden",
        "Flood scenario",
        "Knee level reached",
        "Water position updated",
        "Flood complete",

        // Location selection system (MainMenu and persistence)
        "[SelectedLocationManager]",
        "[MainMenuLocation]",
        "[LocationSelection]",
        "Selected location",
        "SELECTED LOCATION",
        "Preselected location",
        "PRESELECTED LOCATION",
        "Applying preselected location",
        "APPLYING PRESELECTED LOCATION",
    "MultiArea pose source",
    "pose source lost tracking",
    "Maintaining visibility while tracking",
    "Maintaining room visibility",

        // Performance monitoring system (ARSafePerformanceMonitor)
        "[ARSafePerformanceMonitor]",
        "Performance score:",
        "FPS score:",
        "Memory score:",
        "GC score:",
        "Overall score:",
        "Performance exported:",
        "CSV export",
        
        // Anchor switching events (detailed logging)
        "★★★ ANCHOR SWITCHED ★★★",
        "ANCHOR SWITCHED",
        "★★★ BOUNDARY-BASED ANCHOR SWITCH",
        "BOUNDARY-BASED ANCHOR SWITCH",
        "★★★ ANCHOR SWITCH APPROVED",
        "ANCHOR SWITCH APPROVED",
        "ANCHOR SWITCH BLOCKED",
        "★★★",
        "Switching anchor from",
        "Switching anchor:",
        "Old anchor:",
        "New anchor:",
        "Anchor remains:",
        "Anchor unchanged:",
        "Distance to anchor",
        "Cooldown blocks anchor switch",
        "Anchor switch blocked by cooldown",
        "Bypassing cooldown and distance checks",
        
        // Anchor switching analysis
        "=== ANCHOR SWITCHING ANALYSIS ===",
        "Current:",
        "Candidate:",
        "Switch:",
        "Reason:",
        "Switch threshold:",
        
        // Localization events
        "✓ LOCALIZATION COMPLETE",
        "LOCALIZATION COMPLETE",
        "✓ LOCALIZATION CONFIRMED",
        "LOCALIZATION CONFIRMED",
        "LOCALIZATION PENDING",
        "⏳ LOCALIZATION PENDING",
        "✓",
        "First anchor established",
        "Pre-localization anchor search",
        "Post-localization anchor search",
        "Starting targets only",
        "Waiting for tracking confirmation",
        "WAITING FOR CONFIRMATION",
        "SEARCHING FOR TARGET",
        "⏳",
        
        // Priority calculations (for directly connected rooms)
        "Priority=",
        "base=",
        "directlyConnected=True",
        "directlyConnected=False",
        "approaching=True",
        "approaching=False",
        "+200",
        "+50",
        "Effective priority",
        "CONNECTED ROOM (+200)",
        "approaching (+50)",
        
        // Activation changes (targets being enabled/disabled)
        "Activation changes:",
        "enabled:",
        "disabled:",
        "Enabling:",
        "Disabling:",
        "ACTIVATED",
        "DEACTIVATED",
        
        // Tracking state changes
        "TRACKING",
        "Tracking count:",
        "Active tracked count:",
        "maxSimultaneousTracking",
        "Observer activated",
        "Observer deactivated",
        "[T]",
        "Tracking:",
        "[TRACKING]",
        "not tracking",
        "lost tracking",
        "tracking yet",
        "establish tracking",
        "TRACKING PRIORITY",
        "tracking) replaces",
        "not tracking)",
        
        // Tracking grace period (2.5-second block after anchor switches)
        "[GRACE PERIOD]",
        "GRACE PERIOD",
        "Blocking all anchor switches",
        "grace period",
        "remaining for",
        "to establish tracking",

        // Hysteresis (prevents ping-pong between overlapping targets)
        "[HYSTERESIS]",
        "HYSTERESIS",
        "Skipping neighbor",
        "inside both targets",
        "needs",
        "m advantage",
        "has sufficient advantage",
        "depth advantage",
        "anchorSwitchHysteresis",
        
        // Boundary-aware tracking loss (NEW - prevents switching to farther targets)
        "BLOCKED switch to",
        "BLOCKED switch -",
        "boundary priority > tracking",
        "proximity priority > tracking",  // NEW: Proximity hysteresis
        "NEAR boundary",
        "FAR OUTSIDE",
        "only",
        "m outside)",
        "Both INSIDE, neither tracking",
        "candidate not significantly",
        "deeper inside",
        "need 2m deeper",
        "m deep)",
        
        // Distance-based tracking loss protection (NEW - prevents deadlock from far-away switches)
        "[TRACKING LOSS]",
        "BLOCKED switch from",
        "m FARTHER",
        "would create deadlock",
        "Staying on",
        "Allowing switch to",
        "within",
        "m tolerance",
        "current area gets disabled",
        "waiting for nearby targets",
        "improvementThreshold",
        "staying inside current area",
        "user still INSIDE boundary",
        "prioritizing current anchor",
        "user moved into neighbor",
        "natural progression",
        "user hasn't moved into neighbor",
        
        // Content visibility (prevents old content showing)
        "[ARSafeProximityDisplay]",
        "Not current anchor or neighbor",
        "current anchor:",
        "hiding:",
        "showing:",
        "user INSIDE",
        "Candidate",
        "is",
        "m deeper inside - allowing switch",
        
        // Boundary awareness and detection
        "INSIDE",
        "inside",
        "outside",
        "Boundary:",
        "Distance:",
        "DistanceToBoundary",
        "User INSIDE",
        "INSIDE BOUNDARY",
        "outside boundary",
        "boundary distance",
        "ComputeDistanceToBoundary",
        
        // Neighbor selection and adjacency
        "=== NEIGHBOR SELECTION FOR",
        "NEIGHBOR SELECTION",
        "Adjacent targets:",
        "Adding adjacent target:",
        "Including neighbors of anchor:",
        "connectedRooms",
        "AdjacentTargets:",
        "Connected Rooms:",
        "Manual Adjacent Targets:",
        "Total Adjacency Candidates:",
        "Adjacency candidates",
        "Adjacent:",
        "Neighbor",
        "✓ SELECTED FROM ADJACENCY",
        "SELECTED FROM ADJACENCY",
        "⚠️ DISTANCE FALLBACK",
        "DISTANCE FALLBACK",
        "Added distance-based target:",
        "NOT ADJACENT",
        
        // Target state markers
        "Anchor:",
        "★",
        "[A]",
        "[T]",
        "[E]",
        "[D]",
        "isAnchor",
        "isTracking",
        "isEnabled",
        
        // Best anchor determination
        "Best anchor:",
        "Best anchor determined:",
        "Candidate anchor:",
        "Evaluating anchor:",
        "No suitable anchor found",
        "No anchor candidate found",
        "Deactivating starting targets",
        
        // Boundary-based selection priority
        "BOUNDARY PRIORITY",
        "inside-bonus applied",
        "inside bonus",
        "effective distance",
        "replaces",
        
        // Debug overlay target list
        "● TRACKING",
        "● ENABLED", 
        "● DISABLED",
        "Total:",
        
        // MultiArea pose updates
        "MultiArea pose updated",
        "Group root pose",

        // Augmentation reparenting (dynamic anchor + neighbor attachment)
        "[AUGMENTATION ROOT]",
        "AUGMENTATION ROOT",
        "Attached",
        "augmentations to shared root",
        "Restored",
        "augmentations to original parent",
        "Notified",
        "of augmentations root reparenting",
        "Augmentations root set to:",
        "Auto-collected",
        "items on",

        // Relocalization system
        "[RELOCALIZATION]",
        "RELOCALIZATION",
        "Relocalizing",
        "User requested relocalization",
        "User selected:",
        "Relocalize to",
        "anchor history",
        "FORCE HIDE",
        "ForceHide",

        // General debugging
        "DEBUG:",
        "WARNING:",
        "ERROR:",

        // Boundary computation debugging
        "BoundaryDebug"
    };

    [Tooltip("Maximum number of log entries to keep in memory (prevents memory overflow)")]
    public int maxLogEntries = 5000;

    [Tooltip("Auto-flush logs to disk every N seconds (0 = only flush on quit)")]
    public float autoFlushInterval = 10f;

    [Header("Output Settings")]
    [Tooltip("Custom log file path (leave empty to use default Documents folder)")]
    public string customLogPath = "";

    [Tooltip("Include Unity's built-in logs (Warning, Error, Assert, Exception)")]
    public bool includeUnitySystemLogs = true;

    [Tooltip("Include timestamp for each log entry")]
    public bool includeTimestamps = true;

    [Tooltip("Include frame count for each log entry")]
    public bool includeFrameCount = false;

    [Header("Runtime Info")]
    [SerializeField] private string currentLogFilePath = "";
    [SerializeField] private int capturedLogCount = 0;
    [SerializeField] private bool isCapturing = false;

    private List<string> logBuffer = new List<string>();
    private string logFileName;
    private float timeSinceLastFlush = 0f;
    private object lockObject = new object();

    private void OnEnable()
    {
        // CRITICAL: Disable file logging ONLY on Android devices (not in editor)
        #if UNITY_ANDROID && !UNITY_EDITOR
        if (captureLogsToFile)
        {
            Debug.Log("<color=yellow>[ARDebugLogger] File logging disabled on Android device to improve performance</color>");
            captureLogsToFile = false;
            isCapturing = false;
            return;
        }
        #endif

        // Enable file logging on PC, Editor, and all non-Android platforms
        if (captureLogsToFile)
        {
            StartLogging();
        }
    }

    private void OnDisable()
    {
        StopLogging();
    }

    private void OnApplicationQuit()
    {
        StopLogging();
    }

    private void Update()
    {
        if (!isCapturing || autoFlushInterval <= 0f)
            return;

        timeSinceLastFlush += Time.deltaTime;
        if (timeSinceLastFlush >= autoFlushInterval)
        {
            FlushLogsToFile();
            timeSinceLastFlush = 0f;
        }
    }

    [ContextMenu("Start Logging")]
    public void StartLogging()
    {
        if (isCapturing)
        {
            Debug.LogWarning("[ARDebugLogger] Already capturing logs.");
            return;
        }

        // Generate log file path
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        logFileName = $"ARDebug_{timestamp}.txt";

        if (string.IsNullOrEmpty(customLogPath))
        {
            // On Android/mobile, use Application.persistentDataPath instead of Documents
            string logFolder;
            
            #if UNITY_ANDROID || UNITY_IOS
                // Mobile: Use persistent data path (accessible via file manager or ADB)
                logFolder = Path.Combine(Application.persistentDataPath, "ARSAFE_Logs");
            #else
                // Desktop: Use Documents folder
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                logFolder = Path.Combine(documentsPath, "ARSAFE_Logs");
            #endif
            
            // Create directory if it doesn't exist
            try
            {
                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }
                currentLogFilePath = Path.Combine(logFolder, logFileName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARDebugLogger] Failed to create log directory: {ex.Message}");
                isCapturing = false;
                return;
            }
        }
        else
        {
            currentLogFilePath = Path.Combine(customLogPath, logFileName);
        }

        // Clear buffer and reset counters
        lock (lockObject)
        {
            logBuffer.Clear();
            capturedLogCount = 0;
        }

        // Subscribe to Unity log messages
        Application.logMessageReceived += HandleLog;

        isCapturing = true;

        // Write header to file
        WriteHeaderToFile();

        Debug.Log($"[ARDebugLogger] Started logging to: {currentLogFilePath}");
    }

    [ContextMenu("Stop Logging")]
    public void StopLogging()
    {
        if (!isCapturing)
            return;

        // Unsubscribe from logs
        Application.logMessageReceived -= HandleLog;

        // Flush remaining logs
        FlushLogsToFile();

        isCapturing = false;

        Debug.Log($"[ARDebugLogger] Stopped logging. Total entries captured: {capturedLogCount}");
        Debug.Log($"[ARDebugLogger] Log file: {currentLogFilePath}");
    }

    [ContextMenu("Flush Logs to File")]
    public void FlushLogsToFile()
    {
        if (string.IsNullOrEmpty(currentLogFilePath))
            return;

        lock (lockObject)
        {
            if (logBuffer.Count == 0)
                return;

            try
            {
                // Append all buffered logs to file
                File.AppendAllLines(currentLogFilePath, logBuffer);
                logBuffer.Clear();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARDebugLogger] Failed to write logs to file: {ex.Message}");
            }
        }
    }

    [ContextMenu("Open Log Folder")]
    public void OpenLogFolder()
    {
        if (string.IsNullOrEmpty(currentLogFilePath))
        {
            Debug.LogWarning("[ARDebugLogger] No log file path set yet. Start logging first.");
            return;
        }

        string folder = Path.GetDirectoryName(currentLogFilePath);
        
        if (Directory.Exists(folder))
        {
            #if UNITY_ANDROID
                Debug.Log($"[ARDebugLogger] Android log folder: {folder}");
                Debug.Log("[ARDebugLogger] Use ADB to pull logs: adb pull " + folder.Replace("\\", "/"));
            #elif UNITY_IOS
                Debug.Log($"[ARDebugLogger] iOS log folder: {folder}");
                Debug.Log("[ARDebugLogger] Access via Xcode > Devices > Download Container");
            #else
                // Desktop: Open folder in file explorer
                System.Diagnostics.Process.Start("explorer.exe", folder);
            #endif
        }
        else
        {
            Debug.LogWarning($"[ARDebugLogger] Log folder does not exist: {folder}");
        }
    }

    [ContextMenu("Clear Log Buffer")]
    public void ClearLogBuffer()
    {
        lock (lockObject)
        {
            logBuffer.Clear();
            Debug.Log("[ARDebugLogger] Log buffer cleared.");
        }
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (!isCapturing)
            return;

        // Filter by keywords if specified
        if (filterKeywords != null && filterKeywords.Length > 0)
        {
            bool matchesFilter = false;
            foreach (string keyword in filterKeywords)
            {
                if (!string.IsNullOrEmpty(keyword) && logString.Contains(keyword))
                {
                    matchesFilter = true;
                    break;
                }
            }

            if (!matchesFilter)
            {
                // Also include Unity system logs if enabled
                if (!includeUnitySystemLogs || type == LogType.Log)
                    return;
            }
        }

        // Build log entry
        StringBuilder entry = new StringBuilder();

        // Add timestamp
        if (includeTimestamps)
        {
            entry.Append($"[{DateTime.Now:HH:mm:ss.fff}] ");
        }

        // Add frame count
        if (includeFrameCount)
        {
            entry.Append($"[Frame {Time.frameCount}] ");
        }

        // Add log type for non-normal logs
        if (type != LogType.Log)
        {
            entry.Append($"[{type}] ");
        }

        // Add the actual log message
        entry.Append(logString);

        // Add stack trace for errors/exceptions
        if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
        {
            entry.AppendLine();
            entry.Append("  Stack: ");
            entry.Append(stackTrace.Replace("\n", "\n  "));
        }

        lock (lockObject)
        {
            // Add to buffer
            logBuffer.Add(entry.ToString());
            capturedLogCount++;

            // Prevent memory overflow
            if (logBuffer.Count > maxLogEntries)
            {
                FlushLogsToFile();
            }
        }
    }

    private void WriteHeaderToFile()
    {
        try
        {
            var header = new StringBuilder();
            header.AppendLine("=".PadRight(80, '='));
            header.AppendLine($"ARSAFE AR Activation Debug Log");
            header.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine($"Unity Version: {Application.unityVersion}");
            header.AppendLine($"Platform: {Application.platform}");
            header.AppendLine($"Device: {SystemInfo.deviceModel}");
            header.AppendLine("=".PadRight(80, '='));
            header.AppendLine();
            header.AppendLine("Filtering Keywords: " + (filterKeywords.Length > 0 ? string.Join(", ", filterKeywords) : "None (capturing all)"));
            header.AppendLine();
            header.AppendLine("--- Log Entries ---");
            header.AppendLine();

            File.WriteAllText(currentLogFilePath, header.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ARDebugLogger] Failed to write header: {ex.Message}");
        }
    }

    [ContextMenu("Add Session Marker")]
    public void AddSessionMarker()
    {
        string marker = $"\n{'='.ToString().PadRight(80, '=')}";
        marker += $"\n=== SESSION MARKER: {DateTime.Now:HH:mm:ss} ===";
        marker += $"\n{'='.ToString().PadRight(80, '=')}\n";

        lock (lockObject)
        {
            logBuffer.Add(marker);
        }

        Debug.Log("[ARDebugLogger] Session marker added to log.");
    }

    [ContextMenu("Write Current System State")]
    public void WriteCurrentSystemState()
    {
        // LEGACY: Use ARSafeDebugHelper context menus instead
        Debug.LogWarning("[ARDebugLogger] WriteCurrentSystemState is deprecated. Use ARSafeDebugHelper context menus instead.");
        
        StringBuilder state = new StringBuilder();
        state.AppendLine("\n" + "=".PadRight(80, '='));
        state.AppendLine($"=== SYSTEM STATE SNAPSHOT (LEGACY): {DateTime.Now:HH:mm:ss} ===");
        state.AppendLine("=".PadRight(80, '='));
        state.AppendLine("DEPRECATED: Use ARSafeDebugHelper for current system state.");
        state.AppendLine("=".PadRight(80, '='));
        
        Debug.Log(state.ToString());
    }
}
