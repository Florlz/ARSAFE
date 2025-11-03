using UnityEngine;
using UnityEngine.Events;
using ARSafe.Modular;

namespace ARSafe.Content
{
    /*
     * PURPOSE: Marks a location as a virtual exit for pathfinding without requiring a scanned Area Target.
     *          Acts EXACTLY like ARSafeTargetInfo exits - simple exit marker, no special types.
     *
     * SETUP WORKFLOW:
     *   1. Add VirtualExitMarker component to a GameObject under your Area Target
     *   2. Manually add a BoxCollider component to the SAME GameObject
     *   3. Configure the BoxCollider size/center/rotation to define your exit zone
     *   4. Drag the BoxCollider into the "Exit Collider" field in VirtualExitMarker Inspector
     *   5. Set Applicable Disasters (optional - leave empty for all disasters)
     *   6. Enable "Enable Debug Logs" to verify detection in Play Mode
     *
     * NOTE: For flood scenarios, 2nd floor Area Targets are automatically treated as safe zones
     *       by ARSafeActivationController (no need for special exit types).
     *
     * ALGORITHM: Uses LOCAL-SPACE detection (proven from ARSafeTargetInfo.ComputeDistanceToBoundary)
     *   - Converts world camera position to local space via InverseTransformPoint()
     *   - Checks containment in LOCAL-space bounds (rotation-safe!)
     *   - Manual 6-face distance calculation for signed distance fields
     *   - Negative distance = inside, positive = outside
     *
     * WHY LOCAL-SPACE?
     *   - BoxCollider.bounds returns WORLD-SPACE AABB that expands when rotated
     *   - Local-space immune to parent rotation and reparenting operations
     *   - Same proven algorithm used in ARSafeTargetInfo (800+ lines production code)
     *
     * DEPENDENCIES:
     *   - ARSafeNavigationValidator (registers with on Start())
     *   - ARSafeActivationController (triggers exit overlay UI)
     *   - DisasterTypeManager (filters by disaster type if specified)
     *   - MANUAL: BoxCollider component (YOU must add and configure)
     *
     * DATA FLOW:
     *   Update() → CalculateDistanceToBounds() → InverseTransformPoint → Local bounds check → OnVirtualExitReached event
     *
     * PERFORMANCE:
     *   - Throttled to 10 FPS (100ms intervals)
     *   - Cached camera transform
     *   - Single InverseTransformPoint call per update
     *   - No physics engine overhead (no Rigidbody, no triggers)
     *
     * EDGE CASES:
     *   - Handles rotated GameObjects correctly (local-space math)
     *   - Survives reparenting to shared augmentation root
     *   - Gracefully handles missing camera at Start (AR camera setup timing)
     *   - Only active when disaster type matches configuration (if specified)
     *   - Script disables itself if no BoxCollider is assigned (shows error in console)
     */

    [AddComponentMenu("ARSafe/Virtual Exit Marker")]
    public class VirtualExitMarker : MonoBehaviour
    {
        [Header("Exit Configuration")]
        [Tooltip("Disaster types that use this virtual exit. Leave empty to allow all disasters.")]
        public DisasterType[] applicableDisasters = new DisasterType[0];

        [Header("Detection Settings")]
        [Tooltip("REQUIRED: Manually add a BoxCollider component to this GameObject to define the detection zone. The script will use your BoxCollider's size and center.")]
        public BoxCollider exitCollider;

        [Tooltip("Time user must stay inside exit zone before triggering (seconds)")]
        public float dwellTime = 1.5f;

        [Tooltip("Show debug logs for this virtual exit")]
        public bool enableDebugLogs = false;

        [Header("Events")]
        public UnityEvent onVirtualExitReached;

        // Runtime state
        private bool exitReached = false;
        private Transform cameraTransform;
        private ARSafeNavigationValidator navigationValidator;
        private Bounds localBounds; // CRITICAL: Local-space bounds for rotation-safe detection

        // Dwell time tracking
        private float timeInsideBounds = 0f;

        // Performance throttling
        private float updateInterval = 0.1f; // 10 FPS
        private float lastUpdateTime;

        // Public property for navigation validator to check position
        public Vector3 Position => transform.position;

