using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ARSafe.Modular;

namespace ARSafe.UI
{
    /*
     * ARCHITECTURE PLAN: FloodWarningProgressDisplay
     *
     * PURPOSE:
     *   Real-time UI widget showing flood warning progression during scenarios.
     *   Displays current level, countdown to next transition, and peak level forecast.
     *
     * DEPENDENCIES:
     *   - FloodScenarioManager (events: OnProgressionGenerated, OnWarningLevelChanged)
     *   - UI Toolkit (UXML/USS)
     *   - DisasterTypeManager (disaster type filtering)
     *
     * DATA FLOW:
     *   Input: OnProgressionGenerated → Cache progression timeline
     *          OnWarningLevelChanged → Update current level UI
     *          Update() → Calculate countdown timers
     *   Output: Real-time UI updates (current level, countdown, progress bar)
     *
     * PERFORMANCE CONSIDERATIONS:
     *   - Throttled updates at 10 FPS (100ms intervals) - NOT 60 FPS
     *   - Cache UI element references in OnEnable
     *   - Only update UI when values change (avoid redundant SetText calls)
     *   - Minimal allocations (reuse strings where possible)
     *
     * MOBILE DESIGN:
     *   - Large fonts (≥22px body, ≥28px headers)
     *   - High contrast colors for outdoor AR visibility
     *   - Positioned bottom-left to avoid overlay conflicts
     *   - Compact design (doesn't obscure AR view)
     */

    [RequireComponent(typeof(UIDocument))]
    public class FloodWarningProgressDisplay : MonoBehaviour
    {
        [Header("UI Settings")]
        [Tooltip("Show/hide progress widget. Automatically shown during flood scenarios.")]
        [SerializeField] private bool autoShowDuringScenario = true;

        [Header("Update Performance")]
        [Tooltip("UI update frequency in FPS. Lower = better mobile performance.")]
        [SerializeField] private float updateFPS = 10f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Cached UI element references
        private UIDocument uiDocument;
        private VisualElement rootElement;
        private VisualElement progressWidget;
        private Label currentLevelLabel;
        private Label currentLevelValue;
        private Label peakLevelLabel;
        private Label countdownLabel;
        private VisualElement progressBar;
        private VisualElement progressFill;
        private VisualElement yellowIndicator;
        private VisualElement orangeIndicator;
        private VisualElement redIndicator;

        // Progression state
        private FloodWarningProgression currentProgression;
        private RainfallWarningLevel currentLevel = RainfallWarningLevel.None;
        private int currentPhaseIndex = 0;
        private float scenarioStartTime = 0f;
        private bool isScenarioActive = false;

        // Performance throttling
        private float lastUpdateTime = 0f;
        private float updateInterval = 0.1f; // 10 FPS default

        // Cached values to avoid redundant UI updates
        private string lastLevelText = "";
        private string lastCountdownText = "";
        private float lastProgressValue = -1f;

        public static FloodWarningProgressDisplay Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            updateInterval = 1f / Mathf.Max(1f, updateFPS);
        }

        private void OnEnable()
        {
            CacheUIElements();

            // Subscribe to flood scenario events
            FloodScenarioManager.OnProgressionGenerated += HandleProgressionGenerated;
            FloodScenarioManager.OnWarningLevelChanged += HandleWarningLevelChanged;
            FloodScenarioManager.OnProgressUpdated += HandleProgressUpdated;
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;

            // Check current disaster type
            HandleDisasterTypeChanged(DisasterTypeManager.SelectedDisasterType);
        }

        private void OnDisable()
        {
            FloodScenarioManager.OnProgressionGenerated -= HandleProgressionGenerated;
            FloodScenarioManager.OnWarningLevelChanged -= HandleWarningLevelChanged;
            FloodScenarioManager.OnProgressUpdated -= HandleProgressUpdated;
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;

            HideWidget();
        }

        private void Update()
        {
            // Throttle updates for mobile performance (10 FPS default)
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;

            if (!isScenarioActive || currentProgression == null) return;

            UpdateProgressUI();
        }

        /// <summary>
        /// Cache all UI element references from UXML. Called in OnEnable.
        /// CRITICAL: Elements must exist in FloodWarningProgressDisplay.uxml
        /// </summary>
        private void CacheUIElements()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            if (uiDocument.visualTreeAsset == null)
            {
                Debug.LogError("[FloodWarningProgressDisplay] UIDocument has no visual tree asset! Assign UXML in Inspector.");
                return;
            }

            rootElement = uiDocument.rootVisualElement;
            progressWidget = rootElement.Q<VisualElement>("progress-widget");
            currentLevelLabel = rootElement.Q<Label>("current-level-label");
            currentLevelValue = rootElement.Q<Label>("current-level-value");
            peakLevelLabel = rootElement.Q<Label>("peak-level-label");
            countdownLabel = rootElement.Q<Label>("countdown-label");
            progressBar = rootElement.Q<VisualElement>("progress-bar");
            progressFill = rootElement.Q<VisualElement>("progress-fill");
            yellowIndicator = rootElement.Q<VisualElement>("yellow-indicator");
            orangeIndicator = rootElement.Q<VisualElement>("orange-indicator");
            redIndicator = rootElement.Q<VisualElement>("red-indicator");

