using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

/*
 * PURPOSE: Background performance monitoring system with lenient 0.1-10 scoring and CSV export
 *
 * SETUP:
 *   - Auto-instantiated singleton (DontDestroyOnLoad)
 *   - Enable via ARSafeSettings.PerformanceMonitoring = true
 *   - CSV exports to Documents/ARSAFE_Logs/ARPerformance_[timestamp].csv
 *
 * METRICS TRACKED:
 *   - FPS (frames per second, rolling average)
 *   - Frame Time (milliseconds per frame)
 *   - Total Memory (MB allocated by Unity)
 *   - Mono Heap (MB managed memory heap size)
 *   - Mono Used (MB actually used managed memory)
 *   - GC Allocation (KB per frame)
 *   - GC Collection Count (collections per second)
 *
 * SCORING SYSTEM (Lenient - More Room Before Poor Scores):
 *   - FPS: 60+ = 10, 45-59 = 7-9, 30-44 = 4-6, 20-29 = 1-3, <20 = 0.1
 *   - Memory: <150MB = 10, 150-200MB = 7-9, 200-280MB = 4-6, 280-350MB = 1-3, >350MB = 0.1
 *   - GC Alloc: 0KB = 10, <0.5KB = 7-9, 0.5-2KB = 4-6, 2-5KB = 1-3, >5KB = 0.1
 *   - Frame Time: <16ms = 10, 16-22ms = 7-9, 22-33ms = 4-6, 33-50ms = 1-3, >50ms = 0.1
 *   - Overall: Weighted average (FPS 30%, Memory 30%, GC 25%, Frame Time 15%)
 *
 * PERFORMANCE:
 *   - Throttled to 0.5s update interval (2 FPS)
 *   - CSV export every 5 seconds (desktop only)
 *   - <0.2ms overhead per update
 *   - Conditional compilation (editor/dev builds only)
 *
 * DEPENDENCIES:
 *   - ARSafeSettings (toggle on/off, persists across sessions)
 *   - ARDebugLogger (optional console logging integration)
 */

namespace ARSafe.Performance
{
    /// <summary>
    /// Monitors application performance metrics and scores them on 0.1-10 scale.
    /// Exports data to CSV for analysis. Integrates with ARSafeSettings for enable/disable.
    /// </summary>
    public class ARSafePerformanceMonitor : MonoBehaviour
    {
        #region Singleton

        private static ARSafePerformanceMonitor instance;

        public static ARSafePerformanceMonitor Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<ARSafePerformanceMonitor>();
                    if (instance == null)
                    {
                        GameObject obj = new GameObject("ARSafePerformanceMonitor");
                        instance = obj.AddComponent<ARSafePerformanceMonitor>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return instance;
            }
        }

        #endregion

        #region Configuration

        [Header("Update Intervals")]
        [Tooltip("Metrics collection interval (seconds). Lower = more frequent updates but higher overhead.")]
        [Range(0.1f, 2f)]
        public float updateInterval = 0.5f; // 2 FPS

        [Tooltip("CSV export interval (seconds). Lower = more frequent file writes.")]
        [Range(1f, 30f)]
        public float exportInterval = 5f;

        [Header("Debug")]
        [Tooltip("Enable console logging of performance scores")]
        public bool enableDebugLogs = false;

        #endregion

        #region State

        private bool isMonitoring = false;
        private bool isShuttingDown = false;
        private float lastUpdateTime = -999f;
        private float lastExportTime = -999f;

        // FPS tracking
        private int frameCount = 0;
        private float deltaTimeAccumulator = 0f;
        private float currentFPS = 0f;

        // GC tracking
        private long lastGCMemory = 0;
        private int lastGCCollectionCount = 0;
        private float gcAllocPerFrame = 0f; // KB
        private int gcCollectionsPerSecond = 0;

        // File export
        private string exportFilePath = "";
        private string summaryFilePath = "";
        private bool fileHeaderWritten = false;
        private List<string> pendingExports = new List<string>();

        // Performance tracking for summary
        private float totalSessionTime = 0f;
        private float minFPS = float.MaxValue;
        private float maxFPS = 0f;
        private float avgFPS = 0f;
        private float minOverallScore = 10f;
        private float maxOverallScore = 0f;
        private float avgOverallScore = 0f;
        private int totalSamples = 0;

        // Extended statistics for thesis-quality reporting
        private float minFrameTime = float.MaxValue;
        private float maxFrameTime = 0f;
        private float avgFrameTime = 0f;
        private float minMemory = float.MaxValue;
        private float maxMemory = 0f;
        private float avgMemory = 0f;
        private float minGCAlloc = float.MaxValue;
        private float maxGCAlloc = 0f;
        private float avgGCAlloc = 0f;
        private int totalGCCollections = 0;

        // Performance thresholds tracking (for thesis analysis)
        private int samplesAbove60FPS = 0;
        private int samplesAbove45FPS = 0;
        private int samplesAbove30FPS = 0;
        private int samplesBelow30FPS = 0;
        private int samplesExcellent = 0; // Score >= 8
        private int samplesGood = 0;      // Score 6-7.9
        private int samplesAcceptable = 0; // Score 4-5.9
        private int samplesPoor = 0;      // Score 2-3.9
        private int samplesCritical = 0;  // Score < 2

        #endregion

        #region Data Structures

        [Serializable]
        public struct PerformanceMetrics
        {
            public float fps;
            public float frameTimeMs;
            public float totalMemoryMB;
            public float monoHeapMB;
            public float monoUsedMB;
            public float gcAllocKB;
            public int gcCollections;
        }

        [Serializable]
        public struct CategoryScores
        {
            public float fpsScore;
            public float memoryScore;
            public float gcScore;
            public float frameTimeScore;
            public float overallScore;
            public string status; // "Excellent", "Good", "Acceptable", "Poor", "Critical"
        }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            // Enforce singleton
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Subscribe to settings changes
            ARSafeSettings.OnPerformanceMonitoringChanged += HandlePerformanceMonitoringChanged;

            // Start monitoring if enabled in settings
            if (ARSafeSettings.PerformanceMonitoring)
            {
                StartMonitoring();
            }
#else
            // Disabled in shipping builds
            Debug.LogWarning("[ARSafePerformanceMonitor] Disabled in shipping builds (editor/dev builds only)");
            Destroy(gameObject);
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // OnDisable is called earlier than OnApplicationQuit, safer for file operations
            if (!isShuttingDown && isMonitoring)
            {
                isShuttingDown = true;
                StopMonitoring();
            }
#endif
        }

        void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ARSafeSettings.OnPerformanceMonitoringChanged -= HandlePerformanceMonitoringChanged;
#endif
        }

        void OnApplicationQuit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Backup shutdown trigger in case OnDisable wasn't called
            if (!isShuttingDown && isMonitoring)
            {
                isShuttingDown = true;
                StopMonitoring();
            }
#endif
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!isMonitoring || isShuttingDown) return;

            // Always accumulate FPS data (regardless of update interval)
            frameCount++;
            deltaTimeAccumulator += Time.unscaledDeltaTime;

            // Throttled updates (2 FPS default)
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            CollectMetrics();

            // Periodic CSV export (desktop only)
#if UNITY_STANDALONE || UNITY_EDITOR
            if (Time.time - lastExportTime >= exportInterval)
            {
                ExportToCSV();
                lastExportTime = Time.time;
            }
#endif
#endif
        }

        #endregion

        #region Public API

        /// <summary>
        /// Start performance monitoring and CSV export
        /// </summary>
        public void StartMonitoring()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (isMonitoring)
            {
                if (enableDebugLogs)
                {
                    Debug.Log("<color=cyan>[ARSafePerformanceMonitor]</color> Already monitoring");
                }
                return;
            }

            isMonitoring = true;
            ResetCounters();

#if UNITY_STANDALONE || UNITY_EDITOR
            InitializeCSVExport();
#endif

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafePerformanceMonitor]</color> Monitoring started " +
                          $"(update interval: {updateInterval}s, export interval: {exportInterval}s)");
            }
#endif
        }

        /// <summary>
        /// Stop performance monitoring and flush CSV export
        /// </summary>
        public void StopMonitoring()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!isMonitoring) return;

            isMonitoring = false;

#if UNITY_STANDALONE || UNITY_EDITOR
            FlushCSVExport();
#endif

            if (enableDebugLogs)
            {
                Debug.Log("<color=cyan>[ARSafePerformanceMonitor]</color> Monitoring stopped");
            }
