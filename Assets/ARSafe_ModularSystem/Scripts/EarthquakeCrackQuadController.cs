using UnityEngine;

namespace ARSafe.Modular
{
    /// <summary>
    /// Formation style for earthquake crack appearance.
    /// </summary>
    public enum CrackFormationStyle
    {
        RadialSpread,      // Crack spreads outward from origin point (realistic for ground cracks)
        DirectionalWipe,   // Crack wipes in from one direction
        InstantAppear      // Crack appears instantly with fade only
    }

    /// <summary>
    /// Displays an earthquake crack using a standard 3D Quad + Material.
    /// Fades in during the earthquake scenario and remains visible after completion.
    /// Optionally billboards the quad to the AR camera.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class EarthquakeCrackQuadController : MonoBehaviour
    {
    public enum QuadBillboardMode
    {
        None,              // Fixed position - crack stays where placed (RECOMMENDED for ground cracks)
        LookAtCamera,      // Full billboard - always faces camera
        ConstrainedBillboard // Keep Y axis vertical while facing camera
    }        [Header("Material Setup")]
        [Tooltip("Optional override; if null, uses the MeshRenderer's material.")]
        [SerializeField] private Material materialOverride;

        [Tooltip("Color property name used for alpha control. Common: _BaseColor (URP), _Color (legacy/unlit)")]
        [SerializeField] private string colorPropertyName = "_BaseColor";

        [Tooltip("Fallback color property if the first is missing.")]
        [SerializeField] private string fallbackColorPropertyName = "_Color";

        [Tooltip("Use MaterialPropertyBlock to avoid creating unique material instances at runtime.")]
        [SerializeField] private bool usePropertyBlock = true;

        [Header("Billboard")]
        [Tooltip("IMPORTANT: Use 'None' for ground/wall cracks that should stay in place. Only use LookAtCamera for floating effects.")]
        [SerializeField] private QuadBillboardMode billboardMode = QuadBillboardMode.None;
        [SerializeField] private Camera arCamera;

        [Header("Timeline")] 
        [Tooltip("Normalized progress when crack starts appearing.")]
        [Range(0f, 1f)] [SerializeField] private float appearStart = 0.10f;
        [Tooltip("Duration (normalized) of the fade-in.")]
        [Range(0.05f, 0.8f)] [SerializeField] private float appearDuration = 0.35f;
        [Tooltip("Keep visible after the earthquake ends.")]
        [SerializeField] private bool persistAfterCompletion = true;

        [Header("Crack Formation Animation")] 
        [Tooltip("Crack spreading style - how the crack forms and grows.")]
        [SerializeField] private CrackFormationStyle formationStyle = CrackFormationStyle.RadialSpread;
        
        [Header("Spreading Animation")]
        [Tooltip("Starting point of crack (0,0 = center, -0.5 to 0.5 range). For ground cracks, use bottom (-0.5 on Y).")]
        [SerializeField] private Vector2 crackOrigin = new Vector2(0f, -0.5f);
        [Tooltip("How quickly the crack spreads from origin.")]
        [SerializeField] private AnimationCurve spreadCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Maximum spread distance (in quad local units).")]
        [Range(0.5f, 2f)] [SerializeField] private float maxSpreadRadius = 1.2f;
        
        [Header("Scale Animation")]
        [Tooltip("Scale in from this fraction while appearing (set to 1 to disable).")]
        [Range(0f, 1f)] [SerializeField] private float startScale = 0.85f;
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Apply non-uniform scale to simulate crack direction.")]
        [SerializeField] private bool nonUniformScale = true;
        [Tooltip("Scale multiplier for X-axis (crack length).")]
        [Range(0.5f, 2f)] [SerializeField] private float scaleXMultiplier = 1.2f;
        [Tooltip("Scale multiplier for Y-axis (crack width).")]
        [Range(0.5f, 2f)] [SerializeField] private float scaleYMultiplier = 1.0f;
        
        [Header("Fade Animation")]
        [Tooltip("Fade curve for alpha during formation.")]
        [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Debug")] 
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField] private bool forceAlwaysVisible = false;

        private MeshRenderer meshRenderer;
        private Material targetMaterial;
        private MaterialPropertyBlock mpb;
        private int colorID;
        private bool hasColorProperty;
        private Color baseColor;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private bool initialized;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                Debug.LogError("[EarthquakeCrackQuad] No MeshRenderer found.", this);
                enabled = false;
                return;
            }

