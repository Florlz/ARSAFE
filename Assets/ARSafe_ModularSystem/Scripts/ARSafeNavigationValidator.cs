using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace ARSafe.Modular
{
    /*
     * PURPOSE: Validates user navigation during emergencies - detects when moving away from nearest exit
     * DEPENDENCIES: ARSafeActivationController, ARSafeTargetInfo, Camera.main
     * DATA FLOW: Current anchor → BFS pathfinding → Movement tracking → Wrong-way detection → Event broadcast
     * PERFORMANCE: 10-15 FPS throttled, cached pathfinding, reused collections
     * EDGE CASES: No exits in scene, anchor switches, tracking loss, stationary user
     */

    /// <summary>
    /// Validates user navigation to nearest exit using graph-based pathfinding.
    /// Tracks movement history and detects when user is moving away from safety.
    /// Optimized for mobile with 10-15 FPS updates and cached pathfinding results.
    /// </summary>
    public class ARSafeNavigationValidator : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Activation controller (auto-found)")]
        private ARSafeActivationController activationController;

        [Tooltip("AR Camera (auto-found)")]
        private Camera arCamera;

        [Header("Movement Detection")]
        [Tooltip("Number of position samples to track for movement detection")]
        [Range(3, 10)]
        public int positionHistorySize = 5;

        [Tooltip("Minimum movement speed (m/s) to consider user as moving")]
        [Range(0.1f, 2f)]
        public float minimumMovementSpeed = 0.3f;

        [Tooltip("Angle threshold (degrees) to SHOW warning - movement is 'away' if angle > this value (90° = perpendicular)")]
        [Range(60f, 120f)]
        public float wrongWayAngleThreshold = 90f;

        [Tooltip("Angle threshold (degrees) to HIDE warning - warning clears when angle < this value (hysteresis prevents flickering)")]
        [Range(50f, 90f)]
        public float wrongWayAngleClearThreshold = 70f;

        [Tooltip("Time (seconds) user must move wrong way before warning triggers. Lower = faster warning.")]
        [Range(0.5f, 10f)]
        public float wrongWayDwellTime = 1.0f;

        [Header("Pathfinding")]
        [Tooltip("Cache pathfinding results for this many seconds")]
        [Range(1f, 10f)]
        public float pathfindingCacheDuration = 3f;

        [Header("Exit Detection")]
        [Tooltip("Distance threshold (meters) to consider user has reached exit")]
        [Range(1f, 10f)]
        public float exitProximityThreshold = 3f;

        [Header("Performance")]
        [Tooltip("Target update frequency (FPS)")]
        [Range(5f, 30f)]
        public float updateFrequency = 12f;

        [Header("Debug")]
        public bool enableDebugLogs = false;
        public bool drawDebugGizmos = false;

        // Movement tracking
        private Vector3[] positionHistory;
        private float[] positionTimestamps;
        private int historyIndex = 0;
        private bool historyFilled = false;

        // Pathfinding cache
        private ARSafeTargetInfo cachedNearestExit;
        private List<ARSafeTargetInfo> cachedPath;
        private float lastPathfindingTime = -999f;
        private ObserverBehaviour lastPathfindingAnchor;

        // State
        private bool isMovingAwayFromExit = false;
        private bool hasReachedExit = false;
        private float lastUpdateTime = 0f;
        private float wrongWayStartTime = -999f;
        private bool wasMovingWrongWayLastFrame = false;

        // Reusable collections (avoid GC)
        private Queue<PathNode> bfsQueue = new Queue<PathNode>();
        private HashSet<ARSafeTargetInfo> visited = new HashSet<ARSafeTargetInfo>();
        private Dictionary<ARSafeTargetInfo, PathNode> parentMap = new Dictionary<ARSafeTargetInfo, PathNode>();

        // Virtual exits support
        private List<Content.VirtualExitMarker> virtualExits = new List<Content.VirtualExitMarker>();
        private Content.VirtualExitMarker nearestVirtualExit;

        // Events
        public delegate void WrongWayDetectedHandler(bool movingWrongWay);
        public event WrongWayDetectedHandler OnWrongWayStatusChanged;

        public delegate void ExitReachedHandler(ARSafeTargetInfo exitTarget);
        public event ExitReachedHandler OnExitReached;

        public delegate void VirtualExitReachedHandler(Content.VirtualExitMarker exitMarker);
        public event VirtualExitReachedHandler OnVirtualExitReached;

        private class PathNode
        {
            public ARSafeTargetInfo target;
            public PathNode parent;
            public int distance;

            public PathNode(ARSafeTargetInfo target, PathNode parent, int distance)
            {
                this.target = target;
                this.parent = parent;
                this.distance = distance;
            }
        }

        void Awake()
        {
            activationController = FindFirstObjectByType<ARSafeActivationController>();
            arCamera = Camera.main;

            // Initialize position history
            positionHistory = new Vector3[positionHistorySize];
            positionTimestamps = new float[positionHistorySize];
            cachedPath = new List<ARSafeTargetInfo>();
        }

        void Update()
        {
            // PERFORMANCE: Throttle to target FPS (10-15 FPS)
            float updateInterval = 1f / Mathf.Max(1f, updateFrequency);
            if (Time.time - lastUpdateTime < updateInterval)
            {
                return;
            }

            lastUpdateTime = Time.time;

            // Require active system and localization
            if (activationController == null || !activationController.HasLocalized)
            {
                return;
            }

            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null) return;
            }

            // Sample camera position
            SamplePosition(arCamera.transform.position);

            // Check if we have enough history
            if (!historyFilled)
            {
                return;
            }

            // Detect movement
            bool userIsMoving;
            Vector3 movementDirection;
            CalculateMovement(out userIsMoving, out movementDirection);

            if (!userIsMoving)
            {
                // User is stationary - no warning needed
                if (isMovingAwayFromExit)
                {
                    SetWrongWayStatus(false);
                }
                return;
            }

            // Check virtual exits first (they take priority if closer)
            Content.VirtualExitMarker nearestVirtual = GetNearestVirtualExit();

            // Find path to nearest real exit
            ARSafeTargetInfo nearestExit = GetNearestExit();

            // Determine which exit to navigate towards (virtual or real)
            bool usingVirtualExit = false;
            Vector3 targetExitPosition = Vector3.zero;
            float distanceToExit = float.MaxValue;

            if (nearestVirtual != null && nearestExit != null)
            {
                // Both exist - compare distances
                float distanceToVirtual = nearestVirtual.CalculateDistanceFrom(arCamera.transform.position);
                float distanceToReal = Vector3.Distance(arCamera.transform.position, nearestExit.transform.position);

                if (distanceToVirtual < distanceToReal)
                {
                    usingVirtualExit = true;
                    targetExitPosition = nearestVirtual.Position;
                    distanceToExit = distanceToVirtual;
                }
                else
                {
                    targetExitPosition = nearestExit.transform.position;
                    distanceToExit = distanceToReal;
                }
            }
            else if (nearestVirtual != null)
            {
                // Only virtual exit available
                usingVirtualExit = true;
                targetExitPosition = nearestVirtual.Position;
                distanceToExit = nearestVirtual.CalculateDistanceFrom(arCamera.transform.position);
            }
            else if (nearestExit != null)
            {
                // Only real exit available
                targetExitPosition = nearestExit.transform.position;
                distanceToExit = Vector3.Distance(arCamera.transform.position, nearestExit.transform.position);
            }
            else
            {
                // No exits at all
                if (enableDebugLogs)
                {
                    Debug.LogWarning("[ARSafeNavigationValidator] No exits (real or virtual) found in scene!");
                }
                return;
            }

            // Check if user has reached the exit (virtual or real)
            if (!hasReachedExit && distanceToExit <= exitProximityThreshold)
            {
                hasReachedExit = true;

                if (enableDebugLogs)
                {
                    string exitName = usingVirtualExit ? nearestVirtual.gameObject.name : nearestExit.name;
                    Debug.Log($"<color=green>[ARSafeNavigationValidator] ✓ {(usingVirtualExit ? "VIRTUAL " : "")}EXIT REACHED! " +
                              $"Name: {exitName}, Distance: {distanceToExit:F2}m (threshold: {exitProximityThreshold}m)</color>");
                }

                // Fire appropriate event
                if (usingVirtualExit)
                {
                    OnVirtualExitReached?.Invoke(nearestVirtual);
                    OnExitReached?.Invoke(null); // Also fire standard event for compatibility
                }
                else
                {
                    OnExitReached?.Invoke(nearestExit);
                }

                // Stop tracking once exit is reached
                return;
            }

            // If already reached exit, don't check wrong-way status
            if (hasReachedExit)
            {
                return;
            }

            // Calculate if moving away from exit (use position, works for both real and virtual exits)
            bool movingAway = IsMovingAwayFromTarget(movementDirection, targetExitPosition);

            // Sustained wrong-way detection - require user to be moving wrong for dwell time
            if (movingAway)
            {
                // User is moving wrong way
                if (!wasMovingWrongWayLastFrame)
                {
                    // Just started moving wrong way
                    wrongWayStartTime = Time.time;
                    wasMovingWrongWayLastFrame = true;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=yellow>[ARSafeNavigationValidator] User started moving wrong way (dwell time: {wrongWayDwellTime}s)</color>");
                    }
                }
                else
                {
                    // Still moving wrong way - check if dwell time exceeded
                    float wrongWayDuration = Time.time - wrongWayStartTime;
                    if (!isMovingAwayFromExit && wrongWayDuration >= wrongWayDwellTime)
                    {
                        // Dwell time exceeded - trigger warning
                        SetWrongWayStatus(true);

                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=red>[ARSafeNavigationValidator] Wrong-way dwell time exceeded ({wrongWayDuration:F1}s) - triggering warning!</color>");
                        }
                    }
                }
            }
            else
            {
                // User is moving correct way
                if (wasMovingWrongWayLastFrame)
                {
                    // Just corrected direction
                    wasMovingWrongWayLastFrame = false;
                    wrongWayStartTime = -999f;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=green>[ARSafeNavigationValidator] User corrected direction</color>");
                    }
                }

                // Clear warning if it was active
                if (isMovingAwayFromExit)
                {
                    SetWrongWayStatus(false);
                }
            }
        }

        /// <summary>
        /// Sample camera position into circular buffer
        /// </summary>
        private void SamplePosition(Vector3 position)
        {
            positionHistory[historyIndex] = position;
            positionTimestamps[historyIndex] = Time.time;

            historyIndex = (historyIndex + 1) % positionHistorySize;

            if (historyIndex == 0)
            {
                historyFilled = true;
            }
        }

        /// <summary>
        /// Calculate movement direction and speed from position history
        /// </summary>
        private void CalculateMovement(out bool isMoving, out Vector3 direction)
        {
            // Get oldest and newest samples
            int oldestIndex = historyFilled ? historyIndex : 0;
            int newestIndex = historyFilled ? ((historyIndex - 1 + positionHistorySize) % positionHistorySize) : (historyIndex - 1);

            if (newestIndex < 0)
            {
                isMoving = false;
                direction = Vector3.zero;
                return;
            }

            Vector3 oldestPos = positionHistory[oldestIndex];
            Vector3 newestPos = positionHistory[newestIndex];
            float timeDelta = positionTimestamps[newestIndex] - positionTimestamps[oldestIndex];

            if (timeDelta <= 0f)
            {
                isMoving = false;
                direction = Vector3.zero;
                return;
            }

            // Calculate displacement and speed
            Vector3 displacement = newestPos - oldestPos;
            float speed = displacement.magnitude / timeDelta;

            isMoving = speed >= minimumMovementSpeed;
            direction = displacement.normalized;

            if (enableDebugLogs && isMoving)
            {
                Debug.Log($"[ARSafeNavigationValidator] Movement detected: Speed={speed:F2}m/s, Direction={direction}");
            }
        }

        /// <summary>
        /// Check if movement direction is away from target (beyond angle threshold)
        /// </summary>
        private bool IsMovingAwayFromTarget(Vector3 movementDirection, ARSafeTargetInfo targetExit)
        {
            if (targetExit == null)
            {
                return false;
            }

            return IsMovingAwayFromTarget(movementDirection, targetExit.transform.position);
        }

        /// <summary>
        /// Check if movement direction is away from target position (beyond angle threshold)
        /// Overload that works with Vector3 positions (for virtual exits)
        /// </summary>
        private bool IsMovingAwayFromTarget(Vector3 movementDirection, Vector3 targetPosition)
        {
            if (movementDirection == Vector3.zero)
            {
                return false;
            }

            // Get current anchor position
            var currentAnchor = activationController.GetCurrentAnchor();
            if (currentAnchor == null)
            {
                return false;
            }

            // Calculate direction to exit from current anchor
            Vector3 toExit = (targetPosition - currentAnchor.transform.position);
            toExit.y = 0; // Ignore vertical component
            toExit.Normalize();

            // Normalize movement direction (ignore vertical)
            Vector3 flatMovement = movementDirection;
            flatMovement.y = 0;
            flatMovement.Normalize();

            // Calculate angle between movement and exit direction
            float angle = Vector3.Angle(flatMovement, toExit);

            // Apply hysteresis to prevent flickering:
            // - Use higher threshold (wrongWayAngleThreshold) to trigger warning
            // - Use lower threshold (wrongWayAngleClearThreshold) to clear warning
            bool movingAway;
            if (isMovingAwayFromExit)
            {
                // Currently showing warning - use lower threshold to clear (sticky behavior)
                movingAway = angle > wrongWayAngleClearThreshold;
            }
            else
            {
                // Not showing warning - use higher threshold to trigger
                movingAway = angle > wrongWayAngleThreshold;
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeNavigationValidator] Angle to exit: {angle:F1}° {(movingAway ? "<color=red>WRONG WAY!</color>" : "<color=green>Correct</color>")}");
            }

            return movingAway;
        }

        /// <summary>
        /// Find nearest exit using BFS pathfinding through adjacency graph
        /// Results are cached to avoid expensive recalculations
        /// </summary>
        public ARSafeTargetInfo GetNearestExit()
        {
            var currentAnchor = activationController?.GetCurrentAnchor();
            if (currentAnchor == null)
            {
                return null;
            }

            // Check cache validity
            bool cacheValid = cachedNearestExit != null
                && lastPathfindingAnchor == currentAnchor
                && (Time.time - lastPathfindingTime) < pathfindingCacheDuration;

            if (cacheValid)
            {
                return cachedNearestExit;
            }

            // Run BFS to find nearest exit
            var anchorInfo = currentAnchor.GetComponent<ARSafeTargetInfo>();
            if (anchorInfo == null)
            {
                return null;
            }

            ARSafeTargetInfo nearestExit = FindNearestExitBFS(anchorInfo);

            // Update cache
            cachedNearestExit = nearestExit;
            lastPathfindingAnchor = currentAnchor;
            lastPathfindingTime = Time.time;

            if (enableDebugLogs && nearestExit != null)
            {
                Debug.Log($"<color=cyan>[ARSafeNavigationValidator] Nearest exit: {nearestExit.name} (cached for {pathfindingCacheDuration}s)</color>");
            }

            return nearestExit;
        }

        /// <summary>
        /// Breadth-First Search to find nearest exit through adjacency graph
        /// OPTIMIZED: Reuses collections to avoid GC allocations
        /// </summary>
        private ARSafeTargetInfo FindNearestExitBFS(ARSafeTargetInfo startTarget)
        {
            // OPTIMIZATION: Clear reusable collections instead of creating new ones
            bfsQueue.Clear();
            visited.Clear();
            parentMap.Clear();
            cachedPath.Clear();

            // Initialize BFS
            PathNode startNode = new PathNode(startTarget, null, 0);
            bfsQueue.Enqueue(startNode);
            visited.Add(startTarget);
            parentMap[startTarget] = startNode;

            ARSafeTargetInfo nearestExit = null;
            int shortestDistance = int.MaxValue;

            // BFS traversal
            while (bfsQueue.Count > 0)
            {
                PathNode currentNode = bfsQueue.Dequeue();
                ARSafeTargetInfo current = currentNode.target;

                // Check if this is an exit
                if (current.targetType == TargetType.Exit)
                {
                    // Found an exit - check if it's closer than previous
                    if (currentNode.distance < shortestDistance)
                    {
                        nearestExit = current;
                        shortestDistance = currentNode.distance;

                        // Reconstruct path
                        cachedPath.Clear();
                        PathNode pathNode = currentNode;
                        while (pathNode != null)
                        {
                            cachedPath.Add(pathNode.target);
                            pathNode = pathNode.parent;
                        }
                        cachedPath.Reverse();

                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=green>[BFS] Found exit: {nearestExit.name} at distance {shortestDistance} hops</color>");
                        }
                    }

                    // Continue searching in case there's a closer exit
                    continue;
                }

                // Get adjacent targets
                var adjacent = current.GetAllAdjacentTargets();
                if (adjacent == null || adjacent.Length == 0)
                {
                    continue;
                }

                // Add unvisited neighbors to queue
                foreach (var neighbor in adjacent)
                {
                    if (neighbor == null || visited.Contains(neighbor))
                    {
                        continue;
                    }

                    PathNode neighborNode = new PathNode(neighbor, currentNode, currentNode.distance + 1);
                    bfsQueue.Enqueue(neighborNode);
                    visited.Add(neighbor);
                    parentMap[neighbor] = neighborNode;
                }
            }

            if (enableDebugLogs)
            {
                if (nearestExit != null)
                {
                    Debug.Log($"<color=cyan>[BFS] Pathfinding complete: {nearestExit.name} ({shortestDistance} hops)</color>\nPath: {string.Join(" → ", cachedPath.ConvertAll(t => t.name))}");
                }
                else
                {
                    Debug.LogWarning("[BFS] No exit found from current anchor!");
                }
            }

            return nearestExit;
        }

        /// <summary>
        /// Get the calculated path to nearest exit (list of area targets)
        /// </summary>
        public List<ARSafeTargetInfo> GetPathToNearestExit()
        {
            // Ensure we have a path
            if (cachedPath.Count == 0)
            {
                GetNearestExit(); // This will populate cachedPath
            }

            return new List<ARSafeTargetInfo>(cachedPath);
        }

        /// <summary>
        /// Check if user is currently moving away from exit
        /// </summary>
        public bool IsMovingAwayFromExit => isMovingAwayFromExit;

        /// <summary>
        /// Update wrong-way status and fire event if changed
        /// </summary>
        private void SetWrongWayStatus(bool movingWrongWay)
        {
            isMovingAwayFromExit = movingWrongWay;

            if (enableDebugLogs)
            {
                Debug.Log($"<color={(movingWrongWay ? "red" : "green")}>[ARSafeNavigationValidator] Wrong-way status changed: {movingWrongWay}</color>");
            }

            // Broadcast event
            OnWrongWayStatusChanged?.Invoke(movingWrongWay);
        }

        /// <summary>
        /// Clear pathfinding cache (call when anchor switches)
        /// </summary>
        public void InvalidatePathfindingCache()
        {
            cachedNearestExit = null;
            lastPathfindingTime = -999f;
            lastPathfindingAnchor = null;
            cachedPath.Clear();

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeNavigationValidator] Pathfinding cache invalidated");
            }
        }

        /// <summary>
        /// Reset exit reached state (call when starting new scenario)
        /// </summary>
        public void ResetExitState()
        {
            hasReachedExit = false;
            isMovingAwayFromExit = false;
            wasMovingWrongWayLastFrame = false;
            wrongWayStartTime = -999f;
            nearestVirtualExit = null;

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeNavigationValidator] Exit state reset for new scenario");
            }
        }

        /// <summary>
        /// FULL CLEANUP: Reset all navigation state when exiting simulation.
        /// This is more comprehensive than ResetExitState() - it clears virtual exits and pathfinding cache too.
        /// Call this when returning to main menu or changing scenarios.
        /// </summary>
        public void ResetForNewSimulation()
        {
            // Reset exit tracking state
            hasReachedExit = false;
            isMovingAwayFromExit = false;
            wasMovingWrongWayLastFrame = false;
            wrongWayStartTime = -999f;
            nearestVirtualExit = null;

            // Clear pathfinding cache
            cachedNearestExit = null;
            lastPathfindingTime = -999f;
            lastPathfindingAnchor = null;
            cachedPath.Clear();

            // CRITICAL: Clear virtual exits list (prevents stale references)
            virtualExits.Clear();

            // Reset position history
            historyIndex = 0;
            historyFilled = false;

            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[ARSafeNavigationValidator]</color> FULL RESET for new simulation (exit state + cache + virtual exits cleared)");
            }
        }

        // ==================== VIRTUAL EXIT SUPPORT ====================

        /// <summary>
        /// Register a virtual exit marker with the navigation validator
        /// Called automatically by VirtualExitMarker on Start()
        /// </summary>
        public void RegisterVirtualExit(Content.VirtualExitMarker exitMarker)
        {
            if (exitMarker == null)
            {
                Debug.LogWarning("[ARSafeNavigationValidator] Attempted to register null virtual exit marker!");
                return;
            }

            if (!virtualExits.Contains(exitMarker))
            {
                virtualExits.Add(exitMarker);

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[ARSafeNavigationValidator]</color> Registered virtual exit: {exitMarker.gameObject.name} " +
                              $"(Total virtual exits: {virtualExits.Count})");
                }
            }
        }

        /// <summary>
        /// Called by VirtualExitMarker when user reaches the virtual exit
        /// </summary>
        public void NotifyVirtualExitReached(Content.VirtualExitMarker exitMarker)
        {
            hasReachedExit = true;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>★★★ [ARSafeNavigationValidator]</color> Virtual exit reached! " +
                          $"(GameObject: {exitMarker.gameObject.name})");
            }

            // Fire event to notify systems
            OnVirtualExitReached?.Invoke(exitMarker);

            // Also fire the standard exit reached event for compatibility
            // (some systems may only listen to OnExitReached)
            // Note: This passes null for ARSafeTargetInfo since virtual exits don't have one
            OnExitReached?.Invoke(null);
        }

        /// <summary>
        /// Find nearest virtual exit applicable to current disaster type
        /// Returns null if no applicable virtual exit found
        /// </summary>
        private Content.VirtualExitMarker GetNearestVirtualExit()
        {
            if (virtualExits.Count == 0 || arCamera == null)
            {
                if (enableDebugLogs && virtualExits.Count == 0)
                {
                    Debug.LogWarning($"<color=orange>[NavigationValidator]</color> No virtual exits registered!");
                }
                return null;
            }

            DisasterType currentDisaster = DisasterTypeManager.SelectedDisasterType;
            Vector3 cameraPos = arCamera.transform.position;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[NavigationValidator]</color> Checking {virtualExits.Count} virtual exits for {currentDisaster}");
            }

            Content.VirtualExitMarker nearest = null;
            float nearestDistance = float.MaxValue;
            int applicableCount = 0;

            foreach (var virtualExit in virtualExits)
            {
                if (virtualExit == null || !virtualExit.gameObject.activeInHierarchy)
                    continue;

                // Check if this virtual exit applies to current disaster
                if (!virtualExit.IsApplicableToDisaster(currentDisaster))
                    continue;

                applicableCount++;
                float distance = virtualExit.CalculateDistanceFrom(cameraPos);

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[NavigationValidator]</color> Virtual exit '{virtualExit.gameObject.name}': {distance:F2}m");
                }

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = virtualExit;
                }
            }

            if (enableDebugLogs)
            {
                if (nearest != null)
                {
                    Debug.Log($"<color=green>[NavigationValidator]</color> Nearest virtual exit: '{nearest.gameObject.name}' ({nearestDistance:F2}m)");
                }
                else if (applicableCount == 0)
                {
                    Debug.LogWarning($"<color=orange>[NavigationValidator]</color> No virtual exits applicable for {currentDisaster}");
                }
            }

            nearestVirtualExit = nearest;
            return nearest;
        }

        /// <summary>
        /// Check if user has reached an exit
        /// </summary>
        public bool HasReachedExit => hasReachedExit;

        #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!drawDebugGizmos || cachedPath == null || cachedPath.Count == 0)
            {
                return;
            }

            // Draw path as connected line segments
            Gizmos.color = isMovingAwayFromExit ? Color.red : Color.green;
            for (int i = 0; i < cachedPath.Count - 1; i++)
            {
                if (cachedPath[i] != null && cachedPath[i + 1] != null)
                {
                    Vector3 start = cachedPath[i].transform.position + Vector3.up * 2f;
                    Vector3 end = cachedPath[i + 1].transform.position + Vector3.up * 2f;
                    Gizmos.DrawLine(start, end);
                    Gizmos.DrawSphere(start, 0.3f);
                }
            }

            // Draw exit position
            if (cachedNearestExit != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(cachedNearestExit.transform.position + Vector3.up * 2f, 1f);
            }

            // Draw movement direction
            if (historyFilled && arCamera != null)
            {
                Gizmos.color = Color.yellow;
                int newestIndex = (historyIndex - 1 + positionHistorySize) % positionHistorySize;
                Vector3 pos = positionHistory[newestIndex];

                bool isMoving;
                Vector3 direction;
                CalculateMovement(out isMoving, out direction);

                if (isMoving)
                {
                    Gizmos.DrawRay(pos, direction * 3f);
                }
            }
        }
        #endif
    }
}
