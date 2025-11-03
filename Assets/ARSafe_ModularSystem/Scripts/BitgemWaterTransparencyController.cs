/*
 * PURPOSE: Control Bitgem water transparency and visual properties for realistic flood simulation
 *
 * DEPENDENCIES:
 *   - Bitgem StylisedWater package (WaterVolumeBox)
 *   - FloodScenarioManager (flood intensity events)
 *
 * DATA FLOW:
 *   - FloodScenarioManager events → Update water transparency/color → Apply to material
 *
 * PERFORMANCE:
 *   - Uses MaterialPropertyBlock to avoid material instantiation
 *   - Cached property IDs
 *
 * USAGE:
 *   - Attach to same GameObject as BitgemFloodWaterPerTarget
 *   - Configure transparency settings in Inspector
 *   - Auto-applies on flood scenario events
 */

using UnityEngine;
using Bitgem.VFX.StylisedWater;

namespace ARSafe.Modular
{
    /// <summary>
    /// Makes Bitgem water look transparent instead of solid by controlling
    /// shader properties like opacity, absorption, and fresnel.
    /// </summary>
    [RequireComponent(typeof(WaterVolumeBox))]
    public class BitgemWaterTransparencyController : MonoBehaviour
    {
        [Header("Water Volume Reference")]
        [SerializeField]
        [Tooltip("WaterVolumeBox component. Auto-found if not assigned.")]
        private WaterVolumeBox waterVolume;

        [Header("Transparency Settings")]
        [Tooltip("Base water opacity (0 = fully transparent, 1 = opaque). Lower = more see-through.")]
        [Range(0f, 1f)]
        [SerializeField] private float baseOpacity = 0.4f;

        [Tooltip("Maximum opacity during peak flood intensity.")]
        [Range(0f, 1f)]
        [SerializeField] private float maxOpacity = 0.7f;

        [Tooltip("Opacity when viewing water at grazing angles (fresnel). Lower = more transparent edges.")]
        [Range(0f, 1f)]
        [SerializeField] private float fresnelOpacity = 0.2f;

        [Header("Water Depth & Absorption")]
        [Tooltip("How quickly light is absorbed by water depth. Lower = more transparent/clear.")]
        [Range(0f, 5f)]
        [SerializeField] private float absorptionDepth = 1.5f;

        [Tooltip("Water color at shallow depths (more transparent look).")]
        [SerializeField] private Color shallowWaterColor = new Color(0.4f, 0.6f, 0.7f, 0.3f);

        [Tooltip("Water color at deep depths (slightly more opaque).")]
        [SerializeField] private Color deepWaterColor = new Color(0.2f, 0.4f, 0.5f, 0.6f);

        [Header("Surface Details")]
        [Tooltip("Normal map strength. Higher = more visible waves/ripples.")]
        [Range(0f, 2f)]
        [SerializeField] private float normalStrength = 0.8f;

        [Tooltip("Smoothness of water surface (0 = rough, 1 = mirror-like).")]
        [Range(0f, 1f)]
        [SerializeField] private float smoothness = 0.9f;

        [Tooltip("Specular reflection intensity (sunlight glints).")]
        [Range(0f, 1f)]
        [SerializeField] private float specularIntensity = 0.5f;

