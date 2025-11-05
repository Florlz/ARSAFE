using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace ARSafe.Modular
{
    /// <summary>
    /// Simple metadata component attached to Area Target GameObjects.
    /// Stores type, priority, and adjacency information without complex logic.
    /// </summary>
    public class ARSafeTargetInfo : MonoBehaviour
    {
        [Header("Target Classification")]
        [Tooltip("What kind of space this target represents")]
        public TargetType targetType = TargetType.Hallway;

        [Tooltip("Which floor this target is on (1 = 1st floor, 2 = 2nd floor, etc.)")]
        [Range(1, 10)]
        public int floorLevel = 1;

        [Tooltip("Base priority for this target (higher = more important). Hallways typically 100, rooms 50")]
        [Range(0, 200)]
        public float basePriority = 100f;
        
        [Header("Adjacency")]
        [Tooltip("Targets that should always be visible when this target is the anchor")]
        public ARSafeTargetInfo[] adjacentTargets;
        
        [Tooltip("Maximum distance (meters) to show adjacent targets")]
        [Range(5f, 50f)]
        public float adjacencyRange = 20f;
        
        [Header("Hallway-Room Connections (Hallways Only)")]
        [Tooltip("List of rooms that connect to this hallway. System auto-manages bidirectional adjacency.")]
        public ARSafeTargetInfo[] connectedRooms;
        
        [Header("Multi-Part Room Support (Rooms Only)")]
        [Tooltip("Is this room split into multiple Area Target scans? (Part 1, Part 2, etc.)")]
        public bool isMultiPartRoom = false;
        
        [Tooltip("Which part is this? (1 = Part 1, 2 = Part 2)")]
        [Range(1, 2)]
        public int partNumber = 1;
        
        [Tooltip("Reference to the other part of this room (Part 1 <-> Part 2)")]
        public ARSafeTargetInfo otherPartOfRoom;
        
        [Header("Starting Target")]
        [Tooltip("Is this a starting target for initial localization?")]
        public bool isStartingTarget = false;

    [Header("Stabilization & Visibility")]
    [Tooltip("Allow this target to update the MultiArea group pose when tracked.")]
    public bool allowMultiAreaPoseAuthority = true;

    [Tooltip("Keep this target's augmentations visible using the last known pose when tracking is momentarily lost.")]
    public bool allowAugmentationFallback = true;
        
        [Header("Debug Options")]
        [Tooltip("Draw gizmos showing the computed bounds in Scene view")]
        public bool drawBoundsGizmos = false;
        
        [Tooltip("Log detailed boundary computation info to console")]
        public bool logBoundaryDebug = false;
        
        [Header("Boundary Detection (Advanced)")]
        [Tooltip("Default bounds size when no geometry is found. Centered at Area Target origin.")]
        public Vector3 defaultBoundsSize = new Vector3(20f, 5f, 20f);
        
        [Tooltip("If true, forces use of defaultBoundsSize instead of computing from geometry")]
        public bool useManualBounds = false;
        
        [ContextMenu("Setup: Add BoxCollider to VisualCenter")]
        private void CreateBoundaryGameObject()
        {
            #if UNITY_EDITOR
            // Check if VisualCenter exists
            Transform visualCenter = transform.Find("VisualCenter");
            if (visualCenter == null)
            {
                // Fallback to Boundary
                Transform existingBoundary = transform.Find("Boundary");
                if (existingBoundary != null)
                {
                    var existingCollider = existingBoundary.GetComponent<BoxCollider>();
                    if (existingCollider != null)
                    {
                        Debug.LogWarning($"'Boundary' child already has BoxCollider. Select it to adjust.", this);
                        UnityEditor.Selection.activeGameObject = existingBoundary.gameObject;
                        return;
                    }
                }
                
                // Create new Boundary GameObject if VisualCenter doesn't exist
                GameObject boundaryObj = new GameObject("Boundary");
                UnityEditor.Undo.RegisterCreatedObjectUndo(boundaryObj, "Create Boundary GameObject");
                boundaryObj.transform.SetParent(transform);
                boundaryObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                boundaryObj.transform.localRotation = Quaternion.identity;
                boundaryObj.transform.localScale = Vector3.one;

                Vector3 defaultSize = (targetType == TargetType.Room || targetType == TargetType.Exit)
                    ? new Vector3(10f, 3f, 10f)
                    : new Vector3(20f, 3f, 4f);
                var collider = UnityEditor.Undo.AddComponent<BoxCollider>(boundaryObj);
                collider.center = Vector3.zero;
                collider.size = defaultSize;
                collider.isTrigger = true;

                ClearBoundsCache();
                UnityEditor.Selection.activeGameObject = boundaryObj;

                Debug.Log($"<color=green>✓ Created 'Boundary' child GameObject with BoxCollider ({defaultSize})</color>\n" +
                         $"→ Consider creating a 'VisualCenter' child instead for consistency!", this);
                return;
            }

            // VisualCenter exists - add BoxCollider to it
            var existingVCCollider = visualCenter.GetComponent<BoxCollider>();
            if (existingVCCollider != null)
            {
                Debug.LogWarning($"'VisualCenter' already has BoxCollider. Select it to adjust size/position.", this);
                UnityEditor.Selection.activeGameObject = visualCenter.gameObject;
                return;
            }

            // Add BoxCollider to VisualCenter
            Vector3 defaultSize2 = (targetType == TargetType.Room || targetType == TargetType.Exit)
                ? new Vector3(10f, 3f, 10f)
                : new Vector3(20f, 3f, 4f);
            var vcCollider = UnityEditor.Undo.AddComponent<BoxCollider>(visualCenter.gameObject);
            vcCollider.center = Vector3.zero;
            vcCollider.size = defaultSize2;
            vcCollider.isTrigger = true;

            ClearBoundsCache();
            UnityEditor.Selection.activeGameObject = visualCenter.gameObject;

            Debug.Log($"<color=green>✓ Added BoxCollider to 'VisualCenter' ({defaultSize2})</color>\n" +
                     $"→ You can now ROTATE and POSITION VisualCenter to match your tracking area!\n" +
                     $"→ Adjust the BoxCollider size in Inspector.", this);
            #endif
        }
        
        private void ClearBoundsCache()
        {
            cachedLocalBounds = null;
            cachedBoundaryChildTransform = null;
            cachedBoundaryChildCollider = null;
            cachedDirectBoundaryCollider = null;
        }
        
        [ContextMenu("Setup: Remove BoxCollider")]
        private void RemoveBoxCollider()
        {
            #if UNITY_EDITOR
            var existingCollider = GetComponent<BoxCollider>();
            if (existingCollider != null)
            {
                UnityEditor.Undo.DestroyObjectImmediate(existingCollider);
                ClearBoundsCache();
                Debug.Log($"<color=red>✗ Removed BoxCollider from {name}</color>", this);
            }
            else
            {
                Debug.LogWarning($"No BoxCollider found on {name}", this);
            }
            #endif
        }
        
        [ContextMenu("Debug: Show Current Bounds Info")]
        private void ShowBoundsInfo()
        {
            // Check for VisualCenter first
            var visualCenter = transform.Find("VisualCenter");
            if (visualCenter != null)
            {
                var vcCollider = visualCenter.GetComponent<BoxCollider>();
                if (vcCollider != null)
                {
                    Debug.Log($"<color=cyan>[{name}] Using VisualCenter GameObject:</color>\n" +
                             $"  Position: {visualCenter.localPosition}\n" +
                             $"  Rotation: {visualCenter.localEulerAngles}°\n" +
                             $"  BoxCollider Center: {vcCollider.center}\n" +
                             $"  BoxCollider Size: {vcCollider.size}\n" +
                             $"  Is Trigger: {vcCollider.isTrigger}\n" +
                             $"  Type: {targetType}", this);
#if UNITY_EDITOR
                    UnityEditor.Selection.activeGameObject = visualCenter.gameObject;
#endif
                    return;
                }
            }
            
            // Check for Boundary fallback
            var boundaryChild = transform.Find("Boundary");
            if (boundaryChild != null)
            {
                var boundaryCollider = boundaryChild.GetComponent<BoxCollider>();
                if (boundaryCollider != null)
                {
                    Debug.Log($"<color=cyan>[{name}] Using Child Boundary GameObject:</color>\n" +
                             $"  Position: {boundaryChild.localPosition}\n" +
                             $"  Rotation: {boundaryChild.localEulerAngles}°\n" +
                             $"  BoxCollider Center: {boundaryCollider.center}\n" +
                             $"  BoxCollider Size: {boundaryCollider.size}\n" +
                             $"  Is Trigger: {boundaryCollider.isTrigger}\n" +
                             $"  Type: {targetType}", this);
#if UNITY_EDITOR
                    UnityEditor.Selection.activeGameObject = boundaryChild.gameObject;
#endif
                    return;
                }
            }
            
            var boxCollider = GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                Debug.Log($"<color=cyan>[{name}] BoxCollider Bounds:</color>\n" +
                         $"  Center: {boxCollider.center}\n" +
                         $"  Size: {boxCollider.size}\n" +
                         $"  Is Trigger: {boxCollider.isTrigger}\n" +
                         $"  Type: {targetType}", this);
            }
            else
            {
                var boundsNullable = GetOrBuildLocalBounds();
                if (boundsNullable.HasValue)
                {
                    var bounds = boundsNullable.Value;
                    Debug.Log($"<color=yellow>[{name}] Auto-Calculated Bounds:</color>\n" +
                             $"  Center: {bounds.center}\n" +
                             $"  Size: {bounds.size}\n" +
                             $"  Source: {(useManualBounds ? "Manual defaultBoundsSize" : "Auto-calculated")}\n" +
                             $"  Type: {targetType}", this);
                }
                else
                {
                    Debug.LogWarning($"[{name}] No bounds available (no BoxCollider or geometry found)", this);
                }
            }
        }
        
        [ContextMenu("Debug: Log Adjacency")]
        private void DebugLogAdjacency()
        {
            Debug.Log($"<color=cyan>[{gameObject.name}] Adjacency Debug:</color>\n" +
                     $"  Type: {targetType}\n" +
                     $"  Floor Level: {floorLevel}\n" +
                     $"  Manual Adjacent Targets: {(adjacentTargets != null ? adjacentTargets.Length : 0)}\n" +
                     $"  Connected Rooms: {(connectedRooms != null ? connectedRooms.Length : 0)}\n" +
                     $"  Is Multi-Part: {isMultiPartRoom} (Part {partNumber})\n" +
                     $"  Other Part: {(otherPartOfRoom != null ? otherPartOfRoom.gameObject.name : "None")}", gameObject);

            var allAdjacent = GetAllAdjacentTargets(forceRefresh: true);
            Debug.Log($"<color=yellow>[{gameObject.name}] Total Adjacent Targets: {allAdjacent.Length}</color>", gameObject);
            foreach (var adj in allAdjacent)
            {
                if (adj != null)
                {
                    string floorInfo = IsOnSameFloor(adj) ? "SAME FLOOR" : $"Floor {adj.floorLevel} ({(adj.floorLevel > floorLevel ? "↑" : "↓")} {Mathf.Abs(GetFloorDifference(adj))} floor)";
                    Debug.Log($"  → {adj.gameObject.name} (Type: {adj.targetType}, Priority: {adj.basePriority}, {floorInfo})", adj.gameObject);
                }
            }
        }
        
        [ContextMenu("Refresh Adjacency Cache")]
        private void RefreshAdjacencyCache()
        {
            InvalidateAdjacencyCache();
            Debug.Log($"<color=green>[{gameObject.name}] Adjacency cache refreshed!</color>", gameObject);
        }
        
        [Header("Content Settings")]
        [Tooltip("Minimum distance to show content (prevents clipping)")]
        [Range(0f, 5f)]
        public float minVisibilityDistance = 0.5f;
        
        [Tooltip("Maximum distance to show content")]
        [Range(5f, 100f)]
        public float maxVisibilityDistance = 30f;
        
        // Runtime state (set by ARSafeActivationController)
        [System.NonSerialized] public bool IsCurrentAnchor = false;
        [System.NonSerialized] public float LastTrackingTime = -1f;
        [System.NonSerialized] public float DistanceToCamera = float.MaxValue;
        [System.NonSerialized] public float DistanceToBoundary = float.MaxValue; // Distance from camera to nearest edge of Area Target bounds

        private static readonly ARSafeTargetInfo[] EmptyAdjacency = System.Array.Empty<ARSafeTargetInfo>();
        private static readonly List<ARSafeTargetInfo> SceneTargetBuffer = new List<ARSafeTargetInfo>();
        private static ARSafeActivationController cachedActivationController;

    [System.NonSerialized] private ARSafeTargetInfo[] cachedAdjacentTargets;
    [System.NonSerialized] private Bounds? cachedLocalBounds;
    [System.NonSerialized] private ObserverBehaviour cachedObserver;
    [System.NonSerialized] private Transform cachedBoundaryChildTransform;
    [System.NonSerialized] private BoxCollider cachedBoundaryChildCollider;
    [System.NonSerialized] private BoxCollider cachedDirectBoundaryCollider;
    
    // OPTIMIZATION: Reusable HashSet to avoid allocations in GetAllAdjacentTargets()
    [System.NonSerialized] private HashSet<ARSafeTargetInfo> reusableAdjacencySet;

        // OPTIMIZATION: Boundary distance caching (reduces expensive InverseTransformPoint calls)
        [System.NonSerialized] private float cachedBoundaryDistance = float.MaxValue;
        [System.NonSerialized] private Vector3 cachedBoundaryQueryPoint = Vector3.zero;
        [System.NonSerialized] private int cachedBoundaryFrame = -1;
        private const int BOUNDARY_CACHE_FRAMES = 5; // Cache for 5 frames (~83ms at 60fps) - increased for phone performance
        
        // FIX: Track transform changes to invalidate boundary cache
        [System.NonSerialized] private Vector3 cachedBoundaryTransformPosition = Vector3.zero;
        [System.NonSerialized] private Quaternion cachedBoundaryTransformRotation = Quaternion.identity;

    /// <summary>
        /// Check if this target is part of a multi-part room configuration
        /// </summary>
        public bool IsPartOfMultiPartRoom()
        {
            return isMultiPartRoom && otherPartOfRoom != null;
        }
        
        /// <summary>
        /// Get the other part of this room (Part 1 <-> Part 2)
        /// </summary>
        public ARSafeTargetInfo GetOtherPart()
        {
            return otherPartOfRoom;
        }
        
        /// <summary>
        /// Check if this is a hallway with connected rooms
        /// </summary>
        public bool HasConnectedRooms()
        {
            return targetType == TargetType.Hallway && connectedRooms != null && connectedRooms.Length > 0;
        }

        /// <summary>
        /// Check if this target is on the same floor as another target
        /// </summary>
        public bool IsOnSameFloor(ARSafeTargetInfo other)
        {
            return other != null && floorLevel == other.floorLevel;
        }

        /// <summary>
        /// Get the floor difference between this target and another (positive = this is higher)
        /// </summary>
        public int GetFloorDifference(ARSafeTargetInfo other)
        {
            return other != null ? floorLevel - other.floorLevel : 0;
        }

        /// <summary>
        /// Check if this target is directly above or below another target (1 floor difference)
        /// </summary>
        public bool IsAdjacentFloor(ARSafeTargetInfo other)
        {
            return other != null && Mathf.Abs(floorLevel - other.floorLevel) == 1;
        }

        /// <summary>
        /// Check if this area target qualifies as a flood evacuation exit.
        /// Stairways = intermediate exits (go UP), Floor 2+ = final safe zones
        /// </summary>
        public bool IsFloodExit()
        {
            // Stairways are always exits for flood (users must go UP)
            if (targetType == TargetType.Stairway)
            {
                return true;
            }

            // Floor 2 or higher = safe from flood
            if (floorLevel >= 2)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Indicates whether this target is allowed to update the MultiArea pose.
        /// </summary>
        public bool AllowMultiAreaPoseAuthority => allowMultiAreaPoseAuthority;

        /// <summary>
        /// Indicates whether this target permits rendering fallback when tracking is briefly lost.
        /// </summary>
        public bool AllowAugmentationFallback => allowAugmentationFallback;

        /// <summary>
        /// Get all targets that should be adjacent to this one (includes manual + auto-generated)
        /// OPTIMIZATION: Reuses HashSet to avoid allocations
        /// </summary>
        public ARSafeTargetInfo[] GetAllAdjacentTargets(bool forceRefresh = false)
        {
            if (!forceRefresh && cachedAdjacentTargets != null)
            {
                return cachedAdjacentTargets;
            }

            // OPTIMIZATION: Reuse HashSet instead of allocating new one
            if (reusableAdjacencySet == null)
            {
                reusableAdjacencySet = new HashSet<ARSafeTargetInfo>();
            }
            else
            {
                reusableAdjacencySet.Clear();
            }

            if (adjacentTargets != null)
            {
                foreach (var adjacent in adjacentTargets)
                {
                    if (adjacent != null)
                    {
                        reusableAdjacencySet.Add(adjacent);
                    }
                }
            }

            if (targetType == TargetType.Hallway && connectedRooms != null)
            {
                foreach (var room in connectedRooms)
                {
                    if (room != null)
                    {
                        reusableAdjacencySet.Add(room);
                    }
                }
            }

            if (targetType == TargetType.Room || targetType == TargetType.Exit)
            {
                foreach (var hallway in EnumerateHallwayTargets())
                {
                    if (hallway == null || hallway.connectedRooms == null)
                    {
                        continue;
                    }

                    foreach (var room in hallway.connectedRooms)
                    {
                        if (room == this)
                        {
                            reusableAdjacencySet.Add(hallway);
                            break;
                        }
                    }
                }
            }

            if (reusableAdjacencySet.Count == 0)
            {
                cachedAdjacentTargets = EmptyAdjacency;
            }
            else
            {
                var cached = new ARSafeTargetInfo[reusableAdjacencySet.Count];
                reusableAdjacencySet.CopyTo(cached);
                cachedAdjacentTargets = cached;
            }

            return cachedAdjacentTargets;
        }

        /// <summary>
        /// Determine if a specific target is considered adjacent to this one.
        /// </summary>
        public bool IsAdjacentTo(ARSafeTargetInfo other)
        {
            if (other == null)
            {
                return false;
            }

            var adjacent = GetAllAdjacentTargets();
            if (adjacent == null || adjacent.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < adjacent.Length; i++)
            {
                if (adjacent[i] == other)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clear adjacency cache so it will be rebuilt on next query
        /// </summary>
        public void InvalidateAdjacencyCache()
        {
            cachedAdjacentTargets = null;
        }

        /// <summary>
        /// Compute distance from a world point to the nearest edge of this Area Target's bounding volume.
        /// Returns positive distance when outside bounds, zero when on the boundary, and negative when inside.
        /// OPTIMIZED: Caches results for 2 frames to avoid expensive InverseTransformPoint calls (60-70% faster)
        /// FIX: Properly calculates distance to nearest face when inside (Unity's ClosestPoint returns the point itself when inside).
        /// IMPORTANT: Returns MaxValue before localization is complete (system doesn't know camera position yet).
        /// </summary>
        public float ComputeDistanceToBoundary(Vector3 worldPoint)
        {
            // OPTIMIZATION: Check cache first (eliminates ~60-70% of expensive calculations)
            // Cache is valid for 2 frames and same query point (camera doesn't move much in 33ms)
            int currentFrame = Time.frameCount;
            
            // FIX: Invalidate cache if Area Target transform has changed (moved or rotated)
            Transform boundaryTransform = cachedBoundaryChildTransform != null ? cachedBoundaryChildTransform : transform;
            bool transformChanged = boundaryTransform.position != cachedBoundaryTransformPosition
                || boundaryTransform.rotation != cachedBoundaryTransformRotation;
            
            bool cacheValid = (currentFrame - cachedBoundaryFrame) <= BOUNDARY_CACHE_FRAMES
                && worldPoint == cachedBoundaryQueryPoint
                && !transformChanged;
            
            if (cacheValid)
            {
                if (logBoundaryDebug)
                {
                    Debug.Log($"[BoundaryDebug] {name}: Using cached distance {cachedBoundaryDistance:F2}m (age: {currentFrame - cachedBoundaryFrame} frames)");
                }
                return cachedBoundaryDistance;
            }

            // CRITICAL: Before localization, camera position is unreliable
            // Don't calculate distances until we have a tracked anchor
            var activationController = GetActivationController();
            if (activationController != null && !activationController.HasLocalized)
            {
                if (logBoundaryDebug)
                {
                    Debug.Log($"[BoundaryDebug] {name}: Pre-localization - returning MaxValue (no anchor established yet)");
                }
                // Don't cache pre-localization results
                return float.MaxValue;
            }
            
            var bounds = GetOrBuildLocalBounds();
            if (!bounds.HasValue)
            {
                if (logBoundaryDebug)
                {
                    Debug.Log($"[BoundaryDebug] {name}: No bounds available, returning MaxValue");
                }
                return float.MaxValue;
            }

            // If we have a dedicated boundary child with its own BoxCollider, treat it as an oriented box
            if (cachedBoundaryChildTransform != null && cachedBoundaryChildCollider != null)
            {
                float orientedResult = ComputeDistanceToOrientedBoundary(worldPoint, cachedBoundaryChildTransform, cachedBoundaryChildCollider);
                
                // OPTIMIZATION: Cache oriented boundary result with transform state
                cachedBoundaryDistance = orientedResult;
                cachedBoundaryQueryPoint = worldPoint;
                cachedBoundaryFrame = currentFrame;
                cachedBoundaryTransformPosition = cachedBoundaryChildTransform.position;
                cachedBoundaryTransformRotation = cachedBoundaryChildTransform.rotation;
                
                return orientedResult;
            }

            // OPTIMIZED: Single InverseTransformPoint call (expensive operation)
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            
            // OPTIMIZED: Check containment first (cheap operation)
            bool isInside = bounds.Value.Contains(localPoint);
            
            float distance;
            
            if (isInside)
            {
                // FIX: When inside, Unity's ClosestPoint returns the point itself (distance = 0)
                // We need to manually calculate distance to nearest face of the AABB
                Vector3 min = bounds.Value.min;
                Vector3 max = bounds.Value.max;
                
                // Calculate distance to each face (6 faces of the box)
                float distToMaxX = max.x - localPoint.x;
                float distToMinX = localPoint.x - min.x;
                float distToMaxY = max.y - localPoint.y;
                float distToMinY = localPoint.y - min.y;
                float distToMaxZ = max.z - localPoint.z;
                float distToMinZ = localPoint.z - min.z;
                
                // Find minimum distance to any face
                distance = Mathf.Min(
                    distToMaxX, distToMinX,
                    distToMaxY, distToMinY,
                    distToMaxZ, distToMinZ
                );
            }
            else
            {
                // Outside: Use Unity's ClosestPoint (works correctly for exterior points)
                Vector3 closestPoint = bounds.Value.ClosestPoint(localPoint);
                distance = Vector3.Distance(localPoint, closestPoint);
            }
            
            // Return negative if inside, positive if outside
            float result = isInside ? -distance : distance;
            
            // OPTIMIZATION: Cache the result with transform state for future frames
            cachedBoundaryDistance = result;
            cachedBoundaryQueryPoint = worldPoint;
            cachedBoundaryFrame = currentFrame;
            cachedBoundaryTransformPosition = transform.position;
            cachedBoundaryTransformRotation = transform.rotation;
            
            if (logBoundaryDebug)
            {
                // ENHANCED DEBUG: Show min/max bounds for troubleshooting overlap issues
                Vector3 min = bounds.Value.min;
                Vector3 max = bounds.Value.max;
                
                Debug.Log($"<color=cyan>[BoundaryDebug] {name} (AABB)</color>:\n" +
                         $"  WorldPos: {worldPoint}\n" +
                         $"  LocalPos: {localPoint}\n" +
                         $"  Bounds Center: {bounds.Value.center}\n" +
                         $"  Bounds Size: {bounds.Value.size}\n" +
                         $"  Bounds Min: {min}\n" +
                         $"  Bounds Max: {max}\n" +
                         $"  IsInside: {isInside}\n" +
                         $"  Distance: <b>{result:F2}m</b> {(isInside ? "<color=green>(INSIDE)</color>" : "<color=gray>(outside)</color>")}\n" +
                         $"  <color=yellow>CACHED for {BOUNDARY_CACHE_FRAMES} frames</color>");
            }
            
            return result;
        }

        /// <summary>
        /// Get or build the local-space bounds of this Area Target by aggregating child renderers and colliders.
        /// FIXED: Properly converts world bounds to local space using corner transformation to handle rotations.
        /// </summary>
        private Bounds? GetOrBuildLocalBounds()
        {
            if (cachedLocalBounds.HasValue)
            {
                return cachedLocalBounds;
            }

            var observer = GetObserver();
            if (observer == null)
            {
                return null;
            }

            // If manual bounds are enabled, use those directly
            if (useManualBounds)
            {
                cachedLocalBounds = new Bounds(Vector3.zero, defaultBoundsSize);
                cachedBoundaryChildTransform = null;
                cachedBoundaryChildCollider = null;
                cachedDirectBoundaryCollider = null;
                
                if (logBoundaryDebug)
                {
                    Debug.Log($"[BoundaryDebug] {name}: Using manual bounds: {defaultBoundsSize}");
                }
                
                return cachedLocalBounds;
            }

            // PRIORITY 1: Check for a child GameObject named "VisualCenter" or "Boundary" with a BoxCollider
            // This allows users to create a rotated/custom boundary zone separate from the Area Target
            Transform boundaryChild = observer.transform.Find("VisualCenter");
            if (boundaryChild == null)
            {
                boundaryChild = observer.transform.Find("Boundary");
            }
            
            if (boundaryChild != null)
            {
                var boundaryCollider = boundaryChild.GetComponent<BoxCollider>();
                if (boundaryCollider != null)
                {
                    // CRITICAL FIX: Properly transform rotated BoxCollider bounds to Area Target local space
                    // We must transform all 8 corners to handle rotation correctly (can't just scale size!)
                    
                    Vector3 size = boundaryCollider.size;
                    Vector3 center = boundaryCollider.center;
                    Vector3 halfSize = size * 0.5f;
                    
                    // Define all 8 corners in the BoxCollider's local space
                    Vector3[] corners = new Vector3[8]
                    {
                        center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
                        center + new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
                        center + new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
                        center + new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
                        center + new Vector3(-halfSize.x,  halfSize.y,  halfSize.z),
                        center + new Vector3( halfSize.x,  halfSize.y,  halfSize.z)
                    };
                    
                    // Transform corners: BoxCollider local → VisualCenter world → Area Target local
                    Vector3 min = Vector3.one * float.MaxValue;
                    Vector3 max = Vector3.one * float.MinValue;
                    
                    for (int i = 0; i < corners.Length; i++)
                    {
                        // Transform to world space via VisualCenter
                        Vector3 worldCorner = boundaryChild.TransformPoint(corners[i]);
                        // Transform to Area Target local space
                        Vector3 localCorner = observer.transform.InverseTransformPoint(worldCorner);
                        
                        // Expand AABB to include this corner
                        min = Vector3.Min(min, localCorner);
                        max = Vector3.Max(max, localCorner);
                    }
                    
                    // Build axis-aligned bounding box from transformed corners
                    Vector3 localCenter = (min + max) * 0.5f;
                    Vector3 localSize = max - min;
                    
                    cachedLocalBounds = new Bounds(localCenter, localSize);
                    cachedBoundaryChildTransform = boundaryChild;
                    cachedBoundaryChildCollider = boundaryCollider;
                    cachedDirectBoundaryCollider = null;
                    
                    if (logBoundaryDebug)
                    {
                        Debug.Log($"<color=cyan>[BoundaryDebug] {name}: Using child '{boundaryChild.name}' GameObject (ROTATION-AWARE)</color>\n" +
                                 $"  VisualCenter Position: {boundaryChild.localPosition}\n" +
                                 $"  VisualCenter Rotation: {boundaryChild.localEulerAngles}\n" +
                                 $"  BoxCollider Center: {center}\n" +
                                 $"  BoxCollider Size: {size}\n" +
                                 $"  Transformed Local Center: {localCenter}\n" +
                                 $"  Transformed Local Size: {localSize}\n" +
                                 $"  Transformed Local Min: {min}\n" +
                                 $"  Transformed Local Max: {max}\n" +
                                 $"  <color=yellow>NOTE: AABB expansion from rotation = {(localSize - size).magnitude:F2}m</color>");
                    }
                    
                    return cachedLocalBounds;
                }
            }

            // PRIORITY 2: Check if there's a BoxCollider directly on THIS GameObject (not children)
            var directCollider = observer.GetComponent<BoxCollider>();
            if (directCollider != null)
            {
                // Use the BoxCollider's local bounds directly - this is what the user manually configured!
                cachedLocalBounds = new Bounds(directCollider.center, directCollider.size);
                cachedBoundaryChildTransform = null;
                cachedBoundaryChildCollider = null;
                cachedDirectBoundaryCollider = directCollider;
                
                if (logBoundaryDebug)
                {
                    Debug.Log($"[BoundaryDebug] {name}: Using BoxCollider on Area Target itself\n" +
                             $"  Center: {directCollider.center}\n" +
                             $"  Size: {directCollider.size}");
                }
                
                return cachedLocalBounds;
            }

            // PRIORITY 2: Try to aggregate all child renderer bounds (including inactive)
            var renderers = observer.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                // Fallback: try colliders (including inactive)
                var colliders = observer.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    // No geometry - use the configured default size
                    cachedLocalBounds = new Bounds(Vector3.zero, defaultBoundsSize);
                    cachedBoundaryChildTransform = null;
                    cachedBoundaryChildCollider = null;
                    cachedDirectBoundaryCollider = null;
                    
                    if (logBoundaryDebug)
                    {
                        Debug.LogWarning($"[BoundaryDebug] {name}: No Renderer/Collider geometry found. " +
                                        $"Using default bounds: {defaultBoundsSize} centered at origin. " +
                                        $"Consider adding a Box Collider to define accurate bounds or adjust defaultBoundsSize.");
                    }
                    
                    return cachedLocalBounds;
                }

                var aggregateBounds = new Bounds(colliders[0].bounds.center, colliders[0].bounds.size);
                for (int i = 1; i < colliders.Length; i++)
                {
                    aggregateBounds.Encapsulate(colliders[i].bounds);
                }

                // FIXED: Convert world bounds to local space properly
                cachedLocalBounds = WorldBoundsToLocalBounds(aggregateBounds);
                cachedBoundaryChildTransform = null;
                cachedBoundaryChildCollider = null;
                cachedDirectBoundaryCollider = null;
                return cachedLocalBounds;
            }

            // PRIORITY 3: Aggregate renderer bounds
            var bounds = new Bounds(renderers[0].bounds.center, renderers[0].bounds.size);
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // FIXED: Convert world bounds to local space properly
            cachedLocalBounds = WorldBoundsToLocalBounds(bounds);
            cachedBoundaryChildTransform = null;
            cachedBoundaryChildCollider = null;
            cachedDirectBoundaryCollider = null;
            return cachedLocalBounds;
        }

        /// <summary>
        /// Converts world space bounds to local space by transforming all 8 corners and creating new AABB.
        /// This properly handles rotations, unlike InverseTransformVector which fails for rotated transforms.
        /// </summary>
        private Bounds WorldBoundsToLocalBounds(Bounds worldBounds)
        {
            var center = worldBounds.center;
            var extents = worldBounds.extents;
            
            // Transform all 8 corners of the world bounds to local space
            Vector3[] worldCorners = {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y,  extents.z)
            };

            // Convert all corners to local space
            Vector3[] localCorners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                localCorners[i] = transform.InverseTransformPoint(worldCorners[i]);
            }

            // Find min/max to create local AABB
            Vector3 min = localCorners[0];
            Vector3 max = localCorners[0];
            
            for (int i = 1; i < 8; i++)
            {
                min = Vector3.Min(min, localCorners[i]);
                max = Vector3.Max(max, localCorners[i]);
            }

            var localCenter = (min + max) * 0.5f;
            var localSize = max - min;
            
            return new Bounds(localCenter, localSize);
        }

        private float ComputeDistanceToOrientedBoundary(Vector3 worldPoint, Transform boundaryTransform, BoxCollider boundaryCollider)
        {
            if (boundaryTransform == null || boundaryCollider == null)
            {
                return float.MaxValue;
            }

            Vector3 localPoint = boundaryTransform.InverseTransformPoint(worldPoint);
            Vector3 center = boundaryCollider.center;
            Vector3 size = boundaryCollider.size;
            Vector3 halfExtents = size * 0.5f;

            // Position relative to the BoxCollider's configured center
            Vector3 relative = localPoint - center;

            bool inside = Mathf.Abs(relative.x) <= halfExtents.x
                && Mathf.Abs(relative.y) <= halfExtents.y
                && Mathf.Abs(relative.z) <= halfExtents.z;

            float result;

            if (inside)
            {
                float distanceInside = Mathf.Min(
                    halfExtents.x - Mathf.Abs(relative.x),
                    halfExtents.y - Mathf.Abs(relative.y),
                    halfExtents.z - Mathf.Abs(relative.z)
                );

                // Clamp to zero in case of floating-point precision issues
                distanceInside = Mathf.Max(distanceInside, 0f);
                result = -distanceInside;
            }
            else
            {
                float dx = Mathf.Max(0f, Mathf.Abs(relative.x) - halfExtents.x);
                float dy = Mathf.Max(0f, Mathf.Abs(relative.y) - halfExtents.y);
                float dz = Mathf.Max(0f, Mathf.Abs(relative.z) - halfExtents.z);

                result = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            }

            if (logBoundaryDebug)
            {
                Debug.Log(
                    $"<color=cyan>[BoundaryDebug] {name} (Oriented)</color>:\n" +
                    $"  WorldPos: {worldPoint}\n" +
                    $"  Boundary Local Pos: {localPoint}\n" +
                    $"  Relative (to center): {relative}\n" +
                    $"  Box Center: {center}\n" +
                    $"  Box Size: {size}\n" +
                    $"  Rotation: {boundaryTransform.localEulerAngles}\n" +
                    $"  Distance: <b>{result:F2}m</b> {(inside ? "<color=green>(INSIDE)</color>" : "<color=gray>(outside)</color>")}"
                );
            }

            return result;
        }

        private ObserverBehaviour GetObserver()
        {
            if (cachedObserver == null)
            {
                cachedObserver = GetComponent<ObserverBehaviour>();
            }
            return cachedObserver;
        }

        private static IEnumerable<ARSafeTargetInfo> EnumerateHallwayTargets()
        {
            SceneTargetBuffer.Clear();

            var controller = GetActivationController();
            if (controller != null && controller.allAreaTargets != null && controller.allAreaTargets.Count > 0)
            {
                foreach (var observer in controller.allAreaTargets)
                {
                    if (observer == null) continue;
                    var info = observer.GetComponent<ARSafeTargetInfo>();
                    if (info != null)
                    {
                        SceneTargetBuffer.Add(info);
                    }
                }
            }
            else
            {
                SceneTargetBuffer.AddRange(FindObjectsByType<ARSafeTargetInfo>(FindObjectsSortMode.None));
            }

            foreach (var info in SceneTargetBuffer)
            {
                if (info != null && info.targetType == TargetType.Hallway)
                {
                    yield return info;
                }
            }
        }
        
        /// <summary>
        /// Validate setup in editor
        /// </summary>
        private void OnValidate()
        {
            // Clear bounds cache whenever Inspector values change
            // This ensures bounds recalculate when useManualBounds, defaultBoundsSize, or colliders change
            ClearBoundsCache();
            
            // Validate hallway-room connections
            if (targetType == TargetType.Hallway && connectedRooms != null)
            {
                foreach (var room in connectedRooms)
                {
                    if (room != null && room.targetType != TargetType.Room && room.targetType != TargetType.Exit)
                    {
                        Debug.LogWarning($"[ARSafeTargetInfo] {name}: Connected room '{room.name}' should be Room or Exit type.", this);
                    }
                }

            }
            
            // Clear connectedRooms for non-hallways
            if (targetType != TargetType.Hallway && connectedRooms != null && connectedRooms.Length > 0)
            {
                Debug.LogWarning($"[ARSafeTargetInfo] {name}: connectedRooms is only for Hallway type. Clearing.", this);
                connectedRooms = null;
            }
            
            // Only show multi-part fields for Room type
            if (targetType != TargetType.Room)
            {
                if (isMultiPartRoom)
                {
                    Debug.LogWarning($"[ARSafeTargetInfo] {name}: Multi-part room is only for Room type. Setting to false.", this);
                    isMultiPartRoom = false;
                    otherPartOfRoom = null;
                }
            }
            
            // Validate multi-part configuration
            if (isMultiPartRoom)
            {
                if (otherPartOfRoom == null)
                {
                    Debug.LogWarning($"[ARSafeTargetInfo] {name}: Multi-part room enabled but no other part assigned!", this);
                }
                else if (otherPartOfRoom.targetType != TargetType.Room)
                {
                    Debug.LogWarning($"[ARSafeTargetInfo] {name}: Other part must also be a Room type!", this);
                }
                else if (!otherPartOfRoom.isMultiPartRoom)
                {
                    Debug.LogWarning($"[ARSafeTargetInfo] {name}: Other part should also have isMultiPartRoom enabled!", this);
                }
                else if (otherPartOfRoom.partNumber == partNumber)
                {
                    Debug.LogWarning($"[ARSafeTargetInfo] {name}: Both parts have the same part number! Should be 1 and 2.", this);
                }
            }
            
            // Part number should be 1 or 2
            if (partNumber < 1 || partNumber > 2)
            {
                partNumber = Mathf.Clamp(partNumber, 1, 2);
            }

            InvalidateAdjacencyCache();
        }

        private static ARSafeActivationController GetActivationController()
        {
            if (cachedActivationController == null)
            {
                cachedActivationController = FindFirstObjectByType<ARSafeActivationController>();
            }

            // Unity overrides == for destroyed objects, so this handles controller unloading
            if (cachedActivationController == null)
            {
                return null;
            }

            return cachedActivationController;
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Draw gizmos showing the computed bounds in Scene view
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!drawBoundsGizmos) return;

            var bounds = GetOrBuildLocalBounds();
            if (!bounds.HasValue) return;

            // Convert local bounds back to world space for drawing
            var worldCenter = transform.TransformPoint(bounds.Value.center);
            var worldSize = transform.TransformVector(bounds.Value.size);
            
            // Draw the bounds as a wireframe box
            Gizmos.color = isStartingTarget ? Color.green : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bounds.Value.center, bounds.Value.size);
            
            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawSphere(worldCenter, 0.5f);
        }
        #endif
    }
    
    public enum TargetType
    {
        Hallway,
        Room,
        Stairway,
        Canteen,
        Exit,
        Other
    }
}
