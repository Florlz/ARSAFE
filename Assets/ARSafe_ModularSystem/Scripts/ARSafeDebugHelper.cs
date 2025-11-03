using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Vuforia;

namespace ARSafe.Modular
{
    /// <summary>
    /// Comprehensive debugging companion for the ARSafe modular stack.
    /// Surfaces high-level health indicators, per-target diagnostics, and
    /// optional runtime overlay output so engineers can quickly understand
    /// what the activation system is doing in the scene.
    ///
    /// KEY CAPABILITIES
    /// ───────────────
    /// • Inspector snapshot of activation / tracking / disaster state
    /// • Change-detection logs for anchors, tracking counts, and visibility
    /// • Scene gizmos that visualize activation radii and adjacency links
    /// • (Optional) Continuous summary pushed to the DebugOverlay HUD
    /// • Context-menu utilities for deep dives (adjacency, full snapshots)
    ///
    /// QUICK START
    /// ───────────
    /// 1. Drop on an empty GameObject to watch the whole system
    /// 2. Assign `targetToMonitor` to follow a specific Area Target
    /// 3. Enable “Push Summary To Debug Overlay” for live HUD output
    /// 4. Use the context menu to log snapshots or adjacency breakdowns
    /// </summary>
    public class ARSafeDebugHelper : MonoBehaviour
    {
        private const string LogPrefix = "[ARSafeDebugHelper]";

        [Header("Debug Targets")]
        [Tooltip("If set, monitor this specific Area Target. Leave empty to monitor all targets")]
        public ObserverBehaviour targetToMonitor;

        [Tooltip("If true, monitor the ARSafeActivationController")]
        public bool monitorActivationController = true;

        [Tooltip("If true, monitor the ARSafeTrackingManager")]
        public bool monitorTrackingManager = true;

        [Header("Gizmo Settings")]
        [Tooltip("Draw activation/deactivation radii in Scene view")]
        public bool drawRadiiGizmos = true;

        [Tooltip("Draw lines to adjacent targets in Scene view")]
        public bool drawAdjacencyGizmos = true;

        [Tooltip("Color for neighbor activation radius")]
        public Color enableRadiusColor = new Color(0f, 1f, 0f, 0.2f);

        [Tooltip("Color for anchor switch radius")]
        public Color disableRadiusColor = new Color(1f, 0f, 0f, 0.2f);

        [Tooltip("Color for adjacency lines")]
        public Color adjacencyLineColor = new Color(1f, 1f, 0f, 0.5f);

        [Header("Logging")]
        [Tooltip("Log tracking state changes")]
        public bool logTrackingChanges = true;

        [Tooltip("Log activation state changes")]
        public bool logActivationChanges = true;

        [Tooltip("Log visibility changes")]
        public bool logVisibilityChanges = true;

        [Tooltip("Log disaster type changes")]
        public bool logDisasterTypeChanges = true;

        [Header("Overlay Output (Optional)")]
        [Tooltip("Push a condensed summary to the runtime DebugOverlay HUD")]
        public bool pushSummaryToDebugOverlay = false;

        [Tooltip("Seconds between overlay refreshes (only used when push is enabled)")]
        [Range(0.1f, 3f)]
        public float overlayRefreshInterval = 0.5f;

        [Tooltip("Include a per-target list in the overlay when monitoring globally")]
        public bool overlayIncludeTargetBreakdown = true;

        [Tooltip(
            "When monitoring a specific target, append its adjacency breakdown to overlay output"
        )]
        public bool overlayIncludeAdjacencyForMonitoredTarget = true;

        [Header("Runtime Info (Read-Only)")]
        [SerializeField]
        private string currentAnchor = "None";

        [SerializeField]
        private int trackingCount = 0;

        [SerializeField]
        private int enabledCount = 0;

        [SerializeField]
        private DisasterType currentDisasterType = DisasterType.GeneralSafety;

