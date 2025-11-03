using UnityEngine;
using System.Collections.Generic;
using ARSafe.Modular;

namespace ARSafe.Content
{
    /// <summary>
    /// Controls earthquake debris particle system with realistic falling rocks/concrete
    /// Attach to a GameObject with a ParticleSystem component
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class EarthquakeDebrisController : MonoBehaviour
    {
        public enum GroundConstraintMode
        {
            ManualHeight,
            UseTransform,
            UseColliderBounds,
            RaycastDown
        }

        [Header("Debris Settings")]
        [Tooltip("Spawn debris only during earthquake disaster")]
        [SerializeField] private bool onlyDuringEarthquake = true;
        
        [Tooltip("Delay before debris starts falling (seconds)")]
        [SerializeField] private float startDelay = 1f;
        
        [Tooltip("How long debris continues falling (0 = infinite)")]
        [SerializeField] private float duration = 15f;
        
        [Header("Intensity Settings")]
        [Tooltip("Initial emission rate (particles/second)")]
        [SerializeField] private float initialEmissionRate = 5f;
        
        [Tooltip("Peak emission rate when earthquake intensifies")]
        [SerializeField] private float peakEmissionRate = 20f;
        
        [Tooltip("Time to reach peak intensity (seconds)")]
        [SerializeField] private float rampUpTime = 5f;
        
        [Header("Spawn Area")]
        [Tooltip("Width of debris spawn area (X axis)")]
        [SerializeField] private float spawnWidth = 5f;
        
        [Tooltip("Length of debris spawn area (Z axis)")]
        [SerializeField] private float spawnLength = 5f;
        
        [Tooltip("Height above ground where debris spawns")]
        [SerializeField] private float spawnHeight = 4f;
        
        [Header("Audio (Optional)")]
        [Tooltip("Sound effect for debris impacts (optional)")]
        [SerializeField] private AudioClip[] impactSounds;
        
        [Tooltip("Audio source for impact sounds")]
        [SerializeField] private AudioSource audioSource;
        
        [Header("Ground Constraint")]
        [Tooltip("Determines how the ground height is calculated for debris and dust")]
        [SerializeField] private GroundConstraintMode groundConstraintMode = GroundConstraintMode.ManualHeight;

        [Tooltip("Optional transform whose Y position defines the ground height when using 'UseTransform'.")]
        [SerializeField] private Transform groundHeightReference;

        [Tooltip("Optional collider whose top surface defines the ground height when using 'UseColliderBounds'.")]
        [SerializeField] private Collider groundBoundsCollider;

        [Tooltip("Layer mask used when raycasting for ground height.")]
        [SerializeField] private LayerMask groundRaycastLayers = Physics.DefaultRaycastLayers;

        [Tooltip("Maximum distance for the downward ground raycast.")]
        [SerializeField] private float groundRaycastMaxDistance = 6f;

        [Tooltip("Local-space offset added to the raycast origin when using 'RaycastDown'.")]
        [SerializeField] private Vector3 groundRaycastOriginOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("Padding added to the resolved ground height (useful to keep debris slightly above geometry).")]
        [SerializeField] private float groundLevelPadding = 0f;

        [Header("Ground Level")]
        [Tooltip("Y position where debris stops falling (ground level)")]
        [SerializeField] private float groundLevel = 0f;
        
        [Header("Ground Dust Layer (DEPRECATED - Use Impact Dust Instead)")]
        [Tooltip("DEPRECATED: Continuous dust layer disabled - use Impact Dust for realistic debris dust")]
        [SerializeField] private bool enableGroundDust = false;
        
        [Tooltip("DEPRECATED")]
        [SerializeField] private ParticleSystem groundDustPrefab;
        
        [Tooltip("DEPRECATED")]
        [SerializeField] private float dustEmissionRate = 15f;
        
        [Tooltip("DEPRECATED")]
        [SerializeField] private float dustHeightOffset = 0.05f;

        [Tooltip("DEPRECATED")]
        [SerializeField] private bool matchDustToSpawnArea = true;

        [Tooltip("DEPRECATED")]
        [SerializeField] private BoxCollider dustAreaOverride;

        [Tooltip("DEPRECATED")]
        [SerializeField] private Vector2 dustAreaPadding = new Vector2(0.35f, 0.35f);
        
        [Header("Dust Visuals (DEPRECATED)")]
        [Tooltip("DEPRECATED")]
        [SerializeField] private Vector2 dustSizeRange = new Vector2(0.9f, 2.2f);
        [Tooltip("DEPRECATED")]
        [SerializeField] private Vector2 dustLifetimeRange = new Vector2(2.5f, 4.5f);
        [Tooltip("DEPRECATED")]
        [SerializeField] private Vector2 dustRotationSpeedRange = new Vector2(-35f, 35f);
        [Tooltip("DEPRECATED")]
        [SerializeField] private float dustNoiseStrength = 0.45f;
        [Tooltip("DEPRECATED")]
        [SerializeField] private float dustNoiseFrequency = 0.25f;
        [Tooltip("DEPRECATED")]
        [SerializeField] private Vector2 dustNoiseScrollSpeedRange = new Vector2(0.05f, 0.18f);
        [Tooltip("DEPRECATED")]
        [SerializeField] private Gradient dustColorGradient;
        
    [Header("Visual Tweaks")]
    [Tooltip("Randomized start size for debris chunks (meters) - ENHANCED for better visibility")]
    [SerializeField] private Vector2 startSizeRange = new Vector2(0.08f, 0.35f);

    [Tooltip("Randomized start lifetime (seconds) to vary fall distance")]
    [SerializeField] private Vector2 startLifetimeRange = new Vector2(1.2f, 2.4f);

    [Tooltip("Initial downward velocity magnitude (m/s) - ENHANCED for more dramatic falls")]
    [SerializeField] private Vector2 startSpeedRange = new Vector2(0.5f, 3.2f);

    [Tooltip("Gravity modifier applied per particle - ENHANCED for dramatic weight variation")]
    [SerializeField] private Vector2 gravityModifierRange = new Vector2(0.9f, 3.5f);

    [Tooltip("Random rotation speed (deg/sec) - ENHANCED for violent tumbling motion")]
    [SerializeField] private Vector2 angularVelocityRange = new Vector2(-480f, 480f);

    [Tooltip("Horizontal drift range (m/s) - ENHANCED to create scattered, chaotic falls")]
    [SerializeField] private float horizontalDrift = 1.8f;

    [Tooltip("Perlin noise strength - ENHANCED for erratic wobble motion (higher = more chaotic)")]
    [SerializeField] private float noiseStrength = 0.85f;

    [Tooltip("Optional color gradient for debris; left = spawn, right = death. Auto-generates realistic concrete/rock colors if empty.")]
    [SerializeField] private Gradient colorGradient;

    [Tooltip("Enable fully random X/Y/Z scaling for each debris chunk - RECOMMENDED for realistic irregular shapes")]
    [SerializeField] private bool use3DRandomSize = true;

    [Tooltip("Minimum 3D size per axis - ENHANCED for better visibility")]
    [SerializeField] private Vector3 startSize3DMin = new Vector3(0.06f, 0.05f, 0.07f);
    [Tooltip("Maximum 3D size per axis - ENHANCED for dramatic size variety")]
    [SerializeField] private Vector3 startSize3DMax = new Vector3(0.38f, 0.28f, 0.42f);