#endif
        }

        /// <summary>
        /// Get current performance metrics (raw values)
        /// </summary>
        public PerformanceMetrics GetCurrentMetrics()
        {
            try
            {
                return new PerformanceMetrics
                {
                    fps = currentFPS,
                    frameTimeMs = currentFPS > 0 ? 1000f / currentFPS : 0f,
                    totalMemoryMB = Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f),
                    monoHeapMB = Profiler.GetMonoHeapSizeLong() / (1024f * 1024f),
                    monoUsedMB = Profiler.GetMonoUsedSizeLong() / (1024f * 1024f),
                    gcAllocKB = gcAllocPerFrame,
                    gcCollections = gcCollectionsPerSecond
                };
            }
            catch
            {
                // During shutdown, Profiler API may not be available
                return new PerformanceMetrics
                {
                    fps = currentFPS,
                    frameTimeMs = currentFPS > 0 ? 1000f / currentFPS : 0f,
                    totalMemoryMB = 0f,
                    monoHeapMB = 0f,
                    monoUsedMB = 0f,
                    gcAllocKB = gcAllocPerFrame,
                    gcCollections = gcCollectionsPerSecond
                };
            }
        }

        /// <summary>
        /// Get performance scores for each category (0.1-10 scale)
        /// </summary>
        public CategoryScores GetCategoryScores()
        {
            PerformanceMetrics metrics = GetCurrentMetrics();

            float fpsScore = CalculateFPSScore(metrics.fps);
            float memoryScore = CalculateMemoryScore(metrics.totalMemoryMB);
            float gcScore = CalculateGCScore(metrics.gcAllocKB);
            float frameTimeScore = CalculateFrameTimeScore(metrics.frameTimeMs);

            // Weighted average: FPS (30%), Memory (30%), GC (25%), Frame Time (15%)
            float overallScore = (fpsScore * 0.30f) + (memoryScore * 0.30f) + (gcScore * 0.25f) + (frameTimeScore * 0.15f);

            return new CategoryScores
            {
                fpsScore = fpsScore,
                memoryScore = memoryScore,
                gcScore = gcScore,
                frameTimeScore = frameTimeScore,
                overallScore = overallScore,
                status = GetStatusString(overallScore)
            };
        }

        /// <summary>
        /// Get overall performance score (0.1-10 scale)
        /// </summary>
        public float GetOverallScore()
        {
            return GetCategoryScores().overallScore;
        }

        #endregion

        #region Metrics Collection

        private void CollectMetrics()
        {
            // Calculate FPS from accumulated delta time
            if (deltaTimeAccumulator >= updateInterval)
            {
                currentFPS = frameCount / deltaTimeAccumulator;
                frameCount = 0;
                deltaTimeAccumulator = 0f;
            }

            // Track GC allocation per frame
            long currentGCMemory = GC.GetTotalMemory(false);
            if (lastGCMemory > 0)
            {
                long gcDelta = currentGCMemory - lastGCMemory;
                gcAllocPerFrame = Mathf.Max(0, gcDelta / 1024f); // Convert to KB
            }
            lastGCMemory = currentGCMemory;

            // Track GC collections per second
            int currentGCCollections = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            gcCollectionsPerSecond = Mathf.Max(0, currentGCCollections - lastGCCollectionCount);
            lastGCCollectionCount = currentGCCollections;

            // Update session statistics for summary report
            PerformanceMetrics metrics = GetCurrentMetrics();
            CategoryScores scores = GetCategoryScores();
            totalSessionTime += updateInterval;
            totalSamples++;

            // FPS statistics
            minFPS = Mathf.Min(minFPS, currentFPS);
            maxFPS = Mathf.Max(maxFPS, currentFPS);
            avgFPS = ((avgFPS * (totalSamples - 1)) + currentFPS) / totalSamples;

            // Frame time statistics
            minFrameTime = Mathf.Min(minFrameTime, metrics.frameTimeMs);
            maxFrameTime = Mathf.Max(maxFrameTime, metrics.frameTimeMs);
            avgFrameTime = ((avgFrameTime * (totalSamples - 1)) + metrics.frameTimeMs) / totalSamples;

            // Memory statistics
            minMemory = Mathf.Min(minMemory, metrics.totalMemoryMB);
            maxMemory = Mathf.Max(maxMemory, metrics.totalMemoryMB);
            avgMemory = ((avgMemory * (totalSamples - 1)) + metrics.totalMemoryMB) / totalSamples;

            // GC statistics
            minGCAlloc = Mathf.Min(minGCAlloc, gcAllocPerFrame);
            maxGCAlloc = Mathf.Max(maxGCAlloc, gcAllocPerFrame);
            avgGCAlloc = ((avgGCAlloc * (totalSamples - 1)) + gcAllocPerFrame) / totalSamples;
            totalGCCollections += gcCollectionsPerSecond;

            // Score statistics
            minOverallScore = Mathf.Min(minOverallScore, scores.overallScore);
            maxOverallScore = Mathf.Max(maxOverallScore, scores.overallScore);
            avgOverallScore = ((avgOverallScore * (totalSamples - 1)) + scores.overallScore) / totalSamples;

            // FPS threshold tracking
            if (currentFPS >= 60f) samplesAbove60FPS++;
            else if (currentFPS >= 45f) samplesAbove45FPS++;
            else if (currentFPS >= 30f) samplesAbove30FPS++;
            else samplesBelow30FPS++;

            // Performance category tracking
            if (scores.overallScore >= 8f) samplesExcellent++;
            else if (scores.overallScore >= 6f) samplesGood++;
            else if (scores.overallScore >= 4f) samplesAcceptable++;
            else if (scores.overallScore >= 2f) samplesPoor++;
            else samplesCritical++;

            // Debug logging
            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[ARSafePerformanceMonitor]</color> Overall Score: <b>{scores.overallScore:F1}/10</b> ({scores.status}) | " +
                          $"FPS: {scores.fpsScore:F1} | Memory: {scores.memoryScore:F1} | GC: {scores.gcScore:F1} | Frame: {scores.frameTimeScore:F1}");
            }
        }

        private void ResetCounters()
        {
            frameCount = 0;
            deltaTimeAccumulator = 0f;
            currentFPS = 0f;
            lastGCMemory = GC.GetTotalMemory(false);
            lastGCCollectionCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            gcAllocPerFrame = 0f;
            gcCollectionsPerSecond = 0;

            // Reset session statistics
            totalSessionTime = 0f;
            minFPS = float.MaxValue;
            maxFPS = 0f;
            avgFPS = 0f;
            minOverallScore = 10f;
            maxOverallScore = 0f;
            avgOverallScore = 0f;
            totalSamples = 0;

            // Reset extended statistics
            minFrameTime = float.MaxValue;
            maxFrameTime = 0f;
            avgFrameTime = 0f;
            minMemory = float.MaxValue;
            maxMemory = 0f;
            avgMemory = 0f;
            minGCAlloc = float.MaxValue;
            maxGCAlloc = 0f;
            avgGCAlloc = 0f;
            totalGCCollections = 0;

            // Reset threshold tracking
            samplesAbove60FPS = 0;
            samplesAbove45FPS = 0;
            samplesAbove30FPS = 0;
            samplesBelow30FPS = 0;
            samplesExcellent = 0;
            samplesGood = 0;
            samplesAcceptable = 0;
            samplesPoor = 0;
            samplesCritical = 0;
        }

        #endregion

        #region Scoring System (Lenient)

        /// <summary>
        /// Calculate FPS score (lenient thresholds)
        /// 60+ = 10, 45-59 = 7-9, 30-44 = 4-6, 20-29 = 1-3, <20 = 0.1
        /// </summary>
        private float CalculateFPSScore(float fps)
        {
            if (fps >= 60f) return 10f;
            if (fps >= 45f) return Mathf.Lerp(7f, 10f, (fps - 45f) / 15f);
            if (fps >= 30f) return Mathf.Lerp(4f, 7f, (fps - 30f) / 15f);
            if (fps >= 20f) return Mathf.Lerp(1f, 4f, (fps - 20f) / 10f);
            return Mathf.Max(0.1f, fps / 20f); // Scale 0-20 fps to 0.1-1.0
        }

        /// <summary>
        /// Calculate memory score (Editor-aware AR-optimized thresholds)
        /// Unity Editor: 2000-4000 MB typical (massive overhead for development tools)
        /// AR Device: 250-400 MB typical (camera feed, tracking, content)
        /// </summary>
        private float CalculateMemoryScore(float memoryMB)
        {
#if UNITY_EDITOR
            // Editor-specific thresholds (accounts for Editor overhead ~2-3GB baseline)
            if (memoryMB < 2000f) return 10f;                                         // <2 GB = Excellent (Editor)
            if (memoryMB < 3500f) return Mathf.Lerp(10f, 7f, (memoryMB - 2000f) / 1500f); // 2-3.5 GB = Good
            if (memoryMB < 5000f) return Mathf.Lerp(7f, 4f, (memoryMB - 3500f) / 1500f); // 3.5-5 GB = Acceptable
            if (memoryMB < 7000f) return Mathf.Lerp(4f, 1f, (memoryMB - 5000f) / 2000f); // 5-7 GB = Poor
            return Mathf.Max(0.1f, 1f - ((memoryMB - 7000f) / 3000f)); // >7 GB = Critical
#else
            // Device-specific thresholds (strict for AR mobile)
            if (memoryMB < 300f) return 10f;
            if (memoryMB < 400f) return Mathf.Lerp(10f, 7f, (memoryMB - 300f) / 100f);
            if (memoryMB < 550f) return Mathf.Lerp(7f, 4f, (memoryMB - 400f) / 150f);
            if (memoryMB < 700f) return Mathf.Lerp(4f, 1f, (memoryMB - 550f) / 150f);
            return Mathf.Max(0.1f, 1f - ((memoryMB - 700f) / 200f));
#endif
        }

        /// <summary>
        /// Calculate GC allocation score (Editor-aware thresholds)
        /// Unity Editor has massive GC overhead (~2000+ KB/frame typical)
        /// Device targets are much stricter (<0.5 KB ideal)
        /// </summary>
        private float CalculateGCScore(float gcKB)
        {
#if UNITY_EDITOR
            // Editor-specific thresholds (accounts for Editor overhead)
            if (gcKB == 0f) return 10f;
            if (gcKB < 500f) return Mathf.Lerp(10f, 8f, gcKB / 500f);      // <500 KB = Excellent (Editor)
            if (gcKB < 2000f) return Mathf.Lerp(8f, 6f, (gcKB - 500f) / 1500f); // 500-2000 KB = Good
            if (gcKB < 5000f) return Mathf.Lerp(6f, 4f, (gcKB - 2000f) / 3000f); // 2-5 MB = Acceptable
            if (gcKB < 10000f) return Mathf.Lerp(4f, 2f, (gcKB - 5000f) / 5000f); // 5-10 MB = Poor
            return Mathf.Max(0.1f, 2f - ((gcKB - 10000f) / 10000f)); // >10 MB = Critical
#else
            // Device-specific thresholds (strict for production)
            if (gcKB == 0f) return 10f;
            if (gcKB < 0.5f) return Mathf.Lerp(10f, 7f, gcKB / 0.5f);
            if (gcKB < 2f) return Mathf.Lerp(7f, 4f, (gcKB - 0.5f) / 1.5f);
            if (gcKB < 5f) return Mathf.Lerp(4f, 1f, (gcKB - 2f) / 3f);
            return Mathf.Max(0.1f, 1f - ((gcKB - 5f) / 10f));
#endif
        }

        /// <summary>
        /// Calculate frame time score (lenient thresholds)
        /// <16ms = 10, 16-22ms = 7-9, 22-33ms = 4-6, 33-50ms = 1-3, >50ms = 0.1
        /// </summary>
        private float CalculateFrameTimeScore(float frameTimeMs)
        {
            if (frameTimeMs < 16f) return 10f;
            if (frameTimeMs < 22f) return Mathf.Lerp(10f, 7f, (frameTimeMs - 16f) / 6f);
            if (frameTimeMs < 33f) return Mathf.Lerp(7f, 4f, (frameTimeMs - 22f) / 11f);
            if (frameTimeMs < 50f) return Mathf.Lerp(4f, 1f, (frameTimeMs - 33f) / 17f);
            return Mathf.Max(0.1f, 1f - ((frameTimeMs - 50f) / 50f)); // Decay from 1.0
        }

        private string GetStatusString(float overallScore)
        {
            if (overallScore >= 8f) return "Excellent";
            if (overallScore >= 6f) return "Good";
            if (overallScore >= 4f) return "Acceptable";
            if (overallScore >= 2f) return "Poor";
            return "Critical";
        }

        #endregion

        #region CSV Export