        [Header("Target Info (if monitoring specific target)")]
        [SerializeField]
        private string targetStatus = "NO_POSE";

        [SerializeField]
        private float distanceToCamera = 0f;

        [SerializeField]
        private float distanceToBoundary = 0f;

        [SerializeField]
        private bool isTracking = false;

        [SerializeField]
        private bool isEnabled = false;

        [SerializeField]
        private bool isContentVisible = false;

        [SerializeField]
        private bool isAnchor = false;

        // Runtime references
        private ARSafeActivationController activationController;
        private ARSafeTrackingManager trackingManager;
        private ARSafeProximityDisplay proximityDisplay;
        private ARSafeTargetInfo targetInfo;
        private ObserverBehaviour cachedMonitoredTarget;
        private Camera arCamera;
        private readonly List<ObserverBehaviour> scratchTargetBuffer = new List<ObserverBehaviour>(
            32
        );
        private readonly StringBuilder scratchBuilder = new StringBuilder(512);

        // Previous state tracking for change detection
        private string prevAnchor = "";
        private int prevTrackingCount = -1;
        private DisasterType prevDisasterType = DisasterType.GeneralSafety;
        private bool prevIsTracking = false;
        private bool prevIsVisible = false;

        // Overlay timing
        private float lastOverlayPushTime = -10f;

        #region Reference Caching

        private void CacheGlobalReferences(bool forceRefresh = false)
        {
            if (forceRefresh || activationController == null)
            {
                activationController = FindFirstObjectByType<ARSafeActivationController>();
            }

            if (forceRefresh || trackingManager == null)
            {
                trackingManager = FindFirstObjectByType<ARSafeTrackingManager>();
            }

            if (forceRefresh || arCamera == null || !arCamera.isActiveAndEnabled)
            {
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    arCamera = mainCamera;
                }
                else
                {
                    arCamera = FindFirstObjectByType<Camera>();
                }
            }
        }

        private void ResolveTargetComponents()
        {
            if (targetToMonitor == cachedMonitoredTarget)
            {
                return;
            }

            cachedMonitoredTarget = targetToMonitor;
            proximityDisplay = null;
            targetInfo = null;

            if (targetToMonitor == null)
            {
                return;
            }

            proximityDisplay = targetToMonitor.GetComponent<ARSafeProximityDisplay>();
            targetInfo = targetToMonitor.GetComponent<ARSafeTargetInfo>();

            // Reset per-target change detection to avoid stale comparisons when swapping targets
            prevIsTracking = false;
            prevIsVisible = false;
        }

        private void ResetTrackedState()
        {
            prevAnchor = string.Empty;
            prevTrackingCount = -1;
            prevDisasterType = currentDisasterType;
            prevIsTracking = false;
            prevIsVisible = false;
        }

        #endregion

        #region Overlay Output

        private void TryPushOverlaySummary()
        {
            if (!pushSummaryToDebugOverlay)
            {
                return;
            }

            if (DebugOverlay.Instance == null)
            {
                return;
            }

            if (Time.time - lastOverlayPushTime < overlayRefreshInterval)
            {
                return;
            }

            BuildOverlayPayload(
                out string statusText,
                out string anchorDetails,
                out string targetListText
            );
            DebugOverlay.Instance.UpdateDisplay(statusText, anchorDetails, targetListText);
            lastOverlayPushTime = Time.time;
        }

