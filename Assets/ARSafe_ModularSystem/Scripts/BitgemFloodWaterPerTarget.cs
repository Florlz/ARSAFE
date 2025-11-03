using UnityEngine;
using Bitgem.VFX.StylisedWater;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ARSafe.Modular
{
    /*
     * ARCHITECTURE PLAN: BitgemFloodWaterPerTarget
     *
     * PURPOSE:
     *   - Animate Bitgem WaterVolumeBox height based on FloodScenarioManager events.
     *   - Each Area Target has its own FloodWater instance that responds to the same shared scenario.
     *   - Multi-area pose system keeps all instances spatially aligned for uniform water level.
     *   - SCALE-AWARE: Respects GameObject scale - height values are in world-space meters, automatically converted to local dimensions.
     *
     * DEPENDENCIES:
     *   - Unity APIs: MonoBehaviour, AnimationCurve, Time, Vector3.
     *   - Project Scripts: FloodScenarioManager (events for parameters/progress).
     *   - External Packages: Bitgem StylisedWater (WaterVolumeBox component).
     *
     * DATA FLOW:
     *   - Input: FloodScenarioParameters (depth, duration) + FloodScenarioProgress (phase, normalized).
     *   - Processing: Calculate target height in world-space meters based on phase curves, convert to local dimensions using scale.
     *   - Output: Update WaterVolumeBox.Dimensions.y (local space), trigger Bitgem mesh rebuild, Unity scales to world space.
     *
     * INTEGRATION POINTS:
     *   - ARSafeActivationController handles multi-area pose (15-30 FPS drift correction).
     *   - All FloodWater instances subscribe to same FloodScenarioManager events → uniform behavior.
     *   - ARSafeDisasterFilter controls visibility (if FloodWater has ARSafeDisasterContent).
     *
     * PERFORMANCE CONSIDERATIONS:
     *   - Update throttled to 30 FPS (matches multi-area pose update rate).
     *   - Bitgem's isDirty flag prevents redundant mesh rebuilds.
     *   - Exponential smoothing for height interpolation (no allocations).
     *   - Cached scale value updated only when needed (not every frame).
     *
     * DEBUG LOGGING:
     *   - Prefix logs with [BitgemFlood] and color per severity (cyan = key events, yellow = warnings).
     */
    public class BitgemFloodWaterPerTarget : MonoBehaviour
    {
        [Header("Bitgem Integration")]
        [Tooltip("WaterVolumeBox component (auto-assigned).")]
        [SerializeField] private WaterVolumeBox waterVolume;

        [Header("Height Configuration")]
        [Tooltip("Baseline water height in WORLD-SPACE METERS (minimal water when inactive). Works correctly regardless of GameObject scale.")]
        [SerializeField] private float baselineHeight = 0.05f;

        [Header("Animation")]
        [Tooltip("Exponential smoothing factor for height changes (higher = faster). Set to 0 to disable smoothing and use direct phase curves.")]
        [SerializeField] private float heightFollowSpeed = 0f; // Direct curve following - no extra smoothing
        [Tooltip("Curve applied during rise phase (0 = start, 1 = peak).")]
        [SerializeField] private AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Curve applied during recede phase (0 = peak, 1 = baseline).")]
        [SerializeField] private AnimationCurve recedeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Performance")]
        [Tooltip("Minimum change in water height (meters) before rebuilding the Bitgem mesh. Lower values rebuild more often but cost more CPU.")]
        [SerializeField] private float rebuildThresholdMeters = 0.02f;

        [Header("Debug")]
        [Tooltip("Enable detailed debug logging.")]
        [SerializeField] private bool enableDebugLogs = true;

        // Runtime state
        private FloodScenarioParameters activeParameters;
        private FloodScenarioProgress activeProgress;
        private float currentHeight;  // World-space height in meters
        private float targetHeight;   // World-space height in meters
        private bool hasActiveScenario;
        private Vector2 baseDimensions; // Store X, Z from initial setup (local dimensions)
        private Vector3 cachedScale;    // Cached lossyScale to avoid Transform access every frame
        private MeshRenderer meshRenderer; // Cached for visibility control
        private float lastRebuildHeightMeters = -1f;
    private static readonly FieldInfo WaterVolumeIsDirtyField = typeof(WaterVolumeBase).GetField("isDirty", BindingFlags.Instance | BindingFlags.NonPublic);

        private void Reset()
        {
            waterVolume = GetComponent<WaterVolumeBox>();
        }

        private void Awake()
        {
            if (waterVolume == null)
            {
                waterVolume = GetComponent<WaterVolumeBox>();
            }

            if (waterVolume == null)
            {
                Debug.LogError($"<color=orange>[BitgemFlood] No WaterVolumeBox found on {name}! This component requires WaterVolumeBox.</color>");
                enabled = false;
                return;
            }

            // Cache MeshRenderer for visibility control
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogWarning($"<color=yellow>[BitgemFlood] No MeshRenderer found on {name}. Visibility control disabled.</color>");
            }

            // Ensure ARSafeDisasterContent exists for modular system integration
            EnsureDisasterContentTag();

            // Cache base dimensions (X, Z should stay constant - these are local dimensions)
            baseDimensions = new Vector2(waterVolume.Dimensions.x, waterVolume.Dimensions.z);

            // Cache scale for world-to-local conversion
            cachedScale = transform.lossyScale;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[BitgemFlood] Initialized on {name}. Local dimensions: {baseDimensions.x}x{baseDimensions.y}, baseline height: {baselineHeight}m (world-space), scale: {cachedScale.x:F2}x{cachedScale.y:F2}x{cachedScale.z:F2}</color>");
            }
        }

        /// <summary>
        /// Ensure this GameObject has ARSafeDisasterContent tag for modular system integration.
        /// This allows ARSafeDisasterFilter to manage visibility based on disaster type.
        /// </summary>
        private void EnsureDisasterContentTag()
        {
            var disasterContent = GetComponent<ARSafeDisasterContent>();
            if (disasterContent == null)
            {
                disasterContent = gameObject.AddComponent<ARSafeDisasterContent>();
                disasterContent.disasterType = DisasterType.Flood;
                disasterContent.contentDescription = $"Flood water for {name}";
                Debug.Log($"<color=cyan>[BitgemFlood] Added ARSafeDisasterContent (Flood) to {name} for modular system integration</color>");
            }
            else if (disasterContent.disasterType != DisasterType.Flood)
            {
                Debug.LogWarning($"<color=yellow>[BitgemFlood] {name} has ARSafeDisasterContent but disasterType is {disasterContent.disasterType}, should be Flood</color>");
            }
        }

        private void OnEnable()
        {
            SubscribeScenarioEvents(true);

            // Initialize to baseline
            currentHeight = baselineHeight;
            targetHeight = baselineHeight;
            ApplyDimensions(baselineHeight);

            // Sync with current scenario state in case we subscribed after the initial broadcasts.
            // Without this, we could miss OnParametersUpdated and keep hasActiveScenario=false,
            // causing progress updates to be ignored (water stays at baseline).
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
                return; // Don't update when no active scenario
            }

            // For smooth animation at full frame rate (60+ FPS)
            float oldHeight = currentHeight;

            if (heightFollowSpeed > 0.01f)
            {
                // Optional smoothing via exponential interpolation
                float lerpFactor = 1f - Mathf.Exp(-heightFollowSpeed * Time.deltaTime);
                currentHeight = Mathf.Lerp(currentHeight, targetHeight, lerpFactor);
            }
            else
            {
                // Direct assignment - follow curves exactly with no extra smoothing
                // FloodScenarioManager updates progress every frame, giving smooth curve evaluation
                currentHeight = targetHeight;
            }

            // Only rebuild mesh when height actually changes
            if (Mathf.Abs(currentHeight - oldHeight) > 0.001f)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[BitgemFlood] {name} → Update: currentHeight {oldHeight:F3}m → {currentHeight:F3}m (target {targetHeight:F3}m)</color>");
                }

                // Apply to Bitgem water volume
                ApplyDimensions(currentHeight);
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
            activeParameters = parameters;
            hasActiveScenario = parameters.IsActive;

            if (!parameters.IsActive)
            {
                targetHeight = baselineHeight;
                currentHeight = baselineHeight;
                lastRebuildHeightMeters = -1f;
                
                // Reset to baseline immediately when scenario ends
                ApplyDimensions(baselineHeight);
                
                // Note: Don't hide meshRenderer here - ARSafeDisasterFilter handles visibility
                // based on disaster type selection. We only control animation.
                
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[BitgemFlood] {name} → Scenario inactive, reset to baseline height {baselineHeight:F2}m</color>");
                }
                return;
            }

            // Scenario is active - start from baseline
            currentHeight = baselineHeight;
            targetHeight = baselineHeight;
            lastRebuildHeightMeters = -1f;
            ApplyDimensions(baselineHeight);

            Debug.Log($"<color=cyan>[BitgemFlood] ★★★ {name} → Scenario started: depth {parameters.TargetDepthMeters:F2}m, rise {parameters.RiseDurationSeconds:F1}s, hold {parameters.HoldDurationSeconds:F1}s, recede {parameters.RecedeDurationSeconds:F1}s</color>");
            
            // Note: ARSafeDisasterFilter + ARSafeDisasterContent control visibility
            // based on DisasterTypeManager.SelectedDisasterType = Flood
            // We focus only on animating the water height
        }

        private void HandleProgressUpdated(FloodScenarioProgress progress)
        {
            activeProgress = progress;

            if (!hasActiveScenario || !activeParameters.IsActive)
            {
                targetHeight = baselineHeight;
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[BitgemFlood] {name} → HandleProgressUpdated called but hasActiveScenario={hasActiveScenario}, activeParameters.IsActive={activeParameters.IsActive}, ignoring progress</color>");
                }
                return;
            }

            float depth = activeParameters.TargetDepthMeters;
            float evaluated;
            float oldTargetHeight = targetHeight;

            switch (progress.Phase)
            {
                case FloodScenarioPhase.Rising:
                    evaluated = riseCurve.Evaluate(progress.PhaseNormalized);
                    targetHeight = baselineHeight + depth * evaluated;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[BitgemFlood] {name} → RISING phase {progress.PhaseNormalized:F2} → targetHeight {oldTargetHeight:F2}m → {targetHeight:F2}m (depth {depth:F2}m, evaluated {evaluated:F2})</color>");
                    }
                    break;

                case FloodScenarioPhase.Sustained:
                    targetHeight = baselineHeight + depth;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[BitgemFlood] {name} → SUSTAINED phase {progress.PhaseNormalized:F2} → targetHeight {targetHeight:F2}m (peak depth)</color>");
                    }
                    break;

                case FloodScenarioPhase.Receding:
                    evaluated = recedeCurve.Evaluate(progress.PhaseNormalized);
                    targetHeight = baselineHeight + depth * evaluated;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[BitgemFlood] {name} → RECEDING phase {progress.PhaseNormalized:F2} → targetHeight {targetHeight:F2}m (evaluated {evaluated:F2})</color>");
                    }
                    break;

                case FloodScenarioPhase.Complete:
                case FloodScenarioPhase.Inactive:
                default:
                    targetHeight = baselineHeight;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=yellow>[BitgemFlood] {name} → {progress.Phase} phase → targetHeight {targetHeight:F2}m (baseline)</color>");
                    }
                    break;
            }
        }

        /// <summary>
        /// Apply new Y dimension to Bitgem WaterVolumeBox and trigger mesh rebuild.
        /// SCALE-AWARE: Converts world-space height (meters) to local-space dimension based on GameObject scale.
        /// </summary>
        /// <param name="worldHeight">Desired height in world-space meters</param>
        private void ApplyDimensions(float worldHeight)
        {
            if (waterVolume == null)
            {
                return;
            }

            cachedScale = transform.lossyScale;

            // Convert world-space height to local-space dimension
            // Example: If GameObject scale.y = 26, and we want 2m world height:
            //   localHeight = 2 / 26 = 0.077
            //   When rendered, Unity scales 0.077 * 26 = 2m (correct!)
            float clampedWorldHeight = Mathf.Max(0.001f, worldHeight);
            float localHeight = cachedScale.y > 0.001f ? clampedWorldHeight / cachedScale.y : clampedWorldHeight;

            // Update dimensions (keep X, Z constant, change Y to local height)
            waterVolume.Dimensions = new Vector3(baseDimensions.x, localHeight, baseDimensions.y);

            // Validate dimensions (Bitgem will auto-rebuild in its Update() method)
            waterVolume.Validate();

            if (Mathf.Abs(clampedWorldHeight - lastRebuildHeightMeters) >= rebuildThresholdMeters || lastRebuildHeightMeters < 0f)
            {
                if (!MarkWaterVolumeDirty())
                {
                    waterVolume.Rebuild();
                }

                lastRebuildHeightMeters = clampedWorldHeight;

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[BitgemFlood] {name} → Marked water volume dirty (height {clampedWorldHeight:F3}m)</color>");
                }
            }
        }

        /// <summary>
        /// Public API: Set baseline height (for editor tools or manual control).
        /// Height is in WORLD-SPACE METERS and respects GameObject scale.
        /// </summary>
        public void SetBaselineHeight(float height)
        {
            baselineHeight = Mathf.Max(0.01f, height); // Bitgem needs >0
            targetHeight = baselineHeight;
            currentHeight = baselineHeight;
            lastRebuildHeightMeters = -1f;

            // Update cached scale in case it changed
            cachedScale = transform.lossyScale;

            ApplyDimensions(baselineHeight);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[BitgemFlood] {name} baseline height set to {baselineHeight:F2}m (world-space)</color>");
            }
        }

        /// <summary>
        /// Public API: Manually set target depth (for testing).
        /// Depth is in WORLD-SPACE METERS and respects GameObject scale.
        /// </summary>
        public void SetManualTargetDepth(float depth)
        {
            targetHeight = baselineHeight + Mathf.Max(0f, depth);

            // Update cached scale in case it changed
            cachedScale = transform.lossyScale;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[BitgemFlood] {name} manual target depth set to {depth:F2}m (target height: {targetHeight:F2}m, world-space)</color>");
            }
        }

        /// <summary>
        /// Mark the Bitgem volume as dirty so it rebuilds on the next WaterVolumeBase.Update call.
        /// Uses reflection to access the protected flag; falls back to direct Rebuild() if unavailable.
        /// </summary>
        private bool MarkWaterVolumeDirty()
        {
            if (WaterVolumeIsDirtyField == null)
            {
                return false;
            }

            try
            {
                WaterVolumeIsDirtyField.SetValue(waterVolume, true);
                return true;
            }
            catch (System.Exception ex)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"<color=yellow>[BitgemFlood] {name} → Failed to mark water volume dirty via reflection: {ex.Message}</color>");
                }
                return false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (waterVolume == null)
            {
                return;
            }

            // Get the MeshRenderer to find the actual mesh bounds center
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                return;
            }

            // Use the actual rendered mesh bounds center (this accounts for everything: position, rotation, scale)
            Bounds meshBounds = meshRenderer.bounds;
            Vector3 meshCenter = meshBounds.center;
            Vector3 meshSize = meshBounds.size;

            // Draw current water volume box (cyan = actual current state, matches mesh exactly)
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireCube(meshCenter, meshSize);
            Gizmos.DrawCube(meshCenter, meshSize * 0.98f); // Filled box slightly smaller

            // Draw baseline height reference (green = minimum water level)
            if (Application.isPlaying)
            {
                Vector3 worldScale = transform.lossyScale;
                float baselineLocalHeight = worldScale.y > 0.001f ? baselineHeight / worldScale.y : baselineHeight;
                float baselineWorldHeight = baselineLocalHeight * worldScale.y;

                Vector3 baselineSize = new Vector3(meshSize.x, baselineWorldHeight, meshSize.z);
                Vector3 baselineCenter = new Vector3(meshCenter.x, meshCenter.y - (meshSize.y - baselineWorldHeight) * 0.5f, meshCenter.z);

                Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
                Gizmos.DrawWireCube(baselineCenter, baselineSize);
            }

            // Draw target height reference (yellow = flood target)
            if (Application.isPlaying && targetHeight > baselineHeight)
            {
                Vector3 worldScale = transform.lossyScale;
                float targetLocalHeight = worldScale.y > 0.001f ? targetHeight / worldScale.y : targetHeight;
                float targetWorldHeight = targetLocalHeight * worldScale.y;

                Vector3 targetSize = new Vector3(meshSize.x, targetWorldHeight, meshSize.z);
                Vector3 targetCenter = new Vector3(meshCenter.x, meshCenter.y - (meshSize.y - targetWorldHeight) * 0.5f, meshCenter.z);

                Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
                Gizmos.DrawWireCube(targetCenter, targetSize);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Setup Water Transparency")]
        private void SetupTransparency()
        {
            // Check if transparency controller exists
            var controller = GetComponent<BitgemWaterTransparencyController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<BitgemWaterTransparencyController>();
                Debug.Log($"<color=green>[BitgemFlood] ✓ Added BitgemWaterTransparencyController to {name}</color>");
            }
            else
            {
                Debug.Log($"<color=cyan>[BitgemFlood] BitgemWaterTransparencyController already exists on {name}</color>");
            }

            // Get material and configure transparency
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                var mat = renderer.sharedMaterial;
                
                // Enable transparency in material
                mat.SetFloat("_Surface", 1); // 1 = Transparent
                mat.SetFloat("_Blend", 0); // 0 = Alpha blend
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0); // Disable ZWrite for transparency
                mat.SetFloat("_AlphaClip", 0); // Disable alpha clipping
                
                // Enable transparency keywords
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                
                // Set render queue to transparent
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                
                Debug.Log($"<color=green>[BitgemFlood] ✓ Configured material transparency on {name}</color>");
                
                EditorUtility.SetDirty(mat);
            }
            else
            {
                Debug.LogWarning($"<color=yellow>[BitgemFlood] No MeshRenderer or material found on {name}</color>");
            }

            EditorUtility.SetDirty(this);
        }
#endif
    }
}
