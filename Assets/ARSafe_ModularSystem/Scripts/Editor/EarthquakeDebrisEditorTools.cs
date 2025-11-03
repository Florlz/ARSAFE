using UnityEngine;
using UnityEditor;
using ARSafe.Content;
using ARSafe.Modular; // For ARSafeDisasterContent and DisasterType tagging

namespace ARSafe.ModularEditor
{
    /// <summary>
    /// Unified editor tools for earthquake debris system setup
    /// Consolidates mesh generation, material creation, and particle system configuration
    /// </summary>
    public static class EarthquakeDebrisEditorTools
    {
        /*
         * ARCHITECTURE PLAN: Debris Mesh Expansion
         * 
         * PURPOSE:
         *   - Provide a richer library of debris chunk meshes and ensure new chunks appear smaller by default.
         * 
         * CHANGES:
         *   - Increase the number of generated debris meshes and adjust generation parameters for smaller, varied chunks.
         *   - Update auto-assignment utilities to work with the larger mesh set.
         *   - Align the particle-system template with the smaller chunk scale for predictable results.
         * 
         * PERFORMANCE NOTES:
         *   - Mesh count increase is editor-only; runtime cost remains unchanged because only references are assigned.
         */
        private const string DEBRIS_FOLDER = "Assets/ARSafe_ModularSystem/Models/GeneratedDebris";
        private const string MATERIAL_PATH = "Assets/ARSafe_ModularSystem/Materials/ConcreteDébris.mat";
        private const int DEBRIS_MESH_COUNT = 12;
        
        #region Main Menu Items
        
        [MenuItem("ARSafe/Setup/Earthquake Debris/Complete Setup (Recommended)", priority = 100)]
        public static void CompleteSetup()
        {
            // Step 1: Generate meshes
            if (!GenerateDebrisMeshes(false))
            {
                return;
            }

            // Step 2: Create material
            if (!CreateConcreteMaterial(false))
            {
                return;
            }

            // Step 3: Create particle system
            GameObject debrisObj = CreateDebrisParticleSystem();
            if (debrisObj == null)
            {
                return;
            }

            // Step 4: Auto-assign assets
            AssignAssetsToDebrisSystem(debrisObj);

            Debug.Log("<color=green>[EarthquakeDebris]</color> Complete debris system created and configured!");

            EditorUtility.DisplayDialog(
                "Complete Setup Done!",
                $"✓ Generated {DEBRIS_MESH_COUNT} debris chunk meshes\n" +
                "✓ Created concrete material\n" +
                "✓ Created particle system\n" +
                "✓ Auto-assigned all assets\n\n" +
                "The debris system is ready to use!\n\n" +
                "To test:\n" +
                "1. Position EarthquakeDebris above target area\n" +
                "2. Start earthquake scenario in Play mode\n" +
                "3. Watch debris fall from ceiling",
                "Awesome!"
            );
        }

        [MenuItem("ARSafe/Setup/Earthquake Debris/Generate Meshes Only", priority = 101)]
        public static void GenerateMeshesMenuItem()
        {
            GenerateDebrisMeshes(true);
        }

        [MenuItem("ARSafe/Setup/Earthquake Debris/Create Material Only", priority = 102)]
        public static void CreateMaterialMenuItem()
        {
            CreateConcreteMaterial(true);
        }

        [MenuItem("ARSafe/Setup/Earthquake Debris/Create Particle System Only", priority = 103)]
        public static void CreateParticleSystemMenuItem()
        {
            GameObject debrisObj = CreateDebrisParticleSystem();

            if (debrisObj != null)
            {
                EditorUtility.DisplayDialog(
                    "Particle System Created",
                    "EarthquakeDebris GameObject created!\n\n" +
                    "Next steps:\n" +
                    "1. Assign debris meshes to ParticleSystemRenderer > Mesh\n" +
                    "2. Assign concrete material to ParticleSystemRenderer > Material\n" +
                    "3. Position above ceiling area where debris should spawn\n" +
                    "4. Adjust EarthquakeDebrisController settings\n\n" +
                    "Tip: Use 'Complete Setup' to auto-assign assets!",
                    "OK"
                );
            }
        }