        private void BuildOverlayPayload(
            out string statusText,
            out string anchorDetails,
            out string targetListText
        )
        {
            // === STATUS SECTION (System overview) ===
            scratchBuilder.Clear();

            // Vuforia status
            bool vuforiaRunning =
                Vuforia.VuforiaApplication.Instance != null
                && Vuforia.VuforiaApplication.Instance.IsRunning;
            scratchBuilder.AppendLine(
                $"<b>Vuforia:</b> {(vuforiaRunning ? "<color=green>Running</color>" : "<color=red>Not Running</color>")}"
            );

            // Tracking and activation counts
            int maxTracking = trackingManager != null ? trackingManager.maxSimultaneousTracking : 2;
            string trackingColor = trackingCount > 0 ? "green" : "yellow";
            scratchBuilder.AppendLine(
                $"<b>Tracking:</b> <color={trackingColor}>{trackingCount}/{maxTracking}</color> | <b>Enabled:</b> {enabledCount}"
            );

            // Disaster filter
            if (currentDisasterType != DisasterType.None)
            {
                scratchBuilder.AppendLine($"<b>Filter:</b> {currentDisasterType}");
            }

            statusText = scratchBuilder.ToString().TrimEnd();

            // === ANCHOR DETAILS SECTION (Rich metadata) ===
            scratchBuilder.Clear();

            if (
                !string.IsNullOrEmpty(currentAnchor)
                && currentAnchor != "None"
                && activationController != null
            )
            {
                var anchor = activationController.GetCurrentAnchor();
                if (anchor != null)
                {
                    var anchorInfo = anchor.GetComponent<ARSafeTargetInfo>();
                    if (anchorInfo != null)
                    {
                        string anchorType = anchorInfo.targetType.ToString();
                        float distance = anchorInfo.DistanceToCamera;
                        float boundaryDist = anchorInfo.DistanceToBoundary;
                        bool inside = boundaryDist < 0f;
                        string trackingStatus =
                            trackingManager != null
                                ? trackingManager.GetTrackingStatus(anchor).ToString()
                                : "Unknown";
                        int adjacentCount = anchorInfo.GetAllAdjacentTargets()?.Length ?? 0;
                        int connectedRooms = anchorInfo.connectedRooms?.Length ?? 0;

                        // Anchor header with star marker
                        scratchBuilder.AppendLine(
                            $"<b>★ Current Anchor:</b> <color=#66CCFF>{currentAnchor}</color>"
                        );

                        // Type and priority
                        scratchBuilder.AppendLine(
                            $"<b>Type:</b> {anchorType} | <b>Priority:</b> {anchorInfo.basePriority}"
                        );

                        // Distance and boundary status WITH IMPROVED DISPLAY
                        scratchBuilder.Append($"<b>Center:</b> {distance:F1}m | <b>Edge:</b> ");
                        if (inside)
                        {
                            // Show depth inside (how far from nearest edge)
                            float depthInside = Mathf.Abs(boundaryDist);
                            scratchBuilder.AppendLine($"<color=green>IN {depthInside:F1}m</color>");
                        }
                        else
                        {
                            scratchBuilder.AppendLine(
                                $"<color=gray>OUT {boundaryDist:F1}m</color>"
                            );
                        }

                        // Tracking and adjacency
                        scratchBuilder.AppendLine(
                            $"<b>Tracking:</b> {trackingStatus} | <b>Adjacent:</b> {adjacentCount}"
                        );

                        // Connected rooms (for hallways)
                        if (connectedRooms > 0)
                        {
                            scratchBuilder.AppendLine(
                                $"<b>Connected Rooms:</b> {connectedRooms} <color=#FFD700>(+200 priority)</color>"
                            );
                        }
                    }
                    else
                    {
                        scratchBuilder.AppendLine(
                            $"<b>★ Anchor:</b> <color=#66CCFF>{currentAnchor}</color>"
                        );
                        scratchBuilder.AppendLine("<i>No ARSafeTargetInfo component</i>");
                    }
                }
                else
                {
                    scratchBuilder.AppendLine($"<b>Anchor:</b> {currentAnchor}");
                    scratchBuilder.AppendLine("<i>(Anchor reference lost)</i>");
                }
            }
            else
            {
                scratchBuilder.AppendLine(
                    "<b>Anchor:</b> <color=yellow>Searching for initial target...</color>"
                );
            }

            anchorDetails = scratchBuilder.ToString().TrimEnd();

            scratchBuilder.Clear();
            if (targetToMonitor != null)
            {
                AppendMonitoredTargetDetails(scratchBuilder);
            }
            else if (overlayIncludeTargetBreakdown)
            {
                AppendGlobalTargetBreakdown(scratchBuilder);
            }

            targetListText = scratchBuilder.ToString().TrimEnd();
            scratchBuilder.Clear();
        }