            // Determine target material
            targetMaterial = materialOverride != null ? materialOverride : meshRenderer.sharedMaterial;
            if (targetMaterial == null)
            {
                Debug.LogError("[EarthquakeCrackQuad] No material assigned on MeshRenderer.", this);
                enabled = false;
                return;
            }

            // Resolve color property
            hasColorProperty = false;
            if (targetMaterial.HasProperty(colorPropertyName))
            {
                colorID = Shader.PropertyToID(colorPropertyName);
                hasColorProperty = true;
            }
            else if (!string.IsNullOrEmpty(fallbackColorPropertyName) && targetMaterial.HasProperty(fallbackColorPropertyName))
            {
                colorID = Shader.PropertyToID(fallbackColorPropertyName);
                hasColorProperty = true;
            }

            // Read base color
            baseColor = hasColorProperty ? targetMaterial.GetColor(colorID) : (meshRenderer.sharedMaterial != null ? meshRenderer.sharedMaterial.color : Color.white);
            if (baseColor.a <= 0f) baseColor.a = 1f;

            // Property block setup
            if (usePropertyBlock)
            {
                mpb = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(mpb);
            }

            // Camera
            if (arCamera == null) arCamera = Camera.main;

            baseScale = transform.localScale;
            baseRotation = transform.localRotation;

            // Initialize visibility
            ApplyAlpha(forceAlwaysVisible ? 1f : 0f);
            meshRenderer.enabled = forceAlwaysVisible;

