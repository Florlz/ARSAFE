using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace ARSafe.Modular
{
    [AddComponentMenu("ARSafe/Modular/ARSafe Disaster Filter")]
    public class ARSafeDisasterFilter : MonoBehaviour
    {
        [Header("Filtering Settings")]
        [Tooltip("If true, GeneralSafety content is always visible regardless of selection")]
        public bool alwaysShowGeneralSafety = true;
        
        [Tooltip("If true, auto-collect ARSafeDisasterContent components in children on Start")]
        public bool autoCollectContent = true;
        
        [Tooltip("If true, auto-detect disaster types from GameObject names and add components")]
        public bool autoDetectFromNames = true;
        
        [Tooltip("Content items to filter (auto-collected if autoCollectContent is true)")]
        public List<ARSafeDisasterContent> contentItems = new List<ARSafeDisasterContent>();

    [Header("Anchor Restrictions")]
    [Tooltip("If true, disaster-specific content only shows when this Area Target is the current anchor. General safety content remains visible for neighbors.")]
    public bool restrictScenarioContentToCurrentAnchor = true;
        
        [Header("Debug")]
        public bool enableDebugLogs = false;
        
        private DisasterType currentDisasterType = DisasterType.GeneralSafety;
    private ARSafeActivationController activationController;
    private ObserverBehaviour observerBehaviour;
    private bool lastIsCurrentAnchor;
    private bool lastIsNeighbor;
    private bool earthquakeScenarioComplete;
    private bool floodScenarioActive;
    
    // CRITICAL: Reference to reparented augmentations (set by ARSafeActivationController)
    private Transform augmentationsRoot;
        
        void Start()
        {
            if (activationController == null)
            {
                activationController = FindFirstObjectByType<ARSafeActivationController>();
            }

            if (observerBehaviour == null)
            {
                observerBehaviour = GetComponentInParent<ObserverBehaviour>();
            }

            if (autoCollectContent)
            {
                AutoCollectContent();
            }
            
            if (DisasterTypeManager.Instance != null)
            {
                DisasterTypeManager.OnDisasterTypeChanged += OnDisasterTypeChanged;
                currentDisasterType = DisasterTypeManager.SelectedDisasterType;
            }
            else
            {
                Debug.LogWarning($"[ARSafeDisasterFilter] No DisasterTypeManager found on {name}");
            }

            EarthquakeScenarioManager.OnProgressUpdated += HandleEarthquakeProgress;
            HandleEarthquakeProgress(EarthquakeScenarioManager.CurrentProgress);

            FloodScenarioManager.OnProgressUpdated += HandleFloodProgress;
            HandleFloodProgress(FloodScenarioManager.CurrentProgress);

            UpdateContentVisibility();
            UpdateAnchorRelationshipCache();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeDisasterFilter] Initialized on {name}. Managing {contentItems.Count} items. Current: {currentDisasterType}");
            }
        }
        
        void OnDestroy()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= OnDisasterTypeChanged;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleEarthquakeProgress;
            FloodScenarioManager.OnProgressUpdated -= HandleFloodProgress;
        }

        void Update()
        {
            if (!restrictScenarioContentToCurrentAnchor || activationController == null || observerBehaviour == null)
            {
                return;
            }

            bool isCurrentAnchor;
            bool isNeighbor;
            EvaluateAnchorRelationship(out isCurrentAnchor, out isNeighbor);

            if (isCurrentAnchor != lastIsCurrentAnchor || isNeighbor != lastIsNeighbor)
            {
                lastIsCurrentAnchor = isCurrentAnchor;
                lastIsNeighbor = isNeighbor;
                UpdateContentVisibility();
            }
        }
        
        private void OnDisasterTypeChanged(DisasterType newType)
        {
            currentDisasterType = newType;
            UpdateContentVisibility();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeDisasterFilter] Type changed to {newType} on {name}");
            }
        }
        
        private void UpdateContentVisibility()
        {
            bool isCurrentAnchor;
            bool isNeighbor;
            EvaluateAnchorRelationship(out isCurrentAnchor, out isNeighbor);
            lastIsCurrentAnchor = isCurrentAnchor;
            lastIsNeighbor = isNeighbor;

            int visibleCount = 0;
            int hiddenCount = 0;

            foreach (var item in contentItems)
            {
                if (item == null) continue;

                bool shouldBeVisible = ShouldContentBeVisible(item, isCurrentAnchor, isNeighbor);
                item.gameObject.SetActive(shouldBeVisible);

                if (shouldBeVisible)
                {
                    visibleCount++;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=green>[ARSafeDisasterFilter] VISIBLE: {item.name} (type={item.disasterType}, current={currentDisasterType})</color>");
                    }
                }
                else
                {
                    hiddenCount++;
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeDisasterFilter] UpdateContentVisibility on {name}: {visibleCount} visible, {hiddenCount} hidden (currentType={currentDisasterType}, isAnchor={isCurrentAnchor}, isNeighbor={isNeighbor})</color>");
            }
        }

        private bool ShouldContentBeVisible(ARSafeDisasterContent item, bool isCurrentAnchor, bool isNeighbor)
        {
            if (item == null)
            {
                return false;
            }

            bool isGeneralSafety = item.disasterType == DisasterType.GeneralSafety;

            // Hide general safety arrows during earthquake until shaking completes
            if (isGeneralSafety && currentDisasterType == DisasterType.Earthquake && !earthquakeScenarioComplete)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[ARSafeDisasterFilter] HIDDEN: {item.name} - General safety hidden during active earthquake</color>");
                }
                return false;
            }

            // Hide general safety arrows during flood (flood-specific arrows shown by FloodWaterController)
            if (isGeneralSafety && currentDisasterType == DisasterType.Flood && floodScenarioActive)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[ARSafeDisasterFilter] HIDDEN: {item.name} - General safety hidden during active flood</color>");
                }
                return false;
            }

            if (restrictScenarioContentToCurrentAnchor)
            {
                if (isNeighbor)
                {
                    // CRITICAL: Floor-based visibility filter for neighbors
                    // Hide neighbor augmentations from different floors
                    // This ensures 2nd floor content doesn't show when on 1st floor (and vice versa)
                    var currentAnchor = activationController.GetCurrentAnchor();
                    if (currentAnchor != null)
                    {
                        var currentAnchorInfo = currentAnchor.GetComponent<ARSafeTargetInfo>();
                        var thisTargetInfo = observerBehaviour.GetComponent<ARSafeTargetInfo>();

                        if (currentAnchorInfo != null && thisTargetInfo != null)
                        {
                            // Hide neighbors from different floors
                            if (currentAnchorInfo.floorLevel != thisTargetInfo.floorLevel)
                            {
                                if (enableDebugLogs)
                                {
                                    Debug.Log($"<color=yellow>[ARSafeDisasterFilter] HIDDEN: {item.name} - Neighbor on different floor (anchor floor={currentAnchorInfo.floorLevel}, this floor={thisTargetInfo.floorLevel})</color>");
                                }
                                return false;
                            }
                        }
                    }

                    bool result = isGeneralSafety;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[ARSafeDisasterFilter] {(result ? "VISIBLE" : "HIDDEN")}: {item.name} - Neighbor (same floor), showing only general safety (isGeneralSafety={isGeneralSafety})</color>");
                    }
                    return result;
                }

                if (!isCurrentAnchor)
                {
                    bool result = isGeneralSafety && alwaysShowGeneralSafety;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=red>[ARSafeDisasterFilter] {(result ? "VISIBLE" : "HIDDEN")}: {item.name} - NOT current anchor, only showing general safety if enabled (isGeneralSafety={isGeneralSafety}, alwaysShowGeneralSafety={alwaysShowGeneralSafety})</color>");
                    }
                    return result;
                }
            }

            if (isGeneralSafety)
            {
                bool result = alwaysShowGeneralSafety || currentDisasterType == DisasterType.GeneralSafety;
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[ARSafeDisasterFilter] {(result ? "VISIBLE" : "HIDDEN")}: {item.name} - General safety content (alwaysShow={alwaysShowGeneralSafety}, currentType={currentDisasterType})</color>");
                }
                return result;
            }

            bool matchesType = item.disasterType == currentDisasterType;
            if (enableDebugLogs)
            {
                Debug.Log($"<color={(matchesType ? "green" : "red")}>[ARSafeDisasterFilter] {(matchesType ? "VISIBLE" : "HIDDEN")}: {item.name} - Type match check (item={item.disasterType}, current={currentDisasterType})</color>");
            }
            return matchesType;
        }

        private void HandleEarthquakeProgress(EarthquakeScenarioProgress progress)
        {
            bool wasComplete = earthquakeScenarioComplete;
            earthquakeScenarioComplete = progress.IsComplete;

            if (wasComplete != earthquakeScenarioComplete && currentDisasterType == DisasterType.Earthquake)
            {
                UpdateContentVisibility();

                if (enableDebugLogs)
                {
                    Debug.Log($"[ARSafeDisasterFilter] Earthquake completion state changed to {earthquakeScenarioComplete}. General safety arrows now {(earthquakeScenarioComplete ? "visible" : "hidden")}.");
                }
            }
        }

        private void HandleFloodProgress(FloodScenarioProgress progress)
        {
            bool wasActive = floodScenarioActive;
            floodScenarioActive = progress.IsActive;

            if (wasActive != floodScenarioActive && currentDisasterType == DisasterType.Flood)
            {
                UpdateContentVisibility();

                if (enableDebugLogs)
                {
                    Debug.Log($"[ARSafeDisasterFilter] Flood active state changed to {floodScenarioActive}. General safety arrows now {(floodScenarioActive ? "hidden" : "visible")}.");
                }
            }
        }
        
        /// <summary>
        /// Register the owning ObserverBehaviour so this filter continues to work after reparenting.
        /// </summary>
        public void RegisterOwningObserver(ObserverBehaviour owner)
        {
            if (owner == null)
            {
                return;
            }

            observerBehaviour = owner;

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeDisasterFilter] Registered owning observer: {owner.name}</color>");
            }
        }

        /// <summary>
        /// Set the augmentations root (called by ARSafeActivationController after reparenting)
        /// The optional owner parameter ensures the observer reference survives reparenting.
        /// </summary>
        public void SetAugmentationsRoot(Transform root, ObserverBehaviour owner = null)
        {
            augmentationsRoot = root;

            if (owner != null)
            {
                observerBehaviour = owner;
            }

            if (observerBehaviour == null)
            {
                observerBehaviour = GetComponentInParent<ObserverBehaviour>();
            }

            if (enableDebugLogs)
            {
                string rootName = root != null ? root.name : "null (original parent)";
                Debug.Log($"<color=cyan>[ARSafeDisasterFilter] Augmentations root set to: {rootName}</color>");
            }

            // Re-collect content from the new location
            if (autoCollectContent)
            {
                AutoCollectContent();
            }

            // CRITICAL: Force visibility refresh after content is re-collected
            // This ensures arrows show up after reparenting
            UpdateAnchorRelationshipCache();
            UpdateContentVisibility();

            // ADDITIONAL FIX: Schedule another refresh after a short delay
            // In case the anchor relationship wasn't established yet
            // Use Invoke instead of StartCoroutine to avoid inactive GameObject issues
            if (enabled && gameObject.activeInHierarchy)
            {
                Invoke(nameof(DelayedVisibilityRefresh), 0.5f);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeDisasterFilter] Forced visibility refresh after reparenting - {contentItems.Count} items managed</color>");
            }
        }

        private void DelayedVisibilityRefresh()
        {
            UpdateAnchorRelationshipCache();
            UpdateContentVisibility();

            if (enableDebugLogs)
            {
                Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Delayed visibility refresh completed on {name}</color>");
            }
        }
        
        private void AutoCollectContent()
        {
            contentItems.Clear();

            if (autoDetectFromNames)
            {
                AutoDetectAndTagContent();
            }

            // CRITICAL: Also auto-tag earthquake debris/crack controllers lacking ARSafeDisasterContent
            // Note: We do NOT set ignoreAnchorRestrictions here; earthquake visuals remain anchor-scoped
            AutoTagEarthquakeDebris();

            // CRITICAL: Search from THIS GameObject's hierarchy
            // The ARSafeDisasterFilter is attached inside the Augmentations hierarchy,
            // so searching from this.transform will find all disaster content in this specific Area Target,
            // regardless of whether the Augmentations container has been reparented to the shared root or not
            contentItems.AddRange(GetComponentsInChildren<ARSafeDisasterContent>(true));

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeDisasterFilter] Auto-collected {contentItems.Count} items on {name} (searching from this.transform)");
            }
        }

        private void AutoTagEarthquakeDebris()
        {
            int tagged = 0;

            // Find all EarthquakeDebrisController components in children
            var debrisControllers = GetComponentsInChildren<ARSafe.Content.EarthquakeDebrisController>(true);
            foreach (var debris in debrisControllers)
            {
                if (debris == null) continue;

                // Check if it already has ARSafeDisasterContent
                var existingContent = debris.GetComponent<ARSafeDisasterContent>();
                if (existingContent == null)
                {
                    // Add ARSafeDisasterContent and set to Earthquake (anchor-scoped)
                    var content = debris.gameObject.AddComponent<ARSafeDisasterContent>();
                    content.disasterType = DisasterType.Earthquake;
                    content.contentDescription = $"Auto-tagged earthquake debris: {debris.name}";
                    tagged++;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Auto-tagged EarthquakeDebrisController as Earthquake content: {debris.name}</color>");
                    }
                }
                else
                {
                    // Ensure debris is tagged as Earthquake
                    if (existingContent.disasterType != DisasterType.Earthquake)
                    {
                        existingContent.disasterType = DisasterType.Earthquake;

                        if (enableDebugLogs)
                        {
                            Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Updated debris content type to Earthquake: {debris.name}</color>");
                        }
                    }
                }
            }

            // [DEPRECATED] Find all EarthquakeCrackProjectorController components
            // DecalProjectors are deprecated - use EarthquakeCrackQuadController instead
            // Commenting out to avoid auto-tagging deprecated components
            /*
            var crackControllers = GetComponentsInChildren<EarthquakeCrackProjectorController>(true);
            foreach (var crack in crackControllers)
            {
                if (crack == null) continue;

                var existingContent = crack.GetComponent<ARSafeDisasterContent>();
                if (existingContent == null)
                {
                    var content = crack.gameObject.AddComponent<ARSafeDisasterContent>();
                    content.disasterType = DisasterType.Earthquake;
                    content.contentDescription = $"Auto-tagged earthquake crack: {crack.name}";
                    tagged++;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Auto-tagged EarthquakeCrackProjectorController as Earthquake content: {crack.name}</color>");
                    }
                }
                else
                {
                    // Ensure cracks are tagged as Earthquake (anchor-scoped)
                    bool changed = false;
                    if (existingContent.disasterType != DisasterType.Earthquake)
                    {
                        existingContent.disasterType = DisasterType.Earthquake;
                        changed = true;
                    }

                    if (enableDebugLogs && changed)
                    {
                        Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Updated crack content flags to Earthquake: {crack.name}</color>");
                    }
                }
            }
            */

            // Find all EarthquakeCrackQuadController components (3D Quad + Material - AR compatible)
            var quadCracks = GetComponentsInChildren<EarthquakeCrackQuadController>(true);
            foreach (var crack in quadCracks)
            {
                if (crack == null) continue;

                var existingContent = crack.GetComponent<ARSafeDisasterContent>();
                if (existingContent == null)
                {
                    var content = crack.gameObject.AddComponent<ARSafeDisasterContent>();
                    content.disasterType = DisasterType.Earthquake;
                    content.contentDescription = $"Auto-tagged earthquake quad crack: {crack.name}";
                    tagged++;

                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Auto-tagged EarthquakeCrackQuadController as Earthquake content: {crack.name}</color>");
                    }
                }
                else
                {
                    // Ensure quad cracks are tagged as Earthquake
                    bool changed = false;
                    if (existingContent.disasterType != DisasterType.Earthquake)
                    {
                        existingContent.disasterType = DisasterType.Earthquake;
                        changed = true;
                    }

                    if (enableDebugLogs && changed)
                    {
                        Debug.Log($"<color=yellow>[ARSafeDisasterFilter] Updated quad crack content type to Earthquake: {crack.name}</color>");
                    }
                }
            }

            if (tagged > 0)
            {
                Debug.Log($"<color=green>[ARSafeDisasterFilter] ✓ Auto-tagged {tagged} earthquake component(s) on {name}</color>");
            }
        }
        
        private void AutoDetectAndTagContent()
        {
            int detected = 0;
            int updated = 0;

            // Search from this GameObject's direct children
            // Since this component is inside the Augmentations hierarchy, this will find the right content
            foreach (Transform child in transform)
            {
                string childName = child.name.ToLower();
                DisasterType? detectedType = null;

                if (childName.Contains("fire"))
                {
                    detectedType = DisasterType.Fire;
                }
                else if (childName.Contains("earthquake") || childName.Contains("quake"))
                {
                    detectedType = DisasterType.Earthquake;
                }
                else if (childName.Contains("flood"))
                {
                    detectedType = DisasterType.Flood;
                }
                else if (childName.Contains("general") || childName.Contains("safety"))
                {
                    detectedType = DisasterType.GeneralSafety;
                }

                if (detectedType.HasValue)
                {
                    var existingComponent = child.GetComponent<ARSafeDisasterContent>();

                    if (existingComponent == null)
                    {
                        var newComponent = child.gameObject.AddComponent<ARSafeDisasterContent>();
                        newComponent.disasterType = detectedType.Value;
                        newComponent.contentDescription = child.name;
                        detected++;

                        if (enableDebugLogs)
                        {
                            Debug.Log($"[ARSafeDisasterFilter] Auto-tagged ''{child.name}'' as {detectedType.Value}");
                        }
                    }
                    else if (existingComponent.disasterType != detectedType.Value)
                    {
                        existingComponent.disasterType = detectedType.Value;
                        updated++;

                        if (enableDebugLogs)
                        {
                            Debug.Log($"[ARSafeDisasterFilter] Updated ''{child.name}'' to {detectedType.Value}");
                        }
                    }
                }
            }

            if (enableDebugLogs && (detected > 0 || updated > 0))
            {
                Debug.Log($"[ARSafeDisasterFilter] Auto-detection complete on {name}: {detected} new, {updated} updated");
            }
        }
        
        public void RefreshVisibility()
        {
            UpdateContentVisibility();
        }
        
        [ContextMenu("Refresh Content List")]
        public void RefreshContentList()
        {
            AutoCollectContent();
            UpdateContentVisibility();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeDisasterFilter] Refreshed content list on {name}. Found {contentItems.Count} items.");
            }
        }
        
        public void AddContentItem(ARSafeDisasterContent item)
        {
            if (item != null && !contentItems.Contains(item))
            {
                contentItems.Add(item);
                UpdateContentVisibility();
            }
        }
        
        public void RemoveContentItem(ARSafeDisasterContent item)
        {
            if (item != null)
            {
                contentItems.Remove(item);
            }
        }

        private void EvaluateAnchorRelationship(out bool isCurrentAnchor, out bool isNeighbor)
        {
            isCurrentAnchor = false;
            isNeighbor = false;

            if (activationController == null || observerBehaviour == null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=red>[ARSafeDisasterFilter] EvaluateAnchorRelationship on {name}: activationController={activationController != null}, observerBehaviour={observerBehaviour != null}</color>");
                }
                return;
            }

            var currentAnchor = activationController.GetCurrentAnchor();
            if (currentAnchor == null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[ARSafeDisasterFilter] EvaluateAnchorRelationship on {name}: currentAnchor is NULL! (this observer={observerBehaviour.name})</color>");
                }
                return;
            }

            if (currentAnchor == observerBehaviour)
            {
                isCurrentAnchor = true;
                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[ARSafeDisasterFilter] ✓ {name} IS the current anchor ({observerBehaviour.name})</color>");
                }
                return;
            }

            isNeighbor = activationController.IsNeighborOfCurrentAnchor(observerBehaviour);
            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafeDisasterFilter] {name} is NOT current anchor. IsNeighbor={isNeighbor} (this={observerBehaviour.name}, current={currentAnchor.name})</color>");
            }
        }

        private void UpdateAnchorRelationshipCache()
        {
            EvaluateAnchorRelationship(out lastIsCurrentAnchor, out lastIsNeighbor);
        }
    }
}