        private void AppendMonitoredTargetDetails(StringBuilder builder)
        {
            builder.AppendLine(targetToMonitor != null ? targetToMonitor.name : "(No target)");
            builder.AppendLine($"Status: {targetStatus}");
            builder.AppendLine(
                $"Tracking: {isTracking} | Enabled: {isEnabled} | Visible: {isContentVisible}"
            );
            builder.AppendLine(
                $"Distance: {distanceToCamera:F1}m | Boundary: {distanceToBoundary:F1}m {(distanceToBoundary < 0f ? "(inside)" : "(outside)")}"
            );

            if (targetInfo != null)
            {
                builder.AppendLine($"Type: {targetInfo.targetType} | Anchor: {isAnchor}");

                if (trackingManager != null && isTracking && targetInfo.LastTrackingTime > 0f)
                {
                    float trackedDuration = Mathf.Max(0f, Time.time - targetInfo.LastTrackingTime);
                    builder.AppendLine($"Tracking For: {trackedDuration:F1}s");
                }
            }

            if (overlayIncludeAdjacencyForMonitoredTarget && targetInfo != null)
            {
                AppendAdjacencyBreakdown(builder, targetInfo);
            }
        }

        private void AppendAdjacencyBreakdown(StringBuilder builder, ARSafeTargetInfo info)
        {
            var adjacent = info.GetAllAdjacentTargets();
            if (adjacent == null || adjacent.Length == 0)
            {
                builder.AppendLine("Adjacency: (none)");
                return;
            }

            builder.AppendLine("Adjacency:");
            foreach (var adjacentInfo in adjacent)
            {
                if (adjacentInfo == null)
                    continue;
                var observer = adjacentInfo.GetComponent<ObserverBehaviour>();
                bool tracked =
                    observer != null
                    && trackingManager != null
                    && trackingManager.IsTracking(observer);
                bool enabled =
                    observer != null
                    && activationController != null
                    && activationController.IsTargetEnabled(observer);

                builder.Append(" • ");
                builder.Append(adjacentInfo.name);
                builder.Append(" [");
                builder.Append(tracked ? 'T' : '-');
                builder.Append(enabled ? 'E' : '-');
                builder.Append(']');
                builder.AppendLine();
            }
        }

