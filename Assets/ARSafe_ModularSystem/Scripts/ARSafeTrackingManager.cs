using UnityEngine;
using Vuforia;
using System.Collections.Generic;
using System.Linq;

namespace ARSafe.Modular
{
    /// <summary>
    /// Monitors Vuforia observer tracking state and reports runtime statistics.
    /// Keeps a running list of tracked observers and surfaces warnings when the
    /// configured simultaneous tracking limit is exceeded.
    /// 
    /// KEY RESPONSIBILITY:
    /// - Maintain tracking status for each registered observer
    /// - Provide simple queries for scripts that need tracking information
    /// - Warn when the simultaneous tracking limit is exceeded (diagnostic only)
    /// 
    /// WORKS WITH:
    /// - ARSafeActivationController: determines which targets are enabled
    /// - Vuforia ObserverBehaviour: raises tracking status events
    /// </summary>
    public class ARSafeTrackingManager : MonoBehaviour
    {
        [Header("Tracking Limits")]
        [Tooltip("Maximum Area Targets that can be TRACKING simultaneously (Vuforia hardware limit)")]
        [Range(1, 3)]
        public int maxSimultaneousTracking = 2;
        
        [Header("Tracking Quality")]
        [Tooltip("Minimum tracking quality to consider target 'tracked'")]
        public TrackingQualityThreshold qualityThreshold = TrackingQualityThreshold.Limited;
        
        [Header("Debug")]
        public bool enableDebugLogs = false;
        
        // Runtime tracking state
        private Dictionary<ObserverBehaviour, TrackingState> trackingStates = new Dictionary<ObserverBehaviour, TrackingState>();
        private List<ObserverBehaviour> currentlyTracking = new List<ObserverBehaviour>();
        private List<ObserverBehaviour> registeredObservers = new List<ObserverBehaviour>(); // Cache for cleanup
        
        // Performance optimization: Cache tracking count to avoid repeated calculations
        private int currentTrackingCount = 0;
        private bool hasTrackingLimitWarning = false;
        
        void Start()
        {
            // Subscribe to all ObserverBehaviours in scene
            var observers = FindObjectsByType<ObserverBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obs in observers)
            {
                RegisterObserver(obs);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[ARSafeTrackingManager] Initialized. Max simultaneous tracking: {maxSimultaneousTracking}. Registered {observers.Length} observers.");
            }
        }
        
        void OnDestroy()
        {
            // CRITICAL: Proper cleanup to prevent memory leaks
            
            // Unregister all observers (unsubscribe from events)
            for (int i = registeredObservers.Count - 1; i >= 0; i--)
            {
                if (registeredObservers[i] != null)
                {
                    UnregisterObserver(registeredObservers[i]);
                }
            }
            
            // Clear all collections
            registeredObservers.Clear();
            
            if (trackingStates != null)
            {
                trackingStates.Clear();
            }
            
            if (enableDebugLogs)
            {
                Debug.Log("[ARSafeTrackingManager] Cleanup completed");
            }
        }
        
        /// <summary>
        /// Register an observer for tracking management
        /// </summary>
        private void RegisterObserver(ObserverBehaviour observer)
        {
            if (observer == null || trackingStates.ContainsKey(observer)) return;
            
            trackingStates[observer] = new TrackingState();
            registeredObservers.Add(observer); // Cache for efficient cleanup
            
            // Subscribe to Vuforia events
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
            observer.OnBehaviourDestroyed += OnBehaviourDestroyed;
            
            // FIX: Check current tracking status (handle race condition if already tracking)
            var currentStatus = observer.TargetStatus;
            if (currentStatus.Status == Status.TRACKED || currentStatus.Status == Status.EXTENDED_TRACKED)
            {
                // Manually invoke the callback to sync state
                OnTargetStatusChanged(observer, currentStatus);
            }
        }
        
        /// <summary>
        /// Unregister an observer
        /// </summary>
        private void UnregisterObserver(ObserverBehaviour observer)
        {
            if (observer == null) return;
            
            observer.OnTargetStatusChanged -= OnTargetStatusChanged;
            observer.OnBehaviourDestroyed -= OnBehaviourDestroyed;
            
            trackingStates.Remove(observer);
            currentlyTracking.Remove(observer);
        }
        