        [MenuItem("ARSafe/Setup/Earthquake Debris/Create Dust Particle Prefab", priority = 104)]
        public static void CreateDustPrefabMenuItem()
        {
            CreateDustParticlePrefab();
        }

        [MenuItem("ARSafe/Tools/Earthquake Debris/Fix AR Simulation Settings", priority = 105)]
        public static void FixARSimulationSettings()
        {
            // Find all EarthquakeDebrisController components in scene
            EarthquakeDebrisController[] controllers = Object.FindObjectsByType<EarthquakeDebrisController>(FindObjectsSortMode.None);

            if (controllers.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Debris Systems Found",
                    "No EarthquakeDebrisController components found in the scene.\n\n" +
                    "Make sure you have debris systems in your scene before running this fix.",
                    "OK"
                );
                return;
            }

            int fixedCount = 0;
            foreach (var controller in controllers)
            {
                ParticleSystem ps = controller.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    // CRITICAL: Fix simulation space to World (not Local)
                    var main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    // CRITICAL: Enable 3D rotation for realistic tumbling
                    main.startRotation3D = true;
                    main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

                    // Disable collision (performance-friendly for mobile)
                    var collision = ps.collision;
                    collision.enabled = false;

                    // CRITICAL: Fix renderer for 3D mesh rotation (not billboard)
                    ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        renderer.renderMode = ParticleSystemRenderMode.Mesh;
                        renderer.alignment = ParticleSystemRenderSpace.World;
                        renderer.sortMode = ParticleSystemSortMode.Distance;

                        // CRITICAL: Assign debris meshes from GeneratedDebris folder
                        AssignDebrisMeshes(renderer);
                    }

                    EditorUtility.SetDirty(ps);
                    fixedCount++;