        [Header("Impact Dust Effects")]
        [Tooltip("Enable spawning dust clouds when debris hits the ground (Unity Technologies Dust Storm or similar particle prefab)")]
        [SerializeField] private bool enableImpactSmoke = true;

        [Tooltip("Dust cloud prefab to spawn on debris impact (Unity Technologies Dust Storm, Luke Peek smoke, or custom particle system)")]
        [SerializeField] private GameObject impactSmokePrefab;

        [Tooltip("Chance to spawn dust on impact (0-1). Higher values create denser dust coverage")]
        [SerializeField, Range(0f, 1f)] private float impactSmokeChance = 0.35f;

        [Tooltip("Scale multiplier for spawned dust clouds (smaller = ground-level dust, larger = thick smoke)")]
        [SerializeField] private Vector2 impactSmokeScaleRange = new Vector2(0.4f, 1.0f);

        [Tooltip("Color tint for impact dust (natural dust tan for Dust Storm: RGB(180,165,145), gray-brown for concrete)")]
        [SerializeField] private Color impactSmokeTint = new Color(0.55f, 0.5f, 0.45f, 0.8f); // Dusty light gray-brown

        [Tooltip("How long dust persists before fading (seconds). Shorter = quick puffs, longer = lingering clouds")]
        [SerializeField] private float impactSmokeLifetime = 3.5f;

        [Tooltip("Maximum number of active dust clouds. Balanced for visual density and performance.")]
        [SerializeField] private int maxActiveSmokeInstances = 15;

        [Tooltip("Vertical offset for dust spawn position (0 = ground level, positive = raised slightly)")]
        [SerializeField] private float impactSmokeHeightOffset = 0.05f;
        
        private ParticleSystem ps;
        private ParticleSystem.EmissionModule emission;
        private ParticleSystem.ShapeModule shape;
        private ParticleSystem.MainModule main;
        private float elapsedTime;
        private bool isActive;
        private DisasterType currentDisaster;
        
        // Ground dust system
    private ParticleSystem groundDustSystem;
    private Coroutine groundDustDisableRoutine;
    private bool modulesInitialized;
    private Vector3 lastKnownPosition;
    private float lastKnownGroundLevel;
    private float lastKnownSpawnHeight;
    private float lastKnownSpawnWidth;
    private float lastKnownSpawnLength;
    private float lastKnownDustOffset;
    private bool lastGroundDustEnabled;
    private ParticleSystem lastGroundDustPrefab;
    private bool lastMatchDustToSpawnArea;
    private BoxCollider lastDustAreaOverride;
    private Vector2 lastDustAreaPadding;
    private GroundConstraintMode lastGroundConstraintMode;
    private Transform lastGroundHeightReference;
    private Collider lastGroundBoundsCollider;
    private LayerMask lastGroundRaycastLayers;
    private float lastGroundRaycastDistance;
    private Vector3 lastGroundRaycastOffset;
    private float lastGroundLevelPadding;
    private Vector2 lastDustSizeRange;
    private Vector2 lastDustLifetimeRange;
    private Vector2 lastDustRotationSpeedRange;
    private float lastDustNoiseStrength;
    private float lastDustNoiseFrequency;
    private Vector2 lastDustNoiseScrollSpeedRange;
    private Gradient lastDustColorGradient;

    // Impact smoke tracking
    private List<GameObject> activeSmokePuffs = new List<GameObject>();
    private ParticleSystem.Particle[] particleBuffer;
    private int lastParticleCount;

    private float baseStartDelay;
    private float baseDuration;
    private float baseInitialEmissionRate;
    private float basePeakEmissionRate;
    private float baseRampUpTime;

    private float currentStartDelay;
    private float currentDuration;
    private float currentInitialEmissionRate;
    private float currentPeakEmissionRate;
    private float currentRampUpTime;
    private float scenarioProgressMultiplier = 0f;
    private bool scenarioTimelineActive;
        
        void Awake()
        {
            CacheBaseValues();
        }

        void OnEnable()
        {
            Debug.Log($"<color=cyan>[EarthquakeDebrisController] OnEnable() called on {name} - subscribing to earthquake events</color>");

            // CRITICAL FIX: Initialize currentDisaster BEFORE handling scenario progress
            // OnEnable runs before Start(), so we need to get the disaster type here
            // Check if DisasterTypeManager exists, if not it will be created by property access
            if (onlyDuringEarthquake)
            {
                // Access DisasterTypeManager - this will create it if it doesn't exist
                currentDisaster = DisasterTypeManager.SelectedDisasterType;
                Debug.Log($"<color=yellow>[EarthquakeDebrisController] Initialized currentDisaster in OnEnable: {currentDisaster} (DisasterTypeManager.Instance exists: {DisasterTypeManager.Instance != null})</color>");
            }

            EarthquakeScenarioManager.OnParametersUpdated += HandleScenarioParameters;
            EarthquakeScenarioManager.OnProgressUpdated += HandleScenarioProgress;

            var currentParams = EarthquakeScenarioManager.CurrentParameters;
            var currentProgress = EarthquakeScenarioManager.CurrentProgress;

            Debug.Log($"<color=cyan>[EarthquakeDebrisController] Current earthquake state - IsActive: {currentParams.IsActive}, Progress.IsActive: {currentProgress.IsActive}, currentDisaster: {currentDisaster}</color>");

            ApplyScenarioParameters(currentParams);
            HandleScenarioProgress(currentProgress);

            // If we were disabled while the scenario was active, the ParticleSystem may have stopped.
            // Ensure it resumes immediately upon re-enable when it should be emitting.
            if (currentParams.IsActive || currentProgress.IsActive)
            {
                EnsureParticleSystemReadyToEmit();
            }
        }

        void Start()
        {
            ps = GetComponent<ParticleSystem>();
            emission = ps.emission;
            shape = ps.shape;
            main = ps.main;
            
            // CRITICAL: Set simulation space to World (not Local/Custom) for AR
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            ApplySpawnAreaSettings();

            RefreshGroundHeight();
            
            // Disable collision (performance-friendly for mobile)
            var collision = ps.collision;
            collision.enabled = false;
            
            // Start disabled
            emission.enabled = false;
            ConfigureVisuals();
            ConfigureRenderer();
            ApplyScenarioParameters(EarthquakeScenarioManager.CurrentParameters);
            
            // Subscribe to disaster changes
            if (onlyDuringEarthquake && DisasterTypeManager.Instance != null)
            {
                DisasterTypeManager.OnDisasterTypeChanged += OnDisasterChanged;
                currentDisaster = DisasterTypeManager.SelectedDisasterType;
                
                // DON'T auto-start here! Wait for earthquake scenario to actually begin.
                // HandleScenarioProgress() will start debris when scenario becomes active.
                Debug.Log($"<color=yellow>[EarthquakeDebrisController] Debris controller initialized. Waiting for earthquake scenario to start...</color>");
            }
            else if (!onlyDuringEarthquake)
            {
                // Always active mode (for testing without earthquake)
                StartDebris();
            }
            
            // Setup audio source if not assigned
            if (audioSource == null && impactSounds != null && impactSounds.Length > 0)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D sound
                audioSource.maxDistance = 20f;
            }
            
            // Initialize ground dust system
            if (enableGroundDust && groundDustPrefab != null)
            {
                InitializeGroundDust();
            }
            else
            {
                lastGroundDustEnabled = enableGroundDust;
                lastGroundDustPrefab = groundDustPrefab;
            }

