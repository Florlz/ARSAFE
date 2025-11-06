using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ARSafe.UI;
using ARSAFE.UI;
using UnityEngine;
using Vuforia;

namespace ARSafe.Modular
{
    /// <summary>
    /// Core activation controller built on Vuforia's MultiArea foundation.
    /// Manages which Area Targets remain active by tracking the current MultiArea anchor
    /// and its neighborhood, keeping tracked targets available while preventing observer overload.
    ///
    /// KEY RESPONSIBILITIES:
    /// - Select the best-tracked Area Target to use as the active anchor
    /// - Keep the anchor, its neighbors, and any tracked targets enabled
    /// - Maintain starting targets for initial localization
    /// - Feed MultiArea drift correction data to the shared root transform
    ///
    /// DOES NOT HANDLE:
    /// - Content visibility (see ARSafeProximityDisplay)
    /// - Disaster filtering (see ARSafeDisasterFilter)
    /// - Tracking quality validation (see ARSafeTrackingManager)
    /// </summary>
    [RequireComponent(typeof(ARSafeTrackingManager))]
    public class ARSafeActivationController : MonoBehaviour
    {
        [Header("Core References")]
        [Tooltip("All Area Targets in the scene (assigned automatically on Start if empty)")]
        public List<ObserverBehaviour> allAreaTargets = new List<ObserverBehaviour>();

        [Tooltip("AR Camera for distance calculations")]
        public Camera arCamera;

        [Header("Update Settings")]
        [Tooltip(
            "How often to refresh anchor selection and neighborhood activation (seconds). Increased for better phone performance."
        )]
        [Range(0.1f, 2f)]
        public float updateInterval = 0.5f;

        [Header("Starting Targets")]
        [Tooltip("Targets marked as starting points (enable at launch for localization)")]
        public List<ObserverBehaviour> startingTargets = new List<ObserverBehaviour>();

        [Tooltip(
            "Auto-deactivate all non-starting targets on Start (prevents Vuforia errors at launch)"
        )]
        public bool deactivateNonStartingTargetsOnStart = true;

        [Header("MultiArea Settings")]
        [Tooltip("Hide content when no targets tracking (like original MultiArea)")]
        public bool hideContentWhenNotTracking = false;

        [Header("Anchor Settings")]
        [Tooltip("Distance threshold to switch anchor to closer target")]
        [Range(3f, 30f)]
        public float anchorSwitchDistance = 25f;

        [Tooltip(
            "Minimum time between anchor switches (seconds). Increased to prevent rapid ping-pong."
        )]
        [Range(0.5f, 10f)]
        public float anchorSwitchCooldown = 2.5f;

        [Tooltip(
            "Minimum time user must be INSIDE neighbor boundary before switching. Increased to prevent overlapping boundary thrashing."
        )]
        [Range(0f, 5f)]
        public float neighborBoundaryDwellTime = 1.5f;

        [Tooltip(
            "Hysteresis: How much better (in meters) a candidate must be to replace current anchor. Prevents ping-pong in overlapping areas."
        )]
        [Range(0f, 10f)]
        public float anchorSwitchHysteresis = 3f;

        [Tooltip(
            "If true, prefer targets the device is currently inside (negative boundary distance) when selecting anchor"
        )]
        public bool preferTargetsUserIsInside = true;

        [Tooltip(
            "Bonus priority (meters) subtracted from distance when user is inside a target's bounds"
        )]
        [Range(0f, 50f)]
        public float insideBoundaryBonus = 25f;

        [Header("Neighborhood Settings")]
        [Tooltip("Maximum number of neighbor targets to enable alongside the anchor.")]
        [Range(0, 6)]
        public int maxNeighborTargets = 3;

        [Tooltip(
            "Fallback radius (meters) for auto-selecting neighbors when adjacency metadata is missing."
        )]
        [Range(2f, 40f)]
        public float neighborActivationRadius = 12f;

        [Tooltip("Keep the previously anchored target active to support smooth backtracking.")]
        public bool keepPreviousAnchorActive = true;

        [Tooltip(
            "Always include any Area Target that is currently tracking, even if outside the anchor neighborhood."
        )]
        public bool alwaysIncludeTrackedTargets = true;

        [Header("Adjacency Validation")]
        [Tooltip(
            "Only activate targets that are explicitly configured as adjacent - no distance fallback"
        )]
        public bool strictAdjacencyMode = false;

        [Tooltip("Maximum distance (meters) to consider targets as truly adjacent")]
        [Range(10f, 50f)]
        public float maxAdjacencyDistance = 30f;

        [Tooltip(
            "When deciding neighborhood, prefer targets the user is moving toward (negative boundary distance)."
        )]
        public bool prioritizeTargetsUserIsApproaching = true;

        [Tooltip(
            "Distance to boundary (meters) below which a target is considered 'user is approaching'"
        )]
        [Range(0f, 5f)]
        public float approachingBoundaryThreshold = 2f;

        [Header("Debug")]
        public bool enableDebugLogs = false;

        [ContextMenu("Debug: Log Current State")]
        private void DebugLogCurrentState()
        {
            Debug.Log(
                $"<color=cyan>=== ARSafeActivationController State ===</color>\n"
                    + $"Current Anchor: {(currentAnchor != null ? currentAnchor.gameObject.name : "None")}\n"
                    + $"Previous Anchor: {(previousAnchor != null ? previousAnchor.gameObject.name : "None")}\n"
                    + $"Has Localized: {hasLocalized}\n"
                    + $"Currently Enabled: {currentlyEnabled.Count}\n"
                    + $"Max Neighbors: {maxNeighborTargets}\n"
                    + $"Neighbor Radius: {(neighborActivationRadius > 0 ? neighborActivationRadius + "m" : "Unlimited")}",
                gameObject
            );

            Debug.Log($"<color=yellow>Enabled Targets ({currentlyEnabled.Count}):</color>");
            foreach (var target in currentlyEnabled)
            {
                if (target != null)
                {
                    var info = targetInfoMap.ContainsKey(target) ? targetInfoMap[target] : null;
                    var isTracking = trackingManager != null && trackingManager.IsTracking(target);
                    var isAnchor = target == currentAnchor;
                    Debug.Log(
                        $"  {(isAnchor ? "[ANCHOR] " : "")}{target.gameObject.name} "
                            + $"(Tracking: {isTracking}, Type: {(info != null ? info.targetType.ToString() : "Unknown")}, "
                            + $"Priority: {(info != null ? info.basePriority : 0)}, "
                            + $"Distance: {(info != null ? info.DistanceToCamera.ToString("F2") + "m" : "N/A")})",
                        target.gameObject
                    );
                }
            }

            if (
                currentAnchor != null
                && targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo)
            )
            {
                var adjacent = anchorInfo.GetAllAdjacentTargets(forceRefresh: true);
                Debug.Log($"<color=green>Anchor's Adjacent Targets ({adjacent.Length}):</color>");
                foreach (var adj in adjacent)
                {
                    if (adj != null)
                    {
                        var isEnabled = currentlyEnabled.Any(t =>
                            t != null && t.gameObject == adj.gameObject
                        );
                        Debug.Log(
                            $"  {(isEnabled ? "[ENABLED] " : "[DISABLED] ")}{adj.gameObject.name} "
                                + $"(Type: {adj.targetType}, Priority: {adj.basePriority})",
                            adj.gameObject
                        );
                    }
                }
            }
        }

        // Runtime state
        private ARSafeTrackingManager trackingManager;
        private Dictionary<ObserverBehaviour, ARSafeTargetInfo> targetInfoMap =
            new Dictionary<ObserverBehaviour, ARSafeTargetInfo>();
        private ObserverBehaviour currentAnchor;
        private ObserverBehaviour previousAnchor;
        private float lastUpdateTime;
        private float lastAnchorSwitchTime;
        private HashSet<ObserverBehaviour> currentlyEnabled = new HashSet<ObserverBehaviour>();
        private bool isRelocalizationInProgress = false;

        // Anchor history for smart relocalization (tracks last 5 unique anchors)
        private List<ObserverBehaviour> anchorHistory = new List<ObserverBehaviour>();
        private const int MAX_ANCHOR_HISTORY = 5;

        // Public accessor for diagnostics/debugging
        public ObserverBehaviour CurrentAnchor => currentAnchor;

        // Public accessor for relocalization UI
        public List<ObserverBehaviour> GetAnchorHistory() =>
            new List<ObserverBehaviour>(anchorHistory);

        // Performance optimization: Pre-allocated sort buffer to replace LINQ
        private (ObserverBehaviour target, float distance, ARSafeTargetInfo info)[] sortBuffer;
        private const int MIN_SORT_BUFFER_CAPACITY = 16;
        private System.Text.StringBuilder debugBuilder = new System.Text.StringBuilder(256);
        private bool hasLocalized = false;

        // Performance optimization: Cached camera position (avoid repeated transform access)
        private Vector3 cachedCameraPosition;
        private int lastCameraUpdateFrame = -1;

        // Pre-tracking neighbor switch state tracking
        private ObserverBehaviour pendingNeighborSwitch = null;
        private float pendingNeighborSwitchTime = -1f;

        // Tracking grace period: After switching to a new anchor, give it time to establish tracking
        // before allowing fallback to previous anchor (prevents rapid back-and-forth switching)
        private float trackingGracePeriodEndTime = -1f;
        private const float TRACKING_GRACE_PERIOD = 2.5f; // Give new anchor 2.5s to establish tracking (increased to prevent ping-pong)

        // Anchor stability: Prevent rapid switching by requiring minimum dwell time
        private const float MIN_ANCHOR_STABILITY_TIME = 1.5f; // Must stay on anchor for 1.5s before allowing switch (increased to prevent oscillation)
        private float currentAnchorStartTime = -1f;

        // AUGMENTATION VISIBILITY: Keep augmentations visible even when tracking is lost
        // The MultiArea pose system maintains the last known position
        [Header("Augmentation Fallback")]
        [
            SerializeField,
            Tooltip(
                "Keep augmentations visible using last known pose when anchor temporarily loses tracking"
            )
        ]
        private bool keepAugmentationsVisibleDuringTrackingLoss = true;

        [
            SerializeField,
            Tooltip(
                "Maximum time to show augmentations without tracking before hiding them (seconds)"
            )
        ]
        [Range(1f, 10f)]
        private float maxAugmentationFallbackTime = 5f;

        private float lastSuccessfulTrackingTime = -1f;

        /// <summary>
        /// Public property to check if system has completed initial localization
        /// </summary>
        public bool HasLocalized => hasLocalized;

