using UnityEngine;
using Vuforia;
using System.Collections;
using System.Collections.Generic;

namespace ARSafe.Modular
{
    /// <summary>
    /// Manages content visibility and fade effects based on tracking state and proximity.
    /// Handles particle systems, renderers, and UI elements attached to Area Targets.
    /// 
    /// KEY RESPONSIBILITIES:
    /// - Show/hide content based on tracking and distance
    /// - Smooth fade transitions for renderers
    /// - Particle system control (play when visible, stop when hidden)
    /// - Pose validation (optional minimum tracking time before showing)
    /// 
    /// DOES NOT HANDLE:
    /// - Target activation (see ARSafeActivationController)
    /// - Disaster type filtering (see ARSafeDisasterFilter)
    /// - Tracking management (see ARSafeTrackingManager)
    /// </summary>
    [RequireComponent(typeof(ObserverBehaviour))]
    public class ARSafeProximityDisplay : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Observer behaviour (auto-assigned)")]
        private ObserverBehaviour observerBehaviour;
        
        [Tooltip("Target info component (optional, auto-found)")]
        private ARSafeTargetInfo targetInfo;
        
        [Tooltip("Tracking manager (auto-found)")]
        private ARSafeTrackingManager trackingManager;

    [Tooltip("Activation controller (auto-found)")]
    private ARSafeActivationController activationController;
        
        [Tooltip("AR Camera (auto-found)")]
        private Camera arCamera;
        
        [Header("Visibility Settings")]
        [Tooltip("Show content only when tracked (not just enabled)")]
        public bool requireTracking = true;
        
        [Tooltip("Minimum distance from camera to show content")]
        [Range(0f, 5f)]
        public float minVisibilityDistance = 0.5f;
        
        [Tooltip("Maximum distance from camera to show content (DECREASED so only nearby content shows)")]
        [Range(5f, 100f)]
        public float maxVisibilityDistance = 12f;
        
        [Header("Adjacent Content Settings")]
        [Tooltip("Show hallway content when adjacent hallway is tracking (navigation preview)")]
        public bool showAdjacentHallwayContent = true;
        
        [Tooltip("Rooms require tracking (must be inside) to show content")]
        public bool roomsRequireInside = true;
        
        [Header("Pose Validation")]
        [Tooltip("Require stable tracking for this long before showing content")]
        [Range(0f, 5f)]
        public float minimumTrackingTime = 0.5f;
        
        [Tooltip("If true, use targetInfo distances (if available) instead of these settings")]
        public bool useTargetInfoDistances = true;
        
        [Header("Fade Settings")]
        [Tooltip("Enable smooth fade transitions")]
        public bool enableFade = true;
        
        [Tooltip("Fade in duration")]
        [Range(0.1f, 2f)]
        public float fadeInDuration = 0.5f;
        
        [Tooltip("Fade out duration")]
        [Range(0.1f, 2f)]
        public float fadeOutDuration = 0.3f;
        
        [Header("Content References")]
        [Tooltip("Renderers to fade (auto-collected if empty)")]
        public List<Renderer> renderersToFade = new List<Renderer>();
        
        [Tooltip("Particle systems to play when visible (auto-collected if empty)")]
        public List<ParticleSystem> particleSystems = new List<ParticleSystem>();
        
        [Tooltip("GameObjects to show/hide directly (no fade)")]
        public List<GameObject> objectsToToggle = new List<GameObject>();
        
        [Header("Debug")]
        public bool enableDebugLogs = false;
        
        // Runtime state
        private bool isContentVisible = false;
        private Coroutine fadeCoroutine;
        private float trackingStartTime = -1f;
        private bool hasMinimumTrackingTime = false;
        private float currentAlpha = 1f;
        private float targetAlpha = 1f;
        
        // CRITICAL: Reference to reparented augmentations (set by ARSafeActivationController)
        private Transform augmentationsRoot;
        
        // CRITICAL: Reference to THIS Area Target's specific Augmentations container
        // (cached to survive reparenting to the shared root)
        private Transform myAugmentationsContainer;
        