            initialized = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[EarthquakeCrackQuad] Init - Mat={targetMaterial.name}, ColorProp={(hasColorProperty ? colorID.ToString() : "none")}, AlphaStart={(forceAlwaysVisible ? 1f : 0f)}", this);
            }
        }

        private void OnEnable()
        {
            EarthquakeScenarioManager.OnProgressUpdated += HandleProgress;
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterChanged;

            if (initialized)
            {
                HandleDisasterChanged(DisasterTypeManager.SelectedDisasterType);
                HandleProgress(EarthquakeScenarioManager.CurrentProgress);
            }
        }

        private void OnDisable()
        {
            EarthquakeScenarioManager.OnProgressUpdated -= HandleProgress;
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterChanged;
        }

        private void LateUpdate()
        {
            if (billboardMode == QuadBillboardMode.None || arCamera == null || !meshRenderer.enabled)
                return;

            switch (billboardMode)
            {
                case QuadBillboardMode.LookAtCamera:
                {
                    var toCam = arCamera.transform.position - transform.position;
                    if (toCam.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
                    break;
                }
                case QuadBillboardMode.ConstrainedBillboard:
                {
                    var toCamXZ = arCamera.transform.position - transform.position;
                    toCamXZ.y = 0f;
                    if (toCamXZ.sqrMagnitude > 0.0001f)
                        transform.rotation = Quaternion.LookRotation(toCamXZ.normalized, Vector3.up);
                    break;
                }
            }
        }

        private void HandleDisasterChanged(DisasterType type)
        {
            // Only show for earthquake selection (unless forceAlwaysVisible)
            if (forceAlwaysVisible) return;

            bool isEarthquake = type == DisasterType.Earthquake;
            if (!isEarthquake)
            {
                ApplyAlpha(0f);
                meshRenderer.enabled = false;
                if (enableDebugLogs) Debug.Log("[EarthquakeCrackQuad] Hidden because disaster is not Earthquake.", this);
            }
        }

        private void HandleProgress(EarthquakeScenarioProgress progress)
        {
            if (meshRenderer == null) return;

            // Overrides
            if (forceAlwaysVisible)
            {
                ApplyAlpha(1f);
                meshRenderer.enabled = true;
                return;
            }

            // If the current disaster isn't earthquake, keep hidden
            if (DisasterTypeManager.SelectedDisasterType != DisasterType.Earthquake)
            {
                ApplyAlpha(0f);
                meshRenderer.enabled = false;
                return;
            }

            float alpha = 0f;
            float scale = 1f;

            if (progress.IsActive)
            {
                float t0 = Mathf.Clamp01(appearStart);
                float t1 = Mathf.Clamp01(appearStart + Mathf.Max(0.01f, appearDuration));
                float t = Mathf.InverseLerp(t0, t1, progress.NormalizedTime);
                t = Mathf.Clamp01(t);

                // Base alpha from fade curve
                alpha = fadeCurve != null ? fadeCurve.Evaluate(t) : t;
                
                // Base scale animation
                float sCurve = scaleCurve != null ? scaleCurve.Evaluate(t) : t;
                scale = Mathf.Lerp(Mathf.Clamp01(startScale), 1f, sCurve);
                
                // Apply non-uniform scaling (directional crack growth)
                if (nonUniformScale)
                {
                    Vector3 scaleVec = baseScale;
                    scaleVec.x *= scale * scaleXMultiplier;
                    scaleVec.y *= scale * scaleYMultiplier;
                    scaleVec.z *= scale;
                    transform.localScale = scaleVec;
                }
                else
                {
                    transform.localScale = baseScale * scale;
                }
                
                // Keep rotation stable (no jitter)
                transform.localRotation = baseRotation;
                
                // Apply crack spreading effect based on formation style
                ApplyCrackFormation(t);
            }
            else if (progress.IsComplete)
            {
                alpha = persistAfterCompletion ? 1f : 0f;
                scale = 1f;
                
                // Reset to base transform
                if (nonUniformScale)
                {
                    Vector3 scaleVec = baseScale;
                    scaleVec.x *= scaleXMultiplier;
                    scaleVec.y *= scaleYMultiplier;
                    transform.localScale = scaleVec;
                }
                else
                {
                    transform.localScale = baseScale * scale;
                }
                transform.localRotation = baseRotation;
                
                ApplyCrackFormation(1f); // Full reveal
            }
            else
            {
                // Inactive before scenario starts
                alpha = 0f;
                scale = startScale <= 0f ? 1f : startScale;
                transform.localScale = baseScale * scale;
                transform.localRotation = baseRotation;
                
                ApplyCrackFormation(0f);
            }

            ApplyAlpha(alpha);

            bool visible = alpha > 0.001f;
            if (meshRenderer.enabled != visible)
                meshRenderer.enabled = visible;

            if (enableDebugLogs)
            {
                Debug.Log($"[EarthquakeCrackQuad] progress: active={progress.IsActive}, complete={progress.IsComplete}, t={progress.NormalizedTime:F2}, alpha={alpha:F2}, visible={visible}", this);
            }
        }

        private void ApplyAlpha(float a)
        {
            a = Mathf.Clamp01(a);
            if (hasColorProperty)
            {
                Color c = baseColor;
                c.a = a;
                if (usePropertyBlock)
                {
                    meshRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor(colorID, c);
                    meshRenderer.SetPropertyBlock(mpb);
                }
                else
                {
                    // This path changes the shared material instance; prefer MPB where possible
                    targetMaterial.SetColor(colorID, c);
                }
            }
            else
            {
                // Fallback to renderer material color
                if (usePropertyBlock)
                {
                    meshRenderer.GetPropertyBlock(mpb);
                    Color c = mpb.HasVector(_ColorID) ? mpb.GetVector(_ColorID) : (meshRenderer.material != null ? meshRenderer.material.color : Color.white);
                    c.a = a;
                    mpb.SetColor(_ColorID, c);
                    meshRenderer.SetPropertyBlock(mpb);
                }
                else if (meshRenderer.material != null)
                {
                    var c = meshRenderer.material.color;
                    c.a = a;
                    meshRenderer.material.color = c;
                }
            }
        }

        private static readonly int _ColorID = Shader.PropertyToID("_Color");

        /// <summary>
        /// Apply crack formation effect based on selected style.
        /// Simulates realistic crack spreading from an origin point.
        /// </summary>
        private void ApplyCrackFormation(float progress)
        {
            if (meshRenderer == null)
                return;

            float spreadT = spreadCurve != null ? spreadCurve.Evaluate(progress) : progress;

            switch (formationStyle)
            {
                case CrackFormationStyle.RadialSpread:
                    ApplyRadialSpread(spreadT);
                    break;
                case CrackFormationStyle.DirectionalWipe:
                    ApplyDirectionalWipe(spreadT);
                    break;
                case CrackFormationStyle.InstantAppear:
                    // No additional effect - just use alpha/scale
                    break;
            }
        }

        /// <summary>
        /// Radial spread - crack appears to grow outward from an origin point.
        /// Perfect for ground cracks that spread from an epicenter.
        /// </summary>
        private void ApplyRadialSpread(float progress)
        {
            // Use material property block to set a custom shader parameter
            // This requires a shader that supports _SpreadProgress and _SpreadOrigin
            if (usePropertyBlock && mpb != null && hasColorProperty)
            {
                meshRenderer.GetPropertyBlock(mpb);
                
                // Set spread progress (0 to 1)
                if (targetMaterial.HasProperty("_SpreadProgress"))
                {
                    mpb.SetFloat("_SpreadProgress", progress);
                }
                
                // Set origin point in UV space (0,0 = center, need to convert from -0.5,0.5 range)
                if (targetMaterial.HasProperty("_SpreadOrigin"))
                {
                    Vector2 uvOrigin = new Vector2(crackOrigin.x + 0.5f, crackOrigin.y + 0.5f);
                    mpb.SetVector("_SpreadOrigin", new Vector4(uvOrigin.x, uvOrigin.y, maxSpreadRadius, 0));
                }
                
                meshRenderer.SetPropertyBlock(mpb);
            }
            
            // Fallback: Use simple UV tiling for basic effect
            // This creates a "zoom from origin" effect without custom shader
            else if (targetMaterial != null)
            {
                // Scale UV tiling based on progress (1 = no tiling, smaller = zoomed in)
                float uvScale = Mathf.Lerp(0.01f, 1f, progress);
                Vector2 uvOffset = crackOrigin * (1f - progress);
                
                targetMaterial.mainTextureScale = new Vector2(uvScale, uvScale);
                targetMaterial.mainTextureOffset = uvOffset;
            }
        }

        /// <summary>
        /// Directional wipe - crack appears to slide in from one direction.
        /// </summary>
        private void ApplyDirectionalWipe(float progress)
        {
            if (targetMaterial == null)
                return;

            // Crack slides in from the origin direction
            Vector2 slideOffset = -crackOrigin * (1f - progress);
            
            if (usePropertyBlock && mpb != null)
            {
                meshRenderer.GetPropertyBlock(mpb);
                mpb.SetVector(Shader.PropertyToID("_MainTex_ST"), 
                    new Vector4(1f, 1f, slideOffset.x * 0.5f, slideOffset.y * 0.5f));
                meshRenderer.SetPropertyBlock(mpb);
            }
            else
            {
                targetMaterial.mainTextureOffset = slideOffset * 0.5f;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Diagnostics/Force Show (Alpha=1)")]
        private void ContextForceShow()
        {
            forceAlwaysVisible = true;
            ApplyAlpha(1f);
            if (meshRenderer != null) meshRenderer.enabled = true;
        }

        [ContextMenu("Diagnostics/Force Hide (Alpha=0)")]
        private void ContextForceHide()
        {
            forceAlwaysVisible = false;
            ApplyAlpha(0f);
            if (meshRenderer != null) meshRenderer.enabled = false;
        }
#endif
    }
}