            if (progressWidget == null)
            {
                Debug.LogError("[FloodWarningProgressDisplay] Failed to cache UI elements! Check UXML element names.");
                return;
            }

            // Initially hidden
            HideWidget();

            if (enableDebugLogs)
            {
                Debug.Log("[FloodWarningProgressDisplay] UI elements cached successfully");
            }
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType != DisasterType.Flood)
            {
                isScenarioActive = false;
                HideWidget();
            }
        }

        private void HandleProgressionGenerated(FloodWarningProgression progression)
        {
            currentProgression = progression;
            currentPhaseIndex = 0;
            currentLevel = progression.Phases[0].Level; // Always starts at Yellow
            scenarioStartTime = Time.time;
            isScenarioActive = true;

            // Update peak level display
            if (peakLevelLabel != null)
            {
                peakLevelLabel.text = $"Peak: {GetLevelName(progression.PeakLevel)}";
                peakLevelLabel.style.color = new StyleColor(GetLevelColor(progression.PeakLevel));
            }

            // Update level indicators
            UpdateLevelIndicators();

            if (autoShowDuringScenario)
            {
                ShowWidget();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[FloodWarningProgressDisplay] Progression started → Peak: {progression.PeakLevel}, Phases: {progression.Phases.Count}");
            }
        }

        private void HandleWarningLevelChanged(RainfallWarningLevel previousLevel, RainfallWarningLevel newLevel)
        {
            currentLevel = newLevel;
            currentPhaseIndex++;

            // Update current level display
            if (currentLevelValue != null)
            {
                string levelName = GetLevelName(newLevel);
                currentLevelValue.text = levelName;
                currentLevelValue.style.color = new StyleColor(GetLevelColor(newLevel));
                lastLevelText = levelName;
            }

            // Update level indicators
            UpdateLevelIndicators();

            if (enableDebugLogs)
            {
                Debug.Log($"[FloodWarningProgressDisplay] Level changed → {previousLevel} to {newLevel}");
            }
        }

        /// <summary>
        /// Handle real-time progress updates from FloodScenarioManager.
        /// CRITICAL: Stops progress bar at 100% when sustained phase is reached.
        /// </summary>
        private void HandleProgressUpdated(FloodScenarioProgress progress)
        {
            if (!isScenarioActive || currentProgression == null) return;

            // CRITICAL: Stop updating progress bar when sustain phase reached
            if (progress.Phase == FloodScenarioPhase.Sustained)
            {
                // Clamp progress bar at 100%
                if (progressFill != null && lastProgressValue != 100f)
                {
                    progressFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                    lastProgressValue = 100f;

                    if (enableDebugLogs)
                    {
                        Debug.Log("[FloodWarningProgressDisplay] ✓ Progress bar clamped at 100% (sustained phase reached)");
                    }
                }

                // Show "PEAK REACHED" in red
                if (countdownLabel != null && lastCountdownText != "PEAK REACHED")
                {
                    countdownLabel.text = "PEAK REACHED";
                    countdownLabel.style.color = new StyleColor(Color.red);
                    lastCountdownText = "PEAK REACHED";
                }

                // Stop Update() from continuing to animate
                isScenarioActive = false;
                return;
            }
        }

        /// <summary>
        /// Update progress UI elements (countdown, progress bar).
        /// Throttled to 10 FPS via Update() check.
        /// </summary>
        private void UpdateProgressUI()
        {
            if (currentProgression == null || currentPhaseIndex >= currentProgression.Phases.Count) return;

            // Calculate elapsed time and current phase info (used throughout method)
            float elapsed = Time.time - scenarioStartTime;
            var currentPhase = currentProgression.Phases[currentPhaseIndex];
            float phaseEndTime = currentPhase.StartTime + currentPhase.Duration;

            // SAFETY CHECK: Stop updating if peak reached (belt-and-suspenders with HandleProgressUpdated)
            if (currentPhaseIndex >= currentProgression.Phases.Count - 1)
            {
                // If we've passed the last phase's end time, clamp to 100% and stop
                if (elapsed >= phaseEndTime)
                {
                    if (progressFill != null && lastProgressValue != 100f)
                    {
                        progressFill.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
                        lastProgressValue = 100f;
                    }

                    if (countdownLabel != null && lastCountdownText != "PEAK REACHED")
                    {
                        countdownLabel.text = "PEAK REACHED";
                        countdownLabel.style.color = new StyleColor(Color.red);
                        lastCountdownText = "PEAK REACHED";
                    }

                    return; // Stop updating
                }
            }

            // Update current level (if not already set)
            if (currentLevelValue != null && string.IsNullOrEmpty(lastLevelText))
            {
                string levelName = GetLevelName(currentLevel);
                currentLevelValue.text = levelName;
                currentLevelValue.style.color = new StyleColor(GetLevelColor(currentLevel));
                lastLevelText = levelName;
            }

            // Calculate countdown to next level
            float timeToNextLevel = phaseEndTime - elapsed;

            if (countdownLabel != null)
            {
                if (currentPhaseIndex < currentProgression.Phases.Count - 1 && timeToNextLevel > 0f)
                {
                    // Show countdown to next level
                    string countdownText = $"Next: {Mathf.CeilToInt(timeToNextLevel)}s";
                    if (countdownText != lastCountdownText)
                    {
                        countdownLabel.text = countdownText;
                        lastCountdownText = countdownText;
                    }
                }
                else
                {
                    // At peak level
                    string countdownText = "PEAK REACHED";
                    if (countdownText != lastCountdownText)
                    {
                        countdownLabel.text = countdownText;
                        countdownLabel.style.color = new StyleColor(Color.red);
                        lastCountdownText = countdownText;
                    }
                }
            }

            // Update progress bar (0-100%)
            if (progressFill != null)
            {
                float totalDuration = GetTotalDuration(currentProgression);
                float progressValue = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, totalDuration)) * 100f;

                // Only update if value changed significantly (avoid redundant style updates)
                if (Mathf.Abs(progressValue - lastProgressValue) > 0.5f)
                {
                    progressFill.style.width = new StyleLength(new Length(progressValue, LengthUnit.Percent));
                    lastProgressValue = progressValue;
                }
            }
        }

        /// <summary>
        /// Update level indicators (Yellow/Orange/Red badges).
        /// Highlights completed and current levels, grays out future levels.
        /// </summary>
        private void UpdateLevelIndicators()
        {
            if (currentProgression == null) return;

            // Yellow indicator (always completed or current)
            if (yellowIndicator != null)
            {
                bool isActive = currentLevel >= RainfallWarningLevel.Yellow;
                yellowIndicator.style.opacity = isActive ? 1f : 0.3f;
                if (currentLevel == RainfallWarningLevel.Yellow)
                {
                    yellowIndicator.AddToClassList("indicator--current");
                }
                else
                {
                    yellowIndicator.RemoveFromClassList("indicator--current");
                }
            }

            // Orange indicator
            if (orangeIndicator != null)
            {
                bool hasOrange = currentProgression.PeakLevel >= RainfallWarningLevel.Orange;
                bool isActive = currentLevel >= RainfallWarningLevel.Orange;
                orangeIndicator.style.opacity = isActive ? 1f : (hasOrange ? 0.3f : 0.15f);
                orangeIndicator.style.display = hasOrange ? DisplayStyle.Flex : DisplayStyle.None;

                if (currentLevel == RainfallWarningLevel.Orange)
                {
                    orangeIndicator.AddToClassList("indicator--current");
                }
                else
                {
                    orangeIndicator.RemoveFromClassList("indicator--current");
                }
            }

            // Red indicator
            if (redIndicator != null)
            {
                bool hasRed = currentProgression.PeakLevel == RainfallWarningLevel.Red;
                bool isActive = currentLevel == RainfallWarningLevel.Red;
                redIndicator.style.opacity = isActive ? 1f : (hasRed ? 0.3f : 0.15f);
                redIndicator.style.display = hasRed ? DisplayStyle.Flex : DisplayStyle.None;

                if (isActive)
                {
                    redIndicator.AddToClassList("indicator--current");
                }
                else
                {
                    redIndicator.RemoveFromClassList("indicator--current");
                }
            }
        }

        private void ShowWidget()
        {
            if (progressWidget != null)
            {
                progressWidget.style.display = DisplayStyle.Flex;
            }
        }

        private void HideWidget()
        {
            if (progressWidget != null)
            {
                progressWidget.style.display = DisplayStyle.None;
            }

            // Reset cached values
            lastLevelText = "";
            lastCountdownText = "";
            lastProgressValue = -1f;
            isScenarioActive = false;
        }

        // ========== HELPER METHODS ==========

        private float GetTotalDuration(FloodWarningProgression progression)
        {
            float total = 0f;
            foreach (var phase in progression.Phases)
            {
                total += phase.Duration;
            }
            return total;
        }

        private string GetLevelName(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow: return "YELLOW";
                case RainfallWarningLevel.Orange: return "ORANGE";
                case RainfallWarningLevel.Red: return "RED";
                default: return "NONE";
            }
        }

        private Color GetLevelColor(RainfallWarningLevel level)
        {
            switch (level)
            {
                case RainfallWarningLevel.Yellow:
                    return new Color(1f, 0.92f, 0.016f, 1f); // Bright yellow #FFEB04

                case RainfallWarningLevel.Orange:
                    return new Color(1f, 0.6f, 0f, 1f); // Orange #FF9900

                case RainfallWarningLevel.Red:
                    return new Color(0.9f, 0.1f, 0.1f, 1f); // Bright red #E61A1A

                default:
                    return Color.white;
            }
        }
    }
}