        /// <summary>
        /// Confirm that tracking has been established and mark system as localized.
        /// This should be called AFTER Vuforia confirms tracking, not before.
        /// Called by LocationSelectionController or other initialization systems.
        /// </summary>
        public void ConfirmLocalization()
        {
            if (hasLocalized)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        "<color=yellow>[ARSafeActivationController] ConfirmLocalization() called but already localized. Ignoring.</color>"
                    );
                }
                return;
            }

            hasLocalized = true;

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=green>[ARSafeActivationController] ✓ LOCALIZATION CONFIRMED!</color> First anchor established: {currentAnchor?.name}. System now active."
                );
            }

            // CRITICAL FIX: Force refresh all visibility systems when localization is confirmed
            // This fixes the issue where debris/arrows don't show when manually selecting a location
            // (filters and proximity displays need to be refreshed after localization)
            ForceRefreshAllDisasterFilters();

            // Also refresh proximity displays to show directional arrows
            RefreshAllProximityDisplays();

            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[ARSafeActivationController] Forced visibility refresh after localization confirmation</color>");
            }
        }

        // MultiArea mode fields
        private readonly Dictionary<string, Matrix4x4> relativePoses =
            new Dictionary<string, Matrix4x4>();
        private AreaTargetBehaviour currentBestTracked;
        private GameObject augmentationsRoot;
        private bool multiAreaHasTracking;

        private sealed class AugmentationParentingState
        {
            public Transform Augmentation;
            public Transform OriginalParent;
            public Vector3 OriginalLocalPosition;
            public Quaternion OriginalLocalRotation;
            public Vector3 OriginalLocalScale;
            public bool OriginalActiveState;
            public bool IsReparented;
        }

        private readonly Dictionary<
            ObserverBehaviour,
            AugmentationParentingState
        > augmentationParentingStates =
            new Dictionary<ObserverBehaviour, AugmentationParentingState>();

        private int multiAreaLastPoseFrame = -1;
        private Pose smoothedGroupPose = Pose.identity;
        private bool hasSmoothedGroupPose;
        private Vector3 groupPoseVelocity = Vector3.zero;
        private Quaternion smoothedGroupRotation = Quaternion.identity;

        [Header("MultiArea Pose Control")]
        [SerializeField, Range(0.01f, 0.5f)]
        private float multiAreaPoseSmoothTime = 0.05f;

        [
            SerializeField,
            Tooltip(
                "Allow adjacent targets with fallback enabled to adjust the group pose when the anchor is not tracked."
            )
        ]
        private bool allowNeighborPoseAuthority = true;

        [Header("MultiArea Pose Performance")]
        [
            Tooltip(
                "Update MultiArea pose at this FPS (lower = better performance). 30 FPS provides faster realignment."
            )
        ]
        [Range(5f, 60f)]
        public float multiAreaPoseUpdateFPS = 30f;

        private float lastMultiAreaPoseUpdateTime = -1f;
        private List<AreaTargetBehaviour> cachedTrackedTargets = new List<AreaTargetBehaviour>();

        [Header("Spatial Validation (Prevents Wrong Target Tracking)")]
        [
            SerializeField,
            Tooltip(
                "Enable spatial validation to prevent Vuforia from switching to wrong Area Targets"
            )
        ]
        private bool enableSpatialValidation = true;

        [
            SerializeField,
            Tooltip(
                "Maximum distance (meters) to allow anchor switch. Prevents teleporting across building."
            )
        ]
        [Range(5f, 50f)]
        private float maxAnchorSwitchDistance = 35f;

        [
            SerializeField,
            Tooltip("Maximum movement speed (meters/second). Rejects impossible movements.")
        ]
        [Range(1f, 10f)]
        private float maxMovementSpeed = 3f; // Walking speed ~1.4 m/s, running ~3 m/s

        [
            SerializeField,
            Tooltip(
                "Require TRACKED status (not EXTENDED_TRACKED) for distant non-adjacent targets"
            )
        ]
        private bool requireTrackedForDistantTargets = true;

        [
            SerializeField,
            Tooltip("Distance threshold (meters) to classify target as 'distant' for quality check")
        ]
        [Range(5f, 30f)]
        private float distantTargetThreshold = 12f;

        // Spatial validation tracking
        private Vector3 lastValidatedPosition = Vector3.zero;
        private float lastValidatedPositionTime = -1f;

        // Global position reference tracking
        private Matrix4x4 globalReferenceMatrix = Matrix4x4.identity;
        private Matrix4x4 globalReferenceInverse = Matrix4x4.identity;
        private Matrix4x4 currentGroupPoseMatrix = Matrix4x4.identity;
        private bool hasGlobalReferencePose = false;

        /// <summary>
        /// Invoked whenever the global pose root (MultiArea group pose) is updated.
        /// Provides the latest group pose matrix in world space.
        /// </summary>
        public event Action<Matrix4x4> GlobalPoseUpdated;

        void Awake()
        {
            // Get tracking manager (automatically added by RequireComponent)
            trackingManager = GetComponent<ARSafeTrackingManager>();

            // Find AR Camera if not assigned
            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null)
                {
                    Debug.LogError(
                        "[ARSafeActivationController] No AR Camera found! Assign manually or ensure Camera.main is set."
                    );
                }
            }

            // CRITICAL: Auto-discover Area Targets in Awake (before Vuforia's Start())
            if (allAreaTargets == null || allAreaTargets.Count == 0)
            {
                DiscoverAreaTargets();
            }

            // CRITICAL FIX: Apply preselected location BEFORE deactivating targets
            // This ensures only the selected location is in startingTargets when deactivation runs
            ApplyPreselectedLocation();

            // CRITICAL: Deactivate non-starting targets in Awake (before Vuforia tries to activate observers in Start())
            if (deactivateNonStartingTargetsOnStart)
            {
                DeactivateNonStartingTargetsEarly();
            }
        }

        void Start()
        {
            // CRITICAL: Initialize Vuforia with delayed initialization enabled
            // This ensures Vuforia only starts when user selects a simulation
            InitializeVuforia();

            hasLocalized = false;

            // CRITICAL FIX: Initialize sortBuffer before use (prevents NullReferenceException)
            int initialCapacity = Mathf.Max(allAreaTargets.Count, MIN_SORT_BUFFER_CAPACITY);
            sortBuffer = new (ObserverBehaviour, float, ARSafeTargetInfo)[initialCapacity];

            // Initialize performance optimization buffers
            EnsureSortBufferCapacity(initialCapacity);

            // Build target info map
            BuildTargetInfoMap();

            // Validate starting targets
            ValidateStartingTargets();

            // MultiArea mode initialization (always on)
            InitializeMultiAreaMode();

            // NOTE: ApplyPreselectedLocation() now called in Awake() before target deactivation
            // This ensures only the selected location is activated, not all default starting targets

            // Initial activation of starting targets
            ActivateStartingTargets();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ARSafeActivationController] Initialized with {allAreaTargets.Count} targets, {startingTargets.Count} starting targets. MultiArea mode enabled."
                );
            }
        }

        /// <summary>
        /// Initialize Vuforia Engine when simulation starts.
        /// Called at the beginning of Start() to ensure Vuforia is ready before targets are activated.
        /// </summary>
        private void InitializeVuforia()
        {
            if (VuforiaApplication.Instance == null)
            {
                Debug.LogError(
                    "[ARSafeActivationController] VuforiaApplication.Instance is null! Cannot initialize Vuforia."
                );
                return;
            }

            if (VuforiaApplication.Instance.IsRunning)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[ARSafeActivationController] Vuforia is already running.");
                }
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    "<color=cyan>[ARSafeActivationController] Initializing Vuforia Engine...</color>"
                );
            }

            // Initialize Vuforia
            VuforiaApplication.Instance.Initialize();

            if (enableDebugLogs)
            {
                Debug.Log(
                    "<color=green>[ARSafeActivationController] ✓ Vuforia initialization requested.</color>"
                );
            }
        }

        void OnDestroy()
        {
            // CRITICAL: Disable all currently enabled targets to prevent persistence across scene transitions
            if (currentlyEnabled != null && currentlyEnabled.Count > 0)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] OnDestroy: Disabling {currentlyEnabled.Count} active targets"
                    );
                }

                var targetsToDisable = new List<ObserverBehaviour>(currentlyEnabled);
                foreach (var target in targetsToDisable)
                {
                    if (target != null)
                    {
                        DisableTarget(target);
                    }
                }
            }

            // CRITICAL: Stop Vuforia entirely when simulation is closed
            // This ensures a clean state when user returns to menu or selects another simulation
            if (VuforiaApplication.Instance != null && VuforiaApplication.Instance.IsRunning)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("[ARSafeActivationController] OnDestroy: Stopping Vuforia Engine");
                }

                VuforiaApplication.Instance.Deinit();
            }

            // CRITICAL: Cleanup all dictionaries and references to prevent memory leaks
            if (targetInfoMap != null)
            {
                targetInfoMap.Clear();
                targetInfoMap = null;
            }

            if (relativePoses != null)
            {
                relativePoses.Clear();
            }

            if (allAreaTargets != null)
            {
                allAreaTargets.Clear();
                allAreaTargets = null;
            }

            if (startingTargets != null)
            {
                startingTargets.Clear();
                startingTargets = null;
            }

            // Clear sort buffer
            sortBuffer = null;

            // Clear debug builder
            if (debugBuilder != null)
            {
                debugBuilder.Clear();
                debugBuilder = null;
            }

            // Clear references
            currentAnchor = null;
            pendingNeighborSwitch = null;
            currentBestTracked = null;
            augmentationsRoot = null;

            RestoreAllAugmentationsToOriginalParent();
            augmentationParentingStates.Clear();

            ClearGlobalPositionReference();
            GlobalPoseUpdated = null;

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeActivationController] Cleanup completed");
            }
        }

        void Update()
        {
            // CRITICAL: Null checks to prevent errors after scene transitions or cleanup
            if (
                arCamera == null
                || trackingManager == null
                || allAreaTargets == null
                || allAreaTargets.Count == 0
            )
                return;

            // CRITICAL: Check if we need to confirm localization
            // This happens when currentAnchor is set but hasLocalized is still false
            // (e.g., from preselected location or after relocalization)
            if (!hasLocalized && currentAnchor != null && trackingManager.IsTracking(currentAnchor))
            {
                ConfirmLocalization();
            }

            // MultiArea mode: Update root pose for drift correction
            UpdateMultiAreaPose();

            // Update activation on interval
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateActivation();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Main activation logic: keep MultiArea anchor and its neighborhood active
        /// </summary>
        private void UpdateActivation()
        {
            if (arCamera == null || allAreaTargets.Count == 0)
            {
                return;
            }

            // OPTIMIZED: Cache camera position per frame (avoid repeated transform access)
            if (lastCameraUpdateFrame != Time.frameCount)
            {
                cachedCameraPosition = arCamera.transform.position;
                lastCameraUpdateFrame = Time.frameCount;
            }

            var targetData = BuildTargetData(cachedCameraPosition);

            UpdateAnchorSelection(targetData);

            var desiredTargets = BuildDesiredActivationSet(targetData);
            ApplyActivationSet(desiredTargets);
        }

        private List<(
            ObserverBehaviour target,
            float distance,
            ARSafeTargetInfo info
        )> BuildTargetData(Vector3 cameraPos)
        {
            var targetData = new List<(ObserverBehaviour, float, ARSafeTargetInfo)>(
                allAreaTargets.Count
            );

            // OPTIMIZED: Single loop, cached calculations
            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                // Cache transform position (avoid repeated transform access)
                Vector3 targetPos = target.transform.position;
                float distance = Vector3.Distance(cameraPos, targetPos);

                ARSafeTargetInfo info = null;
                if (targetInfoMap.TryGetValue(target, out var mappedInfo))
                {
                    info = mappedInfo;

                    // Update cached distances (reused across multiple systems)
                    info.DistanceToCamera = distance;

                    // OPTIMIZED: Only compute boundary distance if needed (expensive operation)
                    // Boundary calculation uses InverseTransformPoint which is costly
                    info.DistanceToBoundary = info.ComputeDistanceToBoundary(cameraPos);
                }

                targetData.Add((target, distance, info));
            }

            return targetData;
        }

        private void UpdateAnchorSelection(
            List<(ObserverBehaviour target, float distance, ARSafeTargetInfo info)> targetData
        )
        {
            // CRITICAL: If we're in grace period, block ALL anchor switches
            // This gives the newly switched anchor time to establish tracking
            bool inGracePeriod = Time.time < trackingGracePeriodEndTime;
            if (inGracePeriod && currentAnchor != null)
            {
                if (enableDebugLogs)
                {
                    float remainingGracePeriod = trackingGracePeriodEndTime - Time.time;
                    Debug.Log(
                        $"<color=yellow>[GRACE PERIOD] Blocking all anchor switches - {remainingGracePeriod:F1}s remaining for {currentAnchor.name} to establish tracking</color>"
                    );
                }
                return; // Don't evaluate any anchor switches during grace period
            }

            // CRITICAL FIX: Check for ENABLED neighbors where user is INSIDE FIRST
            // This handles the case where user moves to a neighbor that isn't tracking yet
            // We need to switch BEFORE waiting for it to track, to help Vuforia prioritize it
            // SAFEGUARD: Require dwell time to prevent rapid/accidental switches
            if (hasLocalized && currentAnchor != null)
            {
                ObserverBehaviour bestNeighborInside = null;
                ARSafeTargetInfo bestNeighborInfo = null;
                float bestNeighborBoundaryDist = 0f;

                // CRITICAL: Also check if we're inside the current anchor
                ARSafeTargetInfo currentAnchorInfo = targetInfoMap.ContainsKey(currentAnchor)
                    ? targetInfoMap[currentAnchor]
                    : null;
                bool currentAnchorInsideUser =
                    currentAnchorInfo != null && currentAnchorInfo.DistanceToBoundary < 0f;

                foreach (var (target, distance, info) in targetData)
                {
                    if (target == null || info == null || target == currentAnchor)
                    {
                        continue;
                    }

                    // Check if this is an enabled neighbor and user is INSIDE its boundary
                    bool isEnabled = currentlyEnabled.Contains(target);
                    bool isNeighbor = IsNeighborOfCurrentAnchor(target);
                    bool isInside = info.DistanceToBoundary < 0f;

                    if (isEnabled && isNeighbor && isInside)
                    {
                        // CRITICAL FIX: Apply hysteresis when choosing between overlapping targets
                        // If user is inside BOTH current anchor AND this neighbor, require significant depth advantage
                        // to prevent ping-pong between overlapping boundaries
                        if (currentAnchorInsideUser)
                        {
                            // User is inside both - require hysteresis advantage (3m deeper by default)
                            float depthAdvantage =
                                currentAnchorInfo.DistanceToBoundary - info.DistanceToBoundary;

                            if (depthAdvantage < anchorSwitchHysteresis)
                            {
                                // Not enough advantage - stay with current anchor
                                if (enableDebugLogs)
                                {
                                    Debug.Log(
                                        $"<color=gray>[HYSTERESIS] Skipping neighbor {target.name} - inside both targets, needs {anchorSwitchHysteresis}m advantage, has {depthAdvantage:F2}m</color>"
                                    );
                                }
                                continue; // Skip this neighbor
                            }

                            if (enableDebugLogs)
                            {
                                Debug.Log(
                                    $"<color=green>[HYSTERESIS] Neighbor {target.name} has sufficient advantage: {depthAdvantage:F2}m > {anchorSwitchHysteresis}m threshold</color>"
                                );
                            }
                        }

                        // Find the neighbor where user is DEEPEST inside (most negative boundary distance)
                        if (
                            bestNeighborInside == null
                            || info.DistanceToBoundary < bestNeighborBoundaryDist
                        )
                        {
                            bestNeighborInside = target;
                            bestNeighborInfo = info;
                            bestNeighborBoundaryDist = info.DistanceToBoundary;
                        }
                    }
                }

                // If we found a neighbor the user is inside, check dwell time requirement
                if (bestNeighborInside != null)
                {
                    // Check if this is a new pending switch or continuing previous one
                    if (pendingNeighborSwitch != bestNeighborInside)
                    {
                        // New neighbor detected - start dwell timer
                        pendingNeighborSwitch = bestNeighborInside;
                        pendingNeighborSwitchTime = Time.time;

                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[NEIGHBOR SWITCH] Started dwell timer for {bestNeighborInside.name} "
                                    + $"(boundary={bestNeighborBoundaryDist:F2}m, dwell time={neighborBoundaryDwellTime:F1}s required)</color>"
                            );
                        }
                    }
                    else
                    {
                        // Same neighbor - check if dwell time has elapsed
                        float dwellTime = Time.time - pendingNeighborSwitchTime;

                        if (dwellTime >= neighborBoundaryDwellTime)
                        {
                            // Dwell time satisfied - switch now!
                            bool isTracking = trackingManager.IsTracking(bestNeighborInside);

                            if (enableDebugLogs)
                            {
                                Debug.Log(
                                    $"<color=cyan>★★★ IMMEDIATE NEIGHBOR SWITCH (PRE-TRACKING CHECK)!</color>"
                                );
                                Debug.Log(
                                    $"User INSIDE enabled neighbor '{bestNeighborInside.name}' for {dwellTime:F1}s"
                                );
                                Debug.Log(
                                    $"  Tracking={isTracking}, Boundary={bestNeighborBoundaryDist:F2}m"
                                );
                                Debug.Log(
                                    $"  Switching from {currentAnchor.name} → {bestNeighborInside.name} BEFORE waiting for tracking"
                                );
                            }

                            // Reset pending state
                            pendingNeighborSwitch = null;
                            pendingNeighborSwitchTime = -1f;

                            SwitchAnchor(bestNeighborInside);
                            return;
                        }
                        else if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[NEIGHBOR SWITCH] Dwelling in {bestNeighborInside.name}: {dwellTime:F1}s / {neighborBoundaryDwellTime:F1}s</color>"
                            );
                        }
                    }
                }
                else
                {
                    // User not inside any neighbor - reset pending state
                    if (pendingNeighborSwitch != null)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=gray>[NEIGHBOR SWITCH] User left {pendingNeighborSwitch.name} boundary - resetting dwell timer</color>"
                            );
                        }
                        pendingNeighborSwitch = null;
                        pendingNeighborSwitchTime = -1f;
                    }
                }
            }

            var candidate = DetermineBestAnchor(targetData);

            if (candidate == null)
            {
                if (enableDebugLogs)
                {
                    int trackingCount = targetData.Count(t =>
                        t.target != null && trackingManager.IsTracking(t.target)
                    );
                    Debug.LogWarning(
                        $"<color=red>[ANCHOR DEBUG] No anchor candidate found! Tracking: {trackingCount}/{targetData.Count}</color>"
                    );

                    foreach (var (target, distance, info) in targetData)
                    {
                        if (target != null)
                        {
                            bool isTracking = trackingManager.IsTracking(target);
                            var status = trackingManager.GetTrackingStatus(target);
                            Debug.Log(
                                $"  → {target.name}: tracking={isTracking}, status={status}, distance={distance:F1}m"
                            );
                        }
                    }
                }
                return;
            }

            // Declare info variables once for the entire method scope
            var candidateInfo = targetInfoMap.ContainsKey(candidate)
                ? targetInfoMap[candidate]
                : null;
            var currentInfo =
                currentAnchor != null && targetInfoMap.ContainsKey(currentAnchor)
                    ? targetInfoMap[currentAnchor]
                    : null;

            if (currentAnchor == candidate)
            {
                if (enableDebugLogs)
                {
                    bool isInside = candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;
                    string insideStatus = isInside
                        ? "<color=green>INSIDE</color>"
                        : "<color=gray>outside</color>";
                    Debug.Log(
                        $"[ARSafeActivationController] Anchor unchanged: {candidate.name} "
                            + $"(distance={GetDistance(targetData, candidate):F2}m, "
                            + $"boundary={candidateInfo?.DistanceToBoundary:F2}m {insideStatus}, "
                            + $"tracking={trackingManager.IsTracking(candidate)})"
                    );
                }
                return;
            }

            // Enhanced debug logging for anchor switch analysis
            if (enableDebugLogs)
            {
                Debug.Log($"<color=yellow>=== ANCHOR SWITCHING ANALYSIS ===</color>");
                Debug.Log(
                    $"Current: {currentAnchor?.name ?? "None"} → Candidate: {candidate.name}"
                );

                bool candidateInside =
                    candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;
                bool currentInside = currentInfo != null && currentInfo.DistanceToBoundary < 0f;

                Debug.Log(
                    $"Candidate {candidate.name}: distance={GetDistance(targetData, candidate):F2}m, "
                        + $"boundary={candidateInfo?.DistanceToBoundary:F2}m ({(candidateInside ? "<color=green>INSIDE</color>" : "<color=gray>outside</color>")})"
                );

                if (currentAnchor != null)
                {
                    Debug.Log(
                        $"Current {currentAnchor.name}: distance={GetDistance(targetData, currentAnchor):F2}m, "
                            + $"boundary={currentInfo?.DistanceToBoundary:F2}m ({(currentInside ? "<color=green>INSIDE</color>" : "<color=gray>outside</color>")})"
                    );
                }
            }

            bool currentTracking =
                currentAnchor != null && trackingManager.IsTracking(currentAnchor);

            if (currentAnchor == null || !currentTracking)
            {
                // CRITICAL: If we just switched to this anchor, give it grace period to establish tracking
                // Don't immediately fall back to previous anchor (prevents rapid back-and-forth)
                bool trackingGracePeriodActive = Time.time < trackingGracePeriodEndTime;

                if (trackingGracePeriodActive && currentAnchor != null)
                {
                    if (enableDebugLogs)
                    {
                        float remainingGracePeriod = trackingGracePeriodEndTime - Time.time;
                        Debug.Log(
                            $"<color=yellow>[TRACKING] {currentAnchor.name} not tracking yet, but in grace period ({remainingGracePeriod:F1}s remaining)</color>"
                        );
                    }
                    return; // Don't switch away yet, give it more time
                }

                // FIX: Require minimum stability time before allowing switch away from current anchor
                // This prevents ping-pong switching when temporarily losing tracking
                float timeOnCurrentAnchor =
                    currentAnchor != null ? (Time.time - currentAnchorStartTime) : 0f;
                if (currentAnchor != null && timeOnCurrentAnchor < MIN_ANCHOR_STABILITY_TIME)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=yellow>[STABILITY] {currentAnchor.name} lost tracking but only been active {timeOnCurrentAnchor:F1}s < {MIN_ANCHOR_STABILITY_TIME}s - staying put</color>"
                        );
                    }
                    return; // Stay on current anchor - it's too soon to switch
                }

                // SIMPLIFIED APPROACH: Prioritize current anchor to re-track
                // Only switch if user has clearly moved (entered a neighbor's boundary)
                // This prevents deadlock and is much simpler than complex distance thresholds

                if (currentAnchor != null)
                {
                    // Check if user is still within or near current anchor's boundary
                    bool currentInside = currentInfo != null && currentInfo.DistanceToBoundary < 0f;
                    bool candidateInside =
                        candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;

                    // If user is INSIDE current anchor, stay on it even without tracking
                    // Let Vuforia re-establish tracking on the correct anchor
                    if (currentInside)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[TRACKING LOSS] {currentAnchor.name} lost tracking, but user still INSIDE boundary ({currentInfo.DistanceToBoundary:F1}m)</color>"
                            );
                            Debug.Log(
                                $"<color=cyan>[TRACKING LOSS] Staying on {currentAnchor.name} - prioritizing current anchor to re-track</color>"
                            );
                        }
                        return; // Stay on current anchor - let it re-track
                    }

                    // If user has moved INTO a neighbor's boundary, allow the switch
                    // This is natural movement progression
                    if (candidateInside && IsNeighborOfCurrentAnchor(candidate))
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=cyan>[TRACKING LOSS] User moved into neighbor {candidate.name} boundary - allowing natural progression</color>"
                            );
                        }
                        // Fall through to switch
                    }
                    else
                    {
                        // User not clearly inside another area - stay on current anchor
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[TRACKING LOSS] {currentAnchor.name} lost tracking, user OUTSIDE boundaries</color>"
                            );
                            Debug.Log(
                                $"<color=cyan>[TRACKING LOSS] Staying on {currentAnchor.name} - prioritizing current anchor to re-track (user hasn't moved into neighbor)</color>"
                            );
                        }
                        return; // Stay on current anchor - let it re-track
                    }
                }

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Switching anchor: {(currentAnchor != null ? currentAnchor.name : "None")} → {candidate.name} "
                            + $"(reason: {(currentAnchor == null ? "initial anchor" : "user moved into neighbor boundary")})"
                    );
                }
                SwitchAnchor(candidate);
                return;
            }

            // ENHANCED: Check for boundary-based switching (highest priority)
            bool candidateIsInside = candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;
            bool candidateIsNeighbor = IsNeighborOfCurrentAnchor(candidate);
            bool isBoundarySwitchToNeighbor = candidateIsInside && candidateIsNeighbor;

            if (isBoundarySwitchToNeighbor)
            {
                // SAFEGUARD: Also require dwell time for boundary-based switches
                // This prevents rapid switching between overlapping neighbors
                if (
                    pendingNeighborSwitch == candidate
                    && Time.time - pendingNeighborSwitchTime >= neighborBoundaryDwellTime
                )
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=cyan>★★★ BOUNDARY-BASED ANCHOR SWITCH (after dwell time)!</color>"
                        );
                        Debug.Log(
                            $"User INSIDE neighbor '{candidate.name}' boundary (distance: {candidateInfo.DistanceToBoundary:F2}m)"
                        );
                        Debug.Log($"Bypassing cooldown and distance checks for immediate switch");
                        Debug.Log($"Switch: {currentAnchor.name} → {candidate.name}");
                    }

                    // Reset pending state
                    pendingNeighborSwitch = null;
                    pendingNeighborSwitchTime = -1f;

                    SwitchAnchor(candidate);
                    return;
                }
                else if (enableDebugLogs && pendingNeighborSwitch == candidate)
                {
                    float dwellTime = Time.time - pendingNeighborSwitchTime;
                    Debug.Log(
                        $"<color=yellow>[BOUNDARY SWITCH] Dwell time check: {dwellTime:F1}s / {neighborBoundaryDwellTime:F1}s for {candidate.name}</color>"
                    );
                }
                // If dwell time not satisfied, block this switch path and continue to other checks
            }

            if (Time.time - lastAnchorSwitchTime < anchorSwitchCooldown)
            {
                if (enableDebugLogs)
                {
                    float timeSinceSwitch = Time.time - lastAnchorSwitchTime;
                    Debug.Log(
                        $"[ARSafeActivationController] Anchor switch blocked by cooldown: {currentAnchor.name} → {candidate.name} "
                            + $"(cooldown remaining: {(anchorSwitchCooldown - timeSinceSwitch):F1}s)"
                    );
                }
                return;
            }

            float candidateDistance = GetDistance(targetData, candidate);
            float currentDistance = GetDistance(targetData, currentAnchor);

            if (candidateDistance <= anchorSwitchDistance || currentDistance < 0f)
            {
                if (enableDebugLogs)
                {
                    bool candidateInside =
                        candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;
                    bool currentInside = currentInfo != null && currentInfo.DistanceToBoundary < 0f;

                    Debug.Log(
                        $"<color=cyan>★★★ [ARSafeActivationController] ANCHOR SWITCH APPROVED: {currentAnchor.name} → {candidate.name}</color>\n"
                            + $"  Current: distance={currentDistance:F2}m, boundary={currentInfo?.DistanceToBoundary:F2}m ({(currentInside ? "<color=green>INSIDE</color>" : "<color=gray>outside</color>")})\n"
                            + $"  Candidate: distance={candidateDistance:F2}m, boundary={candidateInfo?.DistanceToBoundary:F2}m ({(candidateInside ? "<color=green>INSIDE</color>" : "<color=gray>outside</color>")})\n"
                            + $"  Reason: {(candidateDistance <= anchorSwitchDistance ? $"<color=yellow>within switch distance ({anchorSwitchDistance}m)</color>" : "<color=red>current distance invalid</color>")}\n"
                            + $"  Switch threshold: {anchorSwitchDistance}m | Cooldown: {anchorSwitchCooldown}s"
                    );
                }
                SwitchAnchor(candidate);
            }
            else if (enableDebugLogs)
            {
                bool candidateInside =
                    candidateInfo != null && candidateInfo.DistanceToBoundary < 0f;
                Debug.Log(
                    $"<color=red>[ARSafeActivationController] ANCHOR SWITCH BLOCKED: {currentAnchor.name} vs {candidate.name}</color>\n"
                        + $"  Current distance: {currentDistance:F2}m\n"
                        + $"  Candidate distance: {candidateDistance:F2}m ({(candidateInside ? "<color=green>INSIDE boundary</color>" : "<color=gray>outside boundary</color>")})\n"
                        + $"  Switch threshold: {anchorSwitchDistance:F2}m\n"
                        + $"  <color=yellow>REASON: Candidate too far ({candidateDistance:F2}m > {anchorSwitchDistance:F2}m)</color>"
                );
            }
        }

        /// <summary>
        /// Checks if a target is a neighbor of the current anchor (appears in adjacency lists or connected rooms)
        /// </summary>
        public bool IsNeighborOfCurrentAnchor(ObserverBehaviour target)
        {
            if (currentAnchor == null || target == null)
            {
                return false;
            }

            if (!targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo) || anchorInfo == null)
            {
                return false;
            }

            // Check if target is in the anchor's adjacency list
            var adjacentTargets = anchorInfo.GetAllAdjacentTargets();
            if (adjacentTargets != null)
            {
                if (!targetInfoMap.TryGetValue(target, out var targetInfo))
                {
                    return false;
                }

                foreach (var adjacent in adjacentTargets)
                {
                    if (adjacent == targetInfo)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private ObserverBehaviour DetermineBestAnchor(
            List<(ObserverBehaviour target, float distance, ARSafeTargetInfo info)> targetData
        )
        {
            ObserverBehaviour SelectBestCandidate(
                IEnumerable<ObserverBehaviour> candidates,
                bool allowNonTracking = false
            )
            {
                ObserverBehaviour bestCandidate = null;
                float bestCandidateCenterDistance = float.MaxValue;
                int bestCandidateTrackingQuality = int.MaxValue;
                bool bestCandidateIsInside = false;
                TargetType bestCandidateType = TargetType.Hallway;

                foreach (var target in candidates)
                {
                    // CRITICAL FIX: When allowNonTracking is true, consider current anchor even if not tracking
                    // This prevents reverting to first starting target on temporary tracking loss
                    bool isCurrentAnchor = target == currentAnchor;
                    bool isTracking = trackingManager.IsTracking(target);

                    if (target == null)
                    {
                        continue;
                    }

                    // Skip non-tracking targets UNLESS it's the current anchor and we're allowing non-tracking
                    if (!isTracking && !(allowNonTracking && isCurrentAnchor))
                    {
                        continue;
                    }

                    // Get info for this target
                    if (!targetInfoMap.TryGetValue(target, out var info))
                    {
                        continue;
                    }

                    // Use CENTER distance for anchor selection (more stable for overlapping targets)
                    float centerDistance = info.DistanceToCamera;
                    if (centerDistance < 0f)
                    {
                        continue;
                    }

                    // Check if user is INSIDE boundary
                    bool isInside = info.DistanceToBoundary < 0f;

                    // Get tracking quality (TRACKED=0 is best)
                    var status = trackingManager.GetTrackingStatus(target);
                    int trackingQuality = GetStatusPriority(status);

                    // Get target type for tiebreaking
                    TargetType targetType = info.targetType;

                    if (enableDebugLogs)
                    {
                        string insideText = isInside
                            ? $"<color=green>INSIDE</color> ({Mathf.Abs(info.DistanceToBoundary):F1}m)"
                            : $"outside ({info.DistanceToBoundary:F1}m)";
                        Debug.Log(
                            $"[ARSafeActivationController] Evaluating {target.name}: center={centerDistance:F1}m, {insideText}, quality={status}, type={targetType}"
                        );
                    }

                    // DECISION PRIORITY (in order):
                    // 1. User INSIDE boundary (huge advantage)
                    // 2. Tracking quality (TRACKED > EXTENDED_TRACKED > LIMITED)
                    // 3. Center distance (closer is better)
                    // 4. Target type (Room > Hallway for tiebreaking in overlaps)
                    // 5. SPECIAL: Current anchor gets preference when tracking quality is close (prevents rapid switching)

                    bool shouldReplace = false;

                    if (bestCandidate == null)
                    {
                        shouldReplace = true;
                    }
                    else if (isInside && !bestCandidateIsInside)
                    {
                        // RULE 1: Always prefer target user is INSIDE
                        shouldReplace = true;
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=cyan>★★★ [ARSafeActivationController] INSIDE PRIORITY: {target.name} (INSIDE) replaces {bestCandidate.name} (outside)</color>"
                            );
                        }
                    }
                    else if (!isInside && bestCandidateIsInside)
                    {
                        // Current best is inside, candidate is outside - never replace
                        shouldReplace = false;
                    }
                    else if (isTracking && !trackingManager.IsTracking(bestCandidate))
                    {
                        // RULE 2A: Tracking target beats non-tracking target (when allowing non-tracking)
                        shouldReplace = true;
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=cyan>★★★ [ARSafeActivationController] TRACKING PRIORITY: {target.name} (tracking) replaces {bestCandidate.name} (not tracking)</color>"
                            );
                        }
                    }
                    else if (!isTracking && trackingManager.IsTracking(bestCandidate))
                    {
                        // Current best is tracking, this is not - never replace
                        shouldReplace = false;
                    }
                    else if (trackingQuality < bestCandidateTrackingQuality)
                    {
                        // RULE 2B: Better tracking quality wins (both tracking or both non-tracking)
                        shouldReplace = true;
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=cyan>★★★ [ARSafeActivationController] TRACKING QUALITY: {target.name} (quality={trackingQuality}) replaces {bestCandidate.name} (quality={bestCandidateTrackingQuality})</color>"
                            );
                        }
                    }
                    else if (trackingQuality == bestCandidateTrackingQuality)
                    {
                        // RULE 3: Same quality, check distance (with overlap handling + current anchor bias)
                        float distanceDiff = centerDistance - bestCandidateCenterDistance;

                        // Apply hysteresis to prevent ping-pong:
                        // 1. If both inside boundaries (overlapping area): Use full hysteresis threshold
                        // 2. If current anchor is bestCandidate: Add extra bias to keep it (prevent unnecessary switches)
                        float hysteresisThreshold =
                            (isInside && bestCandidateIsInside)
                                ? anchorSwitchHysteresis
                                : anchorSwitchHysteresis * 0.5f;

                        // Extra bias: Current anchor needs to be significantly worse to lose
                        bool bestIsCurrentAnchor = bestCandidate == currentAnchor;
                        if (bestIsCurrentAnchor && hasLocalized)
                        {
                            hysteresisThreshold += anchorSwitchHysteresis * 0.5f; // Add 50% bonus
                        }

                        if (distanceDiff < -hysteresisThreshold)
                        {
                            shouldReplace = true;
                        }
                        else if (Mathf.Abs(distanceDiff) <= hysteresisThreshold)
                        {
                            // RULE 4: Nearly same distance - prefer Room over Hallway (more specific)
                            bool candidateIsRoomLike =
                                targetType == TargetType.Room || targetType == TargetType.Exit;
                            bool bestIsHallway = bestCandidateType == TargetType.Hallway;

                            if (candidateIsRoomLike && bestIsHallway)
                            {
                                shouldReplace = true;
                                if (enableDebugLogs)
                                {
                                    Debug.Log(
                                        $"<color=cyan>★ [ARSafeActivationController] TYPE PRIORITY: {target.name} (Room) replaces {bestCandidate.name} (Hallway) in overlap</color>"
                                    );
                                }
                            }
                        }
                    }

                    if (shouldReplace)
                    {
                        bestCandidate = target;
                        bestCandidateCenterDistance = centerDistance;
                        bestCandidateTrackingQuality = trackingQuality;
                        bestCandidateIsInside = isInside;
                        bestCandidateType = targetType;
                    }
                }

                return bestCandidate;
            }

            // Before localization, strongly prefer whichever starting target is currently tracked.
            if (!hasLocalized && startingTargets.Count > 0)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Pre-localization anchor search (starting targets only, count={startingTargets.Count})"
                    );
                }

                // Allow current anchor to be considered even if not tracking (prevents reverting to first target)
                var startingAnchor = SelectBestCandidate(startingTargets, allowNonTracking: true);
                if (startingAnchor != null)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[ARSafeActivationController] Pre-localization anchor selected: {startingAnchor.name}"
                        );
                    }
                    return startingAnchor;
                }
                else if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        "[ARSafeActivationController] No starting target is tracking yet"
                    );
                }
            }

            // After localization (or if no starting target is tracked yet), fall back to global search.
            if (enableDebugLogs && hasLocalized)
            {
                var trackingCount = targetData.Count(t =>
                    t.target != null && trackingManager.IsTracking(t.target)
                );
                Debug.Log(
                    $"[ARSafeActivationController] Post-localization anchor search (all targets, tracking={trackingCount}/{targetData.Count})"
                );
            }

            // CRITICAL FIX: Allow current anchor to be considered even if temporarily lost tracking
            // This prevents reverting to starting targets when user is deep in the building
            var bestAnchor = SelectBestCandidate(
                targetData.Select(tuple => tuple.target),
                allowNonTracking: true
            );

            if (enableDebugLogs && bestAnchor != null)
            {
                var anchorInfo = targetInfoMap.ContainsKey(bestAnchor)
                    ? targetInfoMap[bestAnchor]
                    : null;
                Debug.Log(
                    $"[ARSafeActivationController] Best anchor determined: {bestAnchor.name} "
                        + $"(type={anchorInfo?.targetType}, priority={anchorInfo?.basePriority}, "
                        + $"distance={GetDistance(targetData, bestAnchor):F2}m, "
                        + $"boundary={anchorInfo?.DistanceToBoundary:F2}m)"
                );
            }

            return bestAnchor;
        }

        private int GetStatusPriority(Status status)
        {
            switch (status)
            {
                case Status.TRACKED:
                    return 0;
                case Status.EXTENDED_TRACKED:
                    return 1;
                case Status.LIMITED:
                    return 2;
                default:
                    return 3;
            }
        }

        private HashSet<ObserverBehaviour> BuildDesiredActivationSet(
            List<(ObserverBehaviour target, float distance, ARSafeTargetInfo info)> targetData
        )
        {
            var desired = new HashSet<ObserverBehaviour>();

            if (!hasLocalized)
            {
                foreach (var start in startingTargets)
                {
                    if (start != null)
                    {
                        desired.Add(start);
                    }
                }
            }

            // CRITICAL: Skip tracked targets during relocalization (only selected target should be enabled)
            if (alwaysIncludeTrackedTargets && !isRelocalizationInProgress)
            {
                foreach (var (target, _, _) in targetData)
                {
                    if (trackingManager.IsTracking(target))
                    {
                        desired.Add(target);
                    }
                }
            }

            if (currentAnchor != null)
            {
                desired.Add(currentAnchor);

                // CRITICAL: Skip neighbors during relocalization (only selected target should be enabled)
                if (!isRelocalizationInProgress)
                {
                    IncludeAnchorNeighbors(desired, targetData);
                }
            }

            if (
                keepPreviousAnchorActive
                && previousAnchor != null
                && previousAnchor != currentAnchor
            )
            {
                desired.Add(previousAnchor);
            }

            return desired;
        }

        private void IncludeAnchorNeighbors(
            HashSet<ObserverBehaviour> desired,
            List<(ObserverBehaviour target, float distance, ARSafeTargetInfo info)> targetData
        )
        {
            if (!targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo) || anchorInfo == null)
            {
                return;
            }

            // Enhanced debug logging for neighbor selection analysis
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=yellow>=== NEIGHBOR SELECTION FOR {currentAnchor.name} ===</color>"
                );
                Debug.Log($"Anchor Type: {anchorInfo.targetType}");
                Debug.Log($"ConnectedRooms: {(anchorInfo.connectedRooms?.Length ?? 0)} targets");
                Debug.Log($"AdjacentTargets: {(anchorInfo.adjacentTargets?.Length ?? 0)} targets");
                Debug.Log($"MaxNeighborTargets: {maxNeighborTargets} slots available");

                // Show connected rooms list
                if (anchorInfo.connectedRooms != null && anchorInfo.connectedRooms.Length > 0)
                {
                    Debug.Log("<color=cyan>Connected Rooms:</color>");
                    foreach (var room in anchorInfo.connectedRooms)
                    {
                        if (room != null)
                        {
                            Debug.Log($"  - {room.name} (type: {room.targetType})");
                        }
                    }
                }

                // Show adjacent targets list
                if (anchorInfo.adjacentTargets != null && anchorInfo.adjacentTargets.Length > 0)
                {
                    Debug.Log("<color=cyan>Manual Adjacent Targets:</color>");
                    foreach (var adj in anchorInfo.adjacentTargets)
                    {
                        if (adj != null)
                        {
                            Debug.Log($"  - {adj.name} (type: {adj.targetType})");
                        }
                    }
                }

                // Show all adjacency candidates
                var allAdjacent = anchorInfo.GetAllAdjacentTargets();
                Debug.Log(
                    $"<color=green>Total Adjacency Candidates: {(allAdjacent?.Length ?? 0)}</color>"
                );
                if (allAdjacent != null)
                {
                    foreach (var adj in allAdjacent)
                    {
                        if (adj != null)
                        {
                            float distance = GetDistance(
                                targetData,
                                adj.GetComponent<ObserverBehaviour>()
                            );
                            Debug.Log(
                                $"  - {adj.name} (type: {adj.targetType}, distance: {distance:F1}m)"
                            );
                        }
                    }
                }
            }

            // Always include the other part of a multi-part room
            if (anchorInfo.IsPartOfMultiPartRoom())
            {
                var otherPartInfo = anchorInfo.GetOtherPart();
                var otherObserver =
                    otherPartInfo != null ? otherPartInfo.GetComponent<ObserverBehaviour>() : null;
                if (otherObserver != null)
                {
                    desired.Add(otherObserver);
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=magenta>Added multi-part partner: {otherObserver.name}</color>"
                        );
                    }
                }
            }

            int remainingSlots = Mathf.Max(0, maxNeighborTargets);
            var adjacencyCandidates =
                new List<(
                    ObserverBehaviour observer,
                    float distance,
                    float priority,
                    float boundaryDist,
                    bool isDirectlyConnected
                )>();

            var adjacentInfos = anchorInfo.GetAllAdjacentTargets();
            if (adjacentInfos != null)
            {
                foreach (var adjacentInfo in adjacentInfos)
                {
                    if (adjacentInfo == null)
                        continue;
                    var observer = adjacentInfo.GetComponent<ObserverBehaviour>();
                    if (observer == null || observer == currentAnchor)
                        continue;

                    float distance = GetDistance(targetData, observer);
                    float priority = adjacentInfo.basePriority;
                    float boundaryDist = adjacentInfo.DistanceToBoundary;

                    // CRITICAL: Check if this target is directly in the anchor's connectedRooms list
                    // Both Hallways and Stairways can have connected rooms
                    bool isDirectlyConnected =
                        (anchorInfo.targetType == TargetType.Hallway || anchorInfo.targetType == TargetType.Stairway)
                        && anchorInfo.connectedRooms != null
                        && System.Array.Exists(
                            anchorInfo.connectedRooms,
                            room => room == adjacentInfo
                        );

                    adjacencyCandidates.Add(
                        (observer, distance, priority, boundaryDist, isDirectlyConnected)
                    );
                }
            }

            // Optimized sorting without LINQ for better performance
            var candidatesArray = adjacencyCandidates.ToArray();
            System.Array.Sort(
                candidatesArray,
                (a, b) =>
                {
                    // Calculate effective priority for candidate A
                    float priorityA = a.priority;
                    if (a.isDirectlyConnected)
                        priorityA += 200f; // Massive boost for directly connected rooms
                    else if (
                        prioritizeTargetsUserIsApproaching
                        && a.boundaryDist <= approachingBoundaryThreshold
                    )
                        priorityA += 50f; // Boost when approaching or inside

                    // Calculate effective priority for candidate B
                    float priorityB = b.priority;
                    if (b.isDirectlyConnected)
                        priorityB += 200f;
                    else if (
                        prioritizeTargetsUserIsApproaching
                        && b.boundaryDist <= approachingBoundaryThreshold
                    )
                        priorityB += 50f;

                    // Sort by priority descending, then by distance ascending
                    int priorityComparison = priorityB.CompareTo(priorityA); // Descending
                    return priorityComparison != 0
                        ? priorityComparison
                        : a.distance.CompareTo(b.distance); // Then ascending distance
                }
            );

            if (enableDebugLogs && adjacencyCandidates.Count > 0)
            {
                Debug.Log(
                    $"<color=cyan>Adjacency candidates for '{currentAnchor.name}' (sorted by priority):</color>"
                );
                int debugCount = System.Math.Min(candidatesArray.Length, 10);
                for (int i = 0; i < debugCount; i++)
                {
                    var candidate = candidatesArray[i];
                    float effectivePriority = candidate.isDirectlyConnected
                        ? candidate.priority + 200f
                        : (
                            prioritizeTargetsUserIsApproaching
                            && candidate.boundaryDist <= approachingBoundaryThreshold
                                ? candidate.priority + 50f
                                : candidate.priority
                        );
                    string priorityColor = candidate.isDirectlyConnected
                        ? "<color=gold>"
                        : "<color=lightblue>";
                    string priorityText = candidate.isDirectlyConnected
                        ? "+200 CONNECTED"
                        : (
                            prioritizeTargetsUserIsApproaching
                            && candidate.boundaryDist <= approachingBoundaryThreshold
                                ? "+50 approaching"
                                : "base"
                        );

                    Debug.Log(
                        $"  {i + 1}. {priorityColor}{candidate.observer.name}</color>: Priority={effectivePriority:F1} "
                            + $"({priorityText}, distance={candidate.distance:F2}m)"
                    );
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"<color=orange>No adjacency candidates found for {currentAnchor.name}!</color>"
                );
            }

            int addedFromAdjacency = 0;
            foreach (var candidate in candidatesArray)
            {
                if (remainingSlots == 0)
                    break;
                desired.Add(candidate.observer);
                remainingSlots--;
                addedFromAdjacency++;

                if (enableDebugLogs)
                {
                    string selectionReason = candidate.isDirectlyConnected
                        ? "<color=gold>CONNECTED ROOM (+200)</color>"
                        : (
                            prioritizeTargetsUserIsApproaching
                            && candidate.boundaryDist <= approachingBoundaryThreshold
                                ? "<color=lightblue>approaching (+50)</color>"
                                : "adjacent"
                        );
                    Debug.Log(
                        $"<color=green>✓ SELECTED FROM ADJACENCY #{addedFromAdjacency}: {candidate.observer.name}</color> - {selectionReason} (distance: {candidate.distance:F1}m)"
                    );
                }
            }

            if (enableDebugLogs)
            {
                if (addedFromAdjacency > 0)
                {
                    Debug.Log(
                        $"<color=green>Added {addedFromAdjacency}/{maxNeighborTargets} targets from ADJACENCY (slots remaining: {remainingSlots})</color>"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"<color=red>Added 0 targets from adjacency - no valid adjacent targets found!</color>"
                    );
                }
            }

            if (remainingSlots <= 0)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=green>All {maxNeighborTargets} neighbor slots filled by adjacency - no distance fallback needed</color>"
                    );
                }
                return;
            }

            // CHECK STRICT ADJACENCY MODE
            if (strictAdjacencyMode)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=purple>STRICT ADJACENCY MODE: Skipping distance fallback. Only {addedFromAdjacency}/{maxNeighborTargets} neighbors activated (adjacency-only).</color>"
                    );
                    if (addedFromAdjacency < maxNeighborTargets)
                    {
                        Debug.LogWarning(
                            $"<color=orange>Consider adding more adjacent targets to {currentAnchor.name} or disable strictAdjacencyMode</color>"
                        );
                    }
                }
                return;
            }

            // DISTANCE FALLBACK - Only when adjacency doesn't fill all slots
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=orange>⚠️ DISTANCE FALLBACK ACTIVATED: Only {addedFromAdjacency}/{maxNeighborTargets} neighbors from adjacency. "
                        + $"Adding {remainingSlots} more targets by distance.</color>"
                );
                Debug.Log(
                    $"<color=red>WARNING: This suggests {currentAnchor.name} may have insufficient adjacent targets configured!</color>"
                );
                Debug.Log(
                    $"<color=yellow>TIP: Enable 'strictAdjacencyMode' to prevent non-adjacent targets from being activated</color>"
                );
            }

            EnsureSortBufferCapacity(targetData.Count);
            // Optimized distance-based sorting without LINQ (performance improvement)
            int count = System.Math.Min(targetData.Count, sortBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                sortBuffer[i] = targetData[i];
            }

            // In-place sort by distance (no GC allocation)
            System.Array.Sort(sortBuffer, 0, count, new DistanceComparer());

            int addedFromDistance = 0;
            for (int i = 0; i < count && remainingSlots > 0; i++)
            {
                var (target, distance, _) = sortBuffer[i];

                if (target == currentAnchor)
                    continue;
                if (desired.Contains(target))
                    continue;
                if (neighborActivationRadius > 0f && distance > neighborActivationRadius)
                    break;

                desired.Add(target);
                remainingSlots--;
                addedFromDistance++;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=red>⚠️ DISTANCE FALLBACK #{addedFromDistance}: {target.name}</color> (distance={distance:F2}m) - <color=yellow>NOT ADJACENT!</color>"
                    );
                }
            }

            if (enableDebugLogs)
            {
                if (addedFromDistance > 0)
                {
                    Debug.Log(
                        $"<color=red>Added {addedFromDistance} NON-ADJACENT targets from distance fallback</color>"
                    );
                    Debug.Log(
                        $"<color=yellow>RECOMMENDATION: Configure more adjacent targets for {currentAnchor.name} to avoid distance fallback</color>"
                    );
                }
                else
                {
                    Debug.Log(
                        $"<color=orange>Distance fallback found no additional targets within radius</color>"
                    );
                }
            }
        }

        private void EnsureSortBufferCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= 0)
            {
                requiredCapacity = MIN_SORT_BUFFER_CAPACITY;
            }

            if (sortBuffer != null && sortBuffer.Length >= requiredCapacity)
            {
                return;
            }

            int newCapacity = sortBuffer != null ? sortBuffer.Length : MIN_SORT_BUFFER_CAPACITY;
            newCapacity = Mathf.Max(newCapacity, MIN_SORT_BUFFER_CAPACITY);

            while (newCapacity < requiredCapacity)
            {
                newCapacity *= 2;
            }

            sortBuffer = new (ObserverBehaviour, float, ARSafeTargetInfo)[newCapacity];

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ARSafeActivationController] Resized sort buffer to {newCapacity} entries (required {requiredCapacity})."
                );
            }
        }

        private void ApplyActivationSet(HashSet<ObserverBehaviour> desiredTargets)
        {
            var toEnable = new List<ObserverBehaviour>();
            var toDisable = new List<ObserverBehaviour>();

            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                bool shouldEnable = desiredTargets.Contains(target);

                if (alwaysIncludeTrackedTargets && trackingManager.IsTracking(target))
                {
                    shouldEnable = true;
                }

                bool isEnabled = currentlyEnabled.Contains(target);

                if (shouldEnable && !isEnabled)
                {
                    toEnable.Add(target);
                }
                else if (!shouldEnable && isEnabled)
                {
                    toDisable.Add(target);
                }
            }

            if (enableDebugLogs && (toEnable.Count > 0 || toDisable.Count > 0))
            {
                Debug.Log(
                    $"[ARSafeActivationController] Activation changes: +{toEnable.Count} enabled, -{toDisable.Count} disabled (total active: {currentlyEnabled.Count} → {currentlyEnabled.Count + toEnable.Count - toDisable.Count})"
                );

                if (toEnable.Count > 0)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Enabling: {string.Join(", ", toEnable.Select(t => t.name))}"
                    );
                }

                if (toDisable.Count > 0)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Disabling: {string.Join(", ", toDisable.Select(t => t.name))}"
                    );
                }
            }

            foreach (var target in toEnable)
            {
                EnableTarget(target);
            }

            foreach (var target in toDisable)
            {
                DisableTarget(target);
            }

            // CRITICAL: Enforce max simultaneous tracking limit
            // Only the current anchor should have Vuforia tracking enabled
            // Neighbors stay enabled (GameObject active) but tracking disabled
            EnforceTrackingLimit();

            UpdateAugmentationParenting();
        }

        private float GetDistance(
            List<(ObserverBehaviour target, float distance, ARSafeTargetInfo info)> targetData,
            ObserverBehaviour target
        )
        {
            foreach (var (entryTarget, distance, _) in targetData)
            {
                if (entryTarget == target)
                {
                    return distance;
                }
            }

            return arCamera != null
                ? Vector3.Distance(arCamera.transform.position, target.transform.position)
                : -1f;
        }

        /// <summary>
        /// Enable a target (but don't activate observer - ARSafeTrackingManager handles that)
        /// </summary>
        private void EnableTarget(ObserverBehaviour target)
        {
            if (target == null || currentlyEnabled.Contains(target))
                return;

            target.gameObject.SetActive(true);
            currentlyEnabled.Add(target);

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeActivationController] Enabled: {target.name}");
            }
        }

        /// <summary>
        /// Disable a target
        /// </summary>
        private void DisableTarget(ObserverBehaviour target)
        {
            if (target == null || !currentlyEnabled.Contains(target))
                return;

            target.gameObject.SetActive(false);
            currentlyEnabled.Remove(target);

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeActivationController] Disabled: {target.name}");
            }
        }

        /// <summary>
        /// CRITICAL: Enforce max simultaneous tracking limit.
        /// When maxSimultaneousTracking = 1, only the current anchor should track.
        /// Neighbors stay enabled (GameObject active) but ObserverBehaviour.enabled = false.
        /// This prevents augmentation overlap while keeping neighbors discoverable.
        /// </summary>
        private void EnforceTrackingLimit()
        {
            if (trackingManager == null || currentAnchor == null)
                return;

            int maxTracking = trackingManager.maxSimultaneousTracking;

            // If max tracking is unlimited (0 or very high), don't enforce
            if (maxTracking <= 0 || maxTracking >= 10)
                return;

            int trackingEnabledCount = 0;
            int trackingDisabledCount = 0;

            // Iterate through all currently enabled targets
            foreach (var target in currentlyEnabled)
            {
                if (target == null)
                    continue;

                // Current anchor: ALWAYS enable tracking
                if (target == currentAnchor)
                {
                    if (!target.enabled)
                    {
                        target.enabled = true;
                        trackingEnabledCount++;

                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=cyan>[TRACKING ENFORCEMENT] ✓ Enabled tracking on ANCHOR: {target.name}</color>");
                        }
                    }
                }
                // All other targets (neighbors): DISABLE tracking
                else
                {
                    if (target.enabled)
                    {
                        target.enabled = false;
                        trackingDisabledCount++;

                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=yellow>[TRACKING ENFORCEMENT] ✗ Disabled tracking on neighbor: {target.name}</color> (GameObject stays active)");
                        }
                    }
                }
            }

            if (enableDebugLogs && (trackingEnabledCount > 0 || trackingDisabledCount > 0))
            {
                Debug.Log(
                    $"<color=lime>[TRACKING ENFORCEMENT] Enforced max tracking limit: {maxTracking}</color>\n" +
                    $"  Anchor tracking enabled: {trackingEnabledCount}\n" +
                    $"  Neighbor tracking disabled: {trackingDisabledCount}\n" +
                    $"  Expected tracking count: 1 (anchor only)"
                );
            }
        }

        /// <summary>
        /// Validate that an anchor switch is spatially plausible (prevents Vuforia from tracking wrong targets)
        /// Returns true if the switch is valid, false if it should be rejected
        /// </summary>
        private bool ValidateAnchorSwitch(ObserverBehaviour candidate, out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (!enableSpatialValidation)
            {
                return true; // Validation disabled
            }

            if (currentAnchor == null)
            {
                // First anchor - always valid
                return true;
            }

            if (candidate == currentAnchor)
            {
                // No switch - always valid
                return true;
            }

            // Get target info
            if (!targetInfoMap.TryGetValue(candidate, out var candidateInfo))
            {
                rejectionReason = "No target info found";
                return false;
            }

            if (!targetInfoMap.TryGetValue(currentAnchor, out var currentInfo))
            {
                rejectionReason = "No current anchor info";
                return false;
            }

            // LAYER 1: ADJACENCY VALIDATION (Highest Priority)
            // Check if candidate is in the adjacency list of current anchor
            bool isAdjacent = IsNeighborOfCurrentAnchor(candidate);

            if (strictAdjacencyMode && !isAdjacent)
            {
                rejectionReason = $"Not adjacent to current anchor (strict adjacency mode enabled)";
                return false;
            }

            // LAYER 2: DISTANCE VALIDATION
            // Calculate 3D distance between target centers
            float targetDistance = Vector3.Distance(
                currentAnchor.transform.position,
                candidate.transform.position
            );

            if (targetDistance > maxAnchorSwitchDistance)
            {
                rejectionReason =
                    $"Too far ({targetDistance:F1}m > {maxAnchorSwitchDistance}m limit) - possible wrong target";
                return false;
            }

            // LAYER 3: MOVEMENT SPEED VALIDATION
            // Check if user could have physically moved this distance
            if (lastValidatedPositionTime > 0)
            {
                float timeSinceLastSwitch = Time.time - lastValidatedPositionTime;
                if (timeSinceLastSwitch > 0.1f) // Ignore very rapid updates
                {
                    float requiredSpeed = targetDistance / timeSinceLastSwitch;
                    if (requiredSpeed > maxMovementSpeed)
                    {
                        rejectionReason =
                            $"Impossible movement speed ({requiredSpeed:F1} m/s > {maxMovementSpeed} m/s limit)";
                        return false;
                    }
                }
            }

            // LAYER 4: TRACKING QUALITY VALIDATION
            // For distant non-adjacent targets, require high tracking quality
            if (!isAdjacent && targetDistance > distantTargetThreshold)
            {
                if (requireTrackedForDistantTargets)
                {
                    var status = trackingManager.GetTrackingStatus(candidate);
                    if (status != Status.TRACKED)
                    {
                        rejectionReason =
                            $"Distant non-adjacent target requires TRACKED status (got {status})";
                        return false;
                    }
                }
            }

            // All validation checks passed
            return true;
        }

        /// <summary>
        /// Switch current anchor to a new target (with spatial validation).
        /// Can be called externally for manual anchor selection (e.g., LocationSelectionController).
        /// </summary>
        public void SwitchAnchor(ObserverBehaviour newAnchor)
        {
            if (newAnchor == currentAnchor)
                return;

            // SPATIAL VALIDATION: Prevent switching to wrong Area Target
            if (!ValidateAnchorSwitch(newAnchor, out string rejectionReason))
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"<color=red>[SPATIAL VALIDATION] Rejected anchor switch to {newAnchor.name}: {rejectionReason}</color>\n"
                            + $"  Current: {currentAnchor?.name}\n"
                            + $"  This prevents Vuforia from tracking the wrong Area Target!"
                    );
                }
                return; // Reject the switch
            }

            // Update validated position for next speed check
            lastValidatedPosition = newAnchor.transform.position;
            lastValidatedPositionTime = Time.time;

            var oldAnchor = currentAnchor;

            // Clear old anchor flag
            if (oldAnchor != null && targetInfoMap.ContainsKey(oldAnchor))
            {
                targetInfoMap[oldAnchor].IsCurrentAnchor = false;
            }

            if (keepPreviousAnchorActive)
            {
                previousAnchor = oldAnchor;
            }
            else
            {
                previousAnchor = null;
            }

            currentAnchor = newAnchor;
            lastAnchorSwitchTime = Time.time;
            currentAnchorStartTime = Time.time; // Track when we started using this anchor

            // Add to anchor history for smart relocalization
            AddToAnchorHistory(newAnchor);

            // CRITICAL: Start tracking grace period for new anchor
            // Give it time to establish tracking before allowing fallback to previous anchor
            trackingGracePeriodEndTime = Time.time + TRACKING_GRACE_PERIOD;

            // Set new anchor flag
            if (targetInfoMap.ContainsKey(newAnchor))
            {
                targetInfoMap[newAnchor].IsCurrentAnchor = true;
            }

            var anchorInfo = targetInfoMap.ContainsKey(newAnchor) ? targetInfoMap[newAnchor] : null;
            Debug.Log(
                $"<color=cyan>[ARSafeActivationController] ★★★ ANCHOR SWITCHED ★★★</color>\n"
                    + $"  Old: {(oldAnchor != null ? oldAnchor.name : "None")}\n"
                    + $"  New: {newAnchor.name}\n"
                    + $"  Type: {anchorInfo?.targetType}\n"
                    + $"  Priority: {anchorInfo?.basePriority}\n"
                    + $"  Distance: {anchorInfo?.DistanceToCamera:F2}m\n"
                    + $"  Boundary: {anchorInfo?.DistanceToBoundary:F2}m {(anchorInfo?.DistanceToBoundary < 0 ? "(INSIDE)" : "(outside)")}\n"
                    + $"  Tracking: {trackingManager.IsTracking(newAnchor)}\n"
                    + $"  Grace Period: {TRACKING_GRACE_PERIOD:F1}s (prevents immediate fallback)\n"
                    + $"  Adjacent Targets: {anchorInfo?.GetAllAdjacentTargets()?.Length ?? 0}\n"
                    + $"  Connected Rooms: {anchorInfo?.connectedRooms?.Length ?? 0}",
                newAnchor.gameObject
            );

            // CRITICAL CHANGE: DO NOT set hasLocalized here!
            // hasLocalized should only be set AFTER tracking is confirmed
            // This prevents augmentations from appearing before Vuforia confirms tracking

            // CRITICAL: Immediately update group pose from new anchor to prevent drift
            // This ensures augmentations are positioned correctly when they reparent
            if (newAnchor is AreaTargetBehaviour)
            {
                ForceUpdateGroupPoseFromAnchor(newAnchor as AreaTargetBehaviour);
            }

            // Deactivate other starting targets (but keep them registered)
            if (!hasLocalized)
            {
                DeactivateOtherStartingTargets();

                Debug.Log(
                    $"<color=yellow>[ARSafeActivationController] ⏳ LOCALIZATION PENDING...</color> Anchor candidate: {newAnchor.name}. Waiting for tracking confirmation...",
                    newAnchor.gameObject
                );
            }

            if (anchorInfo != null && anchorInfo.targetType == TargetType.Exit)
            {
                if (MessageNotificationController.Instance != null)
                {
                    MessageNotificationController.Instance.ShowEvacuationReached();
                }

                if (ExitOverlayController.Instance != null)
                {
                    ExitOverlayController.Instance.ShowForDisaster(
                        DisasterTypeManager.SelectedDisasterType
                    );
                }
            }

            // NOTE: Cache invalidation removed - NavigationValidator manages its own cache
            // Invalidating on every anchor switch caused UI issues from rapid recalculations during anchor jitter

            // CRITICAL: Enforce tracking limit immediately after anchor switch
            // Ensures only the new anchor is tracking, all neighbors have tracking disabled
            EnforceTrackingLimit();

            // FLOOD-SPECIFIC LOGIC: Check for safe zones and stairway checkpoints
            if (anchorInfo != null && DisasterTypeManager.SelectedDisasterType == DisasterType.Flood)
            {
                HandleFloodAnchorSwitch(anchorInfo);
            }
        }

        /// <summary>
        /// NEW: Deactivate all starting targets except the current anchor
        /// Called once after initial localization to prevent tracking limit exceeded errors
        /// </summary>
        private void DeactivateOtherStartingTargets()
        {
            int deactivatedCount = 0;
            var deactivatedNames = new List<string>();

            foreach (var startingTarget in startingTargets)
            {
                if (startingTarget == null)
                    continue;

                // Keep the anchor, deactivate all others
                if (startingTarget != currentAnchor)
                {
                    // Directly disable the GameObject (more aggressive than normal DisableTarget)
                    startingTarget.gameObject.SetActive(false);
                    currentlyEnabled.Remove(startingTarget);
                    deactivatedCount++;
                    deactivatedNames.Add(startingTarget.name);
                }
            }

            Debug.Log(
                $"<color=yellow>[ARSafeActivationController] Deactivated {deactivatedCount} starting targets. Only anchor remains active for localization.</color>\n"
                    + $"  Deactivated: {(deactivatedNames.Count > 0 ? string.Join(", ", deactivatedNames) : "None")}\n"
                    + $"  Active anchor: {(currentAnchor != null ? currentAnchor.name : "None")}"
            );
        }

        /// <summary>
        /// Handle flood-specific anchor switch logic (safe zones and stairway checkpoints)
        /// </summary>
        private void HandleFloodAnchorSwitch(ARSafeTargetInfo anchorInfo)
        {
            if (anchorInfo == null)
                return;

            // Check if reached safe zone (2nd floor or higher)
            if (anchorInfo.floorLevel >= 2)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>★★★ [FLOOD SAFE ZONE]</color> User reached floor {anchorInfo.floorLevel} - SAFE FROM FLOOD!");
                }

                // Show exit overlay (same as reaching ground exit for fire/earthquake)
                if (ExitOverlayController.Instance != null)
                {
                    ExitOverlayController.Instance.ShowForDisaster(DisasterType.Flood);
                }

                // Show evacuation reached message
                if (MessageNotificationController.Instance != null)
                {
                    MessageNotificationController.Instance.ShowEvacuationReached();
                }

                // Disable wrong-way warnings
                var wrongWayWarning = FindFirstObjectByType<ARSafeWrongWayWarning>();
                if (wrongWayWarning != null)
                {
                    wrongWayWarning.DisableWarnings();
                }
            }
            // Check if reached stairway (checkpoint during flood)
            else if (anchorInfo.targetType == TargetType.Stairway)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>★ [FLOOD CHECKPOINT]</color> User reached stairway on floor {anchorInfo.floorLevel} - Keep going up!");
                }

                // Show checkpoint message via ExitOverlay
                if (ExitOverlayController.Instance != null)
                {
                    // Use a custom message for flood stairway checkpoints
                    // TODO: Add ShowCheckpointMessage method to ExitOverlayController
                    // For now, we can show a notification via MessageNotificationController
                    if (MessageNotificationController.Instance != null)
                    {
                        MessageNotificationController.Instance.ShowMessage(
                            "Checkpoint Reached - Keep moving upward to reach safety on higher floors.",
                            MessageNotificationController.MessageType.Info,
                            5f
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Deactivate all non-starting targets to prevent Vuforia observer overflow at launch
        /// </summary>
        /// <summary>
        /// CRITICAL: Deactivate non-starting targets EARLY in Awake() before Vuforia's ObserverBehaviour.Start() runs.
        /// This prevents Vuforia from automatically activating observers when GameObjects are active.
        /// </summary>
        private void DeactivateNonStartingTargetsEarly()
        {
            int deactivatedCount = 0;
            int alreadyInactive = 0;
            int startingCount = 0;

            Debug.Log(
                $"[ARSafeActivationController] Starting early deactivation. Total targets: {allAreaTargets.Count}, Starting targets: {startingTargets.Count}"
            );

            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                // Skip starting targets
                if (startingTargets.Contains(target))
                {
                    startingCount++;
                    Debug.Log(
                        $"[ARSafeActivationController] KEEPING active: {target.name} (starting target)"
                    );
                    continue;
                }

                // Deactivate non-starting target GameObject BEFORE Vuforia's Start() runs
                if (target.gameObject.activeSelf)
                {
                    target.gameObject.SetActive(false);
                    deactivatedCount++;
                    Debug.Log($"[ARSafeActivationController] DEACTIVATED: {target.name}");
                }
                else
                {
                    alreadyInactive++;
                }
            }

            Debug.Log(
                $"[ARSafeActivationController] Early deactivation complete in Awake():\n"
                    + $"  - Total targets: {allAreaTargets.Count}\n"
                    + $"  - Starting targets kept active: {startingCount}\n"
                    + $"  - Deactivated now: {deactivatedCount}\n"
                    + $"  - Already inactive: {alreadyInactive}\n"
                    + $"  ✅ This should prevent Vuforia observer overflow!"
            );
        }

        /// <summary>
        /// Activate starting targets for initial localization
        /// </summary>
        /// <summary>
        /// Check for preselected location from MainMenu and switch to it immediately.
        /// This ensures the user starts at their chosen location instead of auto-detection.
        /// </summary>
        private void ApplyPreselectedLocation()
        {
            // Check if user selected a location in the MainMenu
            if (!SelectedLocationManager.HasSelectedLocation())
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        "<color=yellow>[ARSafeActivationController]</color> No preselected location - using auto-detection"
                    );
                }
                return;
            }

            string selectedLocationName = SelectedLocationManager.GetSelectedLocation();
            if (string.IsNullOrEmpty(selectedLocationName))
            {
                return;
            }

            // Find the Area Target with matching name
            ObserverBehaviour selectedTarget = allAreaTargets.Find(t =>
                t != null && t.gameObject.name == selectedLocationName
            );

            if (selectedTarget == null)
            {
                Debug.LogWarning(
                    $"<color=orange>[ARSafeActivationController]</color> Preselected location '{selectedLocationName}' not found in scene! Using auto-detection fallback."
                );
                SelectedLocationManager.ClearSelectedLocation();
                return;
            }

            // CRITICAL FIX: Replace starting targets with ONLY the selected location
            // This ensures manual selection works exactly like auto-detect but with a single target
            Debug.Log(
                $"<color=cyan>★★★ [ARSafeActivationController]</color> Applying preselected location: {selectedLocationName}"
            );

            // Clear existing starting targets and use ONLY the selected location
            startingTargets.Clear();
            startingTargets.Add(selectedTarget);

            Debug.Log(
                $"<color=green>[ARSafeActivationController]</color> Starting targets replaced with ONLY '{selectedLocationName}' (manual selection)"
            );

            // DO NOT set currentAnchor here! Let the normal starting targets flow handle it
            // This ensures the same initialization as auto-detect
            // currentAnchor = selectedTarget; // REMOVED - let ActivateStartingTargets flow handle this

            Debug.Log(
                $"<color=cyan>⏳ [ARSafeActivationController]</color> Preselected location set as starting target. System will proceed with normal initialization flow."
            );

            // Clear the selection (one-time use)
            SelectedLocationManager.ClearSelectedLocation();
        }

        private void ActivateStartingTargets()
        {
            foreach (var target in startingTargets)
            {
                if (target != null)
                {
                    EnableTarget(target);
                }
            }

            if (enableDebugLogs && startingTargets.Count > 0)
            {
                Debug.Log(
                    $"[ARSafeActivationController] Activated {startingTargets.Count} starting targets for localization"
                );
            }
        }

        /// <summary>
        /// Auto-discover all Area Target ObserverBehaviours in scene (including inactive ones)
        /// </summary>
        private void DiscoverAreaTargets()
        {
            // CRITICAL: Use FindObjectsOfType with includeInactive=true to find ALL targets
            // This is essential for Awake() deactivation to work
            allAreaTargets = FindObjectsByType<ObserverBehaviour>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
                .Where(obs => obs.GetComponent<ModelTargetBehaviour>() == null // Exclude model targets
                          && obs.GetComponent<ImageTargetBehaviour>() == null) // Exclude image targets (managed by standard Vuforia)
                .ToList();

            Debug.Log(
                $"[ARSafeActivationController] Auto-discovered {allAreaTargets.Count} Area Targets (including inactive)"
            );
        }

        /// <summary>
        /// Build map of targets to their ARSafeTargetInfo components
        /// </summary>
        private void BuildTargetInfoMap()
        {
            targetInfoMap.Clear();

            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                var info = target.GetComponent<ARSafeTargetInfo>();
                if (info != null)
                {
                    info.InvalidateAdjacencyCache();
                    targetInfoMap[target] = info;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ARSafeActivationController] Built info map for {targetInfoMap.Count} targets"
                );
            }
        }

        /// <summary>
        /// Validate starting targets and auto-populate from ARSafeTargetInfo if needed
        /// </summary>
        private void ValidateStartingTargets()
        {
            if (startingTargets.Count == 0)
            {
                // Auto-populate from targets marked as starting targets
                foreach (var kvp in targetInfoMap)
                {
                    if (kvp.Value.isStartingTarget)
                    {
                        startingTargets.Add(kvp.Key);
                    }
                }

                if (enableDebugLogs && startingTargets.Count > 0)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Auto-populated {startingTargets.Count} starting targets from ARSafeTargetInfo"
                    );
                }
            }
        }

        /// <summary>
        /// Public API: Get current anchor target
        /// </summary>
        public ObserverBehaviour GetCurrentAnchor()
        {
            return currentAnchor;
        }

        /// <summary>
        /// Public API: Is target currently enabled?
        /// </summary>
        public bool IsTargetEnabled(ObserverBehaviour target)
        {
            return currentlyEnabled.Contains(target);
        }

        /// <summary>
        /// Public API: How many Area Targets are currently enabled?
        /// </summary>
        public int ActiveTargetCount => currentlyEnabled.Count;

        /// <summary>
        /// Public API: Force enable a specific target (for testing/debugging)
        /// </summary>
        public void ForceEnableTarget(ObserverBehaviour target)
        {
            if (target != null && !currentlyEnabled.Contains(target))
            {
                EnableTarget(target);
            }
        }

        /// <summary>
        /// Public API: Request relocalization with UI - Shows panel with recent anchors and manual selection.
        /// Call this when user wants to relocalize (tracking lost, confused location, etc.)
        /// </summary>
        public void RequestRelocalization()
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    "<color=yellow>★★★ [RELOCALIZATION] User requested relocalization UI...</color>"
                );
            }

            // Find and show the relocalization UI panel
            var relocUI = FindFirstObjectByType<RelocalizationPanelController>();
            if (relocUI != null)
            {
                relocUI.ShowPanel();
            }
            else
            {
                Debug.LogWarning(
                    "[ARSafeActivationController] RelocalizationPanelController not found in scene!"
                );

                // Fallback: Relocalize to first starting target if available
                if (startingTargets != null && startingTargets.Count > 0)
                {
                    RelocalizeTo(startingTargets[0]);
                }
            }
        }

        /// <summary>
        /// Reset system state for relocalization - clears state and disables all targets.
        /// CRITICAL: Does NOT deinitialize Vuforia (keeps observers intact).
        /// Vuforia stays running and will naturally re-detect the new anchor target.
        /// </summary>
        private void ResetForRelocalization()
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    "<color=yellow>★★★ [RELOCALIZATION RESET] Clearing state and disabling all targets...</color>"
                );
            }

            // Step 1: Clear all state variables
            hasLocalized = false;
            currentAnchor = null;
            previousAnchor = null;
            pendingNeighborSwitch = null;
            pendingNeighborSwitchTime = -1f;
            trackingGracePeriodEndTime = 0f;
            lastAnchorSwitchTime = 0f;
            currentAnchorStartTime = 0f;

            // FIX: Clear distance cache from all targets to prevent stale Unity Editor camera positions
            // This ensures distance calculations restart fresh with actual AR camera position
            foreach (var target in allAreaTargets)
            {
                if (target == null) continue;

                var targetInfo = target.GetComponent<ARSafeTargetInfo>();
                if (targetInfo != null)
                {
                    targetInfo.DistanceToCamera = float.MaxValue;
                    targetInfo.DistanceToBoundary = float.MaxValue;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[RELOCALIZATION RESET] Cleared distance cache from all targets</color>");
            }

            // Step 2: Force hide all content FIRST (before disabling targets)
            var targetsToDisable = new List<ObserverBehaviour>(currentlyEnabled);
            foreach (var target in targetsToDisable)
            {
                if (target != null)
                {
                    // Force hide content on this target
                    var proximityDisplay = target.GetComponent<ARSafeProximityDisplay>();
                    if (proximityDisplay != null)
                    {
                        proximityDisplay.ForceHide();
                    }
                }
            }

            // Step 3: Disable ALL targets (Vuforia stops tracking them)
            foreach (var target in targetsToDisable)
            {
                if (target != null)
                {
                    DisableTarget(target);
                }
            }

            // Step 4: Reset multi-area state
            ClearGlobalPositionReference();
            multiAreaHasTracking = false;
            currentBestTracked = null;

            // CRITICAL: Clear all saved relative poses to prevent drift
            if (relativePoses != null)
            {
                relativePoses.Clear();

                if (enableDebugLogs)
                {
                    Debug.Log("<color=yellow>★★★ [RELOCALIZATION RESET] Cleared all saved relative poses</color>");
                }
            }

            if (augmentationsRoot != null)
            {
                RestoreAllAugmentationsToOriginalParent();

                // CRITICAL: Hide all augmentations during relocalization
                augmentationsRoot.SetActive(false);

                if (enableDebugLogs)
                {
                    Debug.Log("<color=yellow>★★★ [RELOCALIZATION RESET] Augmentations hidden</color>");
                }
            }

            // IMPORTANT: Vuforia stays running - observers remain intact
            // When we enable the new anchor target, Vuforia will naturally start tracking it

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=green>★★★ [RELOCALIZATION RESET] Complete - disabled {targetsToDisable.Count} targets, Vuforia still running</color>"
                );
            }
        }

        /// <summary>
        /// Public API: Relocalize to a specific Area Target.
        /// Called by RelocalizationPanelController when user selects a target.
        /// CRITICAL: Resets state and disables all targets, then enables new anchor.
        /// Vuforia stays running and will naturally track the new target.
        /// </summary>
        public void RelocalizeTo(ObserverBehaviour targetAnchor)
        {
            if (targetAnchor == null)
            {
                Debug.LogWarning(
                    "[ARSafeActivationController] RelocalizeTo called with null target!"
                );
                return;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>★★★ [RELOCALIZATION] Relocalizing to: {targetAnchor.name}</color>"
                );
            }

            // CRITICAL: Set flag BEFORE reset to prevent neighbor/tracked target activation
            isRelocalizationInProgress = true;

            // Step 1: Reset state and disable all targets (Vuforia stays running)
            ResetForRelocalization();

            // Step 2: Enable ONLY the selected target (NOT neighbors during relocalization)
            EnableTarget(targetAnchor);
            currentAnchor = targetAnchor;
            lastAnchorSwitchTime = Time.time;
            currentAnchorStartTime = Time.time;

            // CRITICAL: DO NOT set hasLocalized here!
            // Let the tracking confirmation system handle it (Update() calls ConfirmLocalization())
            // This prevents augmentations from appearing before tracking is confirmed

            // CRITICAL: DO NOT enable neighbors during relocalization!
            // Neighbors will be enabled automatically after tracking is confirmed in ConfirmLocalization()
            // This ensures only ONE target is tracked at a time during relocalization

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=green>✓ [RELOCALIZATION] Complete - Only anchor enabled: {targetAnchor.name} (neighbors will be enabled after tracking confirmed)</color>"
                );
            }

            // Update parenting
            UpdateAugmentationParenting();

            // CRITICAL: Rebuild relative poses for multi-area drift correction
            // This must be done after disabling all targets and enabling only the anchor
            RebuildRelativePoses();

            // CRITICAL: Force refresh all disaster filters to show arrows
            ForceRefreshAllDisasterFilters();

            // Start tracking confirmation coroutine
            StartCoroutine(WaitForRelocalizationTracking(targetAnchor));
        }

        /// <summary>
        /// Rebuild relative poses for all Area Targets.
        /// Called after relocalization to ensure drift correction works properly.
        /// Lightweight version that only rebuilds poses without destroying augmentation root.
        /// </summary>
        private void RebuildRelativePoses()
        {
            if (relativePoses == null || allAreaTargets == null)
            {
                return;
            }

            // Store relative poses for all Area Targets (for drift correction)
            int rebuiltCount = 0;
            foreach (var observer in allAreaTargets)
            {
                if (observer is AreaTargetBehaviour atb)
                {
                    var matrix = GetFromToMatrix(atb.transform, transform);
                    relativePoses[atb.TargetName] = matrix;
                    rebuiltCount++;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>★★★ [RELOCALIZATION] Rebuilt {rebuiltCount} relative poses for multi-area drift correction</color>"
                );
            }
        }

        /// <summary>
        /// Wait for tracking to be established after relocalization.
        /// Only confirms localization once Vuforia tracking is active.
        /// </summary>
        private IEnumerator WaitForRelocalizationTracking(ObserverBehaviour targetAnchor)
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=yellow>[RELOCALIZATION] Waiting for tracking confirmation on {targetAnchor.name}...</color>"
                );
            }

            // CRITICAL: Wait indefinitely for tracking - no timeout!
            // System will only show augmentations once tracking is actually confirmed
            while (!trackingManager.IsTracking(targetAnchor))
            {
                yield return new WaitForSeconds(0.1f);
            }

            // Tracking confirmed - mark as localized
            ConfirmLocalization();

            // CRITICAL: Clear relocalization flag - normal neighbor activation can now resume
            isRelocalizationInProgress = false;

            if (enableDebugLogs)
            {
                Debug.Log("<color=green>★★★ [RELOCALIZATION] Flag cleared - normal activation rules resume</color>");
            }

            // CRITICAL: Re-enable augmentations after relocalization tracking is confirmed
            if (augmentationsRoot != null)
            {
                augmentationsRoot.SetActive(true);
                lastSuccessfulTrackingTime = Time.time; // Update tracking time for fallback system
                multiAreaHasTracking = true; // Ensure multi-area system knows we have tracking

                if (enableDebugLogs)
                {
                    Debug.Log("<color=green>★★★ [RELOCALIZATION] Augmentations re-enabled</color>");
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=green>✓ [RELOCALIZATION] Tracking confirmed for {targetAnchor.name}! Augmentations now visible.</color>"
                );
            }
        }

        /// <summary>
        /// Force all ARSafeDisasterFilter components to refresh their visibility.
        /// Useful after relocalization or anchor switches.
        /// </summary>
        public void ForceRefreshAllDisasterFilters()
        {
            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                var filters = target.GetComponentsInChildren<ARSafeDisasterFilter>(true);
                foreach (var filter in filters)
                {
                    if (filter != null)
                    {
                        filter.RefreshVisibility();
                    }
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    "<color=cyan>[ARSafeActivationController] Forced refresh on all disaster filters</color>"
                );
            }
        }

        /// <summary>
        /// CRITICAL FIX: Force refresh all proximity displays to show directional arrows.
        /// This fixes the issue where arrows don't show when manually selecting a location.
        /// </summary>
        private void RefreshAllProximityDisplays()
        {
            int refreshedCount = 0;

            foreach (var target in allAreaTargets)
            {
                if (target == null)
                    continue;

                // ARSafeProximityDisplay is attached to the target itself, not children
                var displays = target.GetComponents<ARSafeProximityDisplay>();
                foreach (var display in displays)
                {
                    if (display != null && display.enabled)
                    {
                        display.RefreshVisibilityImmediate();
                        refreshedCount++;
                    }
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>[ARSafeActivationController] Forced refresh on {refreshedCount} proximity display(s)</color>"
                );
            }
        }

        /// <summary>
        /// Helper: Add anchor to history (most recent first, max 5 unique entries)
        /// </summary>
        private void AddToAnchorHistory(ObserverBehaviour anchor)
        {
            if (anchor == null)
                return;

            // Remove if already in history
            anchorHistory.Remove(anchor);

            // Add to front
            anchorHistory.Insert(0, anchor);

            // Trim to max size
            if (anchorHistory.Count > MAX_ANCHOR_HISTORY)
            {
                anchorHistory.RemoveRange(
                    MAX_ANCHOR_HISTORY,
                    anchorHistory.Count - MAX_ANCHOR_HISTORY
                );
            }
        }

        /// <summary>
        /// Public API: Force disable a specific target (for testing/debugging)
        /// </summary>
        public void ForceDisableTarget(ObserverBehaviour target)
        {
            if (
                target != null
                && currentlyEnabled.Contains(target)
                && !trackingManager.IsTracking(target)
            )
            {
                DisableTarget(target);
            }
        }

        /// <summary>
        /// Returns true if a stable global position reference has been established.
        /// </summary>
        public bool HasGlobalPositionReference => hasGlobalReferencePose;

        /// <summary>
        /// Force the system to forget the existing global reference. It will relock on the next tracked Area Target.
        /// </summary>
        public void ResetGlobalPositionReference()
        {
            ClearGlobalPositionReference(log: true);
        }

        /// <summary>
        /// Try to retrieve the global reference pose (world space transform captured when the first reliable Area Target was tracked).
        /// </summary>
        public bool TryGetGlobalReferencePose(out Pose pose)
        {
            if (!hasGlobalReferencePose)
            {
                pose = default;
                return false;
            }

            pose = MatrixToPose(globalReferenceMatrix);
            return true;
        }

        /// <summary>
        /// Try to convert a world-space position into the stabilized global reference space.
        /// </summary>
        public bool TryConvertWorldToGlobalPosition(
            Vector3 worldPosition,
            out Vector3 globalPosition
        )
        {
            if (!hasGlobalReferencePose)
            {
                globalPosition = Vector3.zero;
                return false;
            }

            globalPosition = globalReferenceInverse.MultiplyPoint3x4(worldPosition);
            return true;
        }

        /// <summary>
        /// Try to convert a position from global reference space back into Unity world space.
        /// </summary>
        public bool TryConvertGlobalToWorldPosition(
            Vector3 globalPosition,
            out Vector3 worldPosition
        )
        {
            if (!hasGlobalReferencePose)
            {
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = globalReferenceMatrix.MultiplyPoint3x4(globalPosition);
            return true;
        }

        /// <summary>
        /// Try to obtain the AR Camera pose expressed in the global reference frame.
        /// </summary>
        public bool TryGetGlobalCameraPose(out Pose pose)
        {
            pose = default;

            if (!hasGlobalReferencePose || arCamera == null)
            {
                return false;
            }

            var cameraMatrix = globalReferenceInverse * arCamera.transform.localToWorldMatrix;
            pose = MatrixToPose(cameraMatrix);
            return true;
        }

        /// <summary>
        /// Try to obtain the AR Camera position expressed in the global reference frame.
        /// </summary>
        public bool TryGetGlobalCameraPosition(out Vector3 position)
        {
            position = Vector3.zero;

            if (!hasGlobalReferencePose || arCamera == null)
            {
                return false;
            }

            position = globalReferenceInverse.MultiplyPoint3x4(arCamera.transform.position);
            return true;
        }

        /// <summary>
        /// Retrieve the latest group pose matrix (world space) even while the global reference remains locked.
        /// </summary>
        public Matrix4x4 GetCurrentGroupPoseMatrix()
        {
            return currentGroupPoseMatrix;
        }

        /// <summary>
        /// Determine whether the specified observer's content should remain visible while tracking is momentarily lost.
        /// </summary>
        public bool ShouldKeepContentVisibleWhenUntracked(ObserverBehaviour target)
        {
            if (target == null)
            {
                return false;
            }

            if (!targetInfoMap.TryGetValue(target, out var info) || !info.AllowAugmentationFallback)
            {
                return false;
            }

            if (target == currentAnchor)
            {
                return true;
            }

            if (
                currentAnchor == null
                || !targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo)
            )
            {
                return false;
            }

            return anchorInfo.IsAdjacentTo(info);
        }

        /// <summary>
        /// Retrieve the matrix that defines the stabilized global reference frame captured from the first reliable Area Target.
        /// </summary>
        public Matrix4x4 GetGlobalReferenceMatrix()
        {
            return globalReferenceMatrix;
        }

        /// <summary>
        /// Convert a transformation matrix to a Pose (position + rotation).
        /// </summary>
        private static Pose MatrixToPose(Matrix4x4 matrix)
        {
            var positionColumn = matrix.GetColumn(3);
            var forwardColumn = matrix.GetColumn(2);
            var upColumn = matrix.GetColumn(1);

            var position = new Vector3(positionColumn.x, positionColumn.y, positionColumn.z);
            var forward = new Vector3(forwardColumn.x, forwardColumn.y, forwardColumn.z);
            var upwards = new Vector3(upColumn.x, upColumn.y, upColumn.z);

            if (forward == Vector3.zero || upwards == Vector3.zero)
            {
                return new Pose(position, Quaternion.identity);
            }

            var rotation = Quaternion.LookRotation(forward, upwards);
            return new Pose(position, rotation);
        }

        /// <summary>
        /// Clear global reference data. Optionally logs when instructed.
        /// </summary>
        private void ClearGlobalPositionReference(bool log = false)
        {
            hasGlobalReferencePose = false;
            globalReferenceMatrix = Matrix4x4.identity;
            globalReferenceInverse = Matrix4x4.identity;
            currentGroupPoseMatrix = Matrix4x4.identity;

            if (log && enableDebugLogs)
            {
                Debug.Log("[ARSafeActivationController] Global position reference cleared.");
            }
        }

        // ==================== MultiArea Mode Methods ====================

        /// <summary>
        /// Initialize MultiArea mode: Store relative poses and set up augmentation root
        /// </summary>
        private void InitializeMultiAreaMode()
        {
            relativePoses.Clear();
            multiAreaHasTracking = false;
            multiAreaLastPoseFrame = -1;
            currentBestTracked = null;
            hasSmoothedGroupPose = false;
            groupPoseVelocity = Vector3.zero;
            smoothedGroupPose = Pose.identity;
            smoothedGroupRotation = Quaternion.identity;

            ClearGlobalPositionReference();

            // Ensure any dynamically reparented augmentations return to their original parents
            RestoreAllAugmentationsToOriginalParent();

            if (augmentationsRoot != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(augmentationsRoot);
                }
                else
                {
                    DestroyImmediate(augmentationsRoot);
                }
                augmentationsRoot = null;
            }

            // Store relative poses for all Area Targets (for drift correction)
            foreach (var observer in allAreaTargets)
            {
                if (observer is AreaTargetBehaviour atb)
                {
                    var matrix = GetFromToMatrix(atb.transform, transform);
                    relativePoses[atb.TargetName] = matrix;

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[ARSafeActivationController] MultiArea: Stored pose for {atb.TargetName}"
                        );
                    }
                }
            }

            // Create augmentations root (optional - for re-parenting content like original MultiArea)
            augmentationsRoot = new GameObject("Augmentations");
            augmentationsRoot.transform.SetParent(transform);
            augmentationsRoot.transform.localPosition = Vector3.zero;
            augmentationsRoot.transform.localRotation = Quaternion.identity;

            // Cache original augmentation parents so we can dynamically reparent anchor + neighbors only
            CacheAugmentationStates();

            // CRITICAL: Notify dependent components about the new root location
            NotifyComponentsOfReparenting();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ARSafeActivationController] MultiArea mode initialized with {relativePoses.Count} poses"
                );
            }
        }

        /// <summary>
        /// Cache the original parent and local transform for each Area Target augmentation container.
        /// </summary>
        private void CacheAugmentationStates()
        {
            augmentationParentingStates.Clear();

            foreach (var observer in allAreaTargets)
            {
                if (observer == null)
                {
                    continue;
                }

                Transform augmentations = observer.transform.Find("Augmentations");
                if (augmentations == null)
                {
                    continue;
                }

                augmentationParentingStates[observer] = new AugmentationParentingState
                {
                    Augmentation = augmentations,
                    OriginalParent = augmentations.parent,
                    OriginalLocalPosition = augmentations.localPosition,
                    OriginalLocalRotation = augmentations.localRotation,
                    OriginalLocalScale = augmentations.localScale,
                    OriginalActiveState = augmentations.gameObject.activeSelf,
                    IsReparented = augmentations.parent == augmentationsRoot?.transform,
                };

                var disasterFilters = augmentations.GetComponentsInChildren<ARSafeDisasterFilter>(
                    true
                );
                foreach (var filter in disasterFilters)
                {
                    filter.RegisterOwningObserver(observer);
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ARSafeActivationController] Cached augmentation parents for {augmentationParentingStates.Count} targets."
                );
            }
        }

        /// <summary>
        /// Notify all ARSafeDisasterFilter and ARSafeProximityDisplay components about the new augmentations root.
        /// This allows them to re-collect content from the reparented location.
        /// </summary>
        private void NotifyComponentsOfReparenting()
        {
            if (augmentationsRoot == null)
            {
                return;
            }

            int disasterFiltersNotified = 0;
            int proximityDisplaysNotified = 0;

            foreach (var kvp in augmentationParentingStates)
            {
                var target = kvp.Key;
                var state = kvp.Value;

                if (target == null || state?.Augmentation == null)
                {
                    continue;
                }

                // Notify ARSafeDisasterFilter components attached under the augmentation container
                var disasterFilters =
                    state.Augmentation.GetComponentsInChildren<ARSafeDisasterFilter>(true);
                foreach (var filter in disasterFilters)
                {
                    filter.SetAugmentationsRoot(augmentationsRoot.transform, target);
                    disasterFiltersNotified++;
                }

                // CRITICAL FIX: ARSafeProximityDisplay is attached to the ObserverBehaviour (target), not inside Augmentations
                // Search on the target itself, not in the augmentation children
                var proximityDisplays = target.GetComponents<ARSafeProximityDisplay>();
                foreach (var display in proximityDisplays)
                {
                    display.SetAugmentationsRoot(augmentationsRoot.transform);
                    proximityDisplaysNotified++;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>[ARSafeActivationController] Notified {disasterFiltersNotified} ARSafeDisasterFilter(s) and {proximityDisplaysNotified} ARSafeProximityDisplay(s) of augmentations root reparenting.</color>"
                );
            }
        }

        /// <summary>
        /// Refresh disaster filters and proximity displays after reparenting a specific target.
        /// Forces content re-collection and visibility update to ensure augmentations show correctly.
        /// </summary>
        private void RefreshComponentsAfterReparenting(
            ObserverBehaviour target,
            AugmentationParentingState state
        )
        {
            if (target == null || state?.Augmentation == null)
                return;

            // Force disaster filters to refresh visibility
            var disasterFilters = state.Augmentation.GetComponentsInChildren<ARSafeDisasterFilter>(
                true
            );
            foreach (var filter in disasterFilters)
            {
                if (filter != null)
                {
                    filter.SetAugmentationsRoot(augmentationsRoot.transform, target);
                    filter.RefreshVisibility();

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=green>[AUGMENTATION ROOT] Refreshed disaster filter on {target.name}</color>"
                        );
                    }
                }
            }

            // CRITICAL: ARSafeProximityDisplay is attached to the ObserverBehaviour (target), not inside Augmentations
            // We need to find it on the target itself and tell it to re-collect content
            var proximityDisplays = target.GetComponents<ARSafeProximityDisplay>();
            foreach (var display in proximityDisplays)
            {
                if (display != null && display.enabled)
                {
                    // Force re-collection of content from the reparented Augmentations container
                    display.SetAugmentationsRoot(augmentationsRoot.transform);

                    // Then refresh visibility immediately
                    display.RefreshVisibilityImmediate();

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=green>[AUGMENTATION ROOT] Proximity display on {target.name} re-collected content and refreshed visibility</color>"
                        );
                    }
                }
            }

            // CRITICAL FIX: Refresh earthquake augmentation components after reparenting
            // Earthquake components (debris, cracks) subscribe to EarthquakeScenarioManager events in OnEnable(),
            // but when they're already enabled during reparenting, they don't re-initialize.
            // We manually trigger them to re-process the current earthquake state.
            RefreshEarthquakeAugmentations(state.Augmentation);
        }

        /// <summary>
        /// Refresh earthquake-specific augmentation components to ensure they activate properly after anchor switch.
        /// Forces debris controllers, crack projectors (deprecated), and billboard cracks to re-process current earthquake scenario state.
        /// CRITICAL: Must activate parent GameObjects first, since earthquake controllers only work when active.
        /// ARSafeDisasterFilter uses SetActive() which disables components, preventing the enable/disable toggle from working.
        /// </summary>
        private void RefreshEarthquakeAugmentations(Transform augmentationRoot)
        {
            if (augmentationRoot == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        "[EarthquakeRefresh] Cannot refresh - augmentationRoot is null"
                    );
                }
                return;
            }

            // Use coroutine to ensure refresh happens after all parenting operations complete
            StartCoroutine(RefreshEarthquakeAugmentationsCoroutine(augmentationRoot));
        }

        /// <summary>
        /// Coroutine that performs the actual earthquake augmentation refresh.
        /// Waits one frame to ensure all reparenting operations have completed.
        /// </summary>
        private IEnumerator RefreshEarthquakeAugmentationsCoroutine(Transform augmentationRoot)
        {
            // Wait one frame to ensure all reparenting and transform updates have completed
            yield return null;

            if (augmentationRoot == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        "[EarthquakeRefresh] Augmentation root became null during refresh"
                    );
                }
                yield break;
            }

            int debrisRefreshed = 0;
            int cracksRefreshed = 0;

            // ========================================================================
            // REFRESH DEBRIS CONTROLLERS
            // ========================================================================
            var debrisControllers =
                augmentationRoot.GetComponentsInChildren<ARSafe.Content.EarthquakeDebrisController>(
                    true
                );

            if (enableDebugLogs && debrisControllers.Length > 0)
            {
                Debug.Log(
                    $"<color=cyan>[EarthquakeRefresh] Found {debrisControllers.Length} debris controller(s) to refresh</color>"
                );
            }

            foreach (var debris in debrisControllers)
            {
                if (debris == null || debris.gameObject == null)
                {
                    continue;
                }

                // Store the original hierarchy states (all parents up to root)
                List<(GameObject obj, bool wasActive)> hierarchyStates =
                    new List<(GameObject, bool)>();
                Transform current = debris.transform;

                // Walk up the hierarchy and temporarily activate all parents
                while (current != null)
                {
                    hierarchyStates.Add((current.gameObject, current.gameObject.activeSelf));

                    if (!current.gameObject.activeSelf)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[EarthquakeRefresh] Activating parent: {current.name}</color>"
                            );
                        }
                        current.gameObject.SetActive(true);
                    }

                    current = current.parent;

                    // Stop at the augmentationRoot to avoid affecting too much of the hierarchy
                    if (current != null && current == augmentationRoot)
                    {
                        break;
                    }
                }

                // Now the entire hierarchy is active - force OnEnable() to fire by toggling GameObject
                // CRITICAL: Use GameObject.SetActive() toggle, not component.enabled
                // This ensures OnEnable() fires and earthquake event subscriptions happen
                GameObject debrisObj = debris.gameObject;
                bool debrisWasActive = debrisObj.activeSelf;

                if (debrisWasActive)
                {
                    // If already active, toggle to force OnEnable()
                    debrisObj.SetActive(false);
                    yield return null; // Wait a frame
                    debrisObj.SetActive(true);

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=cyan>[EarthquakeRefresh] Toggled active debris: {debris.name}</color>"
                        );
                    }
                }
                else
                {
                    // If inactive, activate briefly then restore
                    debrisObj.SetActive(true);
                    yield return null; // Wait for OnEnable() to complete
                    debrisObj.SetActive(false);

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=cyan>[EarthquakeRefresh] Briefly activated inactive debris: {debris.name}</color>"
                        );
                    }
                }

                // Restore original hierarchy states (from leaf to root)
                for (int i = 0; i < hierarchyStates.Count; i++)
                {
                    if (
                        hierarchyStates[i].obj != null
                        && hierarchyStates[i].obj.activeSelf != hierarchyStates[i].wasActive
                    )
                    {
                        hierarchyStates[i].obj.SetActive(hierarchyStates[i].wasActive);
                    }
                }

                debrisRefreshed++;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=green>[EarthquakeRefresh] ✓ Refreshed debris: {debris.name} (wasActive={debrisWasActive})</color>"
                    );
                }
            }

            // ========================================================================
            // REFRESH CRACK PROJECTOR CONTROLLERS [DEPRECATED - LEGACY]
            // ========================================================================
            // Note: Projector controllers are deprecated; keeping this section
            // for backward compatibility but it will not find any instances
            // if EarthquakeCrackProjectorController.cs has been removed
            // Variable cracksRefreshed is declared at the top of the coroutine

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=yellow>[EarthquakeRefresh] Skipping deprecated crack projector refresh (legacy path)</color>"
                );
            }

            // ========================================================================
            // REFRESH QUAD CRACK CONTROLLERS (3D QUAD + MATERIAL)
            // ========================================================================
            int quadCracksRefreshed = 0;
            var quadCrackControllers =
                augmentationRoot.GetComponentsInChildren<EarthquakeCrackQuadController>(true);

            if (enableDebugLogs && quadCrackControllers.Length > 0)
            {
                Debug.Log(
                    $"<color=cyan>[EarthquakeRefresh] Found {quadCrackControllers.Length} quad crack controller(s) to refresh</color>"
                );
            }

            foreach (var quadCrack in quadCrackControllers)
            {
                if (quadCrack == null || quadCrack.gameObject == null)
                {
                    continue;
                }

                // Store the original hierarchy states
                List<(GameObject obj, bool wasActive)> hierarchyStates =
                    new List<(GameObject, bool)>();
                Transform current = quadCrack.transform;

                // Walk up and activate hierarchy
                while (current != null)
                {
                    hierarchyStates.Add((current.gameObject, current.gameObject.activeSelf));

                    if (!current.gameObject.activeSelf)
                    {
                        if (enableDebugLogs)
                        {
                            Debug.Log(
                                $"<color=yellow>[EarthquakeRefresh] Activating parent: {current.name}</color>"
                            );
                        }
                        current.gameObject.SetActive(true);
                    }

                    current = current.parent;

                    if (current != null && current == augmentationRoot)
                    {
                        break;
                    }
                }

                // Force OnEnable() to fire using GameObject toggle
                GameObject quadCrackObj = quadCrack.gameObject;
                bool quadCrackWasActive = quadCrackObj.activeSelf;

                if (quadCrackWasActive)
                {
                    quadCrackObj.SetActive(false);
                    yield return null;
                    quadCrackObj.SetActive(true);

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=cyan>[EarthquakeRefresh] Toggled active quad crack: {quadCrack.name}</color>"
                        );
                    }
                }
                else
                {
                    quadCrackObj.SetActive(true);
                    yield return null;
                    quadCrackObj.SetActive(false);

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"<color=cyan>[EarthquakeRefresh] Briefly activated inactive quad crack: {quadCrack.name}</color>"
                        );
                    }
                }

                // Restore hierarchy states
                for (int i = 0; i < hierarchyStates.Count; i++)
                {
                    if (
                        hierarchyStates[i].obj != null
                        && hierarchyStates[i].obj.activeSelf != hierarchyStates[i].wasActive
                    )
                    {
                        hierarchyStates[i].obj.SetActive(hierarchyStates[i].wasActive);
                    }
                }

                quadCracksRefreshed++;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=green>[EarthquakeRefresh] ✓ Refreshed quad crack: {quadCrack.name} (wasActive={quadCrackWasActive})</color>"
                    );
                }
            }

            // ========================================================================
            // FINAL SUMMARY
            // ========================================================================
            if (enableDebugLogs && (debrisRefreshed > 0 || cracksRefreshed > 0 || quadCracksRefreshed > 0))
            {
                Debug.Log(
                    $"<color=green>[EarthquakeRefresh] ✓✓✓ Completed refresh: {debrisRefreshed} debris, {cracksRefreshed} crack projector(s) [deprecated], {quadCracksRefreshed} quad crack(s)</color>"
                );
            }
            else if (debrisRefreshed == 0 && cracksRefreshed == 0 && quadCracksRefreshed == 0)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"<color=yellow>[EarthquakeRefresh] No earthquake components found to refresh in {augmentationRoot.name}</color>"
                    );
                }
            }
        }

        /// <summary>
        /// Keep only the anchor and its currently enabled neighbors parented under the shared root to prevent drift.
        /// Restores all other augmentations to their original Area Target parents so inactive content stays hidden.
        /// </summary>
        private void UpdateAugmentationParenting()
        {
            if (augmentationsRoot == null)
            {
                return;
            }

            if (augmentationParentingStates.Count == 0)
            {
                CacheAugmentationStates();
            }

            bool hasAnchor = currentAnchor != null;

            foreach (var kvp in augmentationParentingStates)
            {
                var target = kvp.Key;
                var state = kvp.Value;

                if (target == null || state?.Augmentation == null)
                {
                    continue;
                }

                if (!hasAnchor)
                {
                    RestoreAugmentationToOriginalParent(target, state);
                    continue;
                }

                bool shouldAttach = currentlyEnabled.Contains(target);

                if (alwaysIncludeTrackedTargets && trackingManager != null)
                {
                    shouldAttach |= trackingManager.IsTracking(target);
                }

                if (shouldAttach)
                {
                    AttachAugmentationToSharedRoot(target, state);
                }
                else
                {
                    RestoreAugmentationToOriginalParent(target, state);
                }
            }
        }

        /// <summary>
        /// Attach the augmentation container to the shared MultiArea root (preserves world pose).
        /// </summary>
        private void AttachAugmentationToSharedRoot(
            ObserverBehaviour target,
            AugmentationParentingState state
        )
        {
            if (state == null || state.Augmentation == null || augmentationsRoot == null)
            {
                return;
            }

            // Null safety: Check augmentationsRoot.transform exists
            if (
                state.IsReparented
                || (
                    augmentationsRoot?.transform != null
                    && state.Augmentation.parent == augmentationsRoot.transform
                )
            )
            {
                return;
            }

            // CRITICAL: Calculate proper local transform using current Area Target pose
            // This ensures neighbor augmentations are correctly aligned and follow tracking improvements
            if (target is AreaTargetBehaviour atb)
            {
                // Use cached original local transform (the designed relationship to Area Target)
                Vector3 originalLocalPos = state.OriginalLocalPosition;
                Quaternion originalLocalRot = state.OriginalLocalRotation;
                Vector3 originalLocalScale = state.OriginalLocalScale;

                // Parent to shared root (worldPositionStays = false for clean hierarchy)
                state.Augmentation.SetParent(augmentationsRoot.transform, false);

                // CRITICAL: Transform using CURRENT Area Target pose (not static relative poses)
                // This allows augmentations to follow tracking improvements from the start
                Transform targetTransform = atb.transform;
                Transform sharedRoot = augmentationsRoot.transform;

                // Step 1: Calculate augmentation's world position using current Area Target transform
                Vector3 worldPos = targetTransform.TransformPoint(originalLocalPos);
                Quaternion worldRot = targetTransform.rotation * originalLocalRot;

                // Step 2: Transform world position/rotation to shared root's local space
                Vector3 localPosInSharedRoot = sharedRoot.InverseTransformPoint(worldPos);
                Quaternion localRotInSharedRoot = Quaternion.Inverse(sharedRoot.rotation) * worldRot;

                // Step 3: Apply the calculated local transform
                state.Augmentation.localPosition = localPosInSharedRoot;
                state.Augmentation.localRotation = localRotInSharedRoot;
                state.Augmentation.localScale = originalLocalScale; // Preserve original local scale
            }
            else
            {
                // Fallback: Use worldPositionStays for non-AreaTarget observers
                state.Augmentation.SetParent(augmentationsRoot.transform, true);
            }

            state.Augmentation.gameObject.SetActive(true);
            state.IsReparented = true;

            // CRITICAL: Notify components to refresh after reparenting
            RefreshComponentsAfterReparenting(target, state);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>[AUGMENTATION ROOT] Attached {target.name} augmentations to shared root with proper alignment.</color>"
                );
            }
        }

        /// <summary>
        /// Continuously update reparented augmentation transforms to correct for drift.
        /// Called during UpdateMultiAreaPose() to keep augmentations aligned as tracking improves.
        /// CRITICAL: Uses CURRENT Area Target poses to ensure augmentations follow tracking improvements.
        /// </summary>
        private void UpdateReparentedAugmentationTransforms()
        {
            if (augmentationsRoot == null || augmentationsRoot.transform == null)
            {
                return;
            }

            Transform sharedRoot = augmentationsRoot.transform;

            // Iterate through all reparented augmentations
            foreach (var kvp in augmentationParentingStates)
            {
                var target = kvp.Key;
                var state = kvp.Value;

                // Skip if not reparented or invalid
                if (!state.IsReparented || state.Augmentation == null || target == null)
                {
                    continue;
                }

                // Skip if not an Area Target
                if (!(target is AreaTargetBehaviour atb))
                {
                    continue;
                }

                // CRITICAL: Calculate augmentation's world position based on CURRENT Area Target pose
                // This ensures augmentations follow the Area Target as its tracking improves

                // Step 1: Calculate augmentation's world position using current Area Target transform
                Transform targetTransform = atb.transform;
                Vector3 worldPos = targetTransform.TransformPoint(state.OriginalLocalPosition);
                Quaternion worldRot = targetTransform.rotation * state.OriginalLocalRotation;

                // Step 2: Transform world position/rotation to shared root's local space
                Vector3 localPosInSharedRoot = sharedRoot.InverseTransformPoint(worldPos);
                Quaternion localRotInSharedRoot = Quaternion.Inverse(sharedRoot.rotation) * worldRot;

                // Step 3: Apply the calculated local transform
                state.Augmentation.localPosition = localPosInSharedRoot;
                state.Augmentation.localRotation = localRotInSharedRoot;
                state.Augmentation.localScale = state.OriginalLocalScale; // Preserve original local scale
            }
        }

        /// <summary>
        /// Restore the augmentation container back to its Area Target (original local pose + active state).
        /// </summary>
        private void RestoreAugmentationToOriginalParent(
            ObserverBehaviour target,
            AugmentationParentingState state
        )
        {
            if (state == null || state.Augmentation == null || state.OriginalParent == null)
            {
                return;
            }

            bool alreadyOriginalParent = state.Augmentation.parent == state.OriginalParent;

            if (!state.IsReparented && alreadyOriginalParent)
            {
                return;
            }

            state.Augmentation.SetParent(state.OriginalParent, false);
            state.Augmentation.localPosition = state.OriginalLocalPosition;
            state.Augmentation.localRotation = state.OriginalLocalRotation;
            state.Augmentation.localScale = state.OriginalLocalScale;
            state.Augmentation.gameObject.SetActive(state.OriginalActiveState);
            state.IsReparented = false;

            // CRITICAL: Notify proximity display that augmentations are back to original parent
            // This triggers re-collection of content and hides visibility (since target is no longer anchor)
            var proximityDisplays = target.GetComponents<ARSafeProximityDisplay>();
            foreach (var display in proximityDisplays)
            {
                if (display != null)
                {
                    // Re-collect content from original location and hide it
                    display.SetAugmentationsRoot(null);
                    display.ForceHide(); // Force hide immediately
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=gray>[AUGMENTATION ROOT] Restored {target.name} augmentations to original parent and hid content.</color>"
                );
            }
        }

        /// <summary>
        /// Force every tracked augmentation container back onto its Area Target parent.
        /// Used before rebuilding the shared root or during shutdown to avoid destroying content.
        /// </summary>
        private void RestoreAllAugmentationsToOriginalParent()
        {
            if (augmentationParentingStates.Count == 0)
            {
                return;
            }

            foreach (var kvp in augmentationParentingStates)
            {
                var target = kvp.Key;
                var state = kvp.Value;

                if (target == null || state?.Augmentation == null || state.OriginalParent == null)
                {
                    continue;
                }

                if (state.Augmentation.parent != state.OriginalParent)
                {
                    state.Augmentation.SetParent(state.OriginalParent, false);
                    state.Augmentation.localPosition = state.OriginalLocalPosition;
                    state.Augmentation.localRotation = state.OriginalLocalRotation;
                    state.Augmentation.localScale = state.OriginalLocalScale;
                }

                state.Augmentation.gameObject.SetActive(state.OriginalActiveState);
                state.IsReparented = false;
            }
        }

        /// <summary>
        /// Update MultiArea root transform based on best tracked target (drift correction)
        /// OPTIMIZED: Throttled to configurable FPS for better performance (default 15 FPS)
        /// </summary>
        private void UpdateMultiAreaPose()
        {
            // OPTIMIZATION: Throttle updates to target FPS (default 15 FPS = ~67ms interval)
            // MultiArea drift correction doesn't need 60+ FPS, 15 FPS is smooth enough
            float updateInterval = 1f / Mathf.Max(1f, multiAreaPoseUpdateFPS);
            if (Time.time - lastMultiAreaPoseUpdateTime < updateInterval)
            {
                return;
            }

            lastMultiAreaPoseUpdateTime = Time.time;

            // OPTIMIZATION: Frame check is now redundant with time-based throttling, but keeping for safety
            if (multiAreaLastPoseFrame == Time.frameCount)
            {
                return;
            }

            multiAreaLastPoseFrame = Time.frameCount;

            if (VuforiaApplication.Instance == null || !VuforiaApplication.Instance.IsRunning)
            {
                return;
            }

            // OPTIMIZATION: Reuse cached list instead of allocating new one each frame
            cachedTrackedTargets.Clear();
            GetTrackedAreaTargetsOptimized(cachedTrackedTargets, includeLimited: true);

            // OPTIMIZATION: Early exit if no tracking (avoid unnecessary work)
            if (cachedTrackedTargets.Count == 0)
            {
                if (currentBestTracked != null && enableDebugLogs)
                {
                    Debug.Log("[ARSafeActivationController] MultiArea pose source lost tracking.");
                }
                currentBestTracked = null;
                multiAreaHasTracking = false;
                return;
            }

            var preferred = GetBestTrackedAreaTarget(cachedTrackedTargets);
            var poseSource = ResolvePoseAuthority(preferred, cachedTrackedTargets);

            bool hadTracking = multiAreaHasTracking;
            multiAreaHasTracking = poseSource != null;

            // FIX: Keep augmentations visible during tracking loss (using last known pose)
            // Only hide if tracking has been lost for longer than maxAugmentationFallbackTime
            if (multiAreaHasTracking)
            {
                lastSuccessfulTrackingTime = Time.time;
            }

            bool shouldShowAugmentations = multiAreaHasTracking;
            if (!multiAreaHasTracking && keepAugmentationsVisibleDuringTrackingLoss)
            {
                float timeSinceTracking = Time.time - lastSuccessfulTrackingTime;
                shouldShowAugmentations = timeSinceTracking < maxAugmentationFallbackTime;

                if (enableDebugLogs && shouldShowAugmentations)
                {
                    Debug.Log(
                        $"<color=yellow>[AUGMENTATION FALLBACK] Keeping augmentations visible despite tracking loss ({timeSinceTracking:F1}s < {maxAugmentationFallbackTime:F1}s)</color>"
                    );
                }
            }

            if (
                hideContentWhenNotTracking
                && augmentationsRoot != null
                && hadTracking != shouldShowAugmentations
            )
            {
                augmentationsRoot.SetActive(shouldShowAugmentations);
            }

            if (poseSource == null)
            {
                if (currentBestTracked != null && enableDebugLogs)
                {
                    float timeSinceTracking = Time.time - lastSuccessfulTrackingTime;
                    Debug.Log(
                        $"[ARSafeActivationController] MultiArea pose source lost tracking (fallback time: {timeSinceTracking:F1}s / {maxAugmentationFallbackTime:F1}s)."
                    );
                }

                currentBestTracked = null;

                // FIX: Don't return early - allow pose to persist using last known transform
                // The transform will stay at its last position, providing visual stability
                if (!keepAugmentationsVisibleDuringTrackingLoss)
                {
                    return;
                }

                // Continue using last known pose for augmentation fallback
                return;
            }

            if (!GetGroupPoseFromAreaTarget(poseSource, out Matrix4x4 rawGroupPose))
            {
                return;
            }

            if (!hasGlobalReferencePose)
            {
                globalReferenceMatrix = rawGroupPose;
                globalReferenceInverse = globalReferenceMatrix.inverse;
                hasGlobalReferencePose = true;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] Global position reference locked to {poseSource.TargetName}"
                    );
                }
            }

            var targetPose = MatrixToPose(rawGroupPose);
            var smoothedPose = ApplyGroupPoseSmoothing(targetPose);

            currentGroupPoseMatrix = Matrix4x4.TRS(
                smoothedPose.position,
                smoothedPose.rotation,
                Vector3.one
            );

            transform.SetPositionAndRotation(smoothedPose.position, smoothedPose.rotation);

            // CRITICAL: Update reparented augmentation transforms to correct for drift
            UpdateReparentedAugmentationTransforms();

            GlobalPoseUpdated?.Invoke(currentGroupPoseMatrix);

            if (poseSource != currentBestTracked)
            {
                currentBestTracked = poseSource;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[ARSafeActivationController] MultiArea pose source: {poseSource.TargetName}"
                    );
                }
            }
        }

        /// <summary>
        /// Force immediate group pose update from specified Area Target.
        /// Called when anchor switches to prevent drift during reparenting.
        /// CRITICAL: Ensures shared root is positioned correctly BEFORE augmentations reparent.
        /// </summary>
        private void ForceUpdateGroupPoseFromAnchor(AreaTargetBehaviour newAnchor)
        {
            if (newAnchor == null || augmentationsRoot == null)
            {
                return;
            }

            // TRACKING VALIDATION: Ensure new anchor has good tracking quality
            var status = trackingManager.GetTrackingStatus(newAnchor);
            bool hasGoodTracking = status == Status.TRACKED || status == Status.EXTENDED_TRACKED;

            if (!hasGoodTracking)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"<color=yellow>[ARSafeActivationController] Cannot update group pose from {newAnchor.name} - poor tracking quality ({status})</color>"
                    );
                }
                return;
            }

            // Calculate group pose from new anchor
            if (!GetGroupPoseFromAreaTarget(newAnchor, out Matrix4x4 rawGroupPose))
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"<color=yellow>[ARSafeActivationController] Failed to calculate group pose from {newAnchor.name}</color>"
                    );
                }
                return;
            }

            // Set global reference if not already set
            if (!hasGlobalReferencePose)
            {
                globalReferenceMatrix = rawGroupPose;
                globalReferenceInverse = globalReferenceMatrix.inverse;
                hasGlobalReferencePose = true;

                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"<color=cyan>[ARSafeActivationController] Global position reference locked to {newAnchor.TargetName}</color>"
                    );
                }
            }

            // Apply smoothing (or skip for immediate switch)
            var targetPose = MatrixToPose(rawGroupPose);
            var smoothedPose = ApplyGroupPoseSmoothing(targetPose);

            // Update group pose matrix
            currentGroupPoseMatrix = Matrix4x4.TRS(
                smoothedPose.position,
                smoothedPose.rotation,
                Vector3.one
            );

            // CRITICAL: Update controller transform (shared root parent)
            transform.SetPositionAndRotation(smoothedPose.position, smoothedPose.rotation);

            // Update reparented augmentations to match new pose
            UpdateReparentedAugmentationTransforms();

            // Update tracking state
            currentBestTracked = newAnchor;
            lastSuccessfulTrackingTime = Time.time;
            multiAreaHasTracking = true;

            // Invoke event
            GlobalPoseUpdated?.Invoke(currentGroupPoseMatrix);

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"<color=cyan>[ARSafeActivationController] ★ IMMEDIATE GROUP POSE UPDATE from {newAnchor.TargetName} (tracking: {status})</color>"
                );
            }
        }

        /// <summary>
        /// Get best tracked Area Target (priority: TRACKED > EXTENDED_TRACKED > LIMITED)
        /// </summary>
        private AreaTargetBehaviour GetBestTrackedAreaTarget(
            List<AreaTargetBehaviour> trackedTargets = null
        )
        {
            var tracked = trackedTargets ?? GetTrackedAreaTargets(includeLimited: true);

            if (tracked.Count == 0)
                return null;

            // Prefer TRACKED or EXTENDED_TRACKED
            foreach (var at in tracked)
            {
                var status = trackingManager.GetTrackingStatus(at);
                if (status == Status.TRACKED || status == Status.EXTENDED_TRACKED)
                {
                    return at;
                }
            }

            // Fallback to LIMITED (closest one)
            return tracked[0];
        }

        private AreaTargetBehaviour ResolvePoseAuthority(
            AreaTargetBehaviour preferredTarget,
            List<AreaTargetBehaviour> trackedTargets
        )
        {
            if (preferredTarget != null && ShouldUseTargetForPose(preferredTarget))
            {
                return preferredTarget;
            }

            if (trackedTargets != null)
            {
                foreach (var candidate in trackedTargets)
                {
                    if (candidate == null || candidate == preferredTarget)
                    {
                        continue;
                    }

                    if (ShouldUseTargetForPose(candidate))
                    {
                        return candidate;
                    }
                }
            }

            if (currentBestTracked != null && ShouldUseTargetForPose(currentBestTracked))
            {
                return currentBestTracked;
            }

            return null;
        }

        private bool ShouldUseTargetForPose(AreaTargetBehaviour candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            var status = trackingManager.GetTrackingStatus(candidate);
            if (
                status != Status.TRACKED
                && status != Status.EXTENDED_TRACKED
                && status != Status.LIMITED
            )
            {
                return false;
            }

            if (
                !targetInfoMap.TryGetValue(candidate, out var info)
                || !info.AllowMultiAreaPoseAuthority
            )
            {
                return false;
            }

            if (currentAnchor == null)
            {
                return true;
            }

            if (candidate == currentAnchor)
            {
                return true;
            }

            if (!allowNeighborPoseAuthority)
            {
                return false;
            }

            if (!targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo))
            {
                return false;
            }

            return anchorInfo.IsAdjacentTo(info);
        }

        private Pose ApplyGroupPoseSmoothing(Pose targetPose)
        {
            if (multiAreaPoseSmoothTime <= 0.01f)
            {
                smoothedGroupPose = targetPose;
                smoothedGroupRotation = targetPose.rotation;
                groupPoseVelocity = Vector3.zero;
                hasSmoothedGroupPose = true;
                return targetPose;
            }

            if (!hasSmoothedGroupPose)
            {
                smoothedGroupPose = targetPose;
                smoothedGroupRotation = targetPose.rotation;
                groupPoseVelocity = Vector3.zero;
                hasSmoothedGroupPose = true;
                return targetPose;
            }

            Vector3 smoothedPosition = Vector3.SmoothDamp(
                smoothedGroupPose.position,
                targetPose.position,
                ref groupPoseVelocity,
                multiAreaPoseSmoothTime,
                Mathf.Infinity,
                Time.deltaTime
            );

            smoothedGroupRotation = SmoothDampRotation(
                smoothedGroupRotation,
                targetPose.rotation,
                multiAreaPoseSmoothTime
            );

            smoothedGroupPose = new Pose(smoothedPosition, smoothedGroupRotation);
            return smoothedGroupPose;
        }

        private static Quaternion SmoothDampRotation(
            Quaternion current,
            Quaternion target,
            float smoothTime
        )
        {
            if (smoothTime <= 0f)
            {
                return target;
            }

            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(smoothTime, 0.0001f));
            return Quaternion.Slerp(current, target, Mathf.Clamp01(t));
        }

        /// <summary>
        /// Get all currently tracking Area Targets, optionally including LIMITED status
        /// </summary>
        private List<AreaTargetBehaviour> GetTrackedAreaTargets(bool includeLimited)
        {
            var tracked = new List<AreaTargetBehaviour>();

            foreach (var observer in allAreaTargets)
            {
                if (observer is AreaTargetBehaviour atb)
                {
                    var status = trackingManager.GetTrackingStatus(observer);

                    if (status == Status.TRACKED || status == Status.EXTENDED_TRACKED)
                    {
                        tracked.Add(atb);
                    }
                    else if (includeLimited && status == Status.LIMITED)
                    {
                        tracked.Add(atb);
                    }
                }
            }

            // Sort by distance to camera if multiple tracked
            if (tracked.Count > 1 && arCamera != null)
            {
                tracked.Sort(
                    (a, b) =>
                    {
                        float distA = Vector3.Distance(
                            arCamera.transform.position,
                            a.transform.position
                        );
                        float distB = Vector3.Distance(
                            arCamera.transform.position,
                            b.transform.position
                        );
                        return distA.CompareTo(distB);
                    }
                );
            }

            return tracked;
        }

        /// <summary>
        /// OPTIMIZED: Get tracked Area Targets without allocating a new list (reuses provided list)
        /// This eliminates ~200 bytes of GC allocation per call
        /// </summary>
        private void GetTrackedAreaTargetsOptimized(
            List<AreaTargetBehaviour> targetList,
            bool includeLimited
        )
        {
            // Caller should have cleared the list, but do it here for safety
            targetList.Clear();

            foreach (var observer in allAreaTargets)
            {
                if (observer is AreaTargetBehaviour atb)
                {
                    var status = trackingManager.GetTrackingStatus(observer);

                    if (status == Status.TRACKED || status == Status.EXTENDED_TRACKED)
                    {
                        targetList.Add(atb);
                    }
                    else if (includeLimited && status == Status.LIMITED)
                    {
                        targetList.Add(atb);
                    }
                }
            }

            // Sort by distance to camera if multiple tracked
            if (targetList.Count > 1 && arCamera != null)
            {
                targetList.Sort(
                    (a, b) =>
                    {
                        float distA = Vector3.Distance(
                            arCamera.transform.position,
                            a.transform.position
                        );
                        float distB = Vector3.Distance(
                            arCamera.transform.position,
                            b.transform.position
                        );
                        return distA.CompareTo(distB);
                    }
                );
            }
        }

        /// <summary>
        /// Calculate group pose from a tracked Area Target (drift correction math)
        /// </summary>
        private bool GetGroupPoseFromAreaTarget(AreaTargetBehaviour atb, out Matrix4x4 groupPose)
        {
            groupPose = Matrix4x4.identity;

            if (
                atb == null
                || !relativePoses.TryGetValue(atb.TargetName, out Matrix4x4 areaTargetToGroup)
            )
            {
                return false;
            }

            // Calculate world pose of the group root
            var groupToAreaTarget = areaTargetToGroup.inverse;
            var areaTargetToWorld = atb.transform.localToWorldMatrix;
            groupPose = areaTargetToWorld * groupToAreaTarget;

            return true;
        }

        /// <summary>
        /// Helper: Get transformation matrix from one transform to another
        /// </summary>
        private static Matrix4x4 GetFromToMatrix(Transform from, Transform to)
        {
            var toWorldMatrix = to.localToWorldMatrix;
            var fromWorldMatrix = from.localToWorldMatrix;
            return toWorldMatrix.inverse * fromWorldMatrix;
        }

        /// <summary>
        /// Debug command: Log full system state
        /// </summary>
        [ContextMenu("Debug: Log Full System State")]
        private void DebugLogFullState()
        {
            Debug.Log("=== ARSAFE SYSTEM STATE ===");
            Debug.Log($"Has Localized: {hasLocalized}");
            Debug.Log($"Current Anchor: {currentAnchor?.name ?? "None"}");
            Debug.Log($"Previous Anchor: {previousAnchor?.name ?? "None"}");
            Debug.Log(
                $"Tracking Count: {trackingManager?.GetTrackingCount() ?? 0}/{trackingManager?.maxSimultaneousTracking ?? 0}"
            );
            Debug.Log($"Enabled Targets: {currentlyEnabled.Count}");
            Debug.Log($"Update Interval: {updateInterval}s");
            Debug.Log($"Anchor Switch Distance: {anchorSwitchDistance}m");
            Debug.Log($"Anchor Switch Cooldown: {anchorSwitchCooldown}s");
            Debug.Log($"Time Since Last Switch: {(Time.time - lastAnchorSwitchTime):F2}s");

            Debug.Log("\n=== ALL TARGETS ===");
            foreach (var target in allAreaTargets)
            {
                if (target != null)
                {
                    bool tracking = trackingManager?.IsTracking(target) ?? false;
                    bool enabled = currentlyEnabled.Contains(target);
                    var status = trackingManager?.GetTrackingStatus(target) ?? Status.NO_POSE;
                    var info = targetInfoMap.ContainsKey(target) ? targetInfoMap[target] : null;
                    float distance =
                        arCamera != null
                            ? Vector3.Distance(
                                arCamera.transform.position,
                                target.transform.position
                            )
                            : -1f;

                    string marker = "";
                    if (target == currentAnchor)
                        marker = "[ANCHOR] ";
                    else if (target == previousAnchor)
                        marker = "[PREV] ";

                    Debug.Log(
                        $"{marker}{target.name}: enabled={enabled}, tracking={tracking}, status={status}, "
                            + $"distance={distance:F1}m, type={info?.targetType}, adjacent={info?.GetAllAdjacentTargets()?.Length ?? 0}"
                    );
                }
            }
        }

        /// <summary>
        /// Debug command: Force activate all neighbors of current anchor
        /// </summary>
        [ContextMenu("Debug: Force Activate All Neighbors")]
        private void DebugForceActivateNeighbors()
        {
            if (currentAnchor == null)
            {
                Debug.LogError("No anchor set! Cannot activate neighbors.");
                return;
            }

            if (!targetInfoMap.TryGetValue(currentAnchor, out var anchorInfo))
            {
                Debug.LogError($"Anchor {currentAnchor.name} has no ARSafeTargetInfo component!");
                return;
            }

            var neighbors = anchorInfo.GetAllAdjacentTargets(forceRefresh: true);
            Debug.Log(
                $"<color=cyan>Force activating {neighbors.Length} neighbors of {currentAnchor.name}</color>"
            );

            foreach (var neighborInfo in neighbors)
            {
                if (neighborInfo != null)
                {
                    var obs = neighborInfo.GetComponent<ObserverBehaviour>();
                    if (obs != null)
                    {
                        EnableTarget(obs);
                        Debug.Log($"  ✅ Activated: {obs.name}");
                    }
                }
            }
        }

        /// <summary>
        /// Debug command: Force switch to a specific target (for testing)
        /// </summary>
        [ContextMenu("Debug: List Tracking Targets")]
        private void DebugListTrackingTargets()
        {
            if (trackingManager == null)
            {
                Debug.LogError("No tracking manager!");
                return;
            }

            var tracking = trackingManager.GetCurrentlyTracking();
            Debug.Log(
                $"<color=yellow>=== Currently Tracking Targets ({tracking.Count}) ===</color>"
            );

            foreach (var target in tracking)
            {
                if (target != null)
                {
                    var status = trackingManager.GetTrackingStatus(target);
                    var info = targetInfoMap.ContainsKey(target) ? targetInfoMap[target] : null;
                    float distance =
                        arCamera != null
                            ? Vector3.Distance(
                                arCamera.transform.position,
                                target.transform.position
                            )
                            : -1f;

                    Debug.Log(
                        $"  {target.name}: status={status}, distance={distance:F1}m, "
                            + $"isAnchor={target == currentAnchor}, type={info?.targetType}"
                    );
                }
            }
        }
    }

    /// <summary>
    /// Performance-optimized comparer for sorting target data by distance without LINQ
    /// </summary>
    internal class DistanceComparer
        : System.Collections.Generic.IComparer<(
            ObserverBehaviour target,
            float distance,
            ARSafeTargetInfo info
        )>
    {
        public int Compare(
            (ObserverBehaviour target, float distance, ARSafeTargetInfo info) x,
            (ObserverBehaviour target, float distance, ARSafeTargetInfo info) y
        )
        {
            return x.distance.CompareTo(y.distance);
        }
    }
}
