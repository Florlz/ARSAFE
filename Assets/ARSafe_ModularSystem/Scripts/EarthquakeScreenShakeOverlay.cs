/*
 * PURPOSE: Visual screen shake overlay effect for earthquake simulation on mobile devices
 *
 * DEPENDENCIES:
 *   - Unity UI Toolkit (UIDocument, VisualElement)
 *   - EarthquakeScenarioManager (scenario parameters and progress)
 *   - DisasterTypeManager (disaster type detection)
 *
 * DATA FLOW:
 *   - EarthquakeScenarioManager events → Update shake intensity → Animate UI transform
 *
 * PERFORMANCE:
 *   - Throttled to 30 FPS (mobile-optimized)
 *   - Uses UI Toolkit translate transform (hardware accelerated)
 *   - Perlin noise for smooth shake motion
 *
 * USAGE:
 *   - Attach to a GameObject with UIDocument component
 *   - Create a fullscreen overlay visual element in UXML
 *   - Configure shake parameters in Inspector
 */

using UnityEngine;
using UnityEngine.UIElements;

namespace ARSafe.Modular
{
    /// <summary>
    /// Creates a visual screen shake effect using UI Toolkit overlay.
    /// More visible than camera shake on mobile AR devices.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class EarthquakeScreenShakeOverlay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        [Tooltip("UIDocument component. Auto-found if not assigned.")]
        private UIDocument uiDocument;

        [SerializeField]
        [Tooltip("Name of the root overlay element in UXML. Leave empty to create dynamically.")]
        private string overlayElementName = "earthquake-shake-overlay";

        [Header("Shake Intensity")]
        [Tooltip("Maximum horizontal shake distance (pixels).")]
        [Range(0f, 100f)]
        public float maxHorizontalShake = 30f;

        [Tooltip("Maximum vertical shake distance (pixels).")]
        [Range(0f, 100f)]
        public float maxVerticalShake = 25f;

        [Tooltip("Shake frequency multiplier.")]
        [Range(0.1f, 10f)]
        public float shakeFrequency = 3.5f;

        [Tooltip("Extra multiplier for mobile devices.")]
        [Range(1f, 5f)]
        public float mobileIntensityBoost = 2.5f;

        [Header("Vignette Effect")]
        [Tooltip("Enable pulsing vignette effect during shake.")]
        public bool enableVignette = true;

        [Tooltip("Maximum vignette darkness (0-1).")]
        [Range(0f, 0.8f)]
        public float maxVignetteDarkness = 0.3f;

        [Tooltip("Vignette pulse frequency.")]
        [Range(0.1f, 5f)]
        public float vignettePulseFrequency = 1.5f;

        [Header("Performance")]
        [Tooltip("Update rate for shake effect (FPS). Lower = better performance.")]
        [Range(15f, 60f)]
        public float updateFPS = 30f;

        // State
        private VisualElement overlayElement;
        private VisualElement vignetteElement;
        private bool isShaking;
        private float intensity;
        private float seedX;
        private float seedY;
        private float updateInterval;
        private float lastUpdateTime;
        private float platformBoost = 1f;

        void Awake()
        {
            // Auto-find UIDocument
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            // Random seeds for Perlin noise
            seedX = Random.value * 1000f;
            seedY = Random.value * 1000f + 500f;

            // Platform detection
            if (Application.isMobilePlatform)
            {
                platformBoost = mobileIntensityBoost;
            }

            updateInterval = 1f / updateFPS;
        }

        void OnEnable()
        {
            InitializeUI();

            // Subscribe to earthquake events
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
            EarthquakeScenarioManager.OnProgressUpdated += HandleScenarioProgress;

            // Check initial state
            HandleInitialState();
        }

        void OnDisable()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleScenarioProgress;

