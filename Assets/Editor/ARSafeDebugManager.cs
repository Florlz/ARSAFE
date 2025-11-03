using UnityEngine;
using UnityEditor;
using ARSafe.Modular;
using ARSafe.Modular.Integration;
using ARSafe.Content;
using ARSafe.UI;
using ARSafe.Performance;

/// <summary>
/// Editor utility to manage debug logging across all ARSafe components
/// Disabling debug logs reduces memory usage and improves performance
/// </summary>
public class ARSafeDebugManager : EditorWindow
{
    [MenuItem("ARSafe/Tools/Debug Manager", priority = 200)]
    public static void ShowWindow()
    {
        GetWindow<ARSafeDebugManager>("ARSafe Debug Manager");
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        EditorGUILayout.LabelField("ARSafe Debug Log Manager", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Disable debug logs to reduce memory usage and improve performance.\n" +
            "Debug logs should only be enabled during development and troubleshooting.",
            MessageType.Info
        );

        GUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Disable ALL Debug Logs", GUILayout.Height(30)))
        {
            DisableAllDebugLogs();
        }
        if (GUILayout.Button("Enable ALL Debug Logs", GUILayout.Height(30)))
        {
            EnableAllDebugLogs();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(20);
        EditorGUILayout.LabelField("Individual Component Controls:", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Core Systems:", EditorStyles.boldLabel);
        DrawDebugToggle<ARSafeActivationController>("ARSafeActivationController", "enableDebugLogs");
        DrawDebugToggle<ARSafeNavigationValidator>("ARSafeNavigationValidator", "enableDebugLogs");
        DrawDebugToggle<ARSafeTrackingManager>("ARSafeTrackingManager", "enableDebugLogs");
        DrawDebugToggle<ARSafeLoadingIntegration>("ARSafeLoadingIntegration", "enableDebugLogs");
        DrawDebugToggle<VirtualExitMarker>("VirtualExitMarker", "enableDebugLogs");
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Content Systems:", EditorStyles.boldLabel);
        DrawDebugToggle<ARSafeProximityDisplay>("ARSafeProximityDisplay", "enableDebugLogs");
        DrawDebugToggle<ARSafeDisasterFilter>("ARSafeDisasterFilter", "enableDebugLogs");
        DrawDebugToggle<ARSafeDebugOverlayIntegration>("ARSafeDebugOverlayIntegration", "enableDebugLogs");
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("UI Systems:", EditorStyles.boldLabel);
        DrawDebugToggle<ARSafeWrongWayWarning>("ARSafeWrongWayWarning", "enableDebugLogs");
        DrawDebugToggle("LocalizationInstructionsController", "enableDebugLogs");
        DrawDebugToggle("RelocalizationPanelController", "enableDebugLogs");
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Performance & Logging:", EditorStyles.boldLabel);
        DrawDebugToggle<ARSafePerformanceMonitor>("ARSafePerformanceMonitor", "enableDebugLogs");

        // ARDebugLogger - special handling for file capture
        var debugLogger = Object.FindFirstObjectByType<ARDebugLogger>();
        if (debugLogger != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ARDebugLogger (File Capture)", GUILayout.Width(250));

            bool captureToFile = debugLogger.captureLogsToFile;
            bool newCaptureValue = EditorGUILayout.Toggle(captureToFile, GUILayout.Width(20));

            if (newCaptureValue != captureToFile)
            {
                Undo.RecordObject(debugLogger, "Toggle ARDebugLogger File Capture");
                debugLogger.captureLogsToFile = newCaptureValue;
                EditorUtility.SetDirty(debugLogger);
            }

            if (GUILayout.Button("View", GUILayout.Width(50)))
            {
                Selection.activeGameObject = debugLogger.gameObject;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(20);
        EditorGUILayout.HelpBox(
            "Performance Impact:\n" +
            "• Debug logs use memory for string allocations\n" +
            "• File logging (ARDebugLogger) has the highest overhead\n" +
            "• Recommendation: Disable all debug logs for production builds and performance testing",
            MessageType.Warning
        );
    }

    private void DrawDebugToggle<T>(string componentName, string fieldName) where T : MonoBehaviour
    {
        var component = Object.FindFirstObjectByType<T>();
        if (component != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(componentName, GUILayout.Width(250));

            var field = typeof(T).GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(bool))
            {
                bool currentValue = (bool)field.GetValue(component);
                bool newValue = EditorGUILayout.Toggle(currentValue, GUILayout.Width(20));

                if (newValue != currentValue)
                {
                    Undo.RecordObject(component, $"Toggle {componentName} Debug Logs");
                    field.SetValue(component, newValue);
                    EditorUtility.SetDirty(component);
                }

                if (GUILayout.Button("View", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = component.gameObject;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Field not found", GUILayout.Width(100));
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // String-based overload for types we can't reference directly
    private void DrawDebugToggle(string typeName, string fieldName)
    {
        var allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        var component = System.Array.Find(allComponents, c => c.GetType().Name == typeName);

        if (component != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(typeName, GUILayout.Width(250));

            var field = component.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(bool))
            {
                bool currentValue = (bool)field.GetValue(component);
                bool newValue = EditorGUILayout.Toggle(currentValue, GUILayout.Width(20));

                if (newValue != currentValue)
                {
                    Undo.RecordObject(component, $"Toggle {typeName} Debug Logs");
                    field.SetValue(component, newValue);
                    EditorUtility.SetDirty(component);
                }

                if (GUILayout.Button("View", GUILayout.Width(50)))
                {
                    Selection.activeGameObject = component.gameObject;
                }
            }
            else
            {
                EditorGUILayout.LabelField("Field not found", GUILayout.Width(100));
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DisableAllDebugLogs()
    {
        int disabledCount = 0;

        // Find all MonoBehaviours with enableDebugLogs field
        var allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        foreach (var component in allComponents)
        {
            var field = component.GetType().GetField("enableDebugLogs",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(bool))
            {
                bool currentValue = (bool)field.GetValue(component);
                if (currentValue) // Only change if it was enabled
                {
                    Undo.RecordObject(component, "Disable Debug Logs");
                    field.SetValue(component, false);
                    EditorUtility.SetDirty(component);
                    disabledCount++;
                }
            }
        }

        // ARDebugLogger - special handling for file capture
        var debugLogger = Object.FindFirstObjectByType<ARDebugLogger>();
        if (debugLogger != null && debugLogger.captureLogsToFile)
        {
            Undo.RecordObject(debugLogger, "Disable Debug File Capture");
            debugLogger.captureLogsToFile = false;
            EditorUtility.SetDirty(debugLogger);
            disabledCount++;
        }

        Debug.Log($"<color=green>[ARSafeDebugManager]</color> ✓ Disabled debug logs on {disabledCount} components");
        Repaint();
    }

    private void EnableAllDebugLogs()
    {
        int enabledCount = 0;

        // Find all MonoBehaviours with enableDebugLogs field
        var allComponents = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        foreach (var component in allComponents)
        {
            var field = component.GetType().GetField("enableDebugLogs",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (field != null && field.FieldType == typeof(bool))
            {
                bool currentValue = (bool)field.GetValue(component);
                if (!currentValue) // Only change if it was disabled
                {
                    Undo.RecordObject(component, "Enable Debug Logs");
                    field.SetValue(component, true);
                    EditorUtility.SetDirty(component);
                    enabledCount++;
                }
            }
        }

        // ARDebugLogger - special handling for file capture (editor only)
        var debugLogger = Object.FindFirstObjectByType<ARDebugLogger>();
        if (debugLogger != null && !debugLogger.captureLogsToFile)
        {
            Undo.RecordObject(debugLogger, "Enable Debug File Capture");
            debugLogger.captureLogsToFile = true;
            EditorUtility.SetDirty(debugLogger);
            enabledCount++;
        }

        Debug.Log($"<color=cyan>[ARSafeDebugManager]</color> ✓ Enabled debug logs on {enabledCount} components");
        Repaint();
    }
}
