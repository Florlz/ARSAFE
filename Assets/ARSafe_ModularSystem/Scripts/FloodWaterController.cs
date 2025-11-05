using UnityEngine;

namespace ARSafe.Modular
{
    /*
     * ARCHITECTURE PLAN: FloodWaterController
     *
     * PURPOSE:
     *   - Animate flood water rising from floor (0m) to knee level (~0.6m) based on FloodScenarioManager events
     *   - Simple quad/plane mesh with custom muddy water shader
     *   - Integrates with ARSafeDisasterFilter for visibility control
     *   - References sibling arrow GameObject for user guidance (arrows stay at fixed height)
     *   - SCALE-AWARE: Works with scaled plane meshes (respects Transform scale)
     *
     * DEPENDENCIES:
     *   - Unity APIs: MonoBehaviour, Transform, MeshRenderer, Material, MaterialPropertyBlock
     *   - Project Scripts: FloodScenarioManager (events), ARSafeDisasterContent (disaster tag)
     *   - Shader: ARSafe/MuddyWater URP (custom transparent water shader)
     *
     * DATA FLOW:
     *   - Input: FloodScenarioParameters (duration, colors, wave params), FloodScenarioProgress (phase, normalized)
     *   - Processing: Calculate Y position based on rise curve (0m → knee level), update shader properties per-instance
     *   - Output: Update Transform.position.y (water mesh only), MaterialPropertyBlock shader params, arrow visibility
     *
     * INTEGRATION POINTS:
     *   - ARSafeDisasterFilter controls overall GameObject visibility based on disaster type
     *   - Sibling arrows (separate GameObject) shown/hidden based on water level reaching knee height
     *   - FloodScenarioManager broadcasts progress every frame for smooth animation
     *   - MaterialPropertyBlock ensures per-instance shader control (multiple water planes supported)
     *
     * PERFORMANCE CONSIDERATIONS:
     *   - Direct position updates (no mesh rebuilding)
     *   - Simple quad mesh (4 vertices default, scales with plane scale)
     *   - Shader handles wave animation on GPU (FBM noise, vertex displacement)
     *   - MaterialPropertyBlock avoids material instantiation (zero allocations)
     *   - Exponential smoothing for framerate-independent interpolation
     *
     * DEBUG LOGGING:
     *   - Prefix logs with [FloodWater] and color per severity (cyan = events, green = updates)
     */
    [AddComponentMenu("ARSafe/Modular/Flood Water Controller")]
    public class FloodWaterController : MonoBehaviour
    {
        [Header("Water Levels (World Space Meters)")]
        [Tooltip("Starting height (floor level). Water starts hidden below this.")]
        [SerializeField] private float floorHeight = 0f;
        
        [Tooltip("Target knee level height in meters (average adult knee ~0.6m).")]
        [SerializeField] private float kneeHeight = 0.6f;
        
        [Tooltip("Height below floor where water starts hidden (-0.5m = half meter below floor).")]
        [SerializeField] private float hiddenBelowFloor = -0.5f;
        
        [Header("Animation")]
        [Tooltip("Rise animation curve (0=floor, 1=knee). Use EaseInOut for smooth natural rise.")]
        [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("Recede animation curve (0=knee, 1=floor).")]
        [SerializeField] private AnimationCurve recedeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        
        [Header("Animation")]
        [Tooltip("Smoothing speed for height changes (higher = faster response).")]
        [SerializeField] private float heightSmoothSpeed = 8f;

        [Header("Arrow Guidance")]
        [Tooltip("Parent GameObject containing arrow meshes/sprites. Shown when water reaches knee level.")]
        [SerializeField] private GameObject arrowsParent;

        [Tooltip("Delay in seconds before showing arrows after reaching knee level.")]
        [SerializeField] private float arrowShowDelay = 1f;

        [Header("Shader Integration")]
        [Tooltip("Auto-update shader properties from FloodScenarioManager parameters.")]
        [SerializeField] private bool autoUpdateShaderParams = true;

        [Header("Debug")]
        [Tooltip("Enable detailed debug logging.")]
        [SerializeField] private bool enableDebugLogs = true;

        // Runtime state
        private FloodScenarioParameters activeParameters;
        private FloodScenarioProgress activeProgress;
        private float currentHeight; // Current Y position in world space
        private float targetHeight;  // Target Y position in world space
        private bool hasActiveScenario;
        private Vector3 initialPosition; // Cached starting position
        private bool arrowsShown;
        private float kneeReachedTime = -1f;