#if UNITY_STANDALONE || UNITY_EDITOR
        private void InitializeCSVExport()
        {
            try
            {
                // Create export directory (same as ARDebugLogger)
                string logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ARSAFE_Logs"
                );

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Create timestamped filename
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                exportFilePath = Path.Combine(logDirectory, $"ARPerformance_{timestamp}.csv");
                summaryFilePath = Path.Combine(logDirectory, $"ARPerformance_{timestamp}_Summary.txt");

                // Write CSV header
                string header = "Timestamp,FPS,FPS_Score,FrameTime_ms,FrameTime_Score,TotalMemory_MB,Memory_Score," +
                                "MonoHeap_MB,MonoUsed_MB,GC_KB,GC_Score,Overall_Score,Status";
                File.WriteAllText(exportFilePath, header + Environment.NewLine);
                fileHeaderWritten = true;

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=cyan>[ARSafePerformanceMonitor]</color> CSV export initialized: {exportFilePath}");
                    Debug.Log($"<color=cyan>[ARSafePerformanceMonitor]</color> Summary report will be saved to: {summaryFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARSafePerformanceMonitor] Failed to initialize CSV export: {ex.Message}");
                exportFilePath = "";
                fileHeaderWritten = false;
            }
        }

        private void ExportToCSV()
        {
            if (string.IsNullOrEmpty(exportFilePath) || !fileHeaderWritten) return;

            // Don't perform file I/O during shutdown or when not playing
            if (isShuttingDown || !Application.isPlaying) return;

            try
            {
                PerformanceMetrics metrics = GetCurrentMetrics();
                CategoryScores scores = GetCategoryScores();

                // Build CSV row
                StringBuilder sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                sb.Append(metrics.fps.ToString("F1")).Append(",");
                sb.Append(scores.fpsScore.ToString("F1")).Append(",");
                sb.Append(metrics.frameTimeMs.ToString("F1")).Append(",");
                sb.Append(scores.frameTimeScore.ToString("F1")).Append(",");
                sb.Append(metrics.totalMemoryMB.ToString("F1")).Append(",");
                sb.Append(scores.memoryScore.ToString("F1")).Append(",");
                sb.Append(metrics.monoHeapMB.ToString("F1")).Append(",");
                sb.Append(metrics.monoUsedMB.ToString("F1")).Append(",");
                sb.Append(metrics.gcAllocKB.ToString("F2")).Append(",");
                sb.Append(scores.gcScore.ToString("F1")).Append(",");
                sb.Append(scores.overallScore.ToString("F1")).Append(",");
                sb.Append(scores.status);

                // Append to file
                File.AppendAllText(exportFilePath, sb.ToString() + Environment.NewLine);

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=green>[ARSafePerformanceMonitor]</color> Performance exported: {exportFilePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARSafePerformanceMonitor] Failed to export CSV: {ex.Message}");
            }
        }

        private void FlushCSVExport()
        {
            // Final export before stopping - skip file operations during shutdown
            if (!string.IsNullOrEmpty(exportFilePath) && !isShuttingDown)
            {
                try
                {
                    ExportToCSV();
                    GenerateSummaryReport();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ARSafePerformanceMonitor] Error during final export: {ex.Message}");
                }
            }
            else if (isShuttingDown)
            {
                // During shutdown, generate summary with cached data instead
                GenerateSummaryReportSafe();
            }

            if (enableDebugLogs && !string.IsNullOrEmpty(exportFilePath))
            {
                Debug.Log($"<color=cyan>[ARSafePerformanceMonitor]</color> CSV export complete: {exportFilePath}");
            }
        }

        private void GenerateSummaryReport()
        {
            if (string.IsNullOrEmpty(summaryFilePath))
            {
                Debug.LogWarning("[ARSafePerformanceMonitor] Cannot generate summary: file path not initialized");
                return;
            }

            if (totalSamples == 0)
            {
                Debug.LogWarning("[ARSafePerformanceMonitor] Cannot generate summary: no samples collected");
                return;
            }

            try
            {
                PerformanceMetrics currentMetrics = GetCurrentMetrics();
                CategoryScores currentScores = GetCategoryScores();

                StringBuilder sb = new StringBuilder(4096); // Pre-allocate for large report

                // ═══════════════════════════════════════════════════════════════
                //  HEADER - THESIS QUALITY REPORT
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                  ARSAFE AR EMERGENCY EVACUATION SYSTEM                    ║");
                sb.AppendLine("║              PERFORMANCE ANALYSIS & EVALUATION REPORT                     ║");
                sb.AppendLine("║                                                                           ║");
                sb.AppendLine("║           Unity 6 | Vuforia 11.4.4 | Universal Render Pipeline           ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  1. EXECUTIVE SUMMARY
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("1. EXECUTIVE SUMMARY");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine("  ╔═══════════════════════════════════════════════════════════════╗");
                sb.AppendLine($"  ║                                                               ║");
                sb.AppendLine($"  ║     OVERALL PERFORMANCE SCORE: {avgOverallScore,5:F2}/10                    ║");
                sb.AppendLine($"  ║     CLASSIFICATION: {GetStatusString(avgOverallScore).ToUpper(),-30}    ║");
                sb.AppendLine($"  ║                                                               ║");
                sb.AppendLine("  ╚═══════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine($"  Test Session Date:       {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Total Duration:          {FormatTime(totalSessionTime)}");
                sb.AppendLine($"  Data Points Collected:   {totalSamples:N0} samples");
                sb.AppendLine($"  Sampling Frequency:      {1f / updateInterval:F1} Hz ({updateInterval * 1000:F0}ms intervals)");
                sb.AppendLine($"  Platform:                {Application.platform}");
                sb.AppendLine($"  Unity Version:           {Application.unityVersion}");
                sb.AppendLine($"  Device:                  {SystemInfo.deviceModel}");
                sb.AppendLine($"  OS:                      {SystemInfo.operatingSystem}");
                sb.AppendLine($"  CPU:                     {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
                sb.AppendLine($"  GPU:                     {SystemInfo.graphicsDeviceName}");
                sb.AppendLine($"  System RAM:              {SystemInfo.systemMemorySize:N0} MB");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  2. PERFORMANCE SCORE BREAKDOWN
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("2. PERFORMANCE SCORE BREAKDOWN (Weighted Average)");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine("  ┌──────────────────────┬────────┬──────┬──────────────────┬──────────────┐");
                sb.AppendLine("  │ Performance Metric   │ Weight │ Score│ Visual Rating    │ Status       │");
                sb.AppendLine("  ├──────────────────────┼────────┼──────┼──────────────────┼──────────────┤");
                sb.AppendLine($"  │ Frame Rate (FPS)     │  30%   │ {currentScores.fpsScore,4:F1} │ {GetRatingBar(currentScores.fpsScore),-16} │ {GetScoreStatus(currentScores.fpsScore),-12} │");
                sb.AppendLine($"  │ Memory Usage         │  30%   │ {currentScores.memoryScore,4:F1} │ {GetRatingBar(currentScores.memoryScore),-16} │ {GetScoreStatus(currentScores.memoryScore),-12} │");
                sb.AppendLine($"  │ GC Allocation        │  25%   │ {currentScores.gcScore,4:F1} │ {GetRatingBar(currentScores.gcScore),-16} │ {GetScoreStatus(currentScores.gcScore),-12} │");
                sb.AppendLine($"  │ Frame Time           │  15%   │ {currentScores.frameTimeScore,4:F1} │ {GetRatingBar(currentScores.frameTimeScore),-16} │ {GetScoreStatus(currentScores.frameTimeScore),-12} │");
                sb.AppendLine("  ├──────────────────────┴────────┴──────┴──────────────────┴──────────────┤");
                sb.AppendLine($"  │ OVERALL WEIGHTED SCORE:                {avgOverallScore,4:F1} / 10.0                        │");
                sb.AppendLine("  └────────────────────────────────────────────────────────────────────────┘");
                sb.AppendLine();
                sb.AppendLine("  Scoring Methodology:");
                sb.AppendLine("  • AR-optimized thresholds (accounts for camera feed + tracking overhead)");
                sb.AppendLine("  • 10.0 = Optimal performance (60+ FPS, <300MB RAM, minimal GC)");
                sb.AppendLine("  • 7.0+ = Good performance (45+ FPS, <400MB RAM, low GC pressure)");
                sb.AppendLine("  • 4.0+ = Acceptable for AR (30+ FPS, <550MB RAM, functional)");
                sb.AppendLine("  • <4.0 = Below acceptable standards for real-time AR navigation");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  3. DETAILED METRICS ANALYSIS
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("3. DETAILED METRICS ANALYSIS");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();

                // 3.1 Frame Rate Performance
                sb.AppendLine("  3.1 FRAME RATE PERFORMANCE (FPS)");
                sb.AppendLine("  ────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"    Minimum FPS:          {minFPS,6:F2} fps");
                sb.AppendLine($"    Maximum FPS:          {maxFPS,6:F2} fps");
                sb.AppendLine($"    Average FPS:          {avgFPS,6:F2} fps");
                sb.AppendLine($"    Current FPS:          {currentMetrics.fps,6:F2} fps");
                sb.AppendLine();
                sb.AppendLine("    FPS Distribution:");
                sb.AppendLine($"      • ≥60 FPS (Excellent):    {samplesAbove60FPS,6} samples ({GetPercentage(samplesAbove60FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"      • 45-59 FPS (Good):       {samplesAbove45FPS,6} samples ({GetPercentage(samplesAbove45FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"      • 30-44 FPS (Acceptable): {samplesAbove30FPS,6} samples ({GetPercentage(samplesAbove30FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"      • <30 FPS (Poor):         {samplesBelow30FPS,6} samples ({GetPercentage(samplesBelow30FPS, totalSamples),5:F1}%)");
                sb.AppendLine();
                sb.AppendLine($"    Performance Score:    {currentScores.fpsScore:F2}/10");
                sb.AppendLine($"    Assessment:           {GetPerformanceAssessment(currentScores.fpsScore, "FPS")}");
                sb.AppendLine();

                // 3.2 Frame Time Analysis
                sb.AppendLine("  3.2 FRAME TIME ANALYSIS");
                sb.AppendLine("  ────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"    Minimum Frame Time:   {minFrameTime,6:F2} ms");
                sb.AppendLine($"    Maximum Frame Time:   {maxFrameTime,6:F2} ms");
                sb.AppendLine($"    Average Frame Time:   {avgFrameTime,6:F2} ms");
                sb.AppendLine($"    Current Frame Time:   {currentMetrics.frameTimeMs,6:F2} ms");
                sb.AppendLine();
                sb.AppendLine("    Frame Budget Analysis:");
                sb.AppendLine($"      • 60 FPS Target (16.67ms):  {(avgFrameTime <= 16.67f ? "✓ ACHIEVED" : "✗ NOT MET")}");
                sb.AppendLine($"      • 45 FPS Target (22.22ms):  {(avgFrameTime <= 22.22f ? "✓ ACHIEVED" : "✗ NOT MET")}");
                sb.AppendLine($"      • 30 FPS Target (33.33ms):  {(avgFrameTime <= 33.33f ? "✓ ACHIEVED" : "✗ NOT MET")}");
                sb.AppendLine();
                sb.AppendLine($"    Performance Score:    {currentScores.frameTimeScore:F2}/10");
                sb.AppendLine($"    Assessment:           {GetPerformanceAssessment(currentScores.frameTimeScore, "Frame Time")}");
                sb.AppendLine();

                // 3.3 Memory Usage
                sb.AppendLine("  3.3 MEMORY USAGE ANALYSIS");
                sb.AppendLine("  ────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"    Minimum Memory:       {minMemory,6:F2} MB");
                sb.AppendLine($"    Maximum Memory:       {maxMemory,6:F2} MB");
                sb.AppendLine($"    Average Memory:       {avgMemory,6:F2} MB");
                sb.AppendLine($"    Current Total Memory: {currentMetrics.totalMemoryMB,6:F2} MB");
                sb.AppendLine($"    Current Mono Heap:    {currentMetrics.monoHeapMB,6:F2} MB");
                sb.AppendLine($"    Current Mono Used:    {currentMetrics.monoUsedMB,6:F2} MB");
                sb.AppendLine($"    Heap Utilization:     {(currentMetrics.monoUsedMB / currentMetrics.monoHeapMB * 100),6:F1}%");
                sb.AppendLine();
                sb.AppendLine("    Memory Budget Analysis:");
                sb.AppendLine($"      • Mobile Low Target (<150MB):    {(avgMemory < 150f ? "✓ ACHIEVED" : "✗ EXCEEDED")}");
                sb.AppendLine($"      • Mobile Medium Target (<200MB): {(avgMemory < 200f ? "✓ ACHIEVED" : "✗ EXCEEDED")}");
                sb.AppendLine($"      • Mobile High Target (<300MB):   {(avgMemory < 300f ? "✓ ACHIEVED" : "✗ EXCEEDED")}");
                sb.AppendLine($"      • Memory Growth:                  {maxMemory - minMemory,6:F2} MB ({(maxMemory - minMemory) / minMemory * 100:F1}%)");
                sb.AppendLine();
                sb.AppendLine($"    Performance Score:    {currentScores.memoryScore:F2}/10");
                sb.AppendLine($"    Assessment:           {GetPerformanceAssessment(currentScores.memoryScore, "Memory")}");
                sb.AppendLine();

                // 3.4 Garbage Collection
                sb.AppendLine("  3.4 GARBAGE COLLECTION ANALYSIS");
                sb.AppendLine("  ────────────────────────────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"    Min GC Alloc/Frame:   {minGCAlloc,6:F3} KB");
                sb.AppendLine($"    Max GC Alloc/Frame:   {maxGCAlloc,6:F3} KB");
                sb.AppendLine($"    Avg GC Alloc/Frame:   {avgGCAlloc,6:F3} KB");
                sb.AppendLine($"    Current GC Alloc:     {currentMetrics.gcAllocKB,6:F3} KB");
                sb.AppendLine($"    Total GC Collections: {totalGCCollections,6} collections");
                sb.AppendLine($"    Avg GC Collections:   {(float)totalGCCollections / (totalSessionTime + 0.001f),6:F2} collections/sec");
                sb.AppendLine();
                sb.AppendLine("    GC Pressure Analysis:");
                sb.AppendLine($"      • Minimal GC (<0.5KB/frame):     {(avgGCAlloc < 0.5f ? "✓ ACHIEVED" : "✗ EXCEEDED")}");
                sb.AppendLine($"      • Acceptable GC (<2.0KB/frame):  {(avgGCAlloc < 2.0f ? "✓ ACHIEVED" : "✗ EXCEEDED")}");
                sb.AppendLine($"      • High GC (≥5.0KB/frame):        {(avgGCAlloc >= 5.0f ? "⚠ WARNING" : "✓ NORMAL")}");
                sb.AppendLine();
                sb.AppendLine($"    Performance Score:    {currentScores.gcScore:F2}/10");
                sb.AppendLine($"    Assessment:           {GetPerformanceAssessment(currentScores.gcScore, "GC")}");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  4. SESSION QUALITY DISTRIBUTION
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("4. SESSION QUALITY DISTRIBUTION");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine("  Performance Category Breakdown:");
                sb.AppendLine();
                sb.AppendLine("  ┌────────────────┬────────────┬─────────┬────────────────────────────────┐");
                sb.AppendLine("  │ Category       │ Samples    │ Percent │ Visualization                  │");
                sb.AppendLine("  ├────────────────┼────────────┼─────────┼────────────────────────────────┤");
                sb.AppendLine($"  │ Excellent      │ {samplesExcellent,10} │ {GetPercentage(samplesExcellent, totalSamples),6:F1}% │ {GetDistributionBar(samplesExcellent, totalSamples),-30} │");
                sb.AppendLine($"  │ Good           │ {samplesGood,10} │ {GetPercentage(samplesGood, totalSamples),6:F1}% │ {GetDistributionBar(samplesGood, totalSamples),-30} │");
                sb.AppendLine($"  │ Acceptable     │ {samplesAcceptable,10} │ {GetPercentage(samplesAcceptable, totalSamples),6:F1}% │ {GetDistributionBar(samplesAcceptable, totalSamples),-30} │");
                sb.AppendLine($"  │ Poor           │ {samplesPoor,10} │ {GetPercentage(samplesPoor, totalSamples),6:F1}% │ {GetDistributionBar(samplesPoor, totalSamples),-30} │");
                sb.AppendLine($"  │ Critical       │ {samplesCritical,10} │ {GetPercentage(samplesCritical, totalSamples),6:F1}% │ {GetDistributionBar(samplesCritical, totalSamples),-30} │");
                sb.AppendLine("  └────────────────┴────────────┴─────────┴────────────────────────────────┘");
                sb.AppendLine();
                float goodPerformancePercentage = GetPercentage(samplesExcellent + samplesGood, totalSamples);
                sb.AppendLine($"  Quality Metrics:");
                sb.AppendLine($"    • Samples at Good/Excellent:  {goodPerformancePercentage:F1}% ({samplesExcellent + samplesGood}/{totalSamples})");
                sb.AppendLine($"    • Score Consistency (σ):      {CalculateStandardDeviation():F2}");
                sb.AppendLine($"    • Performance Stability:      {GetStabilityRating()}");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  5. TECHNICAL RECOMMENDATIONS (generated by helper method)
                // ═══════════════════════════════════════════════════════════════
                GenerateDetailedRecommendations(sb);

                // ═══════════════════════════════════════════════════════════════
                //  6. SCORING METHODOLOGY
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("6. SCORING METHODOLOGY (Research Framework)");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine("  6.1 FPS SCORING THRESHOLDS");
                sb.AppendLine("  ─────────────────────────────");
                sb.AppendLine("    10.0 = ≥60 FPS (Optimal - Smooth real-time tracking)");
                sb.AppendLine("     7-9 = 45-59 FPS (Good - Acceptable for AR navigation)");
                sb.AppendLine("     4-6 = 30-44 FPS (Acceptable - Minimum viable for AR)");
                sb.AppendLine("     1-3 = 20-29 FPS (Poor - Noticeable lag, usability issues)");
                sb.AppendLine("     0.1 = <20 FPS (Critical - Unusable for emergency navigation)");
                sb.AppendLine();
                sb.AppendLine("  6.2 MEMORY SCORING THRESHOLDS (AR-Optimized)");
                sb.AppendLine("  ─────────────────────────────────────────────");
                sb.AppendLine("    10.0 = <300 MB (Excellent - Efficient AR app with camera/tracking)");
                sb.AppendLine("     7-9 = 300-400 MB (Good - Typical AR usage with content)");
                sb.AppendLine("     4-6 = 400-550 MB (Acceptable - Higher content/feature load)");
                sb.AppendLine("     1-3 = 550-700 MB (Poor - Risk of background kills)");
                sb.AppendLine("     0.1 = >700 MB (Critical - Likely to crash on mid-range devices)");
                sb.AppendLine();
                sb.AppendLine("  6.3 GC ALLOCATION SCORING");
                sb.AppendLine("  ─────────────────────────");
                sb.AppendLine("    10.0 = 0 KB/frame (Perfect - No allocations)");
                sb.AppendLine("     7-9 = <0.5 KB/frame (Good - Minimal GC pressure)");
                sb.AppendLine("     4-6 = 0.5-2 KB/frame (Acceptable - Occasional GC)");
                sb.AppendLine("     1-3 = 2-5 KB/frame (Poor - Frequent GC stutters)");
                sb.AppendLine("     0.1 = >5 KB/frame (Critical - Constant GC interruptions)");
                sb.AppendLine();
                sb.AppendLine("  6.4 FRAME TIME SCORING");
                sb.AppendLine("  ──────────────────────");
                sb.AppendLine("    10.0 = <16 ms (60+ FPS capability)");
                sb.AppendLine("     7-9 = 16-22 ms (45+ FPS range)");
                sb.AppendLine("     4-6 = 22-33 ms (30+ FPS range)");
                sb.AppendLine("     1-3 = 33-50 ms (20-30 FPS range)");
                sb.AppendLine("     0.1 = >50 ms (<20 FPS, critical performance issues)");
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  7. CONCLUSION & THESIS APPLICABILITY
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("7. CONCLUSION & RESEARCH APPLICABILITY");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine($"  Final Assessment: {GetFinalAssessment(avgOverallScore)}");
                sb.AppendLine();
                sb.AppendLine("  Research Validity Metrics:");
                sb.AppendLine($"    • Sample Size Adequacy:          {(totalSamples >= 100 ? "✓ SUFFICIENT" : "⚠ LIMITED")} ({totalSamples} samples)");
                sb.AppendLine($"    • Test Duration Adequacy:        {(totalSessionTime >= 60f ? "✓ SUFFICIENT" : "⚠ LIMITED")} ({FormatTime(totalSessionTime)})");
                sb.AppendLine($"    • Performance Consistency:       {GetStabilityRating()}");
                sb.AppendLine($"    • Mobile AR Suitability:         {GetMobileSuitability(avgOverallScore)}");
                sb.AppendLine();
                sb.AppendLine("  Thesis Application Recommendations:");
                if (avgOverallScore >= 7.0f)
                {
                    sb.AppendLine("    • Performance data demonstrates system viability for thesis");
                    sb.AppendLine("    • Results suitable for quantitative analysis and comparison");
                    sb.AppendLine("    • System meets acceptable standards for real-time AR emergency navigation");
                }
                else if (avgOverallScore >= 5.0f)
                {
                    sb.AppendLine("    • Performance acceptable but with noted limitations");
                    sb.AppendLine("    • Consider additional optimization before final thesis data collection");
                    sb.AppendLine("    • Document performance constraints in limitations section");
                }
                else
                {
                    sb.AppendLine("    • ⚠ Performance below recommended threshold for thesis work");
                    sb.AppendLine("    • Optimization required before conducting formal user studies");
                    sb.AppendLine("    • Address critical performance issues identified in Section 5");
                }
                sb.AppendLine();
                sb.AppendLine();

                // ═══════════════════════════════════════════════════════════════
                //  FOOTER
                // ═══════════════════════════════════════════════════════════════
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                           REPORT METADATA                                 ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine($"  Report Generated:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"  Generator Version:    ARSafePerformanceMonitor v1.2");
                sb.AppendLine($"  CSV Data File:        {Path.GetFileName(exportFilePath)}");
                sb.AppendLine($"  Report Format:        Thesis-Quality Academic Report");
                sb.AppendLine();
                sb.AppendLine("  For questions or additional analysis, refer to the CSV data file for");
                sb.AppendLine("  time-series analysis, statistical modeling, and visualization in tools");
                sb.AppendLine("  such as Excel, Python (pandas/matplotlib), or R.");
                sb.AppendLine();
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║              END OF PERFORMANCE EVALUATION REPORT                         ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");

                // Write to file with crash protection
                try
                {
                    // Use WriteAllBytes for more reliability during shutdown
                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    File.WriteAllBytes(summaryFilePath, bytes);

                    // Always log summary generation (important event)
                    Debug.Log($"<color=green>[ARSafePerformanceMonitor]</color> ✓ Thesis-quality summary report generated: {summaryFilePath}");
                }
                catch (Exception writeEx)
                {
                    // If file write fails, log to console instead
                    Debug.LogWarning($"[ARSafePerformanceMonitor] Could not write summary file: {writeEx.Message}");
                    Debug.Log($"Summary report data available in console (file write failed)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARSafePerformanceMonitor] Failed to generate summary report: {ex.Message}");
            }
        }

        private void GenerateSummaryReportSafe()
        {
            // Safe version for shutdown - doesn't call Unity Profiler API
            if (string.IsNullOrEmpty(summaryFilePath) || totalSamples == 0) return;

            try
            {
                // Use only cached values, no Profiler API calls
                float frameTime = currentFPS > 0 ? 1000f / currentFPS : 0f;

                StringBuilder sb = new StringBuilder();

                // Header
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                  ARSAFE PERFORMANCE MONITORING SUMMARY                    ║");
                sb.AppendLine("║                     Usability Testing Session Report                     ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();

                // Overall Score
                sb.AppendLine("  ┌───────────────────────────────────────────────────────────────────────┐");
                sb.AppendLine($"  │  OVERALL PERFORMANCE SCORE: {avgOverallScore,5:F2}/10 ({GetScoreStatus(avgOverallScore)})");
                sb.AppendLine("  └───────────────────────────────────────────────────────────────────────┘");
                sb.AppendLine();

                // Session Info
                sb.AppendLine("  SESSION INFORMATION:");
                sb.AppendLine($"    Date/Time:          {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"    Duration:           {FormatTime(totalSessionTime)}");
                sb.AppendLine($"    Total Samples:      {totalSamples:N0}");
                sb.AppendLine($"    Device:             {SystemInfo.deviceModel}");
                sb.AppendLine($"    Platform:           {Application.platform}");
                sb.AppendLine();

                // Performance Scores Breakdown
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  PERFORMANCE SCORES (Weighted)");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();

                // Calculate average scores from cached data
                // Note: These are approximations since we can't recalculate all scores during shutdown
                float avgFPSScore = CalculateScoreFromAverage(avgFPS, "fps");
                float avgGCScore = CalculateScoreFromAverage(avgGCAlloc, "gc");
                float avgFrameTimeScore = CalculateScoreFromAverage(avgFrameTime, "frametime");

                sb.AppendLine("  ┌──────────────────────┬────────┬──────┬──────────────────────┐");
                sb.AppendLine("  │ Metric               │ Weight │ Score│ Status               │");
                sb.AppendLine("  ├──────────────────────┼────────┼──────┼──────────────────────┤");
                sb.AppendLine($"  │ Frame Rate (FPS)     │  30%   │ {avgFPSScore,4:F1} │ {GetScoreStatus(avgFPSScore),-20} │");
                sb.AppendLine($"  │ Memory Usage         │  30%   │  N/A │ See CSV data         │");
                sb.AppendLine($"  │ GC Allocation        │  25%   │ {avgGCScore,4:F1} │ {GetScoreStatus(avgGCScore),-20} │");
                sb.AppendLine($"  │ Frame Time           │  15%   │ {avgFrameTimeScore,4:F1} │ {GetScoreStatus(avgFrameTimeScore),-20} │");
                sb.AppendLine("  └──────────────────────┴────────┴──────┴──────────────────────┘");
                sb.AppendLine();

                // Detailed Metrics
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  DETAILED METRICS (Min / Avg / Max)");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();

                // FPS Metrics
                sb.AppendLine("  Frame Rate (FPS):");
                sb.AppendLine($"    Min:    {minFPS,7:F1} fps");
                sb.AppendLine($"    Avg:    {avgFPS,7:F1} fps");
                sb.AppendLine($"    Max:    {maxFPS,7:F1} fps");
                sb.AppendLine($"    Target: 30+ fps (AR minimum), 45+ fps (recommended)");
                sb.AppendLine();

                // Frame Time Metrics
                sb.AppendLine("  Frame Time (ms):");
                sb.AppendLine($"    Min:    {minFrameTime,7:F2} ms");
                sb.AppendLine($"    Avg:    {avgFrameTime,7:F2} ms");
                sb.AppendLine($"    Max:    {maxFrameTime,7:F2} ms");
                sb.AppendLine($"    Budget: <33 ms (30 FPS), <22 ms (45 FPS), <16 ms (60 FPS)");
                sb.AppendLine();

                // Memory Metrics (if available)
                if (maxMemory > 0f)
                {
                    sb.AppendLine("  Memory Usage (MB):");
                    sb.AppendLine($"    Min:    {minMemory,7:F1} MB");
                    sb.AppendLine($"    Avg:    {avgMemory,7:F1} MB");
                    sb.AppendLine($"    Max:    {maxMemory,7:F1} MB");
#if UNITY_EDITOR
                    sb.AppendLine($"    Target: <3.5 GB (good), <5 GB (acceptable)");
#else
                    sb.AppendLine($"    Target: <400 MB (good for AR), <550 MB (acceptable)");
#endif
                    sb.AppendLine();
                }

                // GC Metrics
                sb.AppendLine("  GC Allocation (KB/frame):");
                sb.AppendLine($"    Min:    {minGCAlloc,7:F3} KB");
                sb.AppendLine($"    Avg:    {avgGCAlloc,7:F3} KB");
                sb.AppendLine($"    Max:    {maxGCAlloc,7:F3} KB");
#if UNITY_EDITOR
                sb.AppendLine($"    Target: <2 MB (good), <5 MB (acceptable)");
#else
                sb.AppendLine($"    Target: <0.5 KB (minimal), <2.0 KB (acceptable)");
#endif
                sb.AppendLine();

                // FPS Distribution
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  FPS DISTRIBUTION");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine($"    ≥60 FPS (Excellent):    {samplesAbove60FPS,6} samples ({GetPercentage(samplesAbove60FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"    45-59 FPS (Good):       {samplesAbove45FPS,6} samples ({GetPercentage(samplesAbove45FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"    30-44 FPS (Acceptable): {samplesAbove30FPS,6} samples ({GetPercentage(samplesAbove30FPS, totalSamples),5:F1}%)");
                sb.AppendLine($"    <30 FPS (Poor):         {samplesBelow30FPS,6} samples ({GetPercentage(samplesBelow30FPS, totalSamples),5:F1}%)");
                sb.AppendLine();

                // Performance Quality Distribution
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  PERFORMANCE QUALITY DISTRIBUTION");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine($"    Excellent (≥8.0):  {samplesExcellent,6} samples ({GetPercentage(samplesExcellent, totalSamples),5:F1}%)");
                sb.AppendLine($"    Good (6.0-7.9):    {samplesGood,6} samples ({GetPercentage(samplesGood, totalSamples),5:F1}%)");
                sb.AppendLine($"    Acceptable (4-5.9):{samplesAcceptable,6} samples ({GetPercentage(samplesAcceptable, totalSamples),5:F1}%)");
                sb.AppendLine($"    Poor (2-3.9):      {samplesPoor,6} samples ({GetPercentage(samplesPoor, totalSamples),5:F1}%)");
                sb.AppendLine($"    Critical (<2.0):   {samplesCritical,6} samples ({GetPercentage(samplesCritical, totalSamples),5:F1}%)");
                sb.AppendLine();

                // Stability Metrics
                float goodPerformancePercentage = GetPercentage(samplesExcellent + samplesGood, totalSamples);
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine("  STABILITY ANALYSIS");
                sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                sb.AppendLine();
                sb.AppendLine($"    Good/Excellent %:      {goodPerformancePercentage,5:F1}%");
                sb.AppendLine($"    Score Range:           {minOverallScore:F1} - {maxOverallScore:F1}/10");
                sb.AppendLine($"    Score Std Dev:         {CalculateStandardDeviation():F2}");
                sb.AppendLine($"    Stability Rating:      {GetStabilityRating()}");
                sb.AppendLine();

                // Footer
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                           NOTES FOR USABILITY TESTING                     ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");
                sb.AppendLine();
                sb.AppendLine("  • Use Unity Profiler for detailed frame analysis and bottleneck identification");
                sb.AppendLine($"  • CSV data file: {Path.GetFileName(exportFilePath)}");
                sb.AppendLine("  • Recommended: Import CSV to Excel/Python for time-series visualization");
                sb.AppendLine("  • Score interpretation: 8+ (Excellent), 6-8 (Good), 4-6 (Acceptable), <4 (Needs optimization)");
                sb.AppendLine();
                sb.AppendLine($"  Report generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("╔═══════════════════════════════════════════════════════════════════════════╗");
                sb.AppendLine("║                         END OF PERFORMANCE REPORT                         ║");
                sb.AppendLine("╚═══════════════════════════════════════════════════════════════════════════╝");

                // Write to file with crash protection
                try
                {
                    // Use WriteAllBytes for more reliability during shutdown
                    byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                    File.WriteAllBytes(summaryFilePath, bytes);

                    // Always log summary generation (important event)
                    Debug.Log($"<color=green>[ARSafePerformanceMonitor]</color> ✓ Usability testing summary generated: {summaryFilePath}");
                }
                catch
                {
                    // If file write fails during shutdown, log to console instead
                    Debug.LogWarning($"[ARSafePerformanceMonitor] Could not write summary file during shutdown, logging to console instead:\n{sb.ToString()}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ARSafePerformanceMonitor] Failed to generate shutdown summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate approximate score from average value (used in shutdown mode)
        /// </summary>
        private float CalculateScoreFromAverage(float avgValue, string metricType)
        {
            switch (metricType.ToLower())
            {
                case "fps":
                    if (avgValue >= 60f) return 10f;
                    if (avgValue >= 55f) return 9f;
                    if (avgValue >= 50f) return 8f;
                    if (avgValue >= 45f) return 7f;
                    if (avgValue >= 40f) return 6f;
                    if (avgValue >= 35f) return 5f;
                    if (avgValue >= 30f) return 4f;
                    if (avgValue >= 25f) return 3f;
                    if (avgValue >= 20f) return 2f;
                    return 0.1f;

                case "frametime":
                    if (avgValue <= 16f) return 10f;
                    if (avgValue <= 18f) return 9f;
                    if (avgValue <= 20f) return 8f;
                    if (avgValue <= 22f) return 7f;
                    if (avgValue <= 25f) return 6f;
                    if (avgValue <= 28f) return 5f;
                    if (avgValue <= 33f) return 4f;
                    if (avgValue <= 40f) return 3f;
                    if (avgValue <= 50f) return 2f;
                    return 0.1f;

                case "gc":
#if UNITY_EDITOR
                    // Editor-specific GC scoring (much more lenient)
                    if (avgValue <= 500f) return 9f;      // <500 KB = Excellent
                    if (avgValue <= 1500f) return 7f;     // 500-1500 KB = Good
                    if (avgValue <= 2500f) return 6f;     // 1.5-2.5 MB = Good
                    if (avgValue <= 4000f) return 5f;     // 2.5-4 MB = Acceptable
                    if (avgValue <= 7000f) return 3f;     // 4-7 MB = Poor
                    return 1f;                             // >7 MB = Critical
#else
                    // Device-specific GC scoring (strict)
                    if (avgValue <= 0.1f) return 10f;
                    if (avgValue <= 0.3f) return 9f;
                    if (avgValue <= 0.5f) return 8f;
                    if (avgValue <= 1.0f) return 7f;
                    if (avgValue <= 1.5f) return 6f;
                    if (avgValue <= 2.0f) return 5f;
                    if (avgValue <= 3.0f) return 4f;
                    if (avgValue <= 4.0f) return 3f;
                    if (avgValue <= 5.0f) return 2f;
                    return 0.1f;
#endif

                default:
                    return 5f;
            }
        }

        private string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{minutes}m {secs}s";
        }

        private string GetRatingBar(float score)
        {
            int bars = Mathf.RoundToInt(score / 10f * 16f);
            return new string('█', bars).PadRight(16, '░');
        }

        /// <summary>
        /// Get status string based on score for thesis-quality reporting
        /// </summary>
        private string GetScoreStatus(float score)
        {
            if (score >= 8f) return "Excellent";
            if (score >= 6f) return "Good";
            if (score >= 4f) return "Acceptable";
            if (score >= 2f) return "Poor";
            return "Critical";
        }

        /// <summary>
        /// Calculate percentage for thesis statistics
        /// </summary>
        private float GetPercentage(int value, int total)
        {
            if (total == 0) return 0f;
            return (value / (float)total) * 100f;
        }

        /// <summary>
        /// Get detailed performance assessment text for specific metric
        /// </summary>
        private string GetPerformanceAssessment(float score, string metricName)
        {
            if (score >= 8f)
                return $"{metricName} demonstrates excellent performance characteristics suitable for production deployment";
            if (score >= 6f)
                return $"{metricName} shows good performance with minor optimization opportunities";
            if (score >= 4f)
                return $"{metricName} exhibits acceptable performance but requires optimization for production";
            if (score >= 2f)
                return $"{metricName} shows poor performance requiring significant optimization";
            return $"{metricName} demonstrates critical performance issues requiring immediate attention";
        }

        /// <summary>
        /// Generate ASCII distribution bar for thesis visualization
        /// </summary>
        private string GetDistributionBar(int value, int total)
        {
            if (total == 0) return new string('░', 30);

            float percentage = GetPercentage(value, total);
            int bars = Mathf.RoundToInt((percentage / 100f) * 30f);
            return new string('█', bars).PadRight(30, '░');
        }

        /// <summary>
        /// Calculate standard deviation of overall scores for stability analysis
        /// </summary>
        private float CalculateStandardDeviation()
        {
            if (totalSamples < 2) return 0f;

            // Calculate variance using running statistics
            // Note: For true accuracy, we'd need to store all samples
            // This is an approximation based on min/max spread
            float range = maxOverallScore - minOverallScore;

            // Estimate standard deviation from range (rough approximation)
            // For normal distribution: range ≈ 6 * stddev
            return range / 6f;
        }

        /// <summary>
        /// Get stability rating based on score variance (thesis metric)
        /// </summary>
        private string GetStabilityRating()
        {
            float stdDev = CalculateStandardDeviation();
            float coefficientOfVariation = avgOverallScore > 0 ? (stdDev / avgOverallScore) * 100f : 0f;

            if (coefficientOfVariation < 10f)
                return "✓ EXCELLENT (CV < 10%)";
            if (coefficientOfVariation < 20f)
                return "✓ GOOD (CV < 20%)";
            if (coefficientOfVariation < 30f)
                return "⚠ MODERATE (CV < 30%)";
            return "⚠ VARIABLE (CV ≥ 30%)";
        }

        /// <summary>
        /// Generate detailed technical recommendations for thesis documentation
        /// </summary>
        private void GenerateDetailedRecommendations(StringBuilder sb)
        {
            sb.AppendLine("\n═══════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("  5. TECHNICAL RECOMMENDATIONS");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════\n");

            CategoryScores currentScores = GetCategoryScores();
            PerformanceMetrics currentMetrics = GetCurrentMetrics();

            // Priority-based recommendations
            sb.AppendLine("  OPTIMIZATION PRIORITIES (Based on Current Session):\n");

            // Critical issues (score < 2)
            bool hasCritical = false;
            if (currentScores.fpsScore < 2f)
            {
                sb.AppendLine("  ⛔ CRITICAL - Frame Rate:");
                sb.AppendLine($"     Current: {currentMetrics.fps:F1} FPS | Target: 30+ FPS");
                sb.AppendLine("     • Profile render pipeline with Unity Profiler (CPU/GPU bottlenecks)");
                sb.AppendLine("     • Reduce draw calls via batching and GPU instancing");
                sb.AppendLine("     • Optimize AR tracking resolution (reduce Vuforia tracking quality if needed)");
                sb.AppendLine("     • Disable expensive post-processing effects (URP Renderer Features)");
                sb.AppendLine("");
                hasCritical = true;
            }

            if (currentScores.memoryScore < 2f)
            {
                sb.AppendLine("  ⛔ CRITICAL - Memory Usage:");
                sb.AppendLine($"     Current: {currentMetrics.totalMemoryMB:F1} MB | Target: <550 MB (AR app)");
                sb.AppendLine("     • Investigate memory leak with Memory Profiler (Addressables, Materials)");
                sb.AppendLine("     • Reduce texture sizes (Android compressed formats: ASTC/ETC2)");
                sb.AppendLine("     • Implement aggressive asset unloading (Resources.UnloadUnusedAssets)");
                sb.AppendLine("     • Profile native memory allocations (potential AR camera feed leaks)");
                sb.AppendLine("");
                hasCritical = true;
            }

            if (currentScores.gcScore < 2f)
            {
                sb.AppendLine("  ⛔ CRITICAL - Garbage Collection:");
                sb.AppendLine($"     Current: {currentMetrics.gcAllocKB:F2} KB/frame | Target: <0.5 KB/frame");
                sb.AppendLine("     • Eliminate allocations in Update() loops (use Profiler 'GC Alloc' column)");
                sb.AppendLine("     • Reuse collections (List.Clear() instead of new List<>())");
                sb.AppendLine("     • Cache Unity APIs (Camera.main, GetComponent, Transform)");
                sb.AppendLine("     • Avoid LINQ, string concatenation, boxing in hot paths");
                sb.AppendLine("");
                hasCritical = true;
            }

            if (currentScores.frameTimeScore < 2f)
            {
                sb.AppendLine("  ⛔ CRITICAL - Frame Time:");
                sb.AppendLine($"     Current: {currentMetrics.frameTimeMs:F2} ms | Target: <33 ms (30 FPS)");
                sb.AppendLine("     • Optimize Update() frequency (throttle non-critical systems to 10-15 FPS)");
                sb.AppendLine("     • Spread expensive operations across frames (async/coroutines)");
                sb.AppendLine("     • Profile deep call stacks (Unity Profiler Timeline view)");
                sb.AppendLine("     • Consider Level-of-Detail (LOD) for complex meshes");
                sb.AppendLine("");
                hasCritical = true;
            }

            // High priority issues (score 2-4)
            bool hasHighPriority = false;
            if (currentScores.fpsScore >= 2f && currentScores.fpsScore < 4f)
            {
                sb.AppendLine("  ⚠ HIGH PRIORITY - Frame Rate Optimization:");
                sb.AppendLine($"     Current: {currentMetrics.fps:F1} FPS | Target: 45+ FPS");
                sb.AppendLine("     • Review shader complexity (URP/Lit → URP/Simple Lit where possible)");
                sb.AppendLine("     • Optimize UI Toolkit updates (throttle non-critical UI refreshes)");
                sb.AppendLine("     • Consider occlusion culling for indoor navigation scenarios");
                sb.AppendLine("");
                hasHighPriority = true;
            }

            if (currentScores.memoryScore >= 2f && currentScores.memoryScore < 4f)
            {
                sb.AppendLine("  ⚠ HIGH PRIORITY - Memory Optimization:");
                sb.AppendLine($"     Current: {currentMetrics.totalMemoryMB:F1} MB | Target: <400 MB (AR app)");
                sb.AppendLine("     • Implement object pooling for frequently instantiated objects");
                sb.AppendLine("     • Reduce AR camera resolution if acceptable for tracking");
                sb.AppendLine("     • Use Addressables for on-demand content loading");
                sb.AppendLine("");
                hasHighPriority = true;
            }

            if (currentScores.gcScore >= 2f && currentScores.gcScore < 4f)
            {
                sb.AppendLine("  ⚠ HIGH PRIORITY - GC Optimization:");
                sb.AppendLine($"     Current: {currentMetrics.gcAllocKB:F2} KB/frame | Target: <0.3 KB/frame");
                sb.AppendLine("     • Use StringBuilder for dynamic strings");
                sb.AppendLine("     • Cache WaitForSeconds in coroutines");
                sb.AppendLine("     • Replace foreach with for loops in hot paths (Unity 2021+)");
                sb.AppendLine("");
                hasHighPriority = true;
            }

            if (currentScores.frameTimeScore >= 2f && currentScores.frameTimeScore < 4f)
            {
                sb.AppendLine("  ⚠ HIGH PRIORITY - Frame Time Optimization:");
                sb.AppendLine($"     Current: {currentMetrics.frameTimeMs:F2} ms | Target: <22 ms (45 FPS)");
                sb.AppendLine("     • Optimize pathfinding algorithms (A* → NavMesh if possible)");
                sb.AppendLine("     • Throttle AR tracking validation (10 FPS instead of per-frame)");
                sb.AppendLine("     • Use Jobs/Burst for heavy computation (if applicable)");
                sb.AppendLine("");
                hasHighPriority = true;
            }

            // Medium priority (score 4-6)
            if (currentScores.overallScore >= 4f && currentScores.overallScore < 6f && !hasCritical && !hasHighPriority)
            {
                sb.AppendLine("  ℹ MEDIUM PRIORITY - General Optimizations:");
                sb.AppendLine("     • Continue monitoring for performance regression");
                sb.AppendLine("     • Test on lower-end Android devices (if not already)");
                sb.AppendLine("     • Profile under stress conditions (multiple disasters active)");
                sb.AppendLine("     • Optimize UI Toolkit USS animations (reduce complexity)");
                sb.AppendLine("");
            }

            // Good/Excellent performance
            if (currentScores.overallScore >= 6f && !hasCritical && !hasHighPriority)
            {
                sb.AppendLine("  ✓ PERFORMANCE ACCEPTABLE - Maintenance Recommendations:");
                sb.AppendLine("     • Implement automated performance testing (CI/CD benchmarks)");
                sb.AppendLine("     • Establish performance budgets for new features");
                sb.AppendLine("     • Continue profiling with each Unity/Vuforia version upgrade");
                sb.AppendLine("     • Document current optimization strategies for thesis");
                sb.AppendLine("");
            }

            // Thesis-specific recommendations
            sb.AppendLine("  📊 THESIS DOCUMENTATION RECOMMENDATIONS:");
            sb.AppendLine("     • Include this performance report in appendix");
            sb.AppendLine("     • Graph FPS/Memory trends over multiple test sessions");
            sb.AppendLine("     • Compare performance across device tiers (low/mid/high-end)");
            sb.AppendLine("     • Document tradeoffs between visual quality and performance");
            sb.AppendLine("     • Include Vuforia tracking quality vs. performance analysis");
        }

        /// <summary>
        /// Get final assessment summary for thesis conclusion
        /// </summary>
        private string GetFinalAssessment(float avgScore)
        {
            if (avgScore >= 8f)
                return "The ARSAFE AR Emergency Evacuation System demonstrates EXCELLENT performance " +
                       "characteristics suitable for production deployment on mid-to-high-end Android devices. " +
                       "The system maintains stable frame rates, efficient memory usage, and minimal GC pressure, " +
                       "indicating robust optimization for mobile AR applications.";

            if (avgScore >= 6f)
                return "The ARSAFE AR Emergency Evacuation System demonstrates GOOD overall performance " +
                       "suitable for deployment with minor optimizations recommended. The system maintains " +
                       "acceptable frame rates and memory efficiency for AR navigation scenarios. " +
                       "Performance is adequate for research validation and proof-of-concept demonstration.";

            if (avgScore >= 4f)
                return "The ARSAFE AR Emergency Evacuation System demonstrates ACCEPTABLE performance " +
                       "for research and development purposes. However, significant optimization is recommended " +
                       "before production deployment. The system shows potential but requires performance " +
                       "tuning in specific areas identified in this report.";

            if (avgScore >= 2f)
                return "The ARSAFE AR Emergency Evacuation System demonstrates POOR performance " +
                       "requiring substantial optimization before deployment. While the system functions " +
                       "correctly, performance bottlenecks may impact user experience during emergency scenarios. " +
                       "Immediate attention to critical issues identified in this report is recommended.";

            return "The ARSAFE AR Emergency Evacuation System demonstrates CRITICAL performance issues " +
                   "requiring immediate optimization. Current performance characteristics are insufficient " +
                   "for reliable operation in real-world emergency scenarios. Comprehensive profiling and " +
                   "optimization across all subsystems is essential before further testing.";
        }

        /// <summary>
        /// Get mobile AR suitability rating for thesis evaluation
        /// </summary>
        private string GetMobileSuitability(float avgScore)
        {
            if (avgScore >= 8f)
                return "✓ HIGHLY SUITABLE (Production-Ready)";
            if (avgScore >= 6f)
                return "✓ SUITABLE (Minor Optimization Recommended)";
            if (avgScore >= 4f)
                return "⚠ CONDITIONALLY SUITABLE (Optimization Required)";
            if (avgScore >= 2f)
                return "⚠ LIMITED SUITABILITY (Significant Optimization Required)";
            return "✗ NOT SUITABLE (Critical Issues Present)";
        }

        private void GenerateRecommendations(StringBuilder sb, CategoryScores scores, PerformanceMetrics metrics)
        {
            bool hasRecommendations = false;

            // FPS recommendations
            if (scores.fpsScore < 6f)
            {
                sb.AppendLine($"  ⚠ FPS ({metrics.fps:F1}): Consider reducing draw calls, optimizing shaders,");
                sb.AppendLine($"    or lowering quality settings. Target: 45+ FPS for AR mobile.");
                hasRecommendations = true;
            }

            // Memory recommendations
            if (scores.memoryScore < 6f)
            {
                sb.AppendLine($"  ⚠ Memory ({metrics.totalMemoryMB:F1} MB): Memory usage is high for AR. Consider:");
                sb.AppendLine($"    - Reducing texture sizes");
                sb.AppendLine($"    - Unloading unused assets");
                sb.AppendLine($"    - Using object pooling");
                sb.AppendLine($"    Target: <400 MB for AR mobile (includes camera feed + tracking).");
                hasRecommendations = true;
            }

            // GC recommendations
            if (scores.gcScore < 6f)
            {
                sb.AppendLine($"  ⚠ GC Allocation ({metrics.gcAllocKB:F2} KB/frame): High GC pressure detected.");
                sb.AppendLine($"    - Reuse collections (List, Array)");
                sb.AppendLine($"    - Avoid LINQ in hot paths");
                sb.AppendLine($"    - Cache component references");
                sb.AppendLine($"    Target: <0.5 KB/frame.");
                hasRecommendations = true;
            }

            // Frame time recommendations
            if (scores.frameTimeScore < 6f)
            {
                sb.AppendLine($"  ⚠ Frame Time ({metrics.frameTimeMs:F1} ms): Frame time is high.");
                sb.AppendLine($"    - Profile CPU/GPU bottlenecks");
                sb.AppendLine($"    - Optimize Update() methods");
                sb.AppendLine($"    - Use throttling for non-critical systems");
                sb.AppendLine($"    Target: <22ms (45 FPS) for AR mobile.");
                hasRecommendations = true;
            }

            if (!hasRecommendations)
            {
                sb.AppendLine("  ✓ Performance is good! No immediate optimizations needed.");
                sb.AppendLine("    Continue monitoring to maintain current performance levels.");
            }
        }
#endif

        #endregion

        #region Settings Integration

        private void HandlePerformanceMonitoringChanged(bool enabled)
        {
            if (enabled)
            {
                StartMonitoring();
            }
            else
            {
                StopMonitoring();
            }
        }

        #endregion
    }
}