            PositionGroundDust();
            UpdateGroundDust();

            CacheLastConfiguration();
            modulesInitialized = true;
            
            Debug.Log($"[EarthquakeDebrisController] Initialized on {gameObject.name}");
        }
        
        void OnDisable()
        {
            EarthquakeScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleScenarioProgress;

            if (groundDustDisableRoutine != null)
            {
                StopCoroutine(groundDustDisableRoutine);
                groundDustDisableRoutine = null;
            }
        }

        void OnDestroy()
        {
            if (DisasterTypeManager.Instance != null)
            {
                DisasterTypeManager.OnDisasterTypeChanged -= OnDisasterChanged;
            }

            EarthquakeScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleScenarioProgress;

            DestroyGroundDustInstance();
            DestroyAllSmokePuffs();
        }
        
        private void OnDisasterChanged(DisasterType newType)
        {
            currentDisaster = newType;
            
            if (newType == DisasterType.Earthquake)
            {
                StartDebris();
            }
            else
            {
                StopDebris();
            }
        }
        
        private void StartDebris()
        {
            if (isActive)
            {
                Debug.Log($"<color=yellow>[EarthquakeDebrisController] StartDebris() called but already active on {name}</color>");
                return;
            }

            ApplyScenarioParameters(EarthquakeScenarioManager.CurrentParameters);
            Invoke(nameof(EnableEmission), currentStartDelay);
            isActive = true;
            elapsedTime = 0f;

            Debug.Log($"<color=green>[EarthquakeDebrisController] ✓ Debris will start in {currentStartDelay:F2}s on {name} (GameObject active={gameObject.activeSelf}, component enabled={enabled})</color>");
        }
        
        private void EnableEmission()
        {
            emission.enabled = true;
            EnsureParticleSystemReadyToEmit();
            Debug.Log($"<color=green>[EarthquakeDebrisController] ✓✓✓ Debris emission ENABLED on {name}! Particles should be falling now!</color>");
        }
        
        private void StopDebris()
        {
            if (!isActive) return;
            
            CancelInvoke(nameof(EnableEmission));
            emission.enabled = false;
            isActive = false;
            elapsedTime = 0f;

            // Ground dust layer removed - using impact dust only
            // UpdateGroundDust();
            
            Debug.Log("[EarthquakeDebrisController] Debris stopped");
        }

        /// <summary>
        /// Make sure the ParticleSystem is playing so an enabled Emission module actually spawns particles.
        /// Handles cases where Play On Awake is off or the GameObject/component was disabled and re-enabled during the quake.
        /// </summary>
        private void EnsureParticleSystemReadyToEmit()
        {
            if (ps == null)
            {
                ps = GetComponent<ParticleSystem>();
                emission = ps.emission;
                shape = ps.shape;
                main = ps.main;
            }

            // If emission should be active during an active scenario, ensure the system is playing
            if (ps != null)
            {
                // Clear lingering state when resuming to avoid stale particles
                if (!ps.isPlaying)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
            }
        }
        
        void Update()
        {
            if (!modulesInitialized)
            {
                return;
            }

            CheckForConfigurationChanges();

            if (!isActive || !emission.enabled)
            {
                // Ground dust layer removed - using impact dust only
                // UpdateGroundDust();
                return;
            }
            
            elapsedTime += Time.deltaTime;
            
            // Ramp up intensity over time
            float rampDurationSeconds = Mathf.Max(0.1f, currentRampUpTime);
            float intensityProgress = Mathf.Clamp01(elapsedTime / rampDurationSeconds);
            float emissionRate = Mathf.Lerp(currentInitialEmissionRate, currentPeakEmissionRate, intensityProgress);
            
            // DON'T multiply by scenarioProgressMultiplier! It starts at 0 which kills emission.
            // The debris should emit at full rate immediately when started.
            emission.rateOverTime = emissionRate;
            
            // Ground dust layer removed - using impact dust only
            // UpdateGroundDust();

            // Track particles for impact dust clouds
            if (enableImpactSmoke && impactSmokePrefab != null)
            {
                CheckParticleImpacts();
            }
            
            // Stop after duration (if set)
            // The scenario manager will call HandleScenarioProgress() with IsComplete=true when earthquake ends
            // Don't try to use scenarioProgressMultiplier here - it starts at 0 which triggers immediate stop!
            bool shouldStopOnMultiplier = scenarioTimelineActive && 
                                scenarioProgressMultiplier <= 0.001f && 
                                elapsedTime > (currentDuration * 0.5f);
            if ((currentDuration > 0 && elapsedTime >= currentDuration) || shouldStopOnMultiplier)
            {
                StopDebris();
            }
        }
        
        // No collision detection needed - debris particles die when reaching ground level
        
        // Manual control methods
        public void ManualStart()
        {
            StartDebris();
        }
        
        public void ManualStop()
        {
            StopDebris();
        }

        /// <summary>
        /// Sets the world-space Y coordinate that debris should land on and updates dust positioning.
        /// </summary>
        public void SetGroundLevel(float worldY)
        {
            groundConstraintMode = GroundConstraintMode.ManualHeight;
            groundLevel = worldY;

            if (!modulesInitialized)
            {
                return;
            }

            ConfigureDustShape();
            ConfigureVisuals();
            PositionGroundDust();
            CacheLastConfiguration();
            UpdateGroundDust();
        }
        
        // Gizmos for visualization
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 spawnCenter = transform.position + Vector3.up * spawnHeight;
            Gizmos.DrawWireCube(spawnCenter, new Vector3(spawnWidth, 0.1f, spawnLength));
            
            // Draw spawn area boundary
            Gizmos.color = Color.red;
            Vector3[] corners = new Vector3[]
            {
                spawnCenter + new Vector3(-spawnWidth/2, 0, -spawnLength/2),
                spawnCenter + new Vector3(spawnWidth/2, 0, -spawnLength/2),
                spawnCenter + new Vector3(spawnWidth/2, 0, spawnLength/2),
                spawnCenter + new Vector3(-spawnWidth/2, 0, spawnLength/2)
            };
            
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
                Gizmos.DrawLine(corners[i], corners[i] - Vector3.up * spawnHeight);
            }
        }
        
#if UNITY_EDITOR
        void OnValidate()
        {
            startSizeRange = EnsureOrderedRange(startSizeRange, 0.01f);
            startLifetimeRange = EnsureOrderedRange(startLifetimeRange, 0.1f);
            startSpeedRange = EnsureOrderedRange(startSpeedRange, 0f);
            gravityModifierRange = EnsureOrderedRange(gravityModifierRange, 0f);
            startSize3DMin = ClampVectorPositive(startSize3DMin, 0.01f);
            startSize3DMax = ClampVectorPositive(startSize3DMax, 0.01f);
            EnsureVectorOrdering(ref startSize3DMin, ref startSize3DMax);
            if (horizontalDrift < 0f) horizontalDrift = 0f;
            if (noiseStrength < 0f) noiseStrength = 0f;
            dustSizeRange = EnsureOrderedRange(dustSizeRange, 0.05f);
            dustLifetimeRange = EnsureOrderedRange(dustLifetimeRange, 0.1f);
            dustNoiseStrength = Mathf.Max(0f, dustNoiseStrength);
            dustNoiseFrequency = Mathf.Max(0.01f, dustNoiseFrequency);
            dustNoiseScrollSpeedRange = EnsureOrderedRange(dustNoiseScrollSpeedRange, 0f);
            dustAreaPadding.x = Mathf.Max(0f, dustAreaPadding.x);
            dustAreaPadding.y = Mathf.Max(0f, dustAreaPadding.y);
            if (dustRotationSpeedRange.x > dustRotationSpeedRange.y)
            {
                (dustRotationSpeedRange.x, dustRotationSpeedRange.y) = (dustRotationSpeedRange.y, dustRotationSpeedRange.x);
            }
            groundRaycastMaxDistance = Mathf.Max(0.25f, groundRaycastMaxDistance);
            CacheBaseValues();
        }
