using UnityEngine;
using UnityEditor;
using System.Linq;
using ARSafe.Content;

namespace ARSafe.ModularEditor
{
    /// <summary>
    /// Editor tool to upgrade existing EarthquakeDebrisController GameObjects with enhanced visual parameters
    /// Applies all the visual improvements made to the debris system for better mobile AR visuals
    /// </summary>
    public static class EarthquakeDebrisUpgradeTool
    {
        /*
         * PURPOSE:
         *   Apply enhanced visual parameters to existing EarthquakeDebrisController instances
         *
         * ENHANCED PARAMETERS (October 2025 Improvements):
         *   - 60% larger debris chunks (more visible on mobile)
         *   - 28% faster falls (more dramatic)
         *   - 33% more rotation (violent tumbling)
         *   - 50% more scatter (chaotic spread)
         *   - 31% more noise (turbulent motion)
         *
         * FEATURES APPLIED:
         *   - 3D tumbling rotation (X, Y, Z axes)
         *   - Size over lifetime curves (spawn pop-in, impact shrink)
         *   - Color over lifetime (dust tint, alpha fade)
         *   - Velocity damping (realistic deceleration)
         *   - Enhanced lateral drift with bounce
         *   - Multi-octave noise turbulence
         *   - Realistic concrete/rust/dust color palette
         */

        [MenuItem("ARSafe/Tools/Earthquake Debris/Upgrade Existing Debris", priority = 110)]
        public static void UpgradeAllDebrisInScene()
        {
            var allDebris = Object.FindObjectsByType<EarthquakeDebrisController>(FindObjectsSortMode.None);

            if (allDebris == null || allDebris.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Debris Found",
                    "No EarthquakeDebrisController components found in the scene.\n\n" +
                    "Create debris first using:\n" +
                    "ARSafe > Setup > Earthquake Debris > Complete Setup",
                    "OK"
                );
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Upgrade Debris Visuals",
                $"Found {allDebris.Length} EarthquakeDebrisController(s) in scene.\n\n" +
                "This will upgrade them with:\n" +
                "• 60% larger chunks (better mobile visibility)\n" +
                "• 3D tumbling rotation (realistic motion)\n" +
                "• Size/color over lifetime curves\n" +
                "• Velocity damping + bounce effects\n" +
                "• Multi-octave noise turbulence\n" +
                "• Enhanced speed/gravity/drift\n\n" +
                "Existing values will be overwritten.\n" +
                "Undo is available if needed.",
                "Upgrade All",
                "Cancel"
            );

            if (!proceed)
            {
                return;
            }

            int upgraded = 0;
            foreach (var debris in allDebris)
            {
                if (UpgradeDebrisController(debris))
                {
                    upgraded++;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[EarthquakeDebris]</color> ✓ Upgraded {upgraded}/{allDebris.Length} debris controllers with enhanced visuals!");

            EditorUtility.DisplayDialog(
                "Upgrade Complete!",
                $"✓ Successfully upgraded {upgraded} debris controller(s)\n\n" +
                "Changes applied:\n" +
                "• Renderer: Mesh mode (fixes 3D rotation)\n" +
                "• Size range: 0.08m - 0.35m (+60% larger)\n" +
                "• Speed range: 0.5 - 3.2 m/s (+28% faster)\n" +
                "• Rotation speed: ±480°/s (+33% more tumbling)\n" +
                "• Horizontal drift: 1.8 m/s (+50% more scatter)\n" +
                "• Noise strength: 0.85 (+31% more chaos)\n\n" +
                "IMPORTANT: Enter Play Mode to see debris falling!\n" +
                "(Changes apply at runtime)",
                "Awesome!"
            );
        }

        [MenuItem("ARSafe/Tools/Earthquake Debris/Upgrade Selected Only", priority = 111)]
        public static void UpgradeSelectedDebris()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Nothing Selected",
                    "Please select one or more GameObjects with EarthquakeDebrisController components.",
                    "OK"
                );
                return;
            }

            var debrisControllers = selected
                .Select(go => go.GetComponent<EarthquakeDebrisController>())
                .Where(dc => dc != null)
                .ToArray();