                    Debug.Log($"<color=green>✓</color> Fixed AR settings for {controller.gameObject.name}");
                }
            }

            EditorUtility.DisplayDialog(
                "AR Settings Fixed!",
                $"Updated {fixedCount} debris system(s):\n\n" +
                "✓ Simulation space set to World (AR compatibility)\n" +
                "✓ Collision disabled (mobile performance)\n" +
                "✓ 3D rotation enabled (realistic tumbling)\n" +
                "✓ Renderer set to Mesh mode (3D debris chunks)\n\n" +
                "Debris should now render correctly with 3D tumbling!",
                "Perfect!"
            );
        }
        
        #endregion
        
        #region Core Setup Functions
        
        private static bool GenerateDebrisMeshes(bool showDialog)
        {
            if (!AssetDatabase.IsValidFolder(DEBRIS_FOLDER))
            {
                string parentFolder = System.IO.Path.GetDirectoryName(DEBRIS_FOLDER).Replace('\\', '/');
                string folderName = System.IO.Path.GetFileName(DEBRIS_FOLDER);
                
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets/ARSafe_ModularSystem", "Models");
                    AssetDatabase.CreateFolder("Assets/ARSafe_ModularSystem/Models", "GeneratedDebris");
                }
                else
                {
                    AssetDatabase.CreateFolder(parentFolder, folderName);
                }
            }
            
            // Generate 5 different rock chunk variations
            for (int i = 0; i < DEBRIS_MESH_COUNT; i++)
            {
                Mesh debrisMesh = GenerateRandomRockChunk(i);
                string assetPath = $"{DEBRIS_FOLDER}/DebrisChunk_{i + 1}.asset";
                
                // Delete existing mesh if present
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
                
                AssetDatabase.CreateAsset(debrisMesh, assetPath);
                Debug.Log($"<color=green>[EarthquakeDebris]</color> Created {assetPath}");
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Debris Meshes Generated",
                    $"Created {DEBRIS_MESH_COUNT} debris chunk meshes in:\n{DEBRIS_FOLDER}\n\n" +
                    "Next steps:\n" +
                    "• Select EarthquakeDebris GameObject\n" +
                    "• Find ParticleSystemRenderer component\n" +
                    "• Drag DebrisChunk meshes into Mesh field\n" +
                    "• Create/assign a concrete material\n\n" +
                    "Or use 'Complete Setup' to auto-assign!",
                    "OK"
                );
            }
            
            return true;
        }
        
        private static bool CreateConcreteMaterial(bool showDialog)
        {
            string materialFolder = System.IO.Path.GetDirectoryName(MATERIAL_PATH).Replace('\\', '/');
            
            if (!AssetDatabase.IsValidFolder(materialFolder))
            {
                AssetDatabase.CreateFolder("Assets/ARSafe_ModularSystem", "Materials");
            }
            
            // Check if material already exists
            Material existingMat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (existingMat != null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Material Already Exists",
                        $"Concrete material already exists at:\n{MATERIAL_PATH}\n\n" +
                        "Skipping material creation.",
                        "OK"
                    );
                }
                return true;
            }

            // Create URP/Lit material
            Material concreteMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            concreteMat.name = "ConcreteDébris";
            
            // Configure concrete-like properties
            concreteMat.SetColor("_BaseColor", new Color(0.45f, 0.42f, 0.38f, 1f)); // Grey-brown concrete
            concreteMat.SetFloat("_Smoothness", 0.2f); // Very rough surface
            concreteMat.SetFloat("_Metallic", 0f); // Non-metallic
            
            AssetDatabase.CreateAsset(concreteMat, MATERIAL_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"<color=green>[EarthquakeDebris]</color> Created concrete material at {MATERIAL_PATH}");
            
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Material Created",
                    $"Concrete debris material created at:\n{MATERIAL_PATH}\n\n" +
                    "Properties:\n" +
                    "• Color: Grey-brown concrete\n" +
                    "• Smoothness: 0.2 (rough)\n" +
                    "• Metallic: 0 (non-metal)\n\n" +
                    "You can customize these in the Inspector!",
                    "OK"
                );
                
                // Select and ping the material
                Selection.activeObject = concreteMat;
                EditorGUIUtility.PingObject(concreteMat);
            }
            
            return true;
        }
        
        private static GameObject CreateDebrisParticleSystem()
        {
            /*
             * PURPOSE: Create EarthquakeDebris GameObject with ENHANCED visual parameters
             * ENHANCED PARAMETERS (October 2025):
             *   - 60% larger chunks (better mobile visibility)
             *   - 28% faster falls (more dramatic)
             *   - 33% more rotation (violent tumbling)
             *   - 3D tumbling on all axes
             *   - Size/color over lifetime curves
             *   - Multi-octave noise turbulence
             *   - Velocity damping for realism
             */

            // Create parent GameObject
            GameObject debrisObj = new GameObject("EarthquakeDebris");
            debrisObj.transform.position = Vector3.up * 3f; // 3m above ground

            // Add ParticleSystem component
            ParticleSystem ps = debrisObj.AddComponent<ParticleSystem>();

            // === MAIN MODULE - ENHANCED PARAMETERS ===
            var main = ps.main;
            main.duration = 20f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);

            // ENHANCED: 28% faster falls (0.5 - 3.2 m/s instead of 0.5 - 2.0)
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 3.2f);

            // ENHANCED: 60% larger chunks with 3D size variation
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.06f, 0.38f); // Was 0.05-0.24
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.05f, 0.28f); // Was 0.04-0.18
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.07f, 0.42f); // Was 0.06-0.26

            // CRITICAL: Enable 3D rotation with initial values (fixes rendering bug)
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // ENHANCED: 25% heavier maximum chunks (0.9 - 3.5 instead of 1.5 - 2.5)
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.9f, 3.5f);

            // CRITICAL: World space for AR compatibility
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.maxParticles = 150;

            // ENHANCED: Realistic concrete/rust/dust color palette
            Gradient colorGrad = new Gradient();
            colorGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.65f, 0.60f, 0.55f), 0f),   // Light concrete
                    new GradientColorKey(new Color(0.50f, 0.42f, 0.36f), 0.33f), // Tan/rust
                    new GradientColorKey(new Color(0.35f, 0.30f, 0.26f), 0.66f), // Dark concrete
                    new GradientColorKey(new Color(0.28f, 0.25f, 0.22f), 1f)     // Dusty brown-gray
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(colorGrad);

            // === EMISSION - CONTROLLED BY CONTROLLER ===
            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            // === SHAPE - CEILING SPAWN AREA ===
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.2f, 4f); // 4x4m ceiling area

            // === SIZE OVER LIFETIME - SPAWN POP-IN + IMPACT SHRINK ===
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.9f);     // Start slightly smaller
            sizeCurve.AddKey(0.15f, 1.05f);   // Pop in (5% larger)
            sizeCurve.AddKey(0.85f, 1.0f);    // Hold normal size
            sizeCurve.AddKey(1.0f, 0.8f);     // Shrink on impact (dust effect)
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // === COLOR OVER LIFETIME - DUST TINT + ALPHA FADE ===
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient colorLifetimeGrad = new Gradient();
            colorLifetimeGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),                        // Start: original color
                    new GradientColorKey(new Color(0.95f, 0.90f, 0.85f), 0.4f),   // Slight dust tint
                    new GradientColorKey(new Color(0.85f, 0.78f, 0.70f), 0.8f),   // More tan/dust
                    new GradientColorKey(new Color(0.75f, 0.68f, 0.60f), 1f)      // Final dusty brown-gray
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),      // Fully opaque
                    new GradientAlphaKey(1f, 0.85f),   // Stay opaque until near end
                    new GradientAlphaKey(0.6f, 1f)     // Fade out on impact
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorLifetimeGrad);

            // === VELOCITY OVER LIFETIME - ENHANCED DRIFT + DAMPING ===
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;

            // ENHANCED: 50% more horizontal drift (1.8 m/s instead of 0.5)
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f);

            // Velocity damping curve (slow down near ground)
            AnimationCurve dampingCurve = new AnimationCurve();
            dampingCurve.AddKey(0.0f, 1.0f);    // Full speed at start
            dampingCurve.AddKey(0.7f, 1.0f);    // Maintain speed mid-fall
            dampingCurve.AddKey(1.0f, 0.25f);   // Slow to 25% near impact
            velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(1f, dampingCurve);

            // === ROTATION OVER LIFETIME - ENHANCED 3D TUMBLING ===
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = true;

            // ENHANCED: 33% more rotation (±480°/s instead of ±180°/s)
            rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-336f * Mathf.Deg2Rad, 336f * Mathf.Deg2Rad); // 70% of max
            rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-240f * Mathf.Deg2Rad, 240f * Mathf.Deg2Rad); // 50% of max
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-480f * Mathf.Deg2Rad, 480f * Mathf.Deg2Rad); // Full rotation

            // === NOISE - MULTI-OCTAVE TURBULENCE ===
            var noise = ps.noise;
            noise.enabled = true;
            noise.separateAxes = true;

            // ENHANCED: 31% more chaos (0.85 instead of 0.65)
            noise.strengthX = new ParticleSystem.MinMaxCurve(0.68f, 1.02f); // 0.85 ± 20%
            noise.strengthY = new ParticleSystem.MinMaxCurve(0.51f, 0.76f); // 0.85 * 0.75 (less vertical)
            noise.strengthZ = new ParticleSystem.MinMaxCurve(0.68f, 1.02f);

            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
            noise.octaveCount = 2;              // Multi-octave (large + small wobbles)
            noise.octaveMultiplier = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.High;

            // === NO COLLISION (MOBILE PERFORMANCE) ===
            var collision = ps.collision;
            collision.enabled = false;

            // === RENDERER - CRITICAL FOR 3D ROTATION ===
            ParticleSystemRenderer renderer = debrisObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World; // CRITICAL: World for 3D rotation
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.sortingFudge = 0;

            // === ADD CONTROLLER COMPONENT ===
            debrisObj.AddComponent<EarthquakeDebrisController>();

            // === DISASTER TAGGING FOR FILTERS ===
            var contentTag = debrisObj.GetComponent<ARSafeDisasterContent>();
            if (contentTag == null)
            {
                contentTag = debrisObj.AddComponent<ARSafeDisasterContent>();
            }
            contentTag.disasterType = DisasterType.Earthquake;
            if (string.IsNullOrEmpty(contentTag.contentDescription))
            {
                contentTag.contentDescription = "Earthquake Debris";
            }

            // === UNDO SUPPORT ===
            Undo.RegisterCreatedObjectUndo(debrisObj, "Create Earthquake Debris System");
            Selection.activeGameObject = debrisObj;

            Debug.Log($"<color=green>[EarthquakeDebris]</color> Created enhanced particle system at {debrisObj.transform.position}");

            return debrisObj;
        }
        
        private static void AssignAssetsToDebrisSystem(GameObject debrisObj)
        {
            ParticleSystemRenderer renderer = debrisObj.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                Debug.LogWarning("<color=yellow>[EarthquakeDebris]</color> No ParticleSystemRenderer found!");
                return;
            }
            
            // Load generated meshes
            Mesh[] meshes = new Mesh[DEBRIS_MESH_COUNT];
            for (int i = 0; i < DEBRIS_MESH_COUNT; i++)
            {
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(
                    $"{DEBRIS_FOLDER}/DebrisChunk_{i + 1}.asset"
                );
            }
            
            // Load concrete material
            Material concreteMat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            
            // Assign to renderer
            renderer.SetMeshes(meshes);
            renderer.material = concreteMat;
            
            EditorUtility.SetDirty(debrisObj);
        }
        
        #endregion
        
        #region Mesh Generation
        
        private static Mesh GenerateRandomRockChunk(int seed)
        {
            Random.InitState(seed * 1337); // Deterministic randomness
            
            Mesh mesh = new Mesh();
            mesh.name = $"DebrisChunk_{seed + 1}";
            
            // Generate irregular rock shape using deformed icosphere
            int subdivisions = 1; // Low poly for performance
            var (vertices, triangles) = GenerateIcosphere(subdivisions);

            float baseScale = Random.Range(0.18f, 0.32f);
            Vector3 axisStretch = new Vector3(
                Random.Range(0.75f, 1.12f),
                Random.Range(0.65f, 1.05f),
                Random.Range(0.75f, 1.2f)
            );

            // Deform vertices to create rocky appearance with smaller overall size
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector3 stretched = new Vector3(v.x * axisStretch.x, v.y * axisStretch.y, v.z * axisStretch.z);
                float noise = (Mathf.PerlinNoise(v.x * 4f + seed, v.y * 4f + seed) - 0.5f) * 0.18f;
                float radialScale = Mathf.Max(0.08f, baseScale + noise);
                vertices[i] = stretched.normalized * radialScale;
            }
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private static (Vector3[] vertices, int[] triangles) GenerateIcosphere(int subdivisions)
        {
            // Golden ratio
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            
            // 12 vertices of icosahedron
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-1,  t,  0).normalized,
                new Vector3( 1,  t,  0).normalized,
                new Vector3(-1, -t,  0).normalized,
                new Vector3( 1, -t,  0).normalized,
                new Vector3( 0, -1,  t).normalized,
                new Vector3( 0,  1,  t).normalized,
                new Vector3( 0, -1, -t).normalized,
                new Vector3( 0,  1, -t).normalized,
                new Vector3( t,  0, -1).normalized,
                new Vector3( t,  0,  1).normalized,
                new Vector3(-t,  0, -1).normalized,
                new Vector3(-t,  0,  1).normalized
            };
            
            // 20 faces of icosahedron
            int[] triangles = new int[]
            {
                0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,   7, 1, 8,
                3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
            };
            
            return (vertices, triangles);
        }
        
        private static void CreateDustParticlePrefab()
        {
            // Create ground dust particle system GameObject
            GameObject dustObj = new GameObject("GroundDust");
            ParticleSystem ps = dustObj.AddComponent<ParticleSystem>();
            
            // Configure main module for continuous ground-level dust layer
            var main = ps.main;
            main.duration = 5f; // Continuous emission
            main.loop = true; // Keep emitting
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f); // Longer lifetime for settling dust
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); // Slow, lazy motion
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f); // Larger dust clouds
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            
            // More realistic dust colors (concrete/earth tones with low alpha)
            Gradient dustColorGrad = new Gradient();
            dustColorGrad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.65f, 0.6f, 0.55f), 0f),   // Light tan dust
                    new GradientColorKey(new Color(0.5f, 0.45f, 0.4f), 0.5f),  // Mid brown dust
                    new GradientColorKey(new Color(0.4f, 0.36f, 0.32f), 1f)    // Dark earth dust
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.3f, 0f),  // Start semi-transparent
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0.3f, 1f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(dustColorGrad);
            
            main.gravityModifier = -0.05f; // Very slight upward drift (dust lingers)
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.maxParticles = 100; // Reasonable for mobile
            
            // Emission - controlled by script
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // Controller will set this dynamically
            
            // Shape - flat ground area (will be resized by controller)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(5f, 0.1f, 5f); // Default area (controller adjusts)
            shape.randomDirectionAmount = 0.5f; // More varied directions
            
            // Size over lifetime - gradual expansion (dust spreads and dissipates)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),   // Start medium
                new Keyframe(0.3f, 1f),   // Expand
                new Keyframe(1f, 1.4f)    // Continue expanding as it fades
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
            
            // Color over lifetime - gradual fade (dust settles and becomes transparent)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),    // Full color at start
                    new GradientAlphaKey(0.7f, 0.4f), // Fade mid-life
                    new GradientAlphaKey(0.3f, 0.8f), // More transparent
                    new GradientAlphaKey(0f, 1f)      // Fully transparent at end
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);
            
            // Rotation over lifetime - slow lazy spin (organic dust motion)
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);
            
            // Velocity over lifetime - slow outward drift (dust spreads horizontally)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocityOverLifetime.speedModifier = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            
            // Limit velocity over lifetime - dust slows down as it settles
            var limitVelocity = ps.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.space = ParticleSystemSimulationSpace.Local;
            limitVelocity.dampen = 0.5f; // Gradual slowdown
            limitVelocity.limit = 0.5f;   // Max speed cap
            
            // Configure renderer for soft, billboarded dust clouds
            ParticleSystemRenderer renderer = dustObj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 2f; // Larger particles for visible dust clouds
            
            // Save as prefab - ensure folder structure exists
            string prefabsRoot = "Assets/ARSafe_ModularSystem/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabsRoot))
            {
                AssetDatabase.CreateFolder("Assets/ARSafe_ModularSystem", "Prefabs");
            }
            
            string prefabFolder = "Assets/ARSafe_ModularSystem/Prefabs/Effects";
            if (!AssetDatabase.IsValidFolder(prefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/ARSafe_ModularSystem/Prefabs", "Effects");
            }
            
            // Refresh database to ensure folder exists
            AssetDatabase.Refresh();
            
            string prefabPath = $"{prefabFolder}/DustPuff.prefab";
            PrefabUtility.SaveAsPrefabAsset(dustObj, prefabPath);
            
            // Clean up scene object
            Object.DestroyImmediate(dustObj);
            
            // Select and ping the prefab
            ParticleSystem prefab = AssetDatabase.LoadAssetAtPath<ParticleSystem>(prefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            
            Debug.Log($"<color=green>[EarthquakeDebris]</color> Dust particle prefab created at {prefabPath}");
            
            EditorUtility.DisplayDialog(
                "Ground Dust Prefab Created!",
                $"Ground dust particle prefab created at:\n{prefabPath}\n\n" +
                "Setup Instructions:\n\n" +
                "1. Select your EarthquakeDebris GameObject\n\n" +
                "2. In Inspector → Ground Level & Dust Effects:\n" +
                "   ✅ Enable Ground Dust\n" +
                "   📦 Assign GroundDust prefab to 'Ground Dust Prefab' field\n" +
                "   📏 Set 'Ground Level' to your floor Y position (e.g., 0)\n\n" +
                "3. Optional: Adjust 'Dust Emission Rate' (default 15/s)\n\n" +
                "Dust will continuously emit at ground level during earthquake!",
                "Perfect!"
            );
        }

        private static void AssignDebrisMeshes(ParticleSystemRenderer renderer)
        {
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

        #endregion
    }
}