        /// <summary>
        /// Optimized Vuforia callback: target status changed
        /// </summary>
        private void OnTargetStatusChanged(ObserverBehaviour observer, TargetStatus targetStatus)
        {
            if (!trackingStates.TryGetValue(observer, out var state)) return;
            
            var prevStatus = state.status;
            state.status = targetStatus.Status;
            state.statusInfo = targetStatus.StatusInfo;
            
            // DEBUG: Log actual Vuforia status to understand Unity Editor behavior
            #if UNITY_EDITOR
            if (enableDebugLogs && targetStatus.Status != Status.NO_POSE)
            {
                Debug.Log($"[ARSafeTrackingManager] Unity Editor - {observer.name} status: {targetStatus.Status} (StatusInfo: {targetStatus.StatusInfo})");
            }
            #endif
            
            // Optimized tracking state management
            bool wasTracking = IsStatusTracking(prevStatus);
            bool isTracking = IsStatusTracking(state.status);
            
            if (wasTracking != isTracking)
            {
                if (isTracking)
                {
                    // Started tracking
                    currentlyTracking.Add(observer);
                    currentTrackingCount++;
                    
                    var info = observer.GetComponent<ARSafeTargetInfo>();
                    if (info != null)
                    {
                        info.LastTrackingTime = Time.time;
                    }
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ARSafeTrackingManager] Target TRACKING: {observer.name} (Total tracking: {currentTrackingCount})");
                    }
                }
                else
                {
                    // Stopped tracking
                    currentlyTracking.Remove(observer);
                    currentTrackingCount--;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[ARSafeTrackingManager] Target LOST: {observer.name} (Total tracking: {currentTrackingCount})");
                    }
                }
                
                // Check tracking limit with cached count
                if (currentTrackingCount > maxSimultaneousTracking && !hasTrackingLimitWarning)
                {
                    Debug.LogWarning($"[ARSafeTrackingManager] Tracking limit exceeded: {currentTrackingCount}/{maxSimultaneousTracking}");
                    hasTrackingLimitWarning = true;
                }
                else if (currentTrackingCount <= maxSimultaneousTracking)
                {
                    hasTrackingLimitWarning = false;
                }
            }
        }
        
        /// <summary>
        /// Vuforia callback: observer destroyed
        /// </summary>
        private void OnBehaviourDestroyed(ObserverBehaviour observer)
        {
            UnregisterObserver(observer);
        }
        
        /// <summary>
        /// Is observer currently tracking?
        /// </summary>
        public bool IsTracking(ObserverBehaviour observer)
        {
            if (observer == null || !trackingStates.ContainsKey(observer)) return false;
            
            return IsStatusTracking(trackingStates[observer].status);
        }
        
        /// <summary>
        /// Get current tracking quality for observer
        /// </summary>
        public Status GetTrackingStatus(ObserverBehaviour observer)
        {
            if (observer == null || !trackingStates.ContainsKey(observer))
            {
                return Status.NO_POSE;
            }
            
            return trackingStates[observer].status;
        }
        
        /// <summary>
        /// Get number of currently tracking targets (optimized with cached count)
        /// </summary>
        public int GetTrackingCount()
        {
            return currentTrackingCount;
        }
        
        /// <summary>
        /// Helper: determine if status represents active tracking
        /// FIX: In Unity Editor, EXTENDED_TRACKED is unreliable (Scene View tracks everything)
        /// </summary>
        private bool IsStatusTracking(Status status)
        {
            #if UNITY_EDITOR
            // FIXED: Allow EXTENDED_TRACKED in editor for proper anchor switching
            // Scene View should be closed during testing to avoid false positives
            return status == Status.TRACKED || status == Status.EXTENDED_TRACKED;
            #else
            // On device: Use normal quality threshold
            switch (qualityThreshold)
            {
                case TrackingQualityThreshold.Any:
                    return status == Status.TRACKED || status == Status.LIMITED || status == Status.EXTENDED_TRACKED;
                
                case TrackingQualityThreshold.Limited:
                    return status == Status.TRACKED || status == Status.EXTENDED_TRACKED;
                
                case TrackingQualityThreshold.Tracked:
                    return status == Status.TRACKED;
                
                default:
                    return false;
            }
            #endif
        }
        
        /// <summary>
        /// Optimized: Check if over tracking limit without list iteration
        /// </summary>
        public bool IsOverTrackingLimit() => currentTrackingCount > maxSimultaneousTracking;
        
        /// <summary>
        /// Get all currently tracking observers (returns readonly list for performance)
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<ObserverBehaviour> GetCurrentlyTracking() => currentlyTracking.AsReadOnly();
        
        /// <summary>
        /// Internal tracking state for an observer
        /// </summary>
        private class TrackingState
        {
            public Status status = Status.NO_POSE;
            public StatusInfo statusInfo = StatusInfo.UNKNOWN;
        }
    }
    
    public enum TrackingQualityThreshold
    {
        Any,        // LIMITED, TRACKED, EXTENDED_TRACKED all count
        Limited,    // TRACKED, EXTENDED_TRACKED count
        Tracked     // Only TRACKED counts
    }
}