            if (debrisControllers.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Debris Found",
                    "Selected GameObjects don't have EarthquakeDebrisController components.\n\n" +
                    "Select GameObjects with debris controllers and try again.",
                    "OK"
                );
                return;
            }

            int upgraded = 0;
            foreach (var debris in debrisControllers)
            {
                if (UpgradeDebrisController(debris))
                {
                    upgraded++;
                }
            }

            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[EarthquakeDebris]</color> ✓ Upgraded {upgraded} selected debris controller(s)!");

            EditorUtility.DisplayDialog(
                "Upgrade Complete!",
                $"✓ Upgraded {upgraded} debris controller(s)\n\n" +
                "Visual improvements applied successfully!",
                "OK"
            );
        }

        private static bool UpgradeDebrisController(EarthquakeDebrisController debris)
        {
            if (debris == null)
            {
                return false;
            }

            // CRITICAL: Fix renderer settings for 3D rotation + assign meshes
            var ps = debris.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    // Must use Mesh mode for 3D rotation
                    renderer.renderMode = ParticleSystemRenderMode.Mesh;
                    renderer.alignment = ParticleSystemRenderSpace.World;
                    renderer.sortMode = ParticleSystemSortMode.Distance;

                    // CRITICAL: Assign debris meshes from GeneratedDebris folder
                    AssignDebrisMeshesToRenderer(renderer);

                    Debug.Log($"<color=yellow>[EarthquakeDebris]</color> Fixed renderer: {debris.gameObject.name} → Mesh mode + meshes assigned");
                }
            }

            // Use SerializedObject to modify inspector values
            SerializedObject so = new SerializedObject(debris);

            // === ENHANCED SIZE PARAMETERS ===
            // 60% larger for better mobile visibility
            SetVector2Property(so, "startSizeRange", new Vector2(0.08f, 0.35f));

            // Enhanced 3D size ranges
            SetVector3Property(so, "startSize3DMin", new Vector3(0.06f, 0.05f, 0.07f));
            SetVector3Property(so, "startSize3DMax", new Vector3(0.38f, 0.28f, 0.42f));

            // === ENHANCED MOTION PARAMETERS ===
            // 28% faster falls for more dramatic effect
            SetVector2Property(so, "startSpeedRange", new Vector2(0.5f, 3.2f));

            // 25% heavier maximum chunks
            SetVector2Property(so, "gravityModifierRange", new Vector2(0.9f, 3.5f));

            // 33% more rotation for violent tumbling
            SetVector2Property(so, "angularVelocityRange", new Vector2(-480f, 480f));

            // === ENHANCED SCATTER PARAMETERS ===
            // 50% more horizontal drift for chaotic scatter
            SetFloatProperty(so, "horizontalDrift", 1.8f);

            // 31% more noise for turbulent motion
            SetFloatProperty(so, "noiseStrength", 0.85f);

            // === ENSURE 3D SIZE IS ENABLED ===
            // Recommended for realistic irregular shapes
            SetBoolProperty(so, "use3DRandomSize", true);

            // Apply all changes
            so.ApplyModifiedProperties();

            // Mark as dirty for saving
            EditorUtility.SetDirty(debris);

            Debug.Log($"<color=cyan>[EarthquakeDebris]</color> Upgraded: {debris.gameObject.name} → Enhanced visuals applied!");

            return true;
        }

        // Helper methods to set serialized properties safely
        private static void SetFloatProperty(SerializedObject so, string propertyName, float value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.floatValue = value;
            }
            else
            {
                Debug.LogWarning($"[EarthquakeDebris] Property '{propertyName}' not found!");
            }
        }

        private static void SetBoolProperty(SerializedObject so, string propertyName, bool value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.boolValue = value;
            }
            else
            {
                Debug.LogWarning($"[EarthquakeDebris] Property '{propertyName}' not found!");
            }
        }

        private static void SetVector2Property(SerializedObject so, string propertyName, Vector2 value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.vector2Value = value;
            }
            else
            {
                Debug.LogWarning($"[EarthquakeDebris] Property '{propertyName}' not found!");
            }
        }

        private static void SetVector3Property(SerializedObject so, string propertyName, Vector3 value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
            {
                prop.vector3Value = value;
            }
            else
            {
                Debug.LogWarning($"[EarthquakeDebris] Property '{propertyName}' not found!");
            }
        }

        private static void AssignDebrisMeshesToRenderer(ParticleSystemRenderer renderer)
        {
            const string DEBRIS_FOLDER = "Assets/ARSafe_ModularSystem/Models/GeneratedDebris";

            // Load all debris meshes from the GeneratedDebris folder
            string[] assetGuids = AssetDatabase.FindAssets("t:Mesh", new[] { DEBRIS_FOLDER });

            if (assetGuids.Length == 0)
            {
                Debug.LogWarning($"<color=yellow>[EarthquakeDebris]</color> No debris meshes found in {DEBRIS_FOLDER}! Please generate meshes first using 'Generate Meshes Only' menu.");
                return;
            }

            // Convert GUIDs to paths and sort by filename for consistent order
            System.Collections.Generic.List<string> assetPaths = new System.Collections.Generic.List<string>();
            for (int i = 0; i < assetGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                assetPaths.Add(path);
            }
            assetPaths.Sort(); // Sort alphabetically (DebrisChunk_1, _2, _3, etc.)

            // Load all meshes in sorted order
            Mesh[] meshes = new Mesh[assetPaths.Count];
            for (int i = 0; i < assetPaths.Count; i++)
            {
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(assetPaths[i]);
                if (meshes[i] != null)
                {
                    Debug.Log($"<color=cyan>[EarthquakeDebris]</color> Loaded mesh {i + 1}/{assetPaths.Count}: {meshes[i].name}");
                }
            }

            // Assign ALL meshes to renderer (Unity will randomly pick from this array for each particle)
            renderer.SetMeshes(meshes);

            // Verify assignment
            int assignedCount = renderer.meshCount;
            Debug.Log($"<color=green>[EarthquakeDebris]</color> ✓ Assigned {assignedCount}/{meshes.Length} debris meshes to renderer (Unity auto-randomizes per particle)");

            if (assignedCount < meshes.Length)
            {
                Debug.LogWarning($"<color=yellow>[EarthquakeDebris]</color> Only {assignedCount} out of {meshes.Length} meshes were assigned! This may be a Unity limitation.");
            }
        }

        // === INFO MENU ITEMS ===

        [MenuItem("ARSafe/Help/Earthquake Debris: What's New?", priority = 200)]
        public static void ShowWhatsNew()
        {
            EditorUtility.DisplayDialog(
                "Enhanced Debris Visuals - What's New",
                "VISUAL IMPROVEMENTS (October 2025):\n\n" +
                "✓ 3D Tumbling Rotation\n" +
                "   • Full X/Y/Z axis rotation\n" +
                "   • Violent, chaotic tumbling like real debris\n\n" +
                "✓ Size Over Lifetime Curves\n" +
                "   • Spawn pop-in (90% → 105%)\n" +
                "   • Impact shrink (100% → 80% for dust effect)\n\n" +
                "✓ Color Over Lifetime\n" +
                "   • Dust tint during fall (white → tan → brown-gray)\n" +
                "   • Alpha fade on impact (100% → 60%)\n\n" +
                "✓ Realistic Physics\n" +
                "   • Velocity damping near ground (20 m/s → 5 m/s)\n" +
                "   • Slight bounce on impact (+0.15 m/s upward)\n\n" +
                "✓ Multi-Octave Noise\n" +
                "   • 2-layer turbulence (large + small wobbles)\n" +
                "   • Per-axis strength control\n\n" +
                "✓ Enhanced Default Values\n" +
                "   • 60% larger chunks (0.08m - 0.35m)\n" +
                "   • 28% faster falls (0.5 - 3.2 m/s)\n" +
                "   • 33% more rotation (±480°/s)\n" +
                "   • 50% more scatter (1.8 m/s drift)\n\n" +
                "Use the upgrade tool to apply these improvements!",
                "Got It!"
            );
        }

        [MenuItem("ARSafe/Tools/Earthquake Debris/Reset to Defaults", priority = 112)]
        public static void ResetToDefaults()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog(
                    "Nothing Selected",
                    "Please select a GameObject with an EarthquakeDebrisController component.",
                    "OK"
                );
                return;
            }

            var debris = selected.GetComponent<EarthquakeDebrisController>();
            if (debris == null)
            {
                EditorUtility.DisplayDialog(
                    "No Debris Controller",
                    "Selected GameObject doesn't have an EarthquakeDebrisController component.",
                    "OK"
                );
                return;
            }

            bool proceed = EditorUtility.DisplayDialog(
                "Reset to Enhanced Defaults",
                "This will reset all visual parameters to the enhanced default values.\n\n" +
                "Current custom values will be lost.\n" +
                "Continue?",
                "Reset",
                "Cancel"
            );

            if (!proceed)
            {
                return;
            }

            if (UpgradeDebrisController(debris))
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=green>[EarthquakeDebris]</color> Reset {debris.gameObject.name} to enhanced defaults!");

                EditorUtility.DisplayDialog(
                    "Reset Complete",
                    "Visual parameters reset to enhanced default values!",
                    "OK"
                );
            }
        }
    }
}