#endif

        private void HandleScenarioParameters(EarthquakeScenarioParameters parameters)
        {
            ApplyScenarioParameters(parameters);
        }

        private void HandleScenarioProgress(EarthquakeScenarioProgress progress)
        {
            if (!enabled)
            {
                return;
            }

            if (!progress.IsActive)
            {
                scenarioTimelineActive = false;
                scenarioProgressMultiplier = 0f;

                if (progress.IsComplete)
                {
                    StopDebris();
                }

                return;
            }

            scenarioTimelineActive = true;
            scenarioProgressMultiplier = EvaluateProgressMultiplier(progress.NormalizedTime);

            if (!isActive && currentDisaster == DisasterType.Earthquake)
            {
                StartDebris();
            }
        }

        private void ApplyScenarioParameters(EarthquakeScenarioParameters parameters)
        {
            if (!enabled)
            {
                return;
            }

            if (!parameters.IsActive)
            {
                currentStartDelay = baseStartDelay;
                currentDuration = baseDuration;
                currentInitialEmissionRate = baseInitialEmissionRate;
                currentPeakEmissionRate = basePeakEmissionRate;
                currentRampUpTime = baseRampUpTime;
                return;
            }

            float normalized = Mathf.Clamp01(parameters.NormalizedMagnitude);
            float rateMultiplier = Mathf.Max(0.1f, parameters.DebrisRateMultiplier);

            currentStartDelay = baseStartDelay * Mathf.Lerp(1f, 0.7f, normalized);
            currentInitialEmissionRate = baseInitialEmissionRate * rateMultiplier;
            currentPeakEmissionRate = basePeakEmissionRate * rateMultiplier;
            currentRampUpTime = Mathf.Max(0.2f, baseRampUpTime * Mathf.Lerp(1.1f, 0.65f, normalized));

            if (baseDuration <= 0f)
            {
                currentDuration = baseDuration;
            }
            else
            {
                float durationMultiplier = Mathf.Max(0.1f, parameters.DebrisDurationMultiplier);
                currentDuration = Mathf.Max(0.5f, baseDuration * durationMultiplier);
            }
        }

        private void CacheBaseValues()
        {
            baseStartDelay = Mathf.Max(0f, startDelay);
            baseDuration = duration;
            baseInitialEmissionRate = Mathf.Max(0f, initialEmissionRate);
            basePeakEmissionRate = Mathf.Max(baseInitialEmissionRate, peakEmissionRate);
            baseRampUpTime = Mathf.Max(0.1f, rampUpTime);

            currentStartDelay = baseStartDelay;
            currentDuration = baseDuration;
            currentInitialEmissionRate = baseInitialEmissionRate;
            currentPeakEmissionRate = basePeakEmissionRate;
            currentRampUpTime = baseRampUpTime;
        }

        private float EvaluateProgressMultiplier(float normalizedTime)
        {
            const float rampPortion = 0.25f;
            const float fadePortion = 0.25f;

            if (normalizedTime <= Mathf.Epsilon)
            {
                return 0f;
            }

            if (normalizedTime < rampPortion)
            {
                return Mathf.Clamp01(normalizedTime / Mathf.Max(0.05f, rampPortion));
            }

            if (normalizedTime > 1f - fadePortion)
            {
                float fadeT = Mathf.InverseLerp(1f - fadePortion, 1f, normalizedTime);
                return Mathf.Lerp(1f, 0f, fadeT);
            }

            return 1f;
        }

        private void ApplySpawnAreaSettings()
        {
            if (ps == null)
            {
                ps = GetComponent<ParticleSystem>();
            }

            if (ps == null)
            {
                return;
            }

            shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(0.1f, spawnWidth), 0.1f, Mathf.Max(0.1f, spawnLength));
            shape.position = new Vector3(0f, spawnHeight, 0f);
            shape.randomDirectionAmount = 0.25f;
        }

        private void CacheLastConfiguration()
        {
            lastKnownPosition = transform.position;
            lastKnownGroundLevel = groundLevel;
            lastKnownSpawnHeight = spawnHeight;
            lastKnownSpawnWidth = spawnWidth;
            lastKnownSpawnLength = spawnLength;
            lastKnownDustOffset = dustHeightOffset;
            lastGroundDustEnabled = enableGroundDust;
            lastGroundDustPrefab = groundDustPrefab;
            lastMatchDustToSpawnArea = matchDustToSpawnArea;
            lastDustAreaOverride = dustAreaOverride;
            lastDustAreaPadding = dustAreaPadding;
            lastGroundConstraintMode = groundConstraintMode;
            lastGroundHeightReference = groundHeightReference;
            lastGroundBoundsCollider = groundBoundsCollider;
            lastGroundRaycastLayers = groundRaycastLayers;
            lastGroundRaycastDistance = groundRaycastMaxDistance;
            lastGroundRaycastOffset = groundRaycastOriginOffset;
            lastGroundLevelPadding = groundLevelPadding;
            lastDustSizeRange = dustSizeRange;
            lastDustLifetimeRange = dustLifetimeRange;
            lastDustRotationSpeedRange = dustRotationSpeedRange;
            lastDustNoiseStrength = dustNoiseStrength;
            lastDustNoiseFrequency = dustNoiseFrequency;
            lastDustNoiseScrollSpeedRange = dustNoiseScrollSpeedRange;
            lastDustColorGradient = CloneGradient(dustColorGradient);
        }

        private void CheckForConfigurationChanges()
        {
            if (!modulesInitialized)
            {
                return;
            }

            bool groundResolvedThisFrame = false;
            if (groundConstraintMode != GroundConstraintMode.ManualHeight)
            {
                groundResolvedThisFrame = RefreshGroundHeight();
            }

            bool positionChanged = transform.position != lastKnownPosition;
            bool spawnHeightChanged = !Mathf.Approximately(spawnHeight, lastKnownSpawnHeight);
            bool spawnWidthChanged = !Mathf.Approximately(spawnWidth, lastKnownSpawnWidth);
            bool spawnLengthChanged = !Mathf.Approximately(spawnLength, lastKnownSpawnLength);
            bool dustOffsetChanged = !Mathf.Approximately(dustHeightOffset, lastKnownDustOffset);
            bool dustToggleChanged = enableGroundDust != lastGroundDustEnabled;
            bool dustPrefabChanged = groundDustPrefab != lastGroundDustPrefab;
            bool groundChanged = groundResolvedThisFrame || !Mathf.Approximately(groundLevel, lastKnownGroundLevel);
            bool groundConstraintChanged = groundConstraintMode != lastGroundConstraintMode
                || groundHeightReference != lastGroundHeightReference
                || groundBoundsCollider != lastGroundBoundsCollider
                || groundRaycastLayers != lastGroundRaycastLayers
                || !Mathf.Approximately(groundRaycastMaxDistance, lastGroundRaycastDistance)
                || !ApproximatelyVector3(groundRaycastOriginOffset, lastGroundRaycastOffset)
                || !Mathf.Approximately(groundLevelPadding, lastGroundLevelPadding);
            bool dustAreaChanged = matchDustToSpawnArea != lastMatchDustToSpawnArea
                || dustAreaOverride != lastDustAreaOverride
                || !ApproximatelyVector2(dustAreaPadding, lastDustAreaPadding);
            bool dustVisualChanged = !ApproximatelyVector2(dustSizeRange, lastDustSizeRange)
                || !ApproximatelyVector2(dustLifetimeRange, lastDustLifetimeRange)
                || !ApproximatelyVector2(dustRotationSpeedRange, lastDustRotationSpeedRange)
                || !Mathf.Approximately(dustNoiseStrength, lastDustNoiseStrength)
                || !Mathf.Approximately(dustNoiseFrequency, lastDustNoiseFrequency)
                || !ApproximatelyVector2(dustNoiseScrollSpeedRange, lastDustNoiseScrollSpeedRange)
                || !GradientsEqual(dustColorGradient, lastDustColorGradient);

            if ((dustToggleChanged || dustPrefabChanged))
            {
                DestroyGroundDustInstance();

                if (enableGroundDust && groundDustPrefab != null)
                {
                    InitializeGroundDust();
                }
            }

            if (enableGroundDust && groundDustSystem != null && (dustAreaChanged || spawnWidthChanged || spawnLengthChanged || groundConstraintChanged))
            {
                ConfigureDustShape();
            }

            if (dustVisualChanged && groundDustSystem != null)
            {
                ConfigureGroundDustAppearance();
            }

            if (spawnHeightChanged || spawnWidthChanged || spawnLengthChanged)
            {
                ApplySpawnAreaSettings();
            }

            if (groundChanged || groundConstraintChanged || spawnHeightChanged || spawnWidthChanged || spawnLengthChanged)
            {
                ConfigureVisuals();
            }

            if (positionChanged || groundChanged || groundConstraintChanged || dustOffsetChanged || dustAreaChanged || dustToggleChanged || dustPrefabChanged)
            {
                PositionGroundDust();
            }

            if (positionChanged || groundChanged || groundConstraintChanged || spawnHeightChanged || spawnWidthChanged || spawnLengthChanged || dustOffsetChanged || dustToggleChanged || dustPrefabChanged || dustAreaChanged || dustVisualChanged)
            {
                CacheLastConfiguration();
            }
        }

        private void DestroyGroundDustInstance()
        {
            if (groundDustSystem == null)
            {
                return;
            }

            if (groundDustDisableRoutine != null)
            {
                StopCoroutine(groundDustDisableRoutine);
                groundDustDisableRoutine = null;
            }

            if (Application.isPlaying)
            {
                Destroy(groundDustSystem.gameObject);
            }
            else
            {
                DestroyImmediate(groundDustSystem.gameObject);
            }

            groundDustSystem = null;
        }

        private void PositionGroundDust()
        {
            if (groundDustSystem == null)
            {
                return;
            }

            Transform dustTransform = groundDustSystem.transform;
            dustTransform.position = ComputeDustWorldCenter();

            if (!matchDustToSpawnArea && dustAreaOverride != null)
            {
                Vector3 euler = dustAreaOverride.transform.rotation.eulerAngles;
                dustTransform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            }
            else
            {
                dustTransform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
        }

        private float CalculateFallDistance()
        {
            Vector3 spawnCenterWorld = transform.TransformPoint(new Vector3(0f, spawnHeight, 0f));
            return Mathf.Max(0f, spawnCenterWorld.y - groundLevel);
        }

        private static float ComputeFallTime(float distance, float initialSpeed, float acceleration)
        {
            distance = Mathf.Max(0f, distance);
            initialSpeed = Mathf.Max(0f, initialSpeed);
            acceleration = Mathf.Max(0f, acceleration);

            if (distance <= 0.0001f)
            {
                return 0.05f;
            }

            if (acceleration <= 0.0001f)
            {
                return initialSpeed > 0.0001f ? distance / initialSpeed : 1f;
            }

            float discriminant = initialSpeed * initialSpeed + 2f * acceleration * distance;
            float sqrt = Mathf.Sqrt(Mathf.Max(0f, discriminant));
            float time = (-initialSpeed + sqrt) / acceleration;

            if (time <= 0f)
            {
                time = (-initialSpeed - sqrt) / acceleration;
            }

            return Mathf.Max(0.05f, time);
        }

        private void ConfigureRenderer()
        {
            var renderer = ps != null ? ps.GetComponent<ParticleSystemRenderer>() : GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                return;
            }

            // CRITICAL: Use Mesh render mode for 3D rotation (not Billboard/Facing)
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.World;

            if (renderer.sortMode == ParticleSystemSortMode.None)
            {
                renderer.sortMode = ParticleSystemSortMode.Distance;
            }
        }

        private void ConfigureVisuals()
        {
            // Ensure gradient has usable defaults so we do not create bland white debris
            if (colorGradient == null || colorGradient.colorKeys.Length == 0)
            {
                colorGradient = CreateDefaultGradient();
            }

            var sortedSize = EnsureOrderedRange(startSizeRange, 0.05f);
            EnsureOrderedRange(startLifetimeRange, 0.2f); // Retained for inspector clarity
            var sortedSpeed = EnsureOrderedRange(startSpeedRange, 0f);
            var sortedGravity = EnsureOrderedRange(gravityModifierRange, 0f);

            // Main module configuration for more natural chunk variety
            main.startSize3D = use3DRandomSize;
            if (use3DRandomSize)
            {
                EnsureVectorOrdering(ref startSize3DMin, ref startSize3DMax);
                main.startSizeX = new ParticleSystem.MinMaxCurve(startSize3DMin.x, startSize3DMax.x);
                main.startSizeY = new ParticleSystem.MinMaxCurve(startSize3DMin.y, startSize3DMax.y);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(startSize3DMin.z, startSize3DMax.z);
            }
            else
            {
                main.startSize = new ParticleSystem.MinMaxCurve(sortedSize.x, sortedSize.y);
            }
            // Calculate lifetimes so debris expires precisely at the configured ground height
            float fallDistance = CalculateFallDistance();
            float gravityMagnitude = Mathf.Abs(Physics.gravity.y);
            float minGravityMultiplier = Mathf.Max(0.05f, sortedGravity.x);
            float maxGravityMultiplier = Mathf.Max(minGravityMultiplier, sortedGravity.y);
            float minGravityAccel = gravityMagnitude * minGravityMultiplier;
            float maxGravityAccel = gravityMagnitude * maxGravityMultiplier;
            float minStartSpeed = Mathf.Max(0f, sortedSpeed.x);
            float maxStartSpeed = Mathf.Max(minStartSpeed, sortedSpeed.y);

            float fastestTime = ComputeFallTime(fallDistance, maxStartSpeed, maxGravityAccel);
            float slowestTime = ComputeFallTime(fallDistance, minStartSpeed, minGravityAccel);

            main.startLifetime = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.1f, fastestTime * 0.9f),
                Mathf.Max(0.1f, slowestTime * 1.1f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(sortedSpeed.x, sortedSpeed.y);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(sortedGravity.x, sortedGravity.y);
            main.startColor = new ParticleSystem.MinMaxGradient(colorGradient);

            // ENHANCED: Enable 3D rotation for realistic tumbling
            main.startRotation3D = true;
            // CRITICAL: Set initial rotation values for 3D mode (random rotation on all axes)
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); // 0-360 degrees
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            // ENHANCED: Dramatic 3D tumbling motion for realistic debris
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.separateAxes = true; // Enable per-axis rotation
            float minAngular = Mathf.Deg2Rad * angularVelocityRange.x;
            float maxAngular = Mathf.Deg2Rad * angularVelocityRange.y;
            if (minAngular > maxAngular)
            {
                float temp = minAngular;
                minAngular = maxAngular;
                maxAngular = temp;
            }
            // Randomize rotation on all axes for chaotic tumbling
            rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(minAngular * 0.7f, maxAngular * 0.7f);
            rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(minAngular * 0.5f, maxAngular * 0.5f);
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(minAngular, maxAngular);

            // ENHANCED: Add size over lifetime for more dramatic visuals
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.separateAxes = false;
            // Subtle size variation: slight grow at start, slight shrink before impact for dust effect
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.9f);     // Start slightly smaller (spawn effect)
            sizeCurve.AddKey(0.15f, 1.05f);   // Grow slightly (visible pop-in)
            sizeCurve.AddKey(0.85f, 1.0f);    // Hold normal size during fall
            sizeCurve.AddKey(1.0f, 0.8f);     // Shrink before impact (dust/break-apart effect)
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // ENHANCED: Add color over lifetime for dust/fading effect
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient colorLifetimeGradient = new Gradient();
            colorLifetimeGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),       // Start: normal color
                    new GradientColorKey(new Color(0.9f, 0.85f, 0.8f), 0.7f), // Mid-fall: slight dust tint
                    new GradientColorKey(new Color(0.7f, 0.65f, 0.6f), 1f)    // Impact: dusty/faded
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),     // Fully visible at start
                    new GradientAlphaKey(1f, 0.85f),  // Solid during fall
                    new GradientAlphaKey(0.6f, 1f)    // Fade slightly on impact (dust effect)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorLifetimeGradient);

            // ENHANCED: Add velocity damping for realistic deceleration near ground
            var limitVelocityOverLifetime = ps.limitVelocityOverLifetime;
            limitVelocityOverLifetime.enabled = true;
            limitVelocityOverLifetime.separateAxes = false;
            limitVelocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            // Gradually reduce velocity in last 30% of lifetime (realistic air resistance/tumbling)
            AnimationCurve dampingCurve = new AnimationCurve();
            dampingCurve.AddKey(0.0f, 20f);    // High speed limit at start (no constraint)
            dampingCurve.AddKey(0.7f, 15f);    // Maintain speed mid-fall
            dampingCurve.AddKey(1.0f, 5f);     // Slow down to ~5 m/s near impact (realistic terminal velocity reduction)
            limitVelocityOverLifetime.dampen = 0.5f; // 50% damping strength for smooth deceleration
            limitVelocityOverLifetime.limit = new ParticleSystem.MinMaxCurve(1f, dampingCurve);

            // ENHANCED: Add lateral drift with slight upward force for debris bounce/scatter
            var velocityOverLifetime = ps.velocityOverLifetime;
            if (horizontalDrift > 0f)
            {
                velocityOverLifetime.enabled = true;
                velocityOverLifetime.space = ParticleSystemSimulationSpace.World;

                // CRITICAL: All velocity curves must be in the same mode (constant)
                velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-horizontalDrift, horizontalDrift);
                velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f); // Slight vertical variation
                velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-horizontalDrift, horizontalDrift);
            }
            else
            {
                velocityOverLifetime.enabled = false;
            }

            // ENHANCED: Stronger noise for more chaotic, realistic debris motion
            var noise = ps.noise;
            if (noiseStrength > 0f)
            {
                noise.enabled = true;
                noise.separateAxes = true;
                noise.strength = new ParticleSystem.MinMaxCurve(noiseStrength * 0.8f, noiseStrength * 1.2f);
                noise.strengthX = new ParticleSystem.MinMaxCurve(noiseStrength * 0.6f, noiseStrength * 1.0f);
                noise.strengthY = new ParticleSystem.MinMaxCurve(noiseStrength * 0.4f, noiseStrength * 0.8f);
                noise.strengthZ = new ParticleSystem.MinMaxCurve(noiseStrength * 0.6f, noiseStrength * 1.0f);
                noise.frequency = 0.45f; // Slightly higher frequency for jitterier motion
                noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); // Varied scroll for chaos
                noise.damping = true;
                noise.octaveCount = 2; // Multi-octave noise for more natural turbulence
                noise.octaveMultiplier = 0.6f;
                noise.octaveScale = 2.5f;
                noise.quality = ParticleSystemNoiseQuality.High;
                noise.remapEnabled = false;
            }
            else
            {
                noise.enabled = false;
            }
        }

        private void ConfigureGroundDustAppearance()
        {
            if (groundDustSystem == null)
            {
                return;
            }

            var dustMain = groundDustSystem.main;
            var sizeRange = EnsureOrderedRange(dustSizeRange, 0.05f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);

            var lifetimeRange = EnsureOrderedRange(dustLifetimeRange, 0.1f);
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            dustMain.gravityModifier = 0f;
            dustMain.simulationSpace = ParticleSystemSimulationSpace.World;

            Gradient gradient = dustColorGradient != null && dustColorGradient.colorKeys.Length > 0
                ? dustColorGradient
                : CreateDefaultDustGradient();
            dustMain.startColor = new ParticleSystem.MinMaxGradient(gradient);

            var dustRotation = groundDustSystem.rotationOverLifetime;
            float minDustAngular = Mathf.Deg2Rad * dustRotationSpeedRange.x;
            float maxDustAngular = Mathf.Deg2Rad * dustRotationSpeedRange.y;
            if (minDustAngular > maxDustAngular)
            {
                (minDustAngular, maxDustAngular) = (maxDustAngular, minDustAngular);
            }
            dustRotation.enabled = true;
            dustRotation.z = new ParticleSystem.MinMaxCurve(minDustAngular, maxDustAngular);

            var dustNoise = groundDustSystem.noise;
            if (dustNoiseStrength > 0f)
            {
                dustNoise.enabled = true;
                dustNoise.strength = dustNoiseStrength;
                dustNoise.frequency = Mathf.Max(0.01f, dustNoiseFrequency);
                dustNoise.scrollSpeed = new ParticleSystem.MinMaxCurve(
                    Mathf.Min(dustNoiseScrollSpeedRange.x, dustNoiseScrollSpeedRange.y),
                    Mathf.Max(dustNoiseScrollSpeedRange.x, dustNoiseScrollSpeedRange.y));
            }
            else
            {
                dustNoise.enabled = false;
            }

            var dustVelocity = groundDustSystem.velocityOverLifetime;
            dustVelocity.enabled = true;
            dustVelocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            dustVelocity.y = new ParticleSystem.MinMaxCurve(0f, 0.05f);
            dustVelocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        }

        private void ConfigureDustShape()
        {
            if (groundDustSystem == null)
            {
                return;
            }

            var dustShape = groundDustSystem.shape;
            dustShape.enabled = true;
            dustShape.shapeType = ParticleSystemShapeType.Box;
            dustShape.scale = ComputeDustAreaSize();
            dustShape.position = Vector3.zero;
        }

        private Vector3 ComputeDustAreaSize()
        {
            if (!matchDustToSpawnArea && dustAreaOverride != null)
            {
                Vector3 scaled = Vector3.Scale(dustAreaOverride.size, dustAreaOverride.transform.lossyScale);
                return new Vector3(
                    Mathf.Max(0.1f, scaled.x + dustAreaPadding.x * 2f),
                    0.1f,
                    Mathf.Max(0.1f, scaled.z + dustAreaPadding.y * 2f));
            }

            return new Vector3(
                Mathf.Max(0.1f, spawnWidth + dustAreaPadding.x * 2f),
                0.1f,
                Mathf.Max(0.1f, spawnLength + dustAreaPadding.y * 2f));
        }

        private Vector3 ComputeDustWorldCenter()
        {
            if (!matchDustToSpawnArea && dustAreaOverride != null)
            {
                Vector3 boundsCenter = dustAreaOverride.bounds.center;
                return new Vector3(boundsCenter.x, groundLevel + dustHeightOffset, boundsCenter.z);
            }

            Vector3 originWorld = transform.TransformPoint(Vector3.zero);
            return new Vector3(originWorld.x, groundLevel + dustHeightOffset, originWorld.z);
        }

        private bool RefreshGroundHeight()
        {
            if (groundConstraintMode == GroundConstraintMode.ManualHeight)
            {
                return false;
            }

            float computed = ResolveGroundHeight();
            float padded = computed + groundLevelPadding;

            if (!Mathf.Approximately(padded, groundLevel))
            {
                groundLevel = padded;
                return true;
            }

            return false;
        }

        private float ResolveGroundHeight()
        {
            switch (groundConstraintMode)
            {
                case GroundConstraintMode.UseTransform:
                    if (groundHeightReference != null)
                    {
                        return groundHeightReference.position.y;
                    }
                    break;
                case GroundConstraintMode.UseColliderBounds:
                    if (groundBoundsCollider != null)
                    {
                        return groundBoundsCollider.bounds.max.y;
                    }
                    break;
                case GroundConstraintMode.RaycastDown:
                    if (TryResolveGroundViaRaycast(out float raycastHeight))
                    {
                        return raycastHeight;
                    }
                    break;
            }

            return groundLevel - groundLevelPadding;
        }

        private bool TryResolveGroundViaRaycast(out float height)
        {
            height = groundLevel;
            float distance = Mathf.Max(0.1f, groundRaycastMaxDistance);
            Vector3 origin = transform.TransformPoint(groundRaycastOriginOffset);
            origin.y += distance * 0.5f;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, groundRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                return true;
            }

            return false;
        }

        private static bool ApproximatelyVector2(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        private static bool ApproximatelyVector3(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y) && Mathf.Approximately(a.z, b.z);
        }

        private static bool GradientsEqual(Gradient a, Gradient b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            var aColors = a.colorKeys;
            var bColors = b.colorKeys;
            if (aColors.Length != bColors.Length)
            {
                return false;
            }

            for (int i = 0; i < aColors.Length; i++)
            {
                if (aColors[i].color != bColors[i].color || !Mathf.Approximately(aColors[i].time, bColors[i].time))
                {
                    return false;
                }
            }

            var aAlpha = a.alphaKeys;
            var bAlpha = b.alphaKeys;
            if (aAlpha.Length != bAlpha.Length)
            {
                return false;
            }

            for (int i = 0; i < aAlpha.Length; i++)
            {
                if (!Mathf.Approximately(aAlpha[i].alpha, bAlpha[i].alpha) || !Mathf.Approximately(aAlpha[i].time, bAlpha[i].time))
                {
                    return false;
                }
            }

            return true;
        }

        private static Gradient CloneGradient(Gradient source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new Gradient();
            clone.SetKeys(source.colorKeys, source.alphaKeys);
            return clone;
        }

        private static Gradient CreateDefaultDustGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.68f, 0.62f, 0.56f), 0f),
                    new GradientColorKey(new Color(0.53f, 0.48f, 0.43f), 0.55f),
                    new GradientColorKey(new Color(0.4f, 0.37f, 0.34f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.45f, 0f),
                    new GradientAlphaKey(0.35f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Vector2 EnsureOrderedRange(Vector2 range, float minimum)
        {
            if (range.x > range.y)
            {
                (range.x, range.y) = (range.y, range.x);
            }
            range.x = Mathf.Max(minimum, range.x);
            range.y = Mathf.Max(range.x, range.y);
            return range;
        }

        private static Gradient CreateDefaultGradient()
        {
            // ENHANCED: More realistic concrete/rock debris colors
            // Color palette inspired by real earthquake debris: concrete gray, rebar rust, dust
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    // Start: Fresh concrete/rock with slight tan/dust (brighter, more visible)
                    new GradientColorKey(new Color(0.72f, 0.65f, 0.58f), 0f),

                    // Mid-life: Aged concrete with dark weathering and rust stains
                    new GradientColorKey(new Color(0.48f, 0.42f, 0.38f), 0.35f),

                    // Later: Dark concrete/charcoal with reddish-brown rust tint
                    new GradientColorKey(new Color(0.35f, 0.28f, 0.25f), 0.7f),

                    // Impact: Very dark, dusty brown-gray (pre-impact darkening)
                    new GradientColorKey(new Color(0.25f, 0.22f, 0.20f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),      // Fully opaque at spawn
                    new GradientAlphaKey(0.98f, 0.5f), // Solid during fall
                    new GradientAlphaKey(0.85f, 0.9f), // Slight fade near impact
                    new GradientAlphaKey(0f, 1f)       // Fully transparent at death (ground level)
                });
            return gradient;
        }
        
        private static Vector3 ClampVectorPositive(Vector3 value, float minimum)
        {
            value.x = Mathf.Max(minimum, value.x);
            value.y = Mathf.Max(minimum, value.y);
            value.z = Mathf.Max(minimum, value.z);
            return value;
        }
        
        private static void EnsureVectorOrdering(ref Vector3 min, ref Vector3 max)
        {
            min = new Vector3(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Min(min.z, max.z));
            max = new Vector3(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), Mathf.Max(min.z, max.z));
        }

        #region Impact Smoke Effects

        /// <summary>
        /// Checks debris particles and spawns smoke puffs when they hit the ground.
        /// Uses particle lifetime to detect impacts near ground level.
        /// </summary>
        private void CheckParticleImpacts()
        {
            if (ps == null || impactSmokePrefab == null)
            {
                return;
            }

            int currentCount = ps.particleCount;

            // Initialize buffer on first use
            if (particleBuffer == null || particleBuffer.Length < ps.main.maxParticles)
            {
                particleBuffer = new ParticleSystem.Particle[ps.main.maxParticles];
            }

            // Get all alive particles
            int aliveCount = ps.GetParticles(particleBuffer);

            // Detect particles that just died (hit ground)
            // We track when particle count decreases and check if any particles are near ground
            if (currentCount < lastParticleCount && aliveCount > 0)
            {
                // Check particles near ground level and spawn smoke based on chance
                for (int i = 0; i < aliveCount; i++)
                {
                    ParticleSystem.Particle p = particleBuffer[i];
                    
                    // Check if particle is near ground (within threshold)
                    float distanceToGround = Mathf.Abs(p.position.y - groundLevel);
                    float remainingLifetime = p.remainingLifetime;

                    // Particle is about to die and is near ground = impact!
                    if (distanceToGround < 0.5f && remainingLifetime < 0.2f)
                    {
                        if (Random.value < impactSmokeChance)
                        {
                            SpawnImpactSmoke(p.position);
                        }
                    }
                }
            }

            lastParticleCount = currentCount;
        }

        /// <summary>
        /// Spawns a smoke puff at the impact location with customized scale and color.
        /// </summary>
        private void SpawnImpactSmoke(Vector3 impactPosition)
        {
            // Adjust spawn position to ground level with offset
            Vector3 spawnPos = new Vector3(
                impactPosition.x,
                groundLevel + impactSmokeHeightOffset,
                impactPosition.z
            );

            // Instantiate dust prefab (Unity Dust Storm, Luke Peek smoke, or custom)
            GameObject smokePuff = Instantiate(impactSmokePrefab, spawnPos, Quaternion.identity);

            // Apply random scale
            float scale = Random.Range(impactSmokeScaleRange.x, impactSmokeScaleRange.y);
            smokePuff.transform.localScale = Vector3.one * scale;

            // Apply dust color tint to all particle systems in the prefab
            ParticleSystem[] smokeSystems = smokePuff.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem smokePS in smokeSystems)
            {
                var smokeMain = smokePS.main;
                
                // Multiply existing color with our dust tint
                ParticleSystem.MinMaxGradient currentColor = smokeMain.startColor;
                
                if (currentColor.mode == ParticleSystemGradientMode.Color)
                {
                    smokeMain.startColor = currentColor.color * impactSmokeTint;
                }
                else if (currentColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    smokeMain.startColor = new ParticleSystem.MinMaxGradient(
                        currentColor.colorMin * impactSmokeTint,
                        currentColor.colorMax * impactSmokeTint
                    );
                }
                else if (currentColor.mode == ParticleSystemGradientMode.Gradient)
                {
                    // For gradients, we'll tint the entire gradient
                    Gradient tintedGradient = TintGradient(currentColor.gradient, impactSmokeTint);
                    smokeMain.startColor = new ParticleSystem.MinMaxGradient(tintedGradient);
                }
            }

            // Track for cleanup
            activeSmokePuffs.Add(smokePuff);

            // Auto-destroy if lifetime is set
            if (impactSmokeLifetime > 0f)
            {
                Destroy(smokePuff, impactSmokeLifetime);
            }

            // Enforce max smoke instance limit
            CleanupOldSmokePuffs();
        }

        /// <summary>
        /// Removes destroyed smoke puffs and enforces maximum instance limit.
        /// </summary>
        private void CleanupOldSmokePuffs()
        {
            // Remove null entries (already destroyed)
            activeSmokePuffs.RemoveAll(puff => puff == null);

            // If over limit, destroy oldest instances
            while (activeSmokePuffs.Count > maxActiveSmokeInstances)
            {
                GameObject oldest = activeSmokePuffs[0];
                activeSmokePuffs.RemoveAt(0);
                
                if (oldest != null)
                {
                    Destroy(oldest);
                }
            }
        }

        /// <summary>
        /// Tints a gradient by multiplying each color key with the tint color.
        /// </summary>
        private static Gradient TintGradient(Gradient source, Color tint)
        {
            if (source == null)
            {
                return null;
            }

            Gradient tinted = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[source.colorKeys.Length];
            
            for (int i = 0; i < source.colorKeys.Length; i++)
            {
                colorKeys[i] = new GradientColorKey(
                    source.colorKeys[i].color * tint,
                    source.colorKeys[i].time
                );
            }

            tinted.SetKeys(colorKeys, source.alphaKeys);
            tinted.mode = source.mode;
            
            return tinted;
        }

        /// <summary>
        /// Cleanup all active smoke puffs when debris controller is destroyed.
        /// </summary>
        private void DestroyAllSmokePuffs()
        {
            foreach (GameObject puff in activeSmokePuffs)
            {
                if (puff != null)
                {
                    Destroy(puff);
                }
            }
            
            activeSmokePuffs.Clear();
        }

        #endregion
        
        #region Ground Dust System
        
        private void InitializeGroundDust()
        {
            if (groundDustPrefab == null)
            {
                Debug.LogWarning("[EarthquakeDebrisController] Ground dust prefab not assigned.");
                return;
            }
            DestroyGroundDustInstance();

            groundDustSystem = Instantiate(groundDustPrefab, transform, false);
            groundDustSystem.gameObject.name = $"{groundDustPrefab.name} (GroundDust Runtime)";
            groundDustSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var dustEmission = groundDustSystem.emission;
            dustEmission.rateOverTime = 0f;

            ConfigureDustShape();
            ConfigureGroundDustAppearance();
            PositionGroundDust();

            groundDustSystem.gameObject.SetActive(false);

            lastGroundDustEnabled = enableGroundDust;
            lastGroundDustPrefab = groundDustPrefab;

            Debug.Log($"[EarthquakeDebrisController] Ground dust initialized at Y={groundLevel + dustHeightOffset:F2}");
        }
        
        private void UpdateGroundDust()
        {
            if (groundDustSystem == null)
            {
                return;
            }

            if (!enableGroundDust)
            {
                if (groundDustSystem.gameObject.activeSelf)
                {
                    if (groundDustDisableRoutine != null)
                    {
                        StopCoroutine(groundDustDisableRoutine);
                        groundDustDisableRoutine = null;
                    }

                    var emissionModule = groundDustSystem.emission;
                    emissionModule.rateOverTime = 0f;
                    groundDustSystem.gameObject.SetActive(false);
                }
                return;
            }

            PositionGroundDust();

            if (isActive && emission.enabled)
            {
                if (!groundDustSystem.gameObject.activeSelf)
                {
                    groundDustSystem.gameObject.SetActive(true);
                }

                if (groundDustDisableRoutine != null)
                {
                    StopCoroutine(groundDustDisableRoutine);
                    groundDustDisableRoutine = null;
                }

                float rampDurationSeconds = Mathf.Max(0.1f, currentRampUpTime);
                float intensityProgress = Mathf.Clamp01(elapsedTime / rampDurationSeconds);
                float adjustedDustRate = dustEmissionRate * intensityProgress * Mathf.Clamp01(scenarioProgressMultiplier);

                var dustEmission = groundDustSystem.emission;
                dustEmission.rateOverTime = adjustedDustRate;
            }
            else if (groundDustSystem.gameObject.activeSelf)
            {
                var dustEmission = groundDustSystem.emission;
                dustEmission.rateOverTime = 0f;

                if (groundDustDisableRoutine == null)
                {
                    groundDustDisableRoutine = StartCoroutine(DisableGroundDustAfterClear());
                }
            }
        }
        
        private System.Collections.IEnumerator DisableGroundDustAfterClear()
        {
            yield return new WaitForSeconds(2f); // Wait for particles to dissipate
            
            if (groundDustSystem != null)
            {
                groundDustSystem.gameObject.SetActive(false);
            }

            groundDustDisableRoutine = null;
        }
        
        #endregion
    }
}