        // Shader control (per-instance)
        private MeshRenderer waterRenderer;
        private MaterialPropertyBlock propertyBlock;
        private bool shaderPropertiesInitialized;

        // Shader property IDs (cached for performance)
        private static readonly int TurbidityId = Shader.PropertyToID("_Turbidity");
        private static readonly int WaveSpeedId = Shader.PropertyToID("_WaveSpeed");
        private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
        private static readonly int DeepColorId = Shader.PropertyToID("_DeepColor");
        private static readonly int ShallowColorId = Shader.PropertyToID("_ShallowColor");
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");
        private static readonly int FlowDirectionId = Shader.PropertyToID("_FlowDirection");
        
        private void Awake()
        {
            // Cache initial position
            initialPosition = transform.position;

            // Initialize MaterialPropertyBlock for per-instance shader control
            InitializeShaderSystem();

            // Ensure ARSafeDisasterContent exists for modular system integration
            EnsureDisasterContentTag();

            // Hide arrows initially (they're siblings of FloodWater, not children)
            if (arrowsParent != null)
            {
                arrowsParent.SetActive(false);
                arrowsShown = false;

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[FloodWater] Arrows linked (will be shown at knee level). NOTE: Arrows should be sibling of FloodWater to stay at fixed height.</color>");
                }
            }