        // Optimized material caching structure
        private struct MaterialCache
        {
            public Material[] originalMaterials;
            public Material[] fadeMaterials;
            public bool isSetup;
            
            public MaterialCache(Material[] original, Material[] fade)
            {
                originalMaterials = original;
                fadeMaterials = fade;
                isSetup = true;
            }
        }
        
        private Dictionary<Renderer, MaterialCache> materialCache = new Dictionary<Renderer, MaterialCache>();
        
        // Cached lookups for adjacent target checking (performance optimization)
        private Dictionary<string, ObserverBehaviour> observerLookup;
        private Dictionary<string, ARSafeTargetInfo> targetInfoLookup;
        
        // OPTIMIZATION: Throttle Update() to 5 FPS for phone performance (visibility checks don't need high frequency)
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.2f; // 5 FPS (reduced from 0.1s/10 FPS)
        
        void Awake()
        {
            observerBehaviour = GetComponent<ObserverBehaviour>();
            targetInfo = GetComponent<ARSafeTargetInfo>();
            arCamera = Camera.main;
            activationController = FindFirstObjectByType<ARSafeActivationController>();
            
            // CRITICAL: Cache reference to Augmentations container NOW (before reparenting)
            // This ensures we can always find our content even after it moves to the shared root
            myAugmentationsContainer = transform.Find("Augmentations");
            
            if (myAugmentationsContainer == null && enableDebugLogs)
            {
                Debug.LogWarning($"[ARSafeProximityDisplay] No 'Augmentations' child found on {name}! Content visibility will not work.");
            }
        }
        