        private void AppendGlobalTargetBreakdown(StringBuilder builder)
        {
            PopulateTargetBuffer();

            if (scratchTargetBuffer.Count == 0)
            {
                builder.Append("No Area Targets discovered.");
                return;
            }

            // FIXED SLOT LAYOUT - matches ARSafeDebugOverlayIntegration
            builder.AppendLine("<b>═══ ACTIVE TARGETS ═══</b>");

            var currentAnchor =
                activationController != null ? activationController.GetCurrentAnchor() : null;
            var displaySlots = new List<ObserverBehaviour>();

            // SLOT 1: Always show current anchor (if exists)
            if (currentAnchor != null && !displaySlots.Contains(currentAnchor))
            {
                displaySlots.Add(currentAnchor);
            }

            // SLOTS 2-N: Show enabled/tracking targets in FIXED order (from buffer)
            int maxSlots = 8; // Limit to prevent overflow
            foreach (var target in scratchTargetBuffer)
            {
                if (displaySlots.Count >= maxSlots)
                    break;
                if (displaySlots.Contains(target))
                    continue; // Skip if already added

                bool isTracking = trackingManager != null && trackingManager.IsTracking(target);
                bool isEnabled =
                    activationController != null && activationController.IsTargetEnabled(target);

                if (isTracking || isEnabled)
                {
                    displaySlots.Add(target);
                }
            }

            // Render the fixed slots
            for (int i = 0; i < maxSlots; i++)
            {
                if (i < displaySlots.Count)
                {
                    var target = displaySlots[i];
                    var info = target.GetComponent<ARSafeTargetInfo>();

                    bool isTracking = trackingManager != null && trackingManager.IsTracking(target);
                    bool isAnchor = currentAnchor != null && target == currentAnchor;

                    // Fixed format elements
                    string slotNum = $"[{i + 1}]";
                    string anchorMarker = isAnchor ? "★" : " ";
                    string statusMarker = isTracking
                        ? "<color=green>T</color>"
                        : "<color=yellow>E</color>";

                    // Distance info
                    string distanceText = "---";
                    if (info != null)
                    {
                        float boundaryDist = info.DistanceToBoundary;
                        float centerDist = info.DistanceToCamera;

                        if (boundaryDist < 0f)
                        {
                            distanceText =
                                $"<color=green>IN {Mathf.Abs(boundaryDist):F0}m</color> (C:{centerDist:F0}m)";
                        }
                        else
                        {
                            distanceText = $"{boundaryDist:F0}m (C:{centerDist:F0}m)";
                        }
                    }

                    // Fixed-width line format
                    builder.AppendLine(
                        $"{slotNum} {anchorMarker}[{statusMarker}] {target.name, -25} {distanceText}"
                    );
                }
                else
                {
                    // Empty slot (maintains layout stability)
                    builder.AppendLine($"[{i + 1}] - <color=gray>---</color>");
                }
            }

            // Fixed footer
            builder.AppendLine("<b>═══════════════════</b>");

            // Summary (counts only)
            int trackingCount = 0;
            int enabledCount = 0;
            foreach (var target in scratchTargetBuffer)
            {
                bool isTracking = trackingManager != null && trackingManager.IsTracking(target);
                bool isEnabled =
                    activationController != null && activationController.IsTargetEnabled(target);

                if (isTracking)
                    trackingCount++;
                else if (isEnabled)
                    enabledCount++;
            }

            builder.AppendLine(
                $"<color=gray>T:{trackingCount} E:{enabledCount} Total:{scratchTargetBuffer.Count}</color>"
            );
        }

        private void PopulateTargetBuffer()
        {
            scratchTargetBuffer.Clear();

            if (
                activationController != null
                && activationController.allAreaTargets != null
                && activationController.allAreaTargets.Count > 0
            )
            {
                foreach (var observer in activationController.allAreaTargets)
                {
                    if (observer != null)
                    {
                        scratchTargetBuffer.Add(observer);
                    }
                }
            }
            else
            {
                var observers = FindObjectsByType<ObserverBehaviour>(FindObjectsSortMode.None);
                foreach (var observer in observers)
                {
                    if (observer != null)
                    {
                        scratchTargetBuffer.Add(observer);
                    }
                }
            }
        }