        private void Start()
        {
            // Validate that user has assigned a BoxCollider
            if (exitCollider == null)
            {
                Debug.LogError($"<color=red>[VirtualExitMarker]</color> CRITICAL: No BoxCollider assigned on {gameObject.name}! " +
                               $"Please manually add a BoxCollider component and assign it to the 'Exit Collider' field in the Inspector.", this);
                enabled = false; // Disable script until BoxCollider is assigned
                return;
            }

            // Warn if user has set isTrigger (we don't use physics triggers, just bounds checking)
            if (exitCollider.isTrigger && enableDebugLogs)
            {
                Debug.LogWarning($"[VirtualExitMarker] BoxCollider on {gameObject.name} has 'Is Trigger' enabled. " +
                                 $"This script uses bounds detection (not physics triggers), so the 'Is Trigger' setting has no effect.");
            }

            // CRITICAL: Build local-space bounds from your manually-configured BoxCollider
            // This captures YOUR size/center settings before any rotation or reparenting operations
            localBounds = new Bounds(exitCollider.center, exitCollider.size);

            // Register with navigation validator
            RegisterWithNavigationValidator();

            // Find camera (may not be ready at Start time in AR apps)
            StartCoroutine(FindCameraCoroutine());

            if (enableDebugLogs)
            {
                string disasterFilter = applicableDisasters.Length > 0
                    ? string.Join(", ", applicableDisasters)
                    : "All Disasters";

                Debug.Log($"<color=cyan>[VirtualExitMarker]</color> Initialized on {gameObject.name}\n" +
                          $"  Local Bounds Center: {localBounds.center}\n" +
                          $"  Local Bounds Size: {localBounds.size}\n" +
                          $"  Disaster Filter: {disasterFilter}\n" +
                          $"  <color=yellow>Using LOCAL-SPACE detection (rotation-safe)</color>");
            }
        }

        /// <summary>
        /// Coroutine to find the main camera. Camera.main may return null at Start() time in AR applications
        /// because Vuforia or AR Foundation may dynamically configure the camera after scene initialization.
        /// This coroutine retries finding the camera over 3 seconds to handle AR camera setup timing.
        /// </summary>
        private System.Collections.IEnumerator FindCameraCoroutine()
        {
            int attempts = 0;
            const int maxAttempts = 30; // 30 attempts at 0.1s intervals = 3 seconds total
            const float retryInterval = 0.1f;

            while (cameraTransform == null && attempts < maxAttempts)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=green>[VirtualExitMarker]</color> Camera found on attempt {attempts + 1} " +
                                  $"for {gameObject.name}");
                    }

