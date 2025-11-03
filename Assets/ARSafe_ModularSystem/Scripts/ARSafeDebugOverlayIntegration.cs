using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ARSafe.Modular.Integration
{
    /// <summary>
    /// Integrates the new modular system with the existing DebugOverlay.
    /// Collects state from ARSafeActivationController and ARSafeTrackingManager,
    /// formats it, and feeds it to DebugOverlay.UpdateDisplay() with 3-section layout.
    ///
    /// REPLACES: AreaTargetActivationManager.UpdateDebugOverlay() logic
    /// WORKS WITH: DebugOverlay.Instance.UpdateDisplay(statusText, anchorDetailsText, targetListText)
    ///
    /// FEATURES:
    /// - System status (Vuforia, tracking counts, disaster filter)
    /// - Rich anchor details (★ marker, type, priority, distance, boundary, adjacency, connected rooms)
    /// - Target list with color-coded states
    ///
    /// USAGE:
    /// 1. Attach to same GameObject as ARSafeActivationController
    /// 2. References are auto-assigned
    /// 3. Configure update rate and display options
    /// 4. Ensure DebugOverlay is in scene
    /// </summary>
    [RequireComponent(typeof(ARSafeActivationController))]
    public class ARSafeDebugOverlayIntegration : MonoBehaviour
    {
        [Header("Required References (Auto-assigned)")]
        [Tooltip("Activation controller - auto-assigned")]
        private ARSafeActivationController activationController;

        [Tooltip("Tracking manager - auto-assigned")]
        private ARSafeTrackingManager trackingManager;

        [Tooltip("Proximity display - optional")]
        private ARSafeProximityDisplay proximityDisplay;

        [Tooltip("Disaster filter - optional")]
        private ARSafeDisasterFilter disasterFilter;

        [Header("Update Settings")]
        [Tooltip("How often to update the overlay (seconds)")]
        [Range(0.1f, 2f)]
        public float updateRate = 1f; // Reduced update frequency

        [Tooltip("Only update when something changes (saves performance)")]
        public bool updateOnlyOnChange = true;

        [Tooltip(
            "Minimum distance change (meters) to trigger update - prevents flicker from tiny changes"
        )]
        [Range(0.1f, 2f)]
        public float distanceChangeThreshold = 1f; // Increased threshold

        [Header("Display Options")]
        [Tooltip("Show Vuforia status in overlay")]
        public bool showVuforiaStatus = true;

        [Tooltip("Show tracking information")]
        public bool showTrackingInfo = true;

        [Tooltip("Show activation states")]
        public bool showActivationStates = true;

        [Tooltip("Show proximity information")]
        public bool showProximityInfo = true;

        [Tooltip("Show disaster filter info")]
        public bool showDisasterFilter = true;

        [Tooltip("Maximum targets to display in list")]
        [Range(5, 50)]
        public int maxTargetsToDisplay = 20;

        [Header("Debug")]
        public bool enableDebugLogs = false;

        private float updateTimer;
        private string lastStatusText;
        private string lastAnchorDetailsText;
        private string lastTargetListText;

        // Cache for distance-based change detection
        private Dictionary<GameObject, float> lastDistances = new Dictionary<GameObject, float>();
        private Vuforia.ObserverBehaviour lastAnchor;
        private int lastTrackingCount;
        private int lastEnabledCount;

        void Awake()
        {
            // Auto-assign references
            activationController = GetComponent<ARSafeActivationController>();
            trackingManager = GetComponent<ARSafeTrackingManager>();
            proximityDisplay = GetComponent<ARSafeProximityDisplay>();
            disasterFilter = GetComponent<ARSafeDisasterFilter>();
        }

        void Start()
        {
            ValidateSetup();
        }

        void Update()
        {
            // CRITICAL: Check if debug overlay is enabled in settings
            if (ARSafe.ARSafeSettings.Instance != null && !ARSafe.ARSafeSettings.Instance.DebugOverlay)
            {
                // User disabled debug overlay in settings - hide it and don't update
                if (DebugOverlay.Instance != null)
                {
                    DebugOverlay.Instance.Hide();
                }
                return;
            }

            // CRITICAL: Null checks to prevent errors during scene transitions or cleanup
            if (DebugOverlay.Instance == null || activationController == null || trackingManager == null)
                return;

            // Update timer
            updateTimer += Time.deltaTime;

            if (updateTimer >= updateRate)
            {
                updateTimer = 0f;

                // Check if significant changes occurred before updating
                if (updateOnlyOnChange && !HasSignificantChanges())
                {
                    return; // Skip update if no significant changes
                }

                UpdateDebugOverlay();
            }
        }

        /// <summary>
        /// Check if there are significant changes that warrant an overlay update
        /// </summary>
        private bool HasSignificantChanges()
        {
            if (activationController == null || trackingManager == null)
                return true;

            // Check anchor change
            var currentAnchor = activationController.GetCurrentAnchor();
            if (currentAnchor != lastAnchor)
            {
                return true;
            }

            // Check tracking count change
            int currentTrackingCount = trackingManager.GetTrackingCount();
            if (currentTrackingCount != lastTrackingCount)
            {
                return true;
            }

            // Check enabled count change
            int currentEnabledCount = activationController.ActiveTargetCount;
            if (currentEnabledCount != lastEnabledCount)
            {
                return true;
            }

            // Check significant distance changes for visible targets
            var allTargets = activationController.allAreaTargets;
            foreach (var target in allTargets)
            {
                bool isTracking = target != null && trackingManager.IsTracking(target);
                bool isEnabled = target != null && activationController.IsTargetEnabled(target);

                // Only check tracking and enabled targets (disabled don't show distance)
                if (isTracking || isEnabled)
                {
                    var info = target.GetComponent<ARSafeTargetInfo>();
                    if (info != null)
                    {
                        float currentDist = info.DistanceToBoundary;

                        if (lastDistances.TryGetValue(target.gameObject, out float lastDist))
                        {
                            // Check if distance changed significantly
                            if (Mathf.Abs(currentDist - lastDist) > distanceChangeThreshold)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // New target appeared
                            return true;
                        }
                    }
                }
            }

            return false; // No significant changes
        }

        /// <summary>
        /// Validate that everything is configured correctly
        /// </summary>
        private void ValidateSetup()
        {
            if (activationController == null)
            {
                Debug.LogError(
                    "[ARSafeDebugOverlayIntegration] ARSafeActivationController not found!"
                );
                enabled = false;
                return;
            }

            if (DebugOverlay.Instance == null)
            {
                Debug.LogWarning(
                    "[ARSafeDebugOverlayIntegration] DebugOverlay not found in scene. Integration will not work."
                );
                enabled = false;
                return;
            }

            if (trackingManager == null)
            {
                Debug.LogWarning(
                    "[ARSafeDebugOverlayIntegration] ARSafeTrackingManager not found. Tracking info will not be displayed."
                );
            }

            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeDebugOverlayIntegration] Integration initialized successfully.");
            }
        }

        /// <summary>
        /// Update the debug overlay with current system state
        /// </summary>
        private void UpdateDebugOverlay()
        {
            // === SECTION 1: STATUS (System Overview) ===
            StringBuilder status = new StringBuilder();

            // Vuforia status
            if (showVuforiaStatus)
            {
                bool vuforiaRunning =
                    Vuforia.VuforiaApplication.Instance != null
                    && Vuforia.VuforiaApplication.Instance.IsRunning;
                status.AppendLine(
                    $"<b>Vuforia:</b> {(vuforiaRunning ? "<color=green>Running</color>" : "<color=red>Not Running</color>")}"
                );
            }

            // Tracking info
            if (showTrackingInfo && trackingManager != null)
            {
                int trackingCount = trackingManager.GetTrackingCount();
                int maxObservers = trackingManager.maxSimultaneousTracking;

                string trackingColor = trackingCount > 0 ? "green" : "yellow";
                status.AppendLine(
                    $"<b>Tracking:</b> <color={trackingColor}>{trackingCount}/{maxObservers}</color>"
                );
            }

            // Activation states
            if (showActivationStates && activationController != null)
            {
                int totalTargets = activationController.allAreaTargets.Count;
                int enabledCount = activationController.ActiveTargetCount;
                int disabledCount = Mathf.Max(0, totalTargets - enabledCount);

                status.AppendLine(
                    $"<b>Targets:</b> {totalTargets} total | {enabledCount} enabled | {disabledCount} disabled"
                );
            }

            // === SECTION 2: ANCHOR DETAILS (Rich Metadata) ===
            StringBuilder anchorDetails = new StringBuilder();

            if (activationController != null)
            {
                var activeAnchor = activationController.GetCurrentAnchor();
                if (activeAnchor != null)
                {
                    var anchorInfo = activeAnchor.GetComponent<ARSafeTargetInfo>();

                    // Anchor header with star marker
                    anchorDetails.AppendLine(
                        $"<b>★ Current Anchor:</b> <color=#66CCFF>{activeAnchor.name}</color>"
                    );

                    if (anchorInfo != null)
                    {
                        // Type and priority
                        string anchorType = anchorInfo.targetType.ToString();
                        anchorDetails.AppendLine(
                            $"<b>Type:</b> {anchorType} | <b>Priority:</b> {anchorInfo.basePriority}"
                        );

                        // Distance and boundary status - check localization first
                        if (activationController != null && !activationController.HasLocalized)
                        {
                            // Check if anchor is set but tracking not yet confirmed
                            bool hasTracking =
                                trackingManager != null && trackingManager.IsTracking(activeAnchor);

                            if (hasTracking)
                            {
                                anchorDetails.AppendLine(
                                    $"<b>Status:</b> <color=cyan>⏳ WAITING FOR CONFIRMATION...</color>"
                                );
                                anchorDetails.AppendLine(
                                    $"<color=gray>Tracking active, confirming localization...</color>"
                                );
                            }
                            else
                            {
                                anchorDetails.AppendLine(
                                    $"<b>Status:</b> <color=yellow>⏳ SEARCHING FOR TARGET...</color>"
                                );
                                anchorDetails.AppendLine(
                                    $"<color=gray>Point camera at area target to begin tracking</color>"
                                );
                            }
                        }
                        else
                        {
                            float distance = anchorInfo.DistanceToCamera;
                            float boundaryDist = anchorInfo.DistanceToBoundary;
                            bool inside = boundaryDist < 0f;

                            // IMPROVED: Show both center distance and edge distance for better spatial awareness
                            anchorDetails.Append($"<b>Center:</b> {distance:F1}m | <b>Edge:</b> ");
                            if (inside)
                            {
                                // Show depth inside (how far from nearest edge)
                                float depthInside = Mathf.Abs(boundaryDist);
                                anchorDetails.AppendLine(
                                    $"<color=green>IN {depthInside:F1}m</color>"
                                );
                            }
                            else
                            {
                                anchorDetails.AppendLine(
                                    $"<color=gray>OUT {boundaryDist:F1}m</color>"
                                );
                            }
                        }

                        // Tracking and adjacency
                        bool isTracking =
                            trackingManager != null && trackingManager.IsTracking(activeAnchor);
                        string trackingStatus = isTracking ? "TRACKED" : "NOT_TRACKING";
                        int adjacentCount = anchorInfo.GetAllAdjacentTargets()?.Length ?? 0;
                        anchorDetails.AppendLine(
                            $"<b>Tracking:</b> {trackingStatus} | <b>Adjacent:</b> {adjacentCount}"
                        );

                        // Connected rooms (for hallways)
                        int connectedRooms = anchorInfo.connectedRooms?.Length ?? 0;
                        if (connectedRooms > 0)
                        {
                            anchorDetails.AppendLine(
                                $"<b>Connected Rooms:</b> {connectedRooms} <color=#FFD700>(+200 priority)</color>"
                            );
                        }
                    }
                }
                else
                {
                    anchorDetails.AppendLine(
                        "<b>Anchor:</b> <color=yellow>Searching for initial target...</color>"
                    );
                }
            }

            // Proximity info
            if (
                showProximityInfo
                && proximityDisplay != null
                && trackingManager != null
                && activationController != null
            )
            {
                // Get all tracked targets
                var trackedTargets =
                    new System.Collections.Generic.List<Vuforia.ObserverBehaviour>();
                foreach (var target in activationController.allAreaTargets)
                {
                    var observer = target.GetComponent<Vuforia.ObserverBehaviour>();
                    if (observer != null && trackingManager.IsTracking(observer))
                    {
                        trackedTargets.Add(observer);
                    }
                }

                if (trackedTargets.Count > 0)
                {
                    // Get proximity for closest tracked target
                    float minDist = float.MaxValue;
                    GameObject closestTarget = null;

                    foreach (var target in trackedTargets)
                    {
                        var info = target.GetComponent<ARSafeTargetInfo>();
                        if (info != null)
                        {
                            float dist = info.DistanceToCamera;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                closestTarget = target.gameObject;
                            }
                        }
                    }

                    if (closestTarget != null)
                    {
                        status.AppendLine($"<b>Closest:</b> {closestTarget.name} ({minDist:F2}m)");
                    }
                }
            }

            // Disaster filter
            if (
                showDisasterFilter
                && disasterFilter != null
                && DisasterTypeManager.Instance != null
            )
            {
                DisasterType currentType = DisasterTypeManager.SelectedDisasterType;
                if (currentType != DisasterType.None)
                {
                    status.AppendLine($"<b>Filter:</b> {currentType}");
                }
            }

            // === SECTION 3: TARGET LIST (TRULY FIXED LAYOUT) ===
            // Show fixed slots that don't change - only values update
            StringBuilder targetList = new StringBuilder();

            if (activationController != null)
            {
                var currentAnchor = activationController.GetCurrentAnchor();
                var allTargets = activationController.allAreaTargets;

                // Fixed header
                targetList.AppendLine("<b>═══ ACTIVE TARGETS ═══</b>");

                // Create fixed slots for consistent display
                var displaySlots = new System.Collections.Generic.List<Vuforia.ObserverBehaviour>();

                // SLOT 1: Always show current anchor (if exists)
                if (currentAnchor != null && !displaySlots.Contains(currentAnchor))
                {
                    displaySlots.Add(currentAnchor);
                }

                // SLOTS 2-N: Show enabled/tracking targets in FIXED order (by list index)
                foreach (var target in allTargets)
                {
                    if (displaySlots.Count >= maxTargetsToDisplay)
                        break;
                    if (displaySlots.Contains(target))
                        continue; // Skip if already added (anchor)

                    bool isTracking =
                        trackingManager != null
                        && target != null
                        && trackingManager.IsTracking(target);
                    bool isEnabled = target != null && activationController.IsTargetEnabled(target);

                    if (isTracking || isEnabled)
                    {
                        displaySlots.Add(target);
                    }
                }

                // Render the fixed slots
                for (int i = 0; i < maxTargetsToDisplay; i++)
                {
                    if (i < displaySlots.Count)
                    {
                        var target = displaySlots[i];
                        var info = target.GetComponent<ARSafeTargetInfo>();

                        bool isTracking =
                            trackingManager != null && trackingManager.IsTracking(target);
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

                            // Check if localized - show placeholder before first anchor
                            if (activationController != null && !activationController.HasLocalized)
                            {
                                distanceText = "<color=gray>Pre-Localization</color>";
                            }
                            else if (boundaryDist < 0f)
                            {
                                distanceText =
                                    $"<color=green>IN {Mathf.Abs(boundaryDist):F0}m</color> (C:{centerDist:F0}m)";
                            }
                            else if (boundaryDist >= float.MaxValue - 1f)
                            {
                                distanceText = "<color=gray>---</color>";
                            }
                            else
                            {
                                distanceText = $"{boundaryDist:F0}m (C:{centerDist:F0}m)";
                            }
                        }

                        // Fixed-width line format
                        targetList.AppendLine(
                            $"{slotNum} {anchorMarker}[{statusMarker}] {target.name, -25} {distanceText}"
                        );
                    }
                    else
                    {
                        // Empty slot (maintains layout stability)
                        targetList.AppendLine($"[{i + 1}] - <color=gray>---</color>");
                    }
                }

                // Fixed footer
                targetList.AppendLine("<b>═══════════════════</b>");

                // Summary (counts only, doesn't reorganize)
                int trackingCount = 0;
                int enabledCount = 0;
                foreach (var target in allTargets)
                {
                    bool isTracking =
                        trackingManager != null
                        && target != null
                        && trackingManager.IsTracking(target);
                    bool isEnabled = target != null && activationController.IsTargetEnabled(target);

                    if (isTracking)
                        trackingCount++;
                    else if (isEnabled)
                        enabledCount++;
                }

                targetList.AppendLine(
                    $"<color=gray>T:{trackingCount} E:{enabledCount} Total:{allTargets.Count}</color>"
                );
            }

            // Convert to strings
            string statusText = status.ToString().TrimEnd();
            string anchorDetailsText = anchorDetails.ToString().TrimEnd();
            string targetListText = targetList.ToString().TrimEnd();

            // Only update if changed (if optimization enabled)
            if (updateOnlyOnChange)
            {
                if (
                    statusText == lastStatusText
                    && anchorDetailsText == lastAnchorDetailsText
                    && targetListText == lastTargetListText
                )
                {
                    return; // No change, skip update
                }
            }

            // Update overlay with NEW 3-parameter API
            DebugOverlay.Instance.UpdateDisplay(statusText, anchorDetailsText, targetListText);

            // Cache for next comparison
            lastStatusText = statusText;
            lastAnchorDetailsText = anchorDetailsText;
            lastTargetListText = targetListText;

            // Update distance cache for flicker prevention
            UpdateDistanceCache();

            // Update state cache
            if (activationController != null)
            {
                lastAnchor = activationController.GetCurrentAnchor();
                lastEnabledCount = activationController.ActiveTargetCount;
            }
            if (trackingManager != null)
            {
                lastTrackingCount = trackingManager.GetTrackingCount();
            }
        }

        /// <summary>
        /// Update the distance cache for significant change detection
        /// </summary>
        private void UpdateDistanceCache()
        {
            if (activationController == null)
                return;

            lastDistances.Clear();

            var allTargets = activationController.allAreaTargets;
            foreach (var target in allTargets)
            {
                if (target == null)
                    continue;

                var info = target.GetComponent<ARSafeTargetInfo>();
                if (info != null)
                {
                    lastDistances[target.gameObject] = info.DistanceToBoundary;
                }
            }
        }

        /// <summary>
        /// Public API: Force immediate update
        /// </summary>
        public void ForceUpdate()
        {
            UpdateDebugOverlay();
        }

        /// <summary>
        /// Public API: Enable/disable integration
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
        }
    }
}
