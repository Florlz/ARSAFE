using UnityEngine;
using UnityEditor;
using ARSafe.Content;

namespace ARSafe.ModularEditor
{
    /// <summary>
    /// Custom Inspector for EarthquakeDebrisController
    /// Provides intuitive controls for spawn area, position, and particle sizing
    /// </summary>
    [CustomEditor(typeof(EarthquakeDebrisController))]
    public class EarthquakeDebrisControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty onlyDuringEarthquake;
        private SerializedProperty startDelay;
        private SerializedProperty duration;
        private SerializedProperty initialEmissionRate;
        private SerializedProperty peakEmissionRate;
        private SerializedProperty rampUpTime;
        private SerializedProperty spawnWidth;
        private SerializedProperty spawnLength;
        private SerializedProperty spawnHeight;
        private SerializedProperty impactSounds;
        private SerializedProperty audioSource;
        private SerializedProperty impactSoundChance;
        private SerializedProperty startSizeRange;
        private SerializedProperty startLifetimeRange;
        private SerializedProperty startSpeedRange;
        private SerializedProperty gravityModifierRange;
        private SerializedProperty angularVelocityRange;
        private SerializedProperty horizontalDrift;
        private SerializedProperty noiseStrength;
        private SerializedProperty colorGradient;
        private SerializedProperty use3DRandomSize;
        private SerializedProperty startSize3DMin;
        private SerializedProperty startSize3DMax;
    private SerializedProperty groundConstraintMode;
    private SerializedProperty groundHeightReference;
    private SerializedProperty groundBoundsCollider;
    private SerializedProperty groundRaycastLayers;
    private SerializedProperty groundRaycastMaxDistance;
    private SerializedProperty groundRaycastOriginOffset;
    private SerializedProperty groundLevelPadding;
        private SerializedProperty groundLevel;
        private SerializedProperty enableGroundDust;
        private SerializedProperty groundDustPrefab;
        private SerializedProperty dustEmissionRate;
        private SerializedProperty dustHeightOffset;
    private SerializedProperty matchDustToSpawnArea;
    private SerializedProperty dustAreaOverride;
    private SerializedProperty dustAreaPadding;
    private SerializedProperty dustSizeRange;
    private SerializedProperty dustLifetimeRange;
    private SerializedProperty dustRotationSpeedRange;
    private SerializedProperty dustNoiseStrength;
    private SerializedProperty dustNoiseFrequency;
    private SerializedProperty dustNoiseScrollSpeedRange;
    private SerializedProperty dustColorGradient;

    // Impact Smoke properties
    private SerializedProperty enableImpactSmoke;
    private SerializedProperty impactSmokePrefab;
    private SerializedProperty impactSmokeChance;
    private SerializedProperty impactSmokeScaleRange;
    private SerializedProperty impactSmokeTint;
    private SerializedProperty impactSmokeLifetime;
    private SerializedProperty maxActiveSmokeInstances;
    private SerializedProperty impactSmokeHeightOffset;

        private bool showDebrisSettings = true;
        private bool showIntensitySettings = true;
        private bool showSpawnAreaSettings = true;
        private bool showAudioSettings = false;
        private bool showDustSettings = true;
    private bool showVisualTweaks = false;
    private bool showDustVisuals = false;
    private bool showImpactSmoke = false;
    private bool showGroundConstraint = true;