        private string FormatTargetDescriptor(ObserverBehaviour observer)
        {
            var info = observer.GetComponent<ARSafeTargetInfo>();
            bool tracked = trackingManager != null && trackingManager.IsTracking(observer);
            bool enabled =
                activationController != null && activationController.IsTargetEnabled(observer);
            bool anchor = info != null && info.IsCurrentAnchor;
            float distance =
                info != null
                    ? info.DistanceToCamera
                    : (
                        arCamera != null
                            ? Vector3.Distance(
                                arCamera.transform.position,
                                observer.transform.position
                            )
                            : 0f
                    );
            float boundaryDist = info != null ? info.DistanceToBoundary : 0f;
            string statusLabel =
                trackingManager != null
                    ? trackingManager.GetTrackingStatus(observer).ToString()
                    : "Unknown";

            var lineBuilder = new StringBuilder(128);

            // Status indicators
            lineBuilder.Append(anchor ? "[A]" : "   ");
            lineBuilder.Append(tracked ? "[T]" : "   ");
            lineBuilder.Append(' ');

            // Target name with color based on status
            if (tracked)
            {
                lineBuilder.Append("<color=green>");
            }
            else if (enabled)
            {
                lineBuilder.Append("<color=yellow>");
            }
            else
            {
                lineBuilder.Append("<color=grey>");
            }

            lineBuilder.Append(observer.name);
            lineBuilder.Append("</color>");

            // Additional metadata
            if (info != null)
            {
                lineBuilder.Append(" | ");
                lineBuilder.AppendFormat("{0:F1}m", distance);
                lineBuilder.Append(" | ");

                // Boundary status with color
                if (boundaryDist < 0f)
                {
                    lineBuilder.Append("<color=green>(INSIDE)</color>");
                }
                else
                {
                    lineBuilder.Append("(outside)");
                }

                // Target type (show for rooms/hallways only)
                if (info.targetType == TargetType.Room || info.targetType == TargetType.Exit || info.targetType == TargetType.Hallway)
                {
                    lineBuilder.Append($" | {info.targetType}");
                }
            }
            else
            {
                lineBuilder.Append(" | ");
                lineBuilder.AppendFormat("{0:F1}m", distance);
            }

            if (info != null)
            {
                lineBuilder.Append(" | ");
                lineBuilder.Append(info.targetType);
            }

            lineBuilder.Append(" | ");
            lineBuilder.Append(statusLabel);

            if (info != null && info.LastTrackingTime > 0f && tracked)
            {
                float trackedFor = Mathf.Max(0f, Time.time - info.LastTrackingTime);
                lineBuilder.AppendFormat(" | {0:F1}s", trackedFor);
            }

            return lineBuilder.ToString();
        }

        #endregion

        private void Awake()
        {
            CacheGlobalReferences(forceRefresh: true);
            ResolveTargetComponents();
            ResetTrackedState();
        }

        void Start()
        {
            CacheGlobalReferences();
            ResolveTargetComponents();

            // Subscribe to disaster type changes
            if (logDisasterTypeChanges && DisasterTypeManager.Instance != null)
            {
                DisasterTypeManager.OnDisasterTypeChanged += OnDisasterTypeChanged;
            }

            Debug.Log(
                $"{LogPrefix} Initialized. Monitoring: {(targetToMonitor != null ? targetToMonitor.name : "Scene-wide")}"
            );
        }

        void OnDestroy()
        {
            // Unsubscribe
            DisasterTypeManager.OnDisasterTypeChanged -= OnDisasterTypeChanged;
        }

        void Update()
        {
            // CRITICAL: Null checks to prevent errors during scene transitions or cleanup
            if (targetToMonitor == null && !monitorActivationController && !monitorTrackingManager)
                return;
            
            CacheGlobalReferences();
            ResolveTargetComponents();
            UpdateRuntimeInfo();
            DetectStateChanges();
            TryPushOverlaySummary();
        }

        /// <summary>
        /// Update runtime info displayed in inspector
        /// </summary>
        private void UpdateRuntimeInfo()
        {
            // Global info
            if (monitorActivationController && activationController != null)
            {
                var anchor = activationController.GetCurrentAnchor();
                currentAnchor = anchor != null ? anchor.name : "None";

                enabledCount = activationController.ActiveTargetCount;
            }

            if (monitorTrackingManager && trackingManager != null)
            {
                trackingCount = trackingManager.GetTrackingCount();
            }

            if (DisasterTypeManager.Instance != null)
            {
                currentDisasterType = DisasterTypeManager.SelectedDisasterType;
            }

            // Target-specific info
            if (targetToMonitor != null)
            {
                if (trackingManager != null)
                {
                    isTracking = trackingManager.IsTracking(targetToMonitor);
                    targetStatus = trackingManager.GetTrackingStatus(targetToMonitor).ToString();
                }

                if (activationController != null)
                {
                    isEnabled = activationController.IsTargetEnabled(targetToMonitor);
                }

                if (proximityDisplay != null)
                {
                    isContentVisible = proximityDisplay.IsContentVisible();
                }

                if (targetInfo != null)
                {
                    distanceToCamera = targetInfo.DistanceToCamera;
                    distanceToBoundary = targetInfo.DistanceToBoundary;
                    isAnchor = targetInfo.IsCurrentAnchor;
                }
                else if (arCamera != null)
                {
                    distanceToCamera = Vector3.Distance(
                        arCamera.transform.position,
                        targetToMonitor.transform.position
                    );
                    distanceToBoundary = 0f;
                }
            }
        }