            StopShake();
        }

        void Update()
        {
            if (!isShaking || intensity <= 0.001f)
            {
                return;
            }

            // Throttle updates for performance
            if (Time.time - lastUpdateTime < updateInterval)
            {
                return;
            }
            lastUpdateTime = Time.time;

            // Calculate shake offset using Perlin noise
            float time = Time.time * shakeFrequency;
            float offsetX = (Mathf.PerlinNoise(seedX, time) - 0.5f) * 2f * maxHorizontalShake * intensity * platformBoost;
            float offsetY = (Mathf.PerlinNoise(seedY, time * 1.3f) - 0.5f) * 2f * maxVerticalShake * intensity * platformBoost;

            // Apply shake via translate transform
            if (overlayElement != null)
            {
                overlayElement.style.translate = new Translate(new Length(offsetX, LengthUnit.Pixel), new Length(offsetY, LengthUnit.Pixel));
            }

            // Update vignette opacity
            if (enableVignette && vignetteElement != null)
            {
                float vignetteTime = Time.time * vignettePulseFrequency;
                float vignettePulse = (Mathf.PerlinNoise(seedX + 100f, vignetteTime) * 0.5f + 0.5f);
                float vignetteOpacity = maxVignetteDarkness * intensity * vignettePulse;
                vignetteElement.style.opacity = vignetteOpacity;
            }
        }

        private void InitializeUI()
        {
            if (uiDocument == null)
            {
                Debug.LogError("[EarthquakeScreenShakeOverlay] UIDocument not found!");
                return;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[EarthquakeScreenShakeOverlay] Root visual element is null!");
                return;
            }

            // Try to find existing overlay element
            if (!string.IsNullOrEmpty(overlayElementName))
            {
                overlayElement = root.Q<VisualElement>(overlayElementName);
            }

            // Create overlay dynamically if not found
            if (overlayElement == null)
            {
                overlayElement = new VisualElement();
                overlayElement.name = overlayElementName;
                overlayElement.style.position = Position.Absolute;
                overlayElement.style.top = 0;
                overlayElement.style.left = 0;
                overlayElement.style.right = 0;
                overlayElement.style.bottom = 0;
                overlayElement.pickingMode = PickingMode.Ignore; // Don't block input
                root.Add(overlayElement);
                Debug.Log("[EarthquakeScreenShakeOverlay] Created dynamic overlay element");
            }

            // Create vignette element if enabled
            if (enableVignette)
            {
                vignetteElement = new VisualElement();
                vignetteElement.name = "earthquake-vignette";
                vignetteElement.style.position = Position.Absolute;
                vignetteElement.style.top = 0;
                vignetteElement.style.left = 0;
                vignetteElement.style.right = 0;
                vignetteElement.style.bottom = 0;
                vignetteElement.style.backgroundColor = new Color(0, 0, 0, 1);
                vignetteElement.style.opacity = 0;
                vignetteElement.pickingMode = PickingMode.Ignore;
                overlayElement.Add(vignetteElement);
            }
        }

        private void HandleInitialState()
        {
            if (DisasterTypeManager.SelectedDisasterType == DisasterType.Earthquake)
            {
                var progress = EarthquakeScenarioManager.CurrentProgress;
                if (progress.IsActive)
                {
                    StartShake(progress.NormalizedTime);
                }
            }
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType == DisasterType.Earthquake)
            {
                // Wait for progress events to start shake
                return;
            }
            else
            {
                StopShake();
            }
        }

        private void HandleScenarioProgress(EarthquakeScenarioProgress progress)
        {
            if (!enabled)
            {
                return;
            }

            if (!progress.IsActive)
            {
                StopShake();
                return;
            }

            // Update intensity based on normalized time
            intensity = EvaluateProgressIntensity(progress.NormalizedTime);

            if (!isShaking && intensity > 0.001f)
            {
                StartShake(progress.NormalizedTime);
            }
        }

        private float EvaluateProgressIntensity(float normalizedTime)
        {
            // Ramp up in first 25%, full intensity in middle, fade out in last 20%
            float rampPortion = 0.25f;
            float fadePortion = 0.2f;

            if (normalizedTime < rampPortion)
            {
                return Mathf.Clamp01(normalizedTime / rampPortion);
            }
            else if (normalizedTime > 1f - fadePortion)
            {
                float fadeT = Mathf.InverseLerp(1f - fadePortion, 1f, normalizedTime);
                return Mathf.Lerp(1f, 0f, fadeT);
            }
            else
            {
                return 1f;
            }
        }

        private void StartShake(float normalizedTime)
        {
            isShaking = true;
            intensity = EvaluateProgressIntensity(normalizedTime);
            lastUpdateTime = Time.time;
            Debug.Log($"<color=cyan>[EarthquakeScreenShakeOverlay] Started shake (intensity: {intensity:F2})</color>");
        }

        private void StopShake()
        {
            isShaking = false;
            intensity = 0f;

            // Reset overlay position
            if (overlayElement != null)
            {
                overlayElement.style.translate = new Translate(0, 0);
            }

            // Hide vignette
            if (vignetteElement != null)
            {
                vignetteElement.style.opacity = 0;
            }
        }
    }
}