        private void OnEnable()
        {
            onlyDuringEarthquake = serializedObject.FindProperty("onlyDuringEarthquake");
            startDelay = serializedObject.FindProperty("startDelay");
            duration = serializedObject.FindProperty("duration");
            initialEmissionRate = serializedObject.FindProperty("initialEmissionRate");
            peakEmissionRate = serializedObject.FindProperty("peakEmissionRate");
            rampUpTime = serializedObject.FindProperty("rampUpTime");
            spawnWidth = serializedObject.FindProperty("spawnWidth");
            spawnLength = serializedObject.FindProperty("spawnLength");
            spawnHeight = serializedObject.FindProperty("spawnHeight");
            impactSounds = serializedObject.FindProperty("impactSounds");
            audioSource = serializedObject.FindProperty("audioSource");
            impactSoundChance = serializedObject.FindProperty("impactSoundChance");
            startSizeRange = serializedObject.FindProperty("startSizeRange");
            startLifetimeRange = serializedObject.FindProperty("startLifetimeRange");
            startSpeedRange = serializedObject.FindProperty("startSpeedRange");
            gravityModifierRange = serializedObject.FindProperty("gravityModifierRange");
            angularVelocityRange = serializedObject.FindProperty("angularVelocityRange");
            horizontalDrift = serializedObject.FindProperty("horizontalDrift");
            noiseStrength = serializedObject.FindProperty("noiseStrength");
            colorGradient = serializedObject.FindProperty("colorGradient");
            use3DRandomSize = serializedObject.FindProperty("use3DRandomSize");
            startSize3DMin = serializedObject.FindProperty("startSize3DMin");
            startSize3DMax = serializedObject.FindProperty("startSize3DMax");
            groundConstraintMode = serializedObject.FindProperty("groundConstraintMode");
            groundHeightReference = serializedObject.FindProperty("groundHeightReference");
            groundBoundsCollider = serializedObject.FindProperty("groundBoundsCollider");
            groundRaycastLayers = serializedObject.FindProperty("groundRaycastLayers");
            groundRaycastMaxDistance = serializedObject.FindProperty("groundRaycastMaxDistance");
            groundRaycastOriginOffset = serializedObject.FindProperty("groundRaycastOriginOffset");
            groundLevelPadding = serializedObject.FindProperty("groundLevelPadding");
            groundLevel = serializedObject.FindProperty("groundLevel");
            enableGroundDust = serializedObject.FindProperty("enableGroundDust");
            groundDustPrefab = serializedObject.FindProperty("groundDustPrefab");
            dustEmissionRate = serializedObject.FindProperty("dustEmissionRate");
            dustHeightOffset = serializedObject.FindProperty("dustHeightOffset");
            matchDustToSpawnArea = serializedObject.FindProperty("matchDustToSpawnArea");
            dustAreaOverride = serializedObject.FindProperty("dustAreaOverride");
            dustAreaPadding = serializedObject.FindProperty("dustAreaPadding");
            dustSizeRange = serializedObject.FindProperty("dustSizeRange");
            dustLifetimeRange = serializedObject.FindProperty("dustLifetimeRange");
            dustRotationSpeedRange = serializedObject.FindProperty("dustRotationSpeedRange");
            dustNoiseStrength = serializedObject.FindProperty("dustNoiseStrength");
            dustNoiseFrequency = serializedObject.FindProperty("dustNoiseFrequency");
            dustNoiseScrollSpeedRange = serializedObject.FindProperty("dustNoiseScrollSpeedRange");
            dustColorGradient = serializedObject.FindProperty("dustColorGradient");

            // Impact Smoke
            enableImpactSmoke = serializedObject.FindProperty("enableImpactSmoke");
            impactSmokePrefab = serializedObject.FindProperty("impactSmokePrefab");
            impactSmokeChance = serializedObject.FindProperty("impactSmokeChance");
            impactSmokeScaleRange = serializedObject.FindProperty("impactSmokeScaleRange");
            impactSmokeTint = serializedObject.FindProperty("impactSmokeTint");
            impactSmokeLifetime = serializedObject.FindProperty("impactSmokeLifetime");
            maxActiveSmokeInstances = serializedObject.FindProperty("maxActiveSmokeInstances");
            impactSmokeHeightOffset = serializedObject.FindProperty("impactSmokeHeightOffset");
        }

        private void OnDisable()
        {
            // Cleanup: Unity UI Toolkit ListView bindings can hold stale SerializedProperty references
            // Explicitly nullify to prevent "SerializedObject destroyed" errors during recompilation
        }