            // Start hidden below floor
            currentHeight = hiddenBelowFloor;
            targetHeight = hiddenBelowFloor;
            UpdateWaterPosition(hiddenBelowFloor);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[FloodWater] {name} initialized. Floor: {floorHeight:F2}m, Knee: {kneeHeight:F2}m, Hidden at: {hiddenBelowFloor:F2}m, Shader system: {(shaderPropertiesInitialized ? "✓" : "✗")}</color>");
            }
        }
        
        private void OnEnable()
        {
            SubscribeScenarioEvents(true);
            
            // Sync with current scenario state
            var currentParams = FloodScenarioManager.CurrentParameters;
            HandleParametersUpdated(currentParams);
            var currentProgress = FloodScenarioManager.CurrentProgress;
            HandleProgressUpdated(currentProgress);
        }
        
        private void OnDisable()
        {
            SubscribeScenarioEvents(false);
        }
        
        private void Update()
        {
            if (!hasActiveScenario)
            {
                return;
            }

            // Framerate-independent exponential smoothing
            float lerpFactor = 1f - Mathf.Exp(-heightSmoothSpeed * Time.deltaTime);
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, lerpFactor);
            UpdateWaterPosition(currentHeight);

            // Check if we should show arrows (arrows are siblings, so they stay at fixed height automatically)
            if (!arrowsShown && currentHeight >= kneeHeight - 0.05f)
            {
                if (kneeReachedTime < 0f)
                {
                    kneeReachedTime = Time.time;
                }
                else if (Time.time - kneeReachedTime >= arrowShowDelay)
                {
                    ShowArrows();
                }
            }
        }
        
        private void SubscribeScenarioEvents(bool subscribe)
        {
            if (subscribe)
            {
                FloodScenarioManager.OnParametersUpdated += HandleParametersUpdated;
                FloodScenarioManager.OnProgressUpdated += HandleProgressUpdated;
            }
            else
            {
                FloodScenarioManager.OnParametersUpdated -= HandleParametersUpdated;
                FloodScenarioManager.OnProgressUpdated -= HandleProgressUpdated;
            }
        }
        
        private void HandleParametersUpdated(FloodScenarioParameters parameters)
        {
            bool wasActive = hasActiveScenario;
            activeParameters = parameters;
            hasActiveScenario = parameters.IsActive;

            if (!parameters.IsActive)
            {
                // Scenario ended - hide water below floor
                targetHeight = hiddenBelowFloor;
                currentHeight = hiddenBelowFloor;
                UpdateWaterPosition(hiddenBelowFloor);
                HideArrows();
                kneeReachedTime = -1f;

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[FloodWater] {name} → Scenario inactive, water hidden below floor</color>");
                }
                return;
            }

            // Only reset water position on INITIAL scenario start, not during level transitions
            if (!wasActive && parameters.IsActive)
            {
                // Scenario just started - reset to hidden position
                currentHeight = hiddenBelowFloor;
                targetHeight = hiddenBelowFloor;
                UpdateWaterPosition(hiddenBelowFloor);
                HideArrows();
                kneeReachedTime = -1f;

                Debug.Log($"<color=cyan>[FloodWater] ★★★ {name} → Scenario started! Water will rise to target depth ({parameters.TargetDepthMeters:F2}m)</color>");
            }
            else
            {
                // Level transition (Yellow→Orange→Red) - don't reset, let water continue rising smoothly
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[FloodWater] {name} → Level transition, new target depth: {parameters.TargetDepthMeters:F2}m</color>");
                }
            }

            // Auto-update shader parameters from scenario (always update for new visual params)
            if (autoUpdateShaderParams)
            {
                UpdateShaderParametersFromScenario(parameters);
            }
        }
        
        private void HandleProgressUpdated(FloodScenarioProgress progress)
        {
            activeProgress = progress;
            
            if (!hasActiveScenario || !activeParameters.IsActive)
            {
                targetHeight = hiddenBelowFloor;
                return;
            }
            
            float evaluated;
            
            switch (progress.Phase)
            {
                case FloodScenarioPhase.Rising:
                    // Rise from floor to target depth (from parameters - changes with warning level)
                    evaluated = riseCurve.Evaluate(progress.PhaseNormalized);
                    float targetDepth = activeParameters.TargetDepthMeters;
                    targetHeight = Mathf.Lerp(floorHeight, targetDepth, evaluated);

                    if (enableDebugLogs && (progress.PhaseNormalized < 0.15f || progress.PhaseNormalized > 0.85f))
                    {
                        Debug.Log($"<color=cyan>[FloodWater] {name} → RISING progress={progress.PhaseNormalized:F3}, curve={evaluated:F3}, target={targetHeight:F3}m (depth={targetDepth:F2}m), current={currentHeight:F3}m</color>");
                    }
                    break;

                case FloodScenarioPhase.Sustained:
                    // Hold at peak level (from parameters)
                    targetHeight = activeParameters.TargetDepthMeters;

                    if (enableDebugLogs && progress.PhaseNormalized < 0.1f)
                    {
                        Debug.Log($"<color=cyan>[FloodWater] {name} → SUSTAINED at peak level ({activeParameters.TargetDepthMeters:F2}m)</color>");
                    }
                    break;
                
                case FloodScenarioPhase.Receding:
                    // Recede from peak level back to floor, then hide
                    evaluated = recedeCurve.Evaluate(progress.PhaseNormalized);
                    targetHeight = Mathf.Lerp(activeParameters.TargetDepthMeters, hiddenBelowFloor, evaluated);

                    if (enableDebugLogs && (progress.PhaseNormalized < 0.1f || progress.PhaseNormalized > 0.9f))
                    {
                        Debug.Log($"<color=cyan>[FloodWater] {name} → RECEDING {progress.PhaseNormalized:F2} → target {targetHeight:F2}m</color>");
                    }

                    // Hide arrows when receding
                    if (progress.PhaseNormalized > 0.1f)
                    {
                        HideArrows();
                    }
                    break;
                
                case FloodScenarioPhase.Complete:
                case FloodScenarioPhase.Inactive:
                default:
                    targetHeight = hiddenBelowFloor;
                    HideArrows();
                    break;
            }
        }
        
        /// <summary>
        /// Update water plane Y position in world space
        /// </summary>
        private void UpdateWaterPosition(float worldHeightY)
        {
            Vector3 pos = transform.position;
            pos.y = worldHeightY;
            transform.position = pos;
        }
        
        /// <summary>
        /// Show arrow guidance when water reaches knee level
        /// </summary>
        private void ShowArrows()
        {
            if (arrowsParent != null && !arrowsShown)
            {
                arrowsParent.SetActive(true);
                arrowsShown = true;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[FloodWater] ✓ {name} → Arrows shown! Water at knee level, guiding user to higher ground</color>");
                }
            }
        }
        
        /// <summary>
        /// Hide arrow guidance
        /// </summary>
        private void HideArrows()
        {
            if (arrowsParent != null && arrowsShown)
            {
                arrowsParent.SetActive(false);
                arrowsShown = false;
                kneeReachedTime = -1f;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[FloodWater] {name} → Arrows hidden</color>");
                }
            }
        }
        
        /// <summary>
        /// Ensure ARSafeDisasterContent exists for modular system integration
        /// </summary>
        private void EnsureDisasterContentTag()
        {
            var disasterContent = GetComponent<ARSafeDisasterContent>();
            if (disasterContent == null)
            {
                disasterContent = gameObject.AddComponent<ARSafeDisasterContent>();
                disasterContent.disasterType = DisasterType.Flood;
                disasterContent.contentDescription = $"Flood water for {name}";
                
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[FloodWater] Added ARSafeDisasterContent (Flood) to {name}</color>");
                }
            }
        }
        
        /// <summary>
        /// Initialize MaterialPropertyBlock for per-instance shader control
        /// </summary>
        private void InitializeShaderSystem()
        {
            waterRenderer = GetComponent<MeshRenderer>();
            if (waterRenderer != null)
            {
                propertyBlock = new MaterialPropertyBlock();
                shaderPropertiesInitialized = true;

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[FloodWater] ✓ {name} shader system initialized (MaterialPropertyBlock ready)</color>");
                }
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[FloodWater] {name} → No MeshRenderer found! Shader updates disabled.</color>");
                shaderPropertiesInitialized = false;
            }
        }

        /// <summary>
        /// Update shader parameters from FloodScenarioManager parameters using MaterialPropertyBlock (per-instance)
        /// </summary>
        private void UpdateShaderParametersFromScenario(FloodScenarioParameters parameters)
        {
            if (!shaderPropertiesInitialized || waterRenderer == null || propertyBlock == null)
            {
                return;
            }

            // Get existing property block
            waterRenderer.GetPropertyBlock(propertyBlock);

            // Update properties from scenario
            propertyBlock.SetFloat(TurbidityId, parameters.Turbidity);
            propertyBlock.SetFloat(WaveSpeedId, parameters.FlowSpeedMultiplier * 0.5f);
            propertyBlock.SetFloat(WaveAmplitudeId, parameters.WaveAmplitudeMultiplier * 0.1f);
            propertyBlock.SetColor(DeepColorId, parameters.DeepWaterColor);
            propertyBlock.SetColor(ShallowColorId, parameters.ShallowWaterColor);
            propertyBlock.SetColor(FoamColorId, parameters.FoamColor);

            // Apply property block
            waterRenderer.SetPropertyBlock(propertyBlock);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[FloodWater] ✓ {name} shader updated: turbidity={parameters.Turbidity:F2}, wave speed={parameters.FlowSpeedMultiplier:F2}x, wave amplitude={parameters.WaveAmplitudeMultiplier:F2}x</color>");
            }
        }

        /// <summary>
        /// Public API: Manually update shader parameters (for testing/external control)
        /// </summary>
        public void SetShaderTurbidity(float turbidity)
        {
            if (!shaderPropertiesInitialized) return;
            waterRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(TurbidityId, Mathf.Clamp01(turbidity));
            waterRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Public API: Set wave parameters manually
        /// </summary>
        public void SetWaveParameters(float speed, float amplitude)
        {
            if (!shaderPropertiesInitialized) return;
            waterRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(WaveSpeedId, Mathf.Max(0f, speed));
            propertyBlock.SetFloat(WaveAmplitudeId, Mathf.Max(0f, amplitude));
            waterRenderer.SetPropertyBlock(propertyBlock);
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw floor level (green)
            Gizmos.color = Color.green;
            Vector3 floorPos = transform.position;
            floorPos.y = floorHeight;
            Gizmos.DrawWireCube(floorPos, new Vector3(1f, 0.01f, 1f));
            
            // Draw knee level (yellow)
            Gizmos.color = Color.yellow;
            Vector3 kneePos = transform.position;
            kneePos.y = kneeHeight;
            Gizmos.DrawWireCube(kneePos, new Vector3(1f, 0.01f, 1f));
            
            // Draw hidden level (red)
            Gizmos.color = Color.red;
            Vector3 hiddenPos = transform.position;
            hiddenPos.y = hiddenBelowFloor;
            Gizmos.DrawWireCube(hiddenPos, new Vector3(1f, 0.01f, 1f));
            
            // Draw current height (cyan - runtime only)
            if (Application.isPlaying)
            {
                Gizmos.color = Color.cyan;
                Vector3 currentPos = transform.position;
                currentPos.y = currentHeight;
                Gizmos.DrawWireCube(currentPos, new Vector3(1.2f, 0.02f, 1.2f));
            }
        }
    }
}