        /// <summary>
        /// Detect and log state changes
        /// </summary>
        private void DetectStateChanges()
        {
            // Anchor change
            if (
                logActivationChanges
                && currentAnchor != prevAnchor
                && !string.IsNullOrEmpty(prevAnchor)
            )
            {
                Debug.Log($"{LogPrefix} Anchor changed: {prevAnchor} → {currentAnchor}");
            }
            prevAnchor = currentAnchor;

            // Tracking count change
            if (logTrackingChanges && trackingCount != prevTrackingCount && prevTrackingCount >= 0)
            {
                Debug.Log(
                    $"{LogPrefix} Tracking count changed: {prevTrackingCount} → {trackingCount}"
                );
            }
            prevTrackingCount = trackingCount;

            // Target-specific changes
            if (targetToMonitor != null)
            {
                // Tracking change
                if (logTrackingChanges && isTracking != prevIsTracking)
                {
                    Debug.Log(
                        $"{LogPrefix} {targetToMonitor.name} tracking: {prevIsTracking} → {isTracking} (Status: {targetStatus})"
                    );
                }
                prevIsTracking = isTracking;

                // Visibility change
                if (logVisibilityChanges && isContentVisible != prevIsVisible)
                {
                    Debug.Log(
                        $"{LogPrefix} {targetToMonitor.name} visibility: {prevIsVisible} → {isContentVisible} (Distance: {distanceToCamera:F2}m)"
                    );
                }
                prevIsVisible = isContentVisible;
            }
        }

        /// <summary>
        /// DisasterTypeManager callback
        /// </summary>
        private void OnDisasterTypeChanged(DisasterType newType)
        {
            if (logDisasterTypeChanges && newType != prevDisasterType)
            {
                Debug.Log($"{LogPrefix} Disaster type changed: {prevDisasterType} → {newType}");
            }
            prevDisasterType = newType;
        }