        void Start()
        {
            if (activationController == null)
            {
                activationController = FindFirstObjectByType<ARSafeActivationController>();
            }

            // Find tracking manager
            trackingManager = FindFirstObjectByType<ARSafeTrackingManager>();
            if (trackingManager == null && activationController != null)
            {
                trackingManager = activationController.GetComponent<ARSafeTrackingManager>();
            }
            if (trackingManager == null)
            {
                Debug.LogError($"[ARSafeProximityDisplay] No ARSafeTrackingManager found in scene! {name} will not work correctly.");
            }
            
            // Auto-collect renderers if empty
            if (renderersToFade.Count == 0)
            {
                AutoCollectRenderers();
            }
            
            // Auto-collect particle systems if empty
            if (particleSystems.Count == 0)
            {
                AutoCollectParticleSystems();
            }
            
            // Setup fade materials
            if (enableFade)
            {
                SetupFadeMaterials();
            }
            
            // Subscribe to Vuforia events
            if (observerBehaviour != null)
            {
                observerBehaviour.OnTargetStatusChanged += OnTargetStatusChanged;
            }
            
            // NEW: Build observer lookup tables for adjacent content checking
            BuildObserverLookupTables();
            
            // Initially hide content
            SetContentVisibility(false, immediate: true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] Initialized on {name}. Renderers: {renderersToFade.Count}, Particles: {particleSystems.Count}");
            }
        }
        
        void OnDestroy()
        {
            // Unsubscribe from Vuforia events
            if (observerBehaviour != null)
            {
                observerBehaviour.OnTargetStatusChanged -= OnTargetStatusChanged;
            }
            
            // CRITICAL: Destroy all instantiated fade materials to prevent memory leaks
            CleanupFadeMaterials();
            
            // Clear lookup dictionaries
            if (observerLookup != null)
            {
                observerLookup.Clear();
                observerLookup = null;
            }
            if (targetInfoLookup != null)
            {
                targetInfoLookup.Clear();
                targetInfoLookup = null;
            }
            
            // Stop any running coroutines
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }
        
        /// <summary>
        /// CRITICAL: Cleanup all instantiated fade materials to prevent memory leaks
        /// Called from OnDestroy() to ensure proper cleanup
        /// </summary>
        private void CleanupFadeMaterials()
        {
            if (materialCache == null || materialCache.Count == 0)
                return;
            
            foreach (var kvp in materialCache)
            {
                if (kvp.Value.fadeMaterials != null)
                {
                    foreach (var mat in kvp.Value.fadeMaterials)
                    {
                        if (mat != null)
                        {
                            Destroy(mat); // Destroy instantiated material
                        }
                    }
                }
            }
            
            materialCache.Clear();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] Cleaned up fade materials on {name}");
            }
        }
        
        void Update()
        {
            // CRITICAL: Null checks to prevent errors after cleanup or missing references
            if (arCamera == null || trackingManager == null || observerBehaviour == null)
                return;
            
            // OPTIMIZATION: Throttle updates to 10 FPS (visibility checks don't need 60 FPS)
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL)
                return;
            
            lastUpdateTime = Time.time;
            
            // Check visibility conditions every interval
            SyncTrackingTimerWithInfo();
            UpdateVisibility();
        }
        
        /// <summary>
        /// Main visibility update logic
        /// </summary>
        private void UpdateVisibility()
        {
            if (arCamera == null || trackingManager == null) return;
            
            bool shouldBeVisible = ShouldContentBeVisible();
            
            if (shouldBeVisible != isContentVisible)
            {
                SetContentVisibility(shouldBeVisible);
            }
        }
        
        /// <summary>
        /// Determine if content should be visible based on tracking, distance, pose validation
        /// </summary>
        private bool ShouldContentBeVisible()
        {
            // CRITICAL: Do NOT show any content until system has confirmed localization
            // This prevents augmentations from appearing before Vuforia tracking is established
            if (activationController != null && !activationController.HasLocalized)
            {
                if (enableDebugLogs && isContentVisible)
                {
                    Debug.Log($"<color=yellow>[ARSafeProximityDisplay] {name} hiding: System not yet localized (waiting for tracking confirmation)</color>");
                }
                return false;
            }

            // CRITICAL: Only show content for the current anchor (or its neighbors)
            // This prevents old content from staying visible when switching anchors
            if (activationController != null && activationController.CurrentAnchor != null)
            {
                bool isCurrentAnchor = (activationController.CurrentAnchor == observerBehaviour);
                bool isNeighborOfCurrent = false;
                
                if (!isCurrentAnchor && targetInfo != null)
                {
                    // Check if this target is a neighbor of the current anchor using cached adjacency data
                    ARSafeTargetInfo currentAnchorInfo = activationController.CurrentAnchor.GetComponent<ARSafeTargetInfo>();
                    if (currentAnchorInfo != null)
                    {
                        var adjacentInfos = currentAnchorInfo.GetAllAdjacentTargets();
                        if (adjacentInfos != null)
                        {
                            foreach (var adjacentInfo in adjacentInfos)
                            {
                                if (adjacentInfo == targetInfo)
                                {
                                    isNeighborOfCurrent = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // If not current anchor or neighbor, hide content immediately
                if (!isCurrentAnchor && !isNeighborOfCurrent)
                {
                    if (enableDebugLogs && isContentVisible)
                    {
                        Debug.Log($"<color=yellow>[ARSafeProximityDisplay] {name} hiding: Not current anchor or neighbor (current anchor: {activationController.CurrentAnchor.name})</color>");
                    }
                    return false;
                }

                // CRITICAL: Floor-based visibility filter for neighbors
                // STRICT RULE: Always hide augmentations from different floors
                // No exceptions - even stairways must respect floor boundaries
                if (!isCurrentAnchor && isNeighborOfCurrent)
                {
                    var currentAnchorInfo = activationController.CurrentAnchor?.GetComponent<ARSafeTargetInfo>();

                    if (currentAnchorInfo != null && targetInfo != null)
                    {
                        // Hide neighbor augmentations from different floors
                        // Use strict floor level comparison (not IsOnSameFloor method)
                        if (currentAnchorInfo.floorLevel != targetInfo.floorLevel)
                        {
                            if (enableDebugLogs && isContentVisible)
                            {
                                Debug.Log($"<color=yellow>[ARSafeProximityDisplay] {name} hiding: Different floor (current={currentAnchorInfo.floorLevel}, target={targetInfo.floorLevel})</color>");
                            }
                            return false;
                        }
                    }
                }
            }

            // NEW: SPECIAL RULE FOR ROOMS - Must be tracking (inside room) to show content
            if (activationController != null && activationController.hideContentWhenNotTracking && (trackingManager == null || trackingManager.GetTrackingCount() == 0))
            {
                if (enableDebugLogs && isContentVisible)
                {
                    Debug.Log($"[ARSafeProximityDisplay] {name} hiding: No targets tracking (global setting)");
                }
                return false;
            }

            if (roomsRequireInside && targetInfo != null && (targetInfo.targetType == TargetType.Room || targetInfo.targetType == TargetType.Exit))
            {
                // CRITICAL: If current anchor is a HALLWAY, NEVER show room content
                // Room content should only show when the room itself is the current anchor
                if (activationController != null && activationController.CurrentAnchor != null)
                {
                    bool isCurrentAnchor = (activationController.CurrentAnchor == observerBehaviour);
                    
                    if (!isCurrentAnchor)
                    {
                        // This room is NOT the current anchor - check if current anchor is a hallway
                        ARSafeTargetInfo currentAnchorInfo = activationController.CurrentAnchor.GetComponent<ARSafeTargetInfo>();
                        if (currentAnchorInfo != null && currentAnchorInfo.targetType == TargetType.Hallway)
                        {
                            // Current anchor is a hallway, this is a room (neighbor) → HIDE room content
                            if (enableDebugLogs && isContentVisible)
                            {
                                Debug.Log($"[ARSafeProximityDisplay] {name} hiding: Current anchor is hallway ({currentAnchorInfo.name}), room content not shown from hallways");
                            }
                            return false;
                        }
                    }
                }
                
                bool isTracking = trackingManager.IsTracking(observerBehaviour);
                
                // CRITICAL: Compute boundary distance in REAL-TIME (don't use cached value)
                // Cached value (targetInfo.DistanceToBoundary) is only updated for enabled targets
                // Room might not be enabled when in hallway, so we need fresh calculation
                float boundaryDistance = targetInfo.ComputeDistanceToBoundary(arCamera.transform.position);
                bool isInsideBoundary = boundaryDistance < 0f;
                
                // Room content requires BOTH tracking AND being inside boundary
                if (!isTracking)
                {
                    if (enableDebugLogs && isContentVisible)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} hiding: Room requires tracking (must be inside)");
                    }
                    return false;
                }
                
                // NEW: Check BoxCollider boundary - user must be INSIDE
                if (!isInsideBoundary)
                {
                    if (enableDebugLogs && isContentVisible)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} hiding: User outside room boundary (distance={boundaryDistance:F2}m)");
                    }
                    return false;
                }
                
                // Check pose validation for rooms
                if (!hasMinimumTrackingTime)
                {
                    if (enableDebugLogs && isContentVisible)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} hiding: Room pose not validated yet");
                    }
                    return false;
                }
                
                // Room is tracking, inside boundary, and validated - show content
                if (enableDebugLogs && !isContentVisible)
                {
                    Debug.Log($"[ARSafeProximityDisplay] {name} showing: Inside room boundary (tracking, distance={boundaryDistance:F2}m)");
                }
                return true;
            }
            
            // HALLWAYS: Check tracking requirement (with adjacent hallway exception)
            if (requireTracking && !trackingManager.IsTracking(observerBehaviour))
            {
                // NEW: Exception for hallways - show content if adjacent hallway is tracking
                if (showAdjacentHallwayContent && targetInfo != null && targetInfo.targetType == TargetType.Hallway)
                {
                    if (IsAnyAdjacentHallwayTracking())
                    {
                        if (enableDebugLogs && !isContentVisible)
                        {
                            Debug.Log($"[ARSafeProximityDisplay] {name} showing: Adjacent hallway is tracking (navigation preview)");
                        }
                        return true; // Show content even without own tracking
                    }
                }
                
                // Not tracking and no adjacent tracking
                if (enableDebugLogs && isContentVisible)
                {
                    Debug.Log($"[ARSafeProximityDisplay] {name} hiding: Not tracking");
                }
                return false;
            }
            
            // Check pose validation (minimum tracking time)
            if (requireTracking && !hasMinimumTrackingTime)
            {
                if (enableDebugLogs && isContentVisible)
                {
                    Debug.Log($"[ARSafeProximityDisplay] {name} hiding: Pose validation not passed yet");
                }
                return false;
            }
            
            // Check distance
            float distance = Vector3.Distance(arCamera.transform.position, transform.position);
            
            float minDist = minVisibilityDistance;
            float maxDist = maxVisibilityDistance;
            
            // Use targetInfo distances if available and enabled
            if (useTargetInfoDistances && targetInfo != null)
            {
                minDist = targetInfo.minVisibilityDistance;
                maxDist = targetInfo.maxVisibilityDistance;
            }
            
            if (distance < minDist || distance > maxDist)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ARSafeProximityDisplay] {name} visibility check: distance={distance:F1}m (min={minDist:F1}m, max={maxDist:F1}m) → HIDE");
                }
                return false;
            }
            
            if (enableDebugLogs && !isContentVisible)
            {
                Debug.Log($"[ARSafeProximityDisplay] {name} should be visible: tracking={trackingManager.IsTracking(observerBehaviour)}, distance={distance:F1}m");
            }
            
            return true;
        }
        
        /// <summary>
        /// Vuforia callback: tracking status changed
        /// </summary>
        private void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
        {
            bool isTracking = trackingManager != null && trackingManager.IsTracking(observerBehaviour);
            
            if (isTracking)
            {
                // Start tracking timer
                if (trackingStartTime < 0)
                {
                    trackingStartTime = (targetInfo != null && targetInfo.LastTrackingTime > 0f) ? targetInfo.LastTrackingTime : Time.time;
                    hasMinimumTrackingTime = false;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} started tracking. Waiting {minimumTrackingTime}s for pose validation...");
                    }
                }
                
                // Check if minimum time passed
                if (!hasMinimumTrackingTime && Time.time - trackingStartTime >= minimumTrackingTime)
                {
                    hasMinimumTrackingTime = true;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} passed pose validation.");
                    }
                }
            }
            else
            {
                // Lost tracking - reset timer
                trackingStartTime = -1f;
                hasMinimumTrackingTime = false;
            }
        }

        private void SyncTrackingTimerWithInfo()
        {
            if (targetInfo == null || trackingManager == null || observerBehaviour == null)
            {
                return;
            }

            if (!trackingManager.IsTracking(observerBehaviour))
            {
                return;
            }

            if (targetInfo.LastTrackingTime > 0f && trackingStartTime < 0f)
            {
                trackingStartTime = targetInfo.LastTrackingTime;
            }

            if (!hasMinimumTrackingTime && targetInfo.LastTrackingTime > 0f)
            {
                if (Time.time - targetInfo.LastTrackingTime >= minimumTrackingTime)
                {
                    hasMinimumTrackingTime = true;
                }
            }
        }
        
        /// <summary>
        /// Show or hide content with optimized fade system
        /// </summary>
        private void SetContentVisibility(bool visible, bool immediate = false)
        {
            isContentVisible = visible;
            targetAlpha = visible ? 1f : 0f;

            if (immediate || !enableFade)
            {
                currentAlpha = targetAlpha;
                ApplyVisibilityImmediate(visible);
                return;
            }

            // Stop existing fade
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }

            fadeCoroutine = StartCoroutine(SmoothFadeCoroutine(visible));

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] {name} content {(visible ? "visible" : "hidden")}");
            }
        }

        /// <summary>
        /// Force hide all content immediately (used during relocalization).
        /// This ensures old augmentations are hidden before switching to a new location.
        /// </summary>
        public void ForceHide()
        {
            if (enableDebugLogs)
            {
                Debug.Log($"<color=yellow>[ARSafeProximityDisplay] {name} FORCE HIDE - Relocalization in progress</color>");
            }

            // Stop any ongoing fade
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }

            // Set visibility state to hidden
            isContentVisible = false;
            currentAlpha = 0f;
            targetAlpha = 0f;

            // Hide everything immediately
            ApplyVisibilityImmediate(false);
        }

        /// <summary>
        /// Force an immediate visibility refresh based on current tracking and proximity state.
        /// </summary>
        public void RefreshVisibilityImmediate()
        {
            if (trackingManager == null || observerBehaviour == null)
            {
                return;
            }

            bool shouldBeVisible = ShouldContentBeVisible();
            SetContentVisibility(shouldBeVisible, immediate: true);
        }

        /// <summary>
        /// Apply visibility immediately without fade
        /// </summary>
        private void ApplyVisibilityImmediate(bool visible)
        {
            SetRenderersAlpha(visible ? 1f : 0f);
            SetParticlesActive(visible);
            SetObjectsActive(visible);
        }
        
        /// <summary>
        /// Optimized fade coroutine with better state management
        /// </summary>
        private IEnumerator SmoothFadeCoroutine(bool fadeIn)
        {
            float duration = fadeIn ? fadeInDuration : fadeOutDuration;
            float startAlpha = currentAlpha;
            float elapsed = 0f;
            
            // Show objects immediately when fading in
            if (fadeIn)
            {
                SetObjectsActive(true);
                SetParticlesActive(true);
            }
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                
                if (enableFade && renderersToFade.Count > 0)
                {
                    SetRenderersAlpha(currentAlpha);
                }
                
                yield return null;
            }
            
            currentAlpha = targetAlpha;
            
            // Hide objects when fully faded out
            if (!fadeIn)
            {
                SetObjectsActive(false);
                SetParticlesActive(false);
            }
            
            fadeCoroutine = null;
        }
        
        /// <summary>
        /// Set alpha for all renderers
        /// FIX: Validate renderer exists before accessing cache
        /// </summary>
        private void SetRenderersAlpha(float alpha)
        {
            // Create list of invalid keys to remove after iteration
            var invalidKeys = new List<Renderer>();
            
            foreach (var kvp in materialCache)
            {
                var renderer = kvp.Key;
                var cache = kvp.Value;
                
                // FIX: Validate renderer still exists and is in renderersToFade list
                if (renderer == null || !renderersToFade.Contains(renderer))
                {
                    invalidKeys.Add(renderer);
                    continue;
                }
                
                if (!cache.isSetup) continue;
                
                foreach (var mat in cache.fadeMaterials)
                {
                    if (mat == null) continue;
                    
                    if (mat.HasProperty("_Color"))
                    {
                        Color color = mat.color;
                        color.a = alpha;
                        mat.color = color;
                    }
                    else if (mat.HasProperty("_BaseColor"))
                    {
                        Color baseColor = mat.GetColor("_BaseColor");
                        baseColor.a = alpha;
                        mat.SetColor("_BaseColor", baseColor);
                    }
                }
            }
            
            // Clean up invalid cache entries
            foreach (var key in invalidKeys)
            {
                materialCache.Remove(key);
            }
        }
        
        /// <summary>
        /// Play or stop particle systems
        /// </summary>
        private void SetParticlesActive(bool active)
        {
            foreach (var ps in particleSystems)
            {
                if (ps == null) continue;
                
                if (active)
                {
                    if (!ps.isPlaying)
                    {
                        ps.Play();
                    }
                }
                else
                {
                    if (ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }
        }
        
        /// <summary>
        /// Show or hide GameObjects
        /// </summary>
        private void SetObjectsActive(bool active)
        {
            foreach (var obj in objectsToToggle)
            {
                if (obj != null)
                {
                    obj.SetActive(active);
                }
            }
        }
        
        /// <summary>
        /// Set the augmentations root (called by ARSafeActivationController after reparenting)
        /// Pass null when augmentations are restored to their original parent
        /// </summary>
        public void SetAugmentationsRoot(Transform root)
        {
            augmentationsRoot = root;
            
            if (enableDebugLogs)
            {
                string rootName = root != null ? root.name : "null (original parent)";
                Debug.Log($"<color=cyan>[ARSafeProximityDisplay] Augmentations root set to: {rootName} - Re-collecting content...</color>");
            }
            
            // CRITICAL: ALWAYS re-collect content when called
            // The augmentations may have moved to a new parent (or back to original)
            AutoCollectRenderers();
            AutoCollectParticleSystems();
            
            // Re-setup fade materials with the newly collected renderers
            if (enableFade)
            {
                SetupFadeMaterials();
            }
            
            // CRITICAL: Hide all content initially after reparenting
            // The visibility system will show only anchor + neighbor content
            SetContentVisibility(false, immediate: true);
            
            if (enableDebugLogs)
            {
                Debug.Log($"<color=green>[ARSafeProximityDisplay] {name} re-collected {renderersToFade.Count} renderers, {particleSystems.Count} particles</color>");
            }
        }
        
        /// <summary>
        /// Auto-collect all renderers in children (supports reparented augmentations)
        /// EXCLUDES renderers that are part of disaster-specific content (managed by ARSafeDisasterFilter)
        /// </summary>
        private void AutoCollectRenderers()
        {
            renderersToFade.Clear();

            // CRITICAL: Determine where to search based on augmentations reparenting state
            Transform searchRoot = GetAugmentationsContainer();
            
            if (searchRoot == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ARSafeProximityDisplay] No Augmentations container found on {name}. Cannot collect renderers.");
                }
                return;
            }

            // Collect all renderers BUT exclude those inside disaster content GameObjects
            // (disaster content visibility is controlled by ARSafeDisasterFilter)
            var allRenderers = searchRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in allRenderers)
            {
                if (renderer == null) continue;
                
                // Check if this renderer is inside a disaster content object
                var disasterContent = renderer.GetComponentInParent<ARSafeDisasterContent>();
                if (disasterContent != null)
                {
                    // Skip this renderer - it's part of disaster-specific content
                    // ARSafeDisasterFilter will manage its visibility via SetActive()
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[ARSafeProximityDisplay] Skipping renderer on '{renderer.name}' - managed by ARSafeDisasterFilter ({disasterContent.disasterType})</color>");
                    }
                    continue;
                }
                
                // This is general content - add it to our management list
                renderersToFade.Add(renderer);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] Auto-collected {renderersToFade.Count} general renderers from {searchRoot.name} (excluded disaster-specific content)");
            }
        }
        
        /// <summary>
        /// Auto-collect all particle systems in children (supports reparented augmentations)
        /// EXCLUDES particles that are part of disaster-specific content (managed by ARSafeDisasterFilter)
        /// </summary>
        private void AutoCollectParticleSystems()
        {
            particleSystems.Clear();

            // CRITICAL: Determine where to search based on augmentations reparenting state
            Transform searchRoot = GetAugmentationsContainer();
            
            if (searchRoot == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"[ARSafeProximityDisplay] No Augmentations container found on {name}. Cannot collect particle systems.");
                }
                return;
            }

            // Collect all particle systems BUT exclude those inside disaster content GameObjects
            var allParticles = searchRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                if (ps == null) continue;
                
                // Check if this particle system is inside a disaster content object
                var disasterContent = ps.GetComponentInParent<ARSafeDisasterContent>();
                if (disasterContent != null)
                {
                    // Skip this particle - it's part of disaster-specific content
                    if (enableDebugLogs)
                    {
                        Debug.Log($"<color=cyan>[ARSafeProximityDisplay] Skipping particle on '{ps.name}' - managed by ARSafeDisasterFilter ({disasterContent.disasterType})</color>");
                    }
                    continue;
                }
                
                // This is general content - add it to our management list
                particleSystems.Add(ps);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] Auto-collected {particleSystems.Count} general particles (excluded disaster-specific content)");
            }
        }
        
        /// <summary>
        /// Find the Augmentations container for this Area Target (works whether reparented or not)
        /// </summary>
        private Transform GetAugmentationsContainer()
        {
            // Use the cached reference (survives reparenting because Transform references persist)
            return myAugmentationsContainer;
        }
        
        /// <summary>
        /// Setup fade materials (create instances to avoid shared material issues)
        /// </summary>
        private void SetupFadeMaterials()
        {
            materialCache.Clear();
            
            foreach (var renderer in renderersToFade)
            {
                if (renderer == null) continue;
                
                // Store original materials
                var originalMats = renderer.sharedMaterials;
                
                // Create material instances for fading
                var instanceMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < renderer.materials.Length; i++)
                {
                    instanceMaterials[i] = new Material(renderer.materials[i]);
                    
                    // Enable transparency if needed
                    if (instanceMaterials[i].HasProperty("_Mode"))
                    {
                        instanceMaterials[i].SetFloat("_Mode", 3); // Transparent mode
                        instanceMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        instanceMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        instanceMaterials[i].SetInt("_ZWrite", 0);
                        instanceMaterials[i].DisableKeyword("_ALPHATEST_ON");
                        instanceMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                        instanceMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        instanceMaterials[i].renderQueue = 3000;
                    }
                }
                
                // Cache both original and fade materials in single structure
                materialCache[renderer] = new MaterialCache(originalMats, instanceMaterials);
                renderer.materials = instanceMaterials;
            }
        }
        
        /// <summary>
        /// Public API: Force show content
        /// </summary>
        public void ForceShow()
        {
            SetContentVisibility(true, immediate: true);
        }

        /// <summary>
        /// Public API: Is content currently visible?
        /// </summary>
        public bool IsContentVisible()
        {
            return isContentVisible;
        }
        
        // ==================== NEW: ADJACENT HALLWAY CONTENT FEATURE ====================
        
        /// <summary>
        /// NEW: Build lookup tables for fast observer/targetInfo access
        /// Called once at Start() for performance optimization
        /// </summary>
        private void BuildObserverLookupTables()
        {
            observerLookup = new Dictionary<string, ObserverBehaviour>();
            targetInfoLookup = new Dictionary<string, ARSafeTargetInfo>();
            
            IEnumerable<ObserverBehaviour> observers;
            if (activationController != null && activationController.allAreaTargets != null && activationController.allAreaTargets.Count > 0)
            {
                observers = activationController.allAreaTargets;
            }
            else
            {
                observers = FindObjectsByType<ObserverBehaviour>(FindObjectsSortMode.None);
            }

            foreach (var obs in observers)
            {
                if (obs == null) continue;
                
                // Cache observer by name
                observerLookup[obs.name] = obs;
                
                // Cache target info if exists
                var info = obs.GetComponent<ARSafeTargetInfo>();
                if (info != null)
                {
                    targetInfoLookup[obs.name] = info;
                }
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeProximityDisplay] Built lookup tables: {observerLookup.Count} observers, {targetInfoLookup.Count} with target info");
            }
        }
        
        /// <summary>
        /// NEW: Check if any adjacent hallway is currently tracking
        /// Returns true if this target has a linked hallway that is tracking
        /// This allows showing content preview for adjacent areas
        /// </summary>
        private bool IsAnyAdjacentHallwayTracking()
        {
            if (targetInfo == null)
                return false;
            
            if (observerLookup == null || targetInfoLookup == null)
                return false; // Lookup not initialized yet
            
            // Get all adjacent targets (includes adjacentTargets + connectedRooms)
            var allAdjacent = targetInfo.GetAllAdjacentTargets();
            if (allAdjacent == null || allAdjacent.Length == 0)
                return false;
            
            // Check each adjacent target
            foreach (ARSafeTargetInfo adjacentInfo in allAdjacent)
            {
                if (adjacentInfo == null) continue;
                
                // Get the observer for this adjacent target
                var adjacentObserver = adjacentInfo.GetComponent<ObserverBehaviour>();
                if (adjacentObserver == null) continue;
                
                // Check if it's tracking
                if (!trackingManager.IsTracking(adjacentObserver))
                    continue;
                
                // Check if it's a hallway (not a room) - rooms don't count for adjacent content
                if (adjacentInfo.targetType == TargetType.Hallway)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ARSafeProximityDisplay] {name} showing because adjacent hallway {adjacentInfo.name} is tracking");
                    }
                    return true; // Found a tracking adjacent hallway!
                }
            }
            
            return false; // No adjacent hallways are tracking
        }
    }
}