        public override void OnInspectorGUI()
        {
            // Guard against destroyed SerializedObject (prevents Unity Editor crashes during script recompilation)
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }
            
            serializedObject.Update();

            EarthquakeDebrisController controller = (EarthquakeDebrisController)target;
            ParticleSystem ps = controller.GetComponent<ParticleSystem>();

            // Title
            EditorGUILayout.Space(5);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 14;
            titleStyle.normal.textColor = new Color(0.85f, 0.55f, 0.2f);
            EditorGUILayout.LabelField("🪨 Earthquake Debris Controller", titleStyle);
            EditorGUILayout.Space(5);

            // Quick Actions
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Spawn Area Changes", GUILayout.Height(30)))
            {
                ApplySpawnAreaToParticleSystem(controller, ps);
                EditorUtility.SetDirty(ps);
            }
            if (GUILayout.Button("Reset to Defaults", GUILayout.Height(30)))
            {
                ResetToDefaults(controller);
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Use the controls below to adjust spawn area, position, and particle sizes.\n" +
                "Click 'Apply Spawn Area Changes' to update the particle system.",
                MessageType.Info
            );
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            // Basic Settings
            showDebrisSettings = EditorGUILayout.Foldout(showDebrisSettings, "Debris Settings", true, EditorStyles.foldoutHeader);
            if (showDebrisSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(onlyDuringEarthquake, new GUIContent("Only During Earthquake", "Spawn debris only during earthquake disaster"));
                EditorGUILayout.PropertyField(startDelay, new GUIContent("Start Delay (s)", "Delay before debris starts falling"));
                EditorGUILayout.PropertyField(duration, new GUIContent("Duration (s)", "How long debris continues falling (0 = infinite)"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Intensity Settings
            showIntensitySettings = EditorGUILayout.Foldout(showIntensitySettings, "Intensity Settings", true, EditorStyles.foldoutHeader);
            if (showIntensitySettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(initialEmissionRate, new GUIContent("Initial Emission Rate", "Starting particles/second"));
                EditorGUILayout.PropertyField(peakEmissionRate, new GUIContent("Peak Emission Rate", "Maximum particles/second"));
                EditorGUILayout.PropertyField(rampUpTime, new GUIContent("Ramp Up Time (s)", "Time to reach peak intensity"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Spawn Area Settings (THE KEY FIX)
            showSpawnAreaSettings = EditorGUILayout.Foldout(showSpawnAreaSettings, "📍 Spawn Area & Position", true, EditorStyles.foldoutHeader);
            if (showSpawnAreaSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Spawn Width/Length: Size of the ceiling area where debris spawns\n" +
                    "Spawn Height: How high above the GameObject origin debris spawns\n" +
                    "Move the GameObject itself to position the debris zone!",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(3);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Spawn Area Size", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(spawnWidth, new GUIContent("Width (X axis)", "Width of debris spawn area"));
                EditorGUILayout.PropertyField(spawnLength, new GUIContent("Length (Z axis)", "Length of debris spawn area"));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Spawn Height", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(spawnHeight, new GUIContent("Height Above Origin", "Height above GameObject where debris spawns"));
                EditorGUILayout.EndVertical();
                
                // Visual preview info
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Current Configuration:", EditorStyles.boldLabel);
                Vector3 worldPos = controller.transform.position;
                EditorGUILayout.LabelField($"GameObject Position: ({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})");
                EditorGUILayout.LabelField($"Spawn Box Center: ({worldPos.x:F2}, {worldPos.y + spawnHeight.floatValue:F2}, {worldPos.z:F2})");
                EditorGUILayout.LabelField($"Spawn Box Size: {spawnWidth.floatValue:F2}m × 0.2m × {spawnLength.floatValue:F2}m");
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Audio Settings
            showAudioSettings = EditorGUILayout.Foldout(showAudioSettings, "Audio (Optional)", true, EditorStyles.foldoutHeader);
            if (showAudioSettings)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(impactSounds, new GUIContent("Impact Sounds", "Sound effects for debris impacts"), true);
                EditorGUILayout.PropertyField(audioSource, new GUIContent("Audio Source", "Audio source for impact sounds"));
                EditorGUILayout.PropertyField(impactSoundChance, new GUIContent("Impact Sound Chance", "Probability of playing sound on impact (0-1)"));
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Ground Level & Dust Effects
            showGroundConstraint = EditorGUILayout.Foldout(showGroundConstraint, "🧭 Ground Constraint", true, EditorStyles.foldoutHeader);
            if (showGroundConstraint)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(groundConstraintMode, new GUIContent("Mode", "How the ground height is sourced"));
                var constraintMode = (EarthquakeDebrisController.GroundConstraintMode)groundConstraintMode.enumValueIndex;

                switch (constraintMode)
                {
                    case EarthquakeDebrisController.GroundConstraintMode.UseTransform:
                        EditorGUILayout.PropertyField(groundHeightReference, new GUIContent("Ground Transform", "Transform whose Y level defines the ground"));
                        break;
                    case EarthquakeDebrisController.GroundConstraintMode.UseColliderBounds:
                        EditorGUILayout.PropertyField(groundBoundsCollider, new GUIContent("Ground Collider", "Collider whose top surface defines the ground"));
                        break;
                    case EarthquakeDebrisController.GroundConstraintMode.RaycastDown:
                        EditorGUILayout.PropertyField(groundRaycastLayers, new GUIContent("Raycast Layers", "Layers considered ground"));
                        EditorGUILayout.PropertyField(groundRaycastMaxDistance, new GUIContent("Raycast Distance", "Maximum raycast length (meters)"));
                        EditorGUILayout.PropertyField(groundRaycastOriginOffset, new GUIContent("Raycast Offset", "Local origin offset for the ground ray"));
                        break;
                }

                EditorGUILayout.PropertyField(groundLevelPadding, new GUIContent("Ground Padding", "Additional offset added to resolved ground height"));
                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(constraintMode != EarthquakeDebrisController.GroundConstraintMode.ManualHeight))
                {
                    EditorGUILayout.PropertyField(groundLevel, new GUIContent("Manual Ground Height", "Y coordinate where debris stops"));
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            showDustSettings = EditorGUILayout.Foldout(showDustSettings, "💨 Dust Emission", true, EditorStyles.foldoutHeader);
            if (showDustSettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Debris automatically stops at Ground Level (no collision needed).\n" +
                    "Continuous dust layer emits at ground during earthquake.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(3);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Dust Configuration", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(enableGroundDust, new GUIContent("Enable Ground Dust", "Spawn continuous dust layer at ground level"));
                
                if (enableGroundDust.boolValue)
                {
                    EditorGUILayout.PropertyField(groundDustPrefab, new GUIContent("Ground Dust Prefab", "Particle system prefab for ground dust layer"));
                    
                    if (groundDustPrefab.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            "No dust prefab assigned!\n\n" +
                            "Use: ARSafe → Earthquake Debris → Create Dust Particle Prefab\n" +
                            "Then assign GroundDust prefab here.",
                            MessageType.Warning
                        );
                    }
                    
                    EditorGUILayout.PropertyField(dustEmissionRate, new GUIContent("Emission Rate", "Particles per second (scales with intensity)"));
                    EditorGUILayout.PropertyField(dustHeightOffset, new GUIContent("Height Offset", "Height above ground for dust"));
                    EditorGUILayout.PropertyField(matchDustToSpawnArea, new GUIContent("Match Spawn Footprint", "Size dust layer using the debris spawn box"));
                    if (!matchDustToSpawnArea.boolValue)
                    {
                        EditorGUILayout.PropertyField(dustAreaOverride, new GUIContent("Dust Area Override", "Optional box collider defining dust footprint"));
                    }
                    EditorGUILayout.PropertyField(dustAreaPadding, new GUIContent("Dust Area Padding", "Extra padding applied on X/Z (meters)"));
                }
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Visual Tweaks
            showVisualTweaks = EditorGUILayout.Foldout(showVisualTweaks, "Visual Tweaks (Advanced)", true, EditorStyles.foldoutHeader);
            if (showVisualTweaks)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Particle Size", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(use3DRandomSize, new GUIContent("Use 3D Random Size", "Enable non-uniform scaling per axis"));
                
                if (use3DRandomSize.boolValue)
                {
                    EditorGUILayout.PropertyField(startSize3DMin, new GUIContent("Min Size (X, Y, Z)", "Minimum size per axis"));
                    EditorGUILayout.PropertyField(startSize3DMax, new GUIContent("Max Size (X, Y, Z)", "Maximum size per axis"));
                }
                else
                {
                    EditorGUILayout.PropertyField(startSizeRange, new GUIContent("Size Range", "Min/Max uniform size"));
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(startLifetimeRange, new GUIContent("Lifetime Range (s)", "How long particles exist"));
                EditorGUILayout.PropertyField(startSpeedRange, new GUIContent("Start Speed (m/s)", "Initial downward velocity"));
                EditorGUILayout.PropertyField(gravityModifierRange, new GUIContent("Gravity Modifier", "Multiplier for gravity force"));
                EditorGUILayout.PropertyField(angularVelocityRange, new GUIContent("Angular Velocity (deg/s)", "Rotation speed for tumbling"));
                EditorGUILayout.PropertyField(horizontalDrift, new GUIContent("Horizontal Drift", "Random sideways motion"));
                EditorGUILayout.PropertyField(noiseStrength, new GUIContent("Noise Strength", "Perlin noise wobble strength"));
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space(3);
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(colorGradient, new GUIContent("Color Gradient", "Color over particle lifetime"));
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Dust Visuals
            showDustVisuals = EditorGUILayout.Foldout(showDustVisuals, "Dust Visuals", true, EditorStyles.foldoutHeader);
            if (showDustVisuals)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(dustSizeRange, new GUIContent("Size Range", "Randomized scale for dust particles"));
                EditorGUILayout.PropertyField(dustLifetimeRange, new GUIContent("Lifetime Range", "How long dust lingers"));
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Motion", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(dustRotationSpeedRange, new GUIContent("Rotation Speed", "Swirl speed in degrees/sec"));
                EditorGUILayout.PropertyField(dustNoiseStrength, new GUIContent("Noise Strength", "Turbulence strength"));
                EditorGUILayout.PropertyField(dustNoiseFrequency, new GUIContent("Noise Frequency", "Noise sampling frequency"));
                EditorGUILayout.PropertyField(dustNoiseScrollSpeedRange, new GUIContent("Noise Scroll Speed", "Animation speed range for turbulence"));
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space(3);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(dustColorGradient, new GUIContent("Dust Gradient", "Tint over dust lifetime"));
                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            // Impact Smoke Effects
            showImpactSmoke = EditorGUILayout.Foldout(showImpactSmoke, "💨 Impact Smoke Effects", true, EditorStyles.foldoutHeader);
            if (showImpactSmoke)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.PropertyField(enableImpactSmoke, new GUIContent("Enable Impact Smoke", "Spawn smoke puffs when debris hits ground"));
                
                if (enableImpactSmoke.boolValue)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.PropertyField(impactSmokePrefab, new GUIContent("Dust Prefab", "Dust/smoke effect to spawn (Unity Dust Storm, Luke Peek smoke, or custom)"));
                    
                    if (impactSmokePrefab.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox(
                            "⚠️ Assign a dust/smoke particle prefab (Unity Technologies Dust Storm recommended) to enable impact dust spawning.",
                            MessageType.Warning
                        );
                    }
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
                    EditorGUILayout.Slider(impactSmokeChance, 0f, 1f, new GUIContent("Spawn Chance", "Probability of spawning dust on impact (0-1)"));
                    EditorGUILayout.PropertyField(maxActiveSmokeInstances, new GUIContent("Max Active Instances", "Limit active dust puffs for performance"));
                    EditorGUILayout.PropertyField(impactSmokeHeightOffset, new GUIContent("Height Offset", "Vertical offset above ground level"));
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Visual Settings", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(impactSmokeScaleRange, new GUIContent("Scale Range", "Random size multiplier for spawned dust"));
                    EditorGUILayout.PropertyField(impactSmokeTint, new GUIContent("Dust Tint", "Color multiplied with prefab's material (natural dust tan RGB(180,165,145) recommended for Dust Storm)"));
                    EditorGUILayout.PropertyField(impactSmokeLifetime, new GUIContent("Auto-Destroy Time", "Seconds before cleanup (0 = use prefab's own lifetime)"));
                    EditorGUILayout.EndVertical();
                    
                    EditorGUILayout.Space(3);
                    EditorGUILayout.HelpBox(
                        "💡 TIP: Use Unity Technologies 'Dust Storm' prefab with RGB(180,165,145) tint for natural debris dust.\n" +
                        "Alternative: Luke Peek's 'Thick Blue Smoke' with gray-brown tint.\n" +
                        "Lower Spawn Chance (0.2-0.35) for better mobile performance.",
                        MessageType.Info
                    );
                }
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }

            serializedObject.ApplyModifiedProperties();

            // Scene View Gizmo reminder
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "💡 TIP: A wire box gizmo shows the spawn area in the Scene view.\n" +
                "Move the GameObject to reposition the entire debris zone!",
                MessageType.Info
            );
        }

        private void ApplySpawnAreaToParticleSystem(EarthquakeDebrisController controller, ParticleSystem ps)
        {
            if (ps == null)
            {
                Debug.LogError("No ParticleSystem found on EarthquakeDebrisController!");
                return;
            }

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spawnWidth.floatValue, 0.1f, spawnLength.floatValue);
            shape.position = new Vector3(0, spawnHeight.floatValue, 0);
            shape.randomDirectionAmount = 0.25f;

            Debug.Log($"<color=green>✓</color> Updated spawn area: {spawnWidth.floatValue}m × {spawnLength.floatValue}m at height {spawnHeight.floatValue}m");
            EditorUtility.SetDirty(ps);
        }

        private void ResetToDefaults(EarthquakeDebrisController controller)
        {
            if (EditorUtility.DisplayDialog(
                "Reset to Defaults?",
                "This will reset all debris settings to default values.\n\nContinue?",
                "Reset",
                "Cancel"))
            {
                spawnWidth.floatValue = 5f;
                spawnLength.floatValue = 5f;
                spawnHeight.floatValue = 4f;
                initialEmissionRate.floatValue = 5f;
                peakEmissionRate.floatValue = 20f;
                rampUpTime.floatValue = 5f;
                startDelay.floatValue = 1f;
                duration.floatValue = 15f;
                
                serializedObject.ApplyModifiedProperties();
                
                ParticleSystem ps = controller.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ApplySpawnAreaToParticleSystem(controller, ps);
                }
                
                Debug.Log("<color=green>✓</color> Debris settings reset to defaults");
            }
        }

        // Draw spawn area gizmo in Scene view
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawSpawnAreaGizmo(EarthquakeDebrisController controller, GizmoType gizmoType)
        {
            if (controller == null) return;

            var type = typeof(EarthquakeDebrisController);
            var spawnWidthField = type.GetField("spawnWidth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawnLengthField = type.GetField("spawnLength", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var spawnHeightField = type.GetField("spawnHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groundLevelField = type.GetField("groundLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dustOffsetField = type.GetField("dustHeightOffset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var matchDustField = type.GetField("matchDustToSpawnArea", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dustOverrideField = type.GetField("dustAreaOverride", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var dustPaddingField = type.GetField("dustAreaPadding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (spawnWidthField == null || spawnLengthField == null || spawnHeightField == null)
            {
                return;
            }

            float width = (float)spawnWidthField.GetValue(controller);
            float length = (float)spawnLengthField.GetValue(controller);
            float height = (float)spawnHeightField.GetValue(controller);
            float groundLevel = groundLevelField != null ? (float)groundLevelField.GetValue(controller) : controller.transform.position.y;
            float dustOffset = dustOffsetField != null ? (float)dustOffsetField.GetValue(controller) : 0f;
            bool matchDust = matchDustField != null && (bool)matchDustField.GetValue(controller);
            Vector2 dustPadding = dustPaddingField != null ? (Vector2)dustPaddingField.GetValue(controller) : Vector2.zero;
            BoxCollider dustOverride = dustOverrideField != null ? (BoxCollider)dustOverrideField.GetValue(controller) : null;

            Vector3 center = controller.transform.position + Vector3.up * height;
            Vector3 size = new Vector3(width, 0.2f, length);

            Gizmos.color = new Color(0.85f, 0.45f, 0.15f, 0.6f);
            Gizmos.matrix = Matrix4x4.TRS(center, controller.transform.rotation, size);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.color = new Color(0.85f, 0.45f, 0.15f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);

            Gizmos.color = new Color(0.85f, 0.45f, 0.15f, 0.4f);
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(controller.transform.position, center);

            Handles.Label(center + Vector3.up * 0.3f, $"Debris Spawn Area\n{width:F1}m × {length:F1}m", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState { textColor = Color.white },
                fontSize = 11
            });

            Vector3 groundCenter = new Vector3(controller.transform.position.x, groundLevel, controller.transform.position.z);
            float radius = Mathf.Max(width, length) * 0.5f;
            Handles.color = new Color(0.2f, 0.75f, 0.35f, 0.45f);
            Handles.DrawWireDisc(groundCenter, Vector3.up, radius);
            Handles.Label(groundCenter + Vector3.up * 0.1f, $"Ground Y = {groundLevel:F2}\nCoverage radius ≈ {radius:F2}m", new GUIStyle
            {
                alignment = TextAnchor.LowerCenter,
                normal = new GUIStyleState { textColor = new Color(0.7f, 1f, 0.7f) },
                fontSize = 10
            });

            Vector3 dustCenter;
            Vector3 dustSize;
            Quaternion dustRotation;

            if (!matchDust && dustOverride != null)
            {
                Bounds dustBounds = dustOverride.bounds;
                dustCenter = new Vector3(dustBounds.center.x, groundLevel + dustOffset, dustBounds.center.z);
                Vector3 scaled = Vector3.Scale(dustOverride.size, dustOverride.transform.lossyScale);
                dustSize = new Vector3(
                    Mathf.Max(0.1f, scaled.x + dustPadding.x * 2f),
                    0.05f,
                    Mathf.Max(0.1f, scaled.z + dustPadding.y * 2f));
                dustRotation = Quaternion.Euler(0f, dustOverride.transform.eulerAngles.y, 0f);
            }
            else
            {
                dustCenter = new Vector3(controller.transform.position.x, groundLevel + dustOffset, controller.transform.position.z);
                dustSize = new Vector3(
                    Mathf.Max(0.1f, width + dustPadding.x * 2f),
                    0.05f,
                    Mathf.Max(0.1f, length + dustPadding.y * 2f));
                dustRotation = Quaternion.Euler(0f, controller.transform.eulerAngles.y, 0f);
            }

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.18f);
            Gizmos.matrix = Matrix4x4.TRS(dustCenter, dustRotation, dustSize);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Handles.Label(dustCenter + Vector3.up * 0.08f, $"Dust Footprint\n{dustSize.x:F1}m × {dustSize.z:F1}m", new GUIStyle
            {
                alignment = TextAnchor.LowerCenter,
                normal = new GUIStyleState { textColor = new Color(0.7f, 0.9f, 1f) },
                fontSize = 10
            });
        }
    }
}