        /// <summary>
        /// Draw gizmos in Scene view
        /// </summary>
        void OnDrawGizmos()
        {
            if (!drawRadiiGizmos && !drawAdjacencyGizmos)
                return;
            CacheGlobalReferences();
            ResolveTargetComponents();

            // Draw radii for activation controller
            if (drawRadiiGizmos && activationController != null && arCamera != null)
            {
                Vector3 cameraPos = arCamera.transform.position;

                if (activationController.neighborActivationRadius > 0f)
                {
                    Gizmos.color = enableRadiusColor;
                    DrawWireDisc(cameraPos, activationController.neighborActivationRadius);
                }

                if (activationController.anchorSwitchDistance > 0f)
                {
                    Gizmos.color = disableRadiusColor;
                    DrawWireDisc(cameraPos, activationController.anchorSwitchDistance);
                }
            }

            // Draw adjacency lines for specific target
            if (drawAdjacencyGizmos && targetToMonitor != null && targetInfo != null)
            {
                if (targetInfo.adjacentTargets != null)
                {
                    Gizmos.color = adjacencyLineColor;
                    foreach (var adjacent in targetInfo.adjacentTargets)
                    {
                        if (adjacent != null)
                        {
                            Gizmos.DrawLine(
                                targetToMonitor.transform.position,
                                adjacent.transform.position
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draw a wire disc (circle) at position with radius
        /// </summary>
        private void DrawWireDisc(Vector3 center, float radius)
        {
            int segments = 32;
            float angleStep = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = Mathf.Deg2Rad * (angleStep * i);
                float angle2 = Mathf.Deg2Rad * (angleStep * (i + 1));

                Vector3 point1 =
                    center + new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 point2 =
                    center + new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                Gizmos.DrawLine(point1, point2);
            }
        }

        /// <summary>
        /// Public API: Log current state snapshot
        /// </summary>
        [ContextMenu("Log State Snapshot")]
        public void LogStateSnapshot()
        {
            UpdateRuntimeInfo();

            BuildOverlayPayload(out string status, out string anchorDetails, out string targets);

            var sb = new StringBuilder(512);
            sb.AppendLine("=== ARSafe Debug Snapshot ===");
            sb.AppendLine(status);

            if (!string.IsNullOrWhiteSpace(anchorDetails))
            {
                sb.AppendLine("--- Anchor Details ---");
                sb.AppendLine(anchorDetails);
            }

            if (!string.IsNullOrWhiteSpace(targets))
            {
                sb.AppendLine("--- Targets ---");
                sb.AppendLine(targets);
            }

            if (targetInfo != null)
            {
                sb.AppendLine("--- Target Metadata ---");
                sb.AppendLine(
                    $"Type: {targetInfo.targetType} | Base Priority: {targetInfo.basePriority}"
                );
                sb.AppendLine(
                    $"Starting Target: {targetInfo.isStartingTarget} | Anchor: {isAnchor}"
                );
                sb.AppendLine(
                    $"Visibility Range: {targetInfo.minVisibilityDistance:F1}m - {targetInfo.maxVisibilityDistance:F1}m"
                );

                scratchBuilder.Clear();
                AppendAdjacencyBreakdown(scratchBuilder, targetInfo);
                var adjacency = scratchBuilder.ToString().TrimEnd();
                scratchBuilder.Clear();
                if (!string.IsNullOrEmpty(adjacency) && !adjacency.Contains("(none)"))
                {
                    sb.AppendLine("--- Adjacency ---");
                    sb.AppendLine(adjacency);
                }
            }

            sb.AppendLine("====================================");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Public API: Force refresh all debug info
        /// </summary>
        [ContextMenu("Force Refresh")]
        public void ForceRefresh()
        {
            CacheGlobalReferences(forceRefresh: true);
            ResolveTargetComponents();
            UpdateRuntimeInfo();
            TryPushOverlaySummary();
            Debug.Log($"{LogPrefix} Inspector values refreshed.");
        }

        /// <summary>
        /// Public API: Log adjacency listing for the monitored target
        /// </summary>
        [ContextMenu("Log Target Adjacency")]
        public void LogTargetAdjacency()
        {
            if (targetInfo == null)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Cannot log adjacency – monitored target has no ARSafeTargetInfo.",
                    this
                );
                return;
            }

            scratchBuilder.Clear();
            AppendAdjacencyBreakdown(scratchBuilder, targetInfo);
            var adjacency = scratchBuilder.ToString();
            scratchBuilder.Clear();

            if (string.IsNullOrWhiteSpace(adjacency) || adjacency.Contains("(none)"))
            {
                Debug.Log($"{LogPrefix} {targetInfo.name} has no adjacency configured.");
                return;
            }

            Debug.Log(
                $"{LogPrefix} Adjacency for {targetInfo.name}:{System.Environment.NewLine}{adjacency}"
            );
        }

        /// <summary>
        /// Public API: Log overview of all discovered targets
        /// </summary>
        [ContextMenu("Log All Targets Overview")]
        public void LogAllTargetsOverview()
        {
            CacheGlobalReferences();
            PopulateTargetBuffer();

            scratchBuilder.Clear();
            AppendGlobalTargetBreakdown(scratchBuilder);
            var overview = scratchBuilder.ToString();
            scratchBuilder.Clear();

            Debug.Log($"{LogPrefix} Target overview:{System.Environment.NewLine}{overview}");
        }
    }
}