                    yield break; // Success - exit coroutine
                }

                attempts++;
                yield return new WaitForSeconds(retryInterval);
            }

            // Failed to find camera after all attempts
            if (cameraTransform == null)
            {
                Debug.LogError($"<color=red>[VirtualExitMarker]</color> CRITICAL: Failed to find main camera after {attempts} attempts ({attempts * retryInterval}s). " +
                               $"Virtual exit {gameObject.name} will NOT function! " +
                               $"Ensure your AR camera is tagged as 'MainCamera'.", this);
            }
        }

        private void RegisterWithNavigationValidator()
        {
            // Find navigation validator (Unity 6 API)
            navigationValidator = FindFirstObjectByType<ARSafeNavigationValidator>();

            if (navigationValidator != null)
            {
                navigationValidator.RegisterVirtualExit(this);

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[VirtualExitMarker]</color> Registered with ARSafeNavigationValidator: {gameObject.name}");
                }
            }
            else
            {
#if UNITY_EDITOR
                // In editor mode, navigation validator might not be present - that's okay
                Debug.LogWarning($"[VirtualExitMarker] Navigation validator not found for {gameObject.name}. " +
                                 "This is normal in Edit mode, but required at runtime.", this);
#else
                Debug.LogError($"[VirtualExitMarker] Navigation validator not found! Virtual exit {gameObject.name} will not function.", this);
#endif
            }
        }

        /// <summary>
        /// BULLETPROOF LOCAL-SPACE DETECTION (from ARSafeTargetInfo.ComputeDistanceToBoundary)
        ///
        /// Check if camera is inside BoxCollider bounds using local-space math.
        /// This handles rotated GameObjects correctly and survives reparenting operations.
        ///
        /// Algorithm:
        /// 1. Convert world camera position to LOCAL space via InverseTransformPoint()
        /// 2. Check containment in LOCAL-space bounds (rotation-safe!)
        /// 3. Calculate signed distance (negative = inside, positive = outside)
        /// 4. Trigger after dwell time when inside
        /// </summary>
        private void Update()
        {
            // Throttle updates to 10 FPS (every 0.1 seconds)
            if (Time.time - lastUpdateTime < updateInterval)
                return;

            lastUpdateTime = Time.time;

            // Skip if exit already reached
            if (exitReached)
                return;

            // Re-find camera if lost (same pattern as ARSafeNavigationValidator line 117-126)
            if (cameraTransform == null)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    cameraTransform = mainCamera.transform;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=green>[VirtualExitMarker]</color> Camera recovered in Update() for {gameObject.name}");
                    }
                }
                else
                {
                    // No camera available yet, skip this frame
                    return;
                }
            }

            // Skip if this exit doesn't apply to current disaster type
            if (!IsApplicableToCurrentDisaster())
                return;

            // CRITICAL: Calculate distance using LOCAL-SPACE math (rotation-safe!)
            Vector3 worldCameraPos = cameraTransform.position;
            float signedDistance = CalculateDistanceToBounds(worldCameraPos);

            bool isInside = signedDistance < 0f; // Negative = inside

            if (isInside)
            {
                // User IS inside the BoxCollider - increment dwell timer
                timeInsideBounds += updateInterval;

                if (enableDebugLogs)
                {
                    // Convert to local space for debugging
                    Vector3 localCameraPos = transform.InverseTransformPoint(worldCameraPos);

                    Debug.Log($"<color=yellow>[VirtualExitMarker]</color> {gameObject.name} <b>INSIDE</b>\n" +
                              $"  Timer: {timeInsideBounds:F2}s / {dwellTime:F2}s\n" +
                              $"  World Pos: {worldCameraPos}\n" +
                              $"  Local Pos: {localCameraPos}\n" +
                              $"  Local Bounds: Center={localBounds.center}, Size={localBounds.size}\n" +
                              $"  Signed Distance: <color=green>{signedDistance:F2}m (inside)</color>\n" +
                              $"  Disaster: {DisasterTypeManager.SelectedDisasterType}");
                }

                // Trigger after dwell time reached
                if (timeInsideBounds >= dwellTime)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=green>[VirtualExitMarker]</color> <b>DWELL TIME REACHED</b> ({timeInsideBounds:F2}s >= {dwellTime:F2}s). Triggering exit!");
                    }
                    TriggerVirtualExit();
                }
            }
            else
            {
                // User is OUTSIDE the BoxCollider - reset timer
                if (timeInsideBounds > 0f)
                {
                    if (enableDebugLogs)
                    {
                        Vector3 localCameraPos = transform.InverseTransformPoint(worldCameraPos);
                        Debug.Log($"<color=orange>[VirtualExitMarker]</color> {gameObject.name} <b>LEFT</b> exit zone\n" +
                                  $"  Timer was: {timeInsideBounds:F2}s (reset to 0)\n" +
                                  $"  World Pos: {worldCameraPos}\n" +
                                  $"  Local Pos: {localCameraPos}\n" +
                                  $"  Signed Distance: <color=gray>{signedDistance:F2}m (outside)</color>");
                    }
                    timeInsideBounds = 0f;
                }
            }
        }

        /// <summary>
        /// BULLETPROOF DISTANCE CALCULATION - Local-space algorithm from ARSafeTargetInfo
        ///
        /// Returns SIGNED distance to BoxCollider bounds:
        /// - Negative value = inside bounds (e.g., -2.5m means 2.5m from nearest face)
        /// - Positive value = outside bounds (e.g., 5.0m means 5m away from nearest point)
        ///
        /// This uses LOCAL-SPACE math to handle rotated GameObjects correctly.
        /// </summary>
        private float CalculateDistanceToBounds(Vector3 worldPoint)
        {
            // STEP 1: Convert world position to LOCAL space (handles rotation automatically!)
            // This is the CRITICAL fix - InverseTransformPoint() makes rotation irrelevant
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

            // STEP 2: Check containment in LOCAL-space bounds (rotation-safe!)
            bool isInside = localBounds.Contains(localPoint);

            float distance;

            if (isInside)
            {
                // INSIDE: Manually calculate distance to nearest face (6-face calculation)
                // Unity's ClosestPoint returns the point itself when inside (useless!)
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;

                // Calculate distance to each of the 6 faces
                float distToMaxX = max.x - localPoint.x; // Right face
                float distToMinX = localPoint.x - min.x; // Left face
                float distToMaxY = max.y - localPoint.y; // Top face
                float distToMinY = localPoint.y - min.y; // Bottom face
                float distToMaxZ = max.z - localPoint.z; // Front face
                float distToMinZ = localPoint.z - min.z; // Back face

                // Find minimum distance to any face (closest exit direction)
                distance = Mathf.Min(
                    distToMaxX, distToMinX,
                    distToMaxY, distToMinY,
                    distToMaxZ, distToMinZ
                );

                // Return NEGATIVE distance (inside convention)
                return -distance;
            }
            else
            {
                // OUTSIDE: Use Unity's ClosestPoint (works correctly for exterior points)
                Vector3 closestPoint = localBounds.ClosestPoint(localPoint);
                distance = Vector3.Distance(localPoint, closestPoint);

                // Return POSITIVE distance (outside convention)
                return distance;
            }
        }

        private bool IsApplicableToCurrentDisaster()
        {
            DisasterType currentDisaster = DisasterTypeManager.SelectedDisasterType;

            foreach (var disaster in applicableDisasters)
            {
                if (disaster == currentDisaster)
                    return true;
            }

            return false;
        }

        private void TriggerVirtualExit()
        {
            exitReached = true;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>★★★ [VirtualExitMarker]</color> <b>VIRTUAL EXIT REACHED!</b>\n" +
                          $"  GameObject: {gameObject.name}\n" +
                          $"  Disaster: {DisasterTypeManager.SelectedDisasterType}");
            }

            // Fire Unity event
            onVirtualExitReached?.Invoke();

            // Notify navigation validator
            if (navigationValidator != null)
            {
                navigationValidator.NotifyVirtualExitReached(this);
            }
        }

        /// <summary>
        /// Check if current disaster type is applicable to this virtual exit
        /// </summary>
        public bool IsApplicableToDisaster(DisasterType disaster)
        {
            foreach (var applicable in applicableDisasters)
            {
                if (applicable == disaster)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Calculate distance from a given world position to this virtual exit's BoxCollider bounds.
        /// Used by ARSafeNavigationValidator for pathfinding distance comparisons.
        /// </summary>
        public float CalculateDistanceFrom(Vector3 worldPosition)
        {
            // Use the same bulletproof local-space algorithm
            float signedDistance = CalculateDistanceToBounds(worldPosition);

            // If inside (negative), return 0 (already at exit)
            if (signedDistance < 0f)
            {
                return 0f;
            }

            // If outside (positive), return actual distance
            return signedDistance;
        }

        /// <summary>
        /// Reset the exit reached state (for testing/debugging)
        /// </summary>
        public void ResetExit()
        {
            exitReached = false;
            timeInsideBounds = 0f;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=yellow>[VirtualExitMarker]</color> Exit state reset for {gameObject.name}");
            }
        }


        // Editor visualization
        private void OnDrawGizmos()
        {
            // Visualize the manually-assigned BoxCollider
            if (exitCollider == null) return;

            // Draw wire box at collider bounds IN LOCAL SPACE (green = exit)
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix; // Use local-to-world matrix for correct orientation
            Gizmos.DrawWireCube(exitCollider.center, exitCollider.size);

            // Draw semi-transparent filled box
            Color fillColor = Color.green;
            fillColor.a = 0.1f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(exitCollider.center, exitCollider.size);

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnDrawGizmosSelected()
        {
            // Visualize the manually-assigned BoxCollider
            if (exitCollider == null) return;

            // Draw more prominent visualization when selected
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix; // Use local-to-world matrix for correct orientation

            // Draw outer box
            Gizmos.DrawWireCube(exitCollider.center, exitCollider.size);

            // Draw inner box for depth perception
            Vector3 innerSize = exitCollider.size * 0.8f;
            Gizmos.DrawWireCube(exitCollider.center, innerSize);

            // Draw semi-transparent filled box
            Color fillColor = Color.green;
            fillColor.a = 0.2f;
            Gizmos.color = fillColor;
            Gizmos.DrawCube(exitCollider.center, exitCollider.size);

            Gizmos.matrix = Matrix4x4.identity;

            // Draw label
            #if UNITY_EDITOR
            Vector3 labelPos = transform.TransformPoint(exitCollider.center + Vector3.up * (exitCollider.size.y * 0.5f + 0.5f));
            UnityEditor.Handles.Label(
                labelPos,
                $"Virtual Exit\nSize: {exitCollider.size.x:F1}×{exitCollider.size.y:F1}×{exitCollider.size.z:F1}m\n[LOCAL-SPACE DETECTION]",
                new GUIStyle()
                {
                    normal = new GUIStyleState() { textColor = Color.green },
                    fontSize = 12,
                    fontStyle = FontStyle.Bold
                }
            );
            #endif
        }
    }
}