        [Header("Refraction & Distortion")]
        [Tooltip("Strength of underwater distortion effect. Makes it look like real water.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float refractionStrength = 0.15f;

        [Tooltip("Enable refraction/distortion for realistic underwater view.")]
        [SerializeField] private bool enableRefraction = true;

        [Header("Foam & Edge Fade")]
        [Tooltip("Foam opacity at water edges.")]
        [Range(0f, 1f)]
        [SerializeField] private float foamOpacity = 0.6f;

        [Header("Flood Integration")]
        [Tooltip("Make water more opaque/muddy during high flood intensity.")]
        [SerializeField] private bool scaleOpacityWithFloodIntensity = true;

        [Tooltip("Muddy water color used during peak flood.")]
        [SerializeField] private Color muddyWaterColor = new Color(0.3f, 0.25f, 0.2f, 0.8f);

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Cached references
        private Material waterMaterial;
        private MaterialPropertyBlock propertyBlock;
        private Renderer waterRenderer;

        // Shader property IDs (common Bitgem water shader properties)
        private readonly int opacityId = Shader.PropertyToID("_Opacity");
        private readonly int fresnelPowerId = Shader.PropertyToID("_FresnelPower");
        private readonly int absorptionId = Shader.PropertyToID("_Absorption");
        private readonly int shallowColorId = Shader.PropertyToID("_ShallowColor");
        private readonly int deepColorId = Shader.PropertyToID("_DeepColor");
        private readonly int normalStrengthId = Shader.PropertyToID("_NormalStrength");
        private readonly int smoothnessId = Shader.PropertyToID("_Smoothness");
        private readonly int specularId = Shader.PropertyToID("_Specular");
        private readonly int refractionId = Shader.PropertyToID("_Refraction");
        private readonly int foamOpacityId = Shader.PropertyToID("_FoamOpacity");

        // Runtime state
        private float currentOpacity;
        private float currentFloodIntensity;

        void Reset()
        {
            waterVolume = GetComponent<WaterVolumeBox>();
        }

        void Awake()
        {
            if (waterVolume == null)
            {
                waterVolume = GetComponent<WaterVolumeBox>();
            }

            if (waterVolume == null)
            {
                Debug.LogError("[BitgemTransparency] No WaterVolumeBox found! This component requires WaterVolumeBox.");
                enabled = false;
                return;
            }

            // Get renderer from water volume
            waterRenderer = waterVolume.GetComponent<Renderer>();
            if (waterRenderer != null)
            {
                waterMaterial = waterRenderer.sharedMaterial;
            }

            propertyBlock = new MaterialPropertyBlock();
            currentOpacity = baseOpacity;
        }

        void OnEnable()
        {
            // Subscribe to flood events
            FloodScenarioManager.OnProgressUpdated += HandleFloodProgress;

            // Apply initial transparency
            ApplyTransparencySettings();
        }

        void OnDisable()
        {
            FloodScenarioManager.OnProgressUpdated -= HandleFloodProgress;
        }

        private void HandleFloodProgress(FloodScenarioProgress progress)
        {
            if (!enabled) return;

            // Update opacity based on flood intensity (use OverallNormalized for entire scenario progress)
            currentFloodIntensity = progress.OverallNormalized;

            if (scaleOpacityWithFloodIntensity && progress.IsActive)
            {
                // More opaque during peak flood (muddy water)
                currentOpacity = Mathf.Lerp(baseOpacity, maxOpacity, currentFloodIntensity);

                // Blend towards muddy color during peak
                Color currentShallow = Color.Lerp(shallowWaterColor, muddyWaterColor, currentFloodIntensity * 0.5f);
                Color currentDeep = Color.Lerp(deepWaterColor, muddyWaterColor, currentFloodIntensity * 0.7f);

                ApplyTransparencySettings(currentShallow, currentDeep);
            }
            else
            {
                currentOpacity = baseOpacity;
                ApplyTransparencySettings();
            }
        }

        /// <summary>
        /// Apply transparency settings to water material via property block.
        /// </summary>
        public void ApplyTransparencySettings()
        {
            ApplyTransparencySettings(shallowWaterColor, deepWaterColor);
        }

        /// <summary>
        /// Apply transparency settings with custom colors.
        /// </summary>
        public void ApplyTransparencySettings(Color shallow, Color deep)
        {
            if (waterRenderer == null || propertyBlock == null)
            {
                return;
            }

            // Get existing properties
            waterRenderer.GetPropertyBlock(propertyBlock);

            // Set transparency properties
            propertyBlock.SetFloat(opacityId, currentOpacity);
            propertyBlock.SetFloat(fresnelPowerId, Mathf.Lerp(2f, 5f, fresnelOpacity)); // Fresnel power (higher = more transparent edges)
            propertyBlock.SetFloat(absorptionId, absorptionDepth);
            propertyBlock.SetColor(shallowColorId, shallow);
            propertyBlock.SetColor(deepColorId, deep);
            propertyBlock.SetFloat(normalStrengthId, normalStrength);
            propertyBlock.SetFloat(smoothnessId, smoothness);
            propertyBlock.SetFloat(specularId, specularIntensity);
            propertyBlock.SetFloat(refractionId, enableRefraction ? refractionStrength : 0f);
            propertyBlock.SetFloat(foamOpacityId, foamOpacity);

            // Apply property block
            waterRenderer.SetPropertyBlock(propertyBlock);

            if (enableDebugLogs)
            {
                Debug.Log($"<color=cyan>[BitgemTransparency] Applied: opacity={currentOpacity:F2}, absorption={absorptionDepth:F2}, refraction={refractionStrength:F2}</color>");
            }
        }

        /// <summary>
        /// Set base opacity manually (useful for runtime control).
        /// </summary>
        public void SetBaseOpacity(float opacity)
        {
            baseOpacity = Mathf.Clamp01(opacity);
            currentOpacity = baseOpacity;
            ApplyTransparencySettings();
        }

        /// <summary>
        /// Make water more transparent (for testing/debugging).
        /// </summary>
        [ContextMenu("Make More Transparent")]
        public void MakeMoreTransparent()
        {
            baseOpacity = Mathf.Max(0f, baseOpacity - 0.1f);
            SetBaseOpacity(baseOpacity);
            Debug.Log($"[BitgemTransparency] Reduced opacity to {baseOpacity:F2}");
        }

        /// <summary>
        /// Make water more opaque (for testing/debugging).
        /// </summary>
        [ContextMenu("Make More Opaque")]
        public void MakeMoreOpaque()
        {
            baseOpacity = Mathf.Min(1f, baseOpacity + 0.1f);
            SetBaseOpacity(baseOpacity);
            Debug.Log($"[BitgemTransparency] Increased opacity to {baseOpacity:F2}");
        }
    }
}
