using UnityEngine;

namespace ARSafe.Modular
{
    /// <summary>
    /// Applies a gradually intensifying shake to the AR camera when the Earthquake simulation is active.
    /// </summary>
    [DisallowMultipleComponent]
    public class EarthquakeCameraShake : MonoBehaviour
    {
    [Header("Shake Target")]
    [SerializeField]
    [Tooltip("Transform to receive the shake offsets. Defaults to this transform.")]
    private Transform shakeTarget;

    [Header("Shake Timing")]
        [Tooltip("Seconds to reach maximum shake intensity once the earthquake scenario starts.")]
        [Min(0.1f)]
        public float rampDuration = 10f;

    [Tooltip("Frequency multiplier for the Perlin noise driving the shake.")]
    [Range(0.1f, 8f)]
    public float noiseFrequency = 2.4f;

        [Header("Position Amplitude (Meters)")]
    [Tooltip("Initial positional shake amplitude when the earthquake begins (meters).")]
    [Range(0f, 0.5f)]
    public float initialPositionAmplitude = 0.05f;

    [Tooltip("Maximum positional shake amplitude once ramp completes (meters).")]
    [Range(0f, 1.5f)]
    public float maxPositionAmplitude = 0.5f;

        [Header("Rotation Amplitude (Degrees)")]
    [Tooltip("Initial rotational shake amplitude (degrees).")]
    [Range(0f, 15f)]
    public float initialRotationAmplitude = 2f;

    [Tooltip("Maximum rotational shake amplitude (degrees).")]
    [Range(0f, 30f)]
    public float maxRotationAmplitude = 18f;

    [Header("Mobile Visibility Tweaks")]
    [Tooltip("Extra multiplier applied on mobile devices to make shake more noticeable.")]
    [Range(1f, 8f)]
    public float mobilePlatformBoost = 4.5f;

    [Tooltip("Ensures a minimum intensity while the scenario is active so subtle shakes are still visible (0-1).")]
    [Range(0f, 1f)]
    public float minActiveStrength = 0.6f;

    [Tooltip("Additional boost applied only to rotational shake to improve perceived motion on phones.")]
    [Range(0.5f, 6f)]
    public float rotationVisibilityBoost = 3.5f;

    [Header("FOV Jitter")]
    [Tooltip("Enable modulating the Camera.fieldOfView for an extra visceral effect.")]
    public bool enableFovJitter = true;

    [Tooltip("Maximum FOV change at peak intensity (degrees).")]
    [Range(0f, 5f)]
    public float maxFovJitter = 1.2f;

    [Tooltip("Scales how fast FOV jitter changes relative to noise frequency.")]
    [Range(0.2f, 2.5f)]
    public float fovJitterFrequencyScale = 0.8f;

    [Header("Haptic Vibration (Mobile Only)")]
    [Tooltip("Enable haptic vibration on mobile devices for enhanced earthquake feedback.")]
    public bool enableHapticVibration = true;

    [Tooltip("Time between haptic vibration pulses (seconds). Lower = more frequent vibration.")]
    [Range(0.05f, 1f)]
    public float hapticPulseInterval = 0.15f;

    [Tooltip("Haptic vibration intensity scales with earthquake intensity.")]
    public bool scaleHapticWithIntensity = true;

    [Header("Audio Rumble")]
    [Tooltip("Enable low-frequency rumble that ramps with the scenario.")]
    public bool enableAudioRumble = true;

    [Tooltip("AudioSource for rumble sound (must have clip assigned in Inspector with Loop enabled).")]
    public AudioSource rumbleSource;

    [Tooltip("Base volume at minimal intensity.")]
    [Range(0f, 1f)]
    public float rumbleBaseVolume = 0.3f;

    [Tooltip("Maximum volume at peak intensity.")]
    [Range(0f, 1f)]
    public float rumbleMaxVolume = 0.9f;

    [Tooltip("Pitch range center – pitch will vary slightly with intensity.")]
    [Range(0.5f, 1.5f)]
    public float rumblePitchCenter = 1.0f;

    [Tooltip("How much pitch can deviate from center based on shake frequency/strength.")]
    [Range(0f, 0.6f)]
    public float rumblePitchVariance = 0.2f;

        [Header("Easing")]
        [Tooltip("Controls how quickly the shake ramps from the initial to maximum amplitude.")]
        public AnimationCurve rampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Scenario Timeline Blending")]
    [Tooltip("Portion of the scenario (0-1) spent ramping from 0 to full intensity.")]
    [Range(0.05f, 0.5f)]
    public float progressRampPortion = 0.25f;

    [Tooltip("Portion of the scenario (0-1) spent fading the shake back to zero before it ends.")]
    [Range(0.05f, 0.5f)]
    public float progressFadePortion = 0.2f;

    private bool shaking;
        private float elapsed;
        private float seedX;
        private float seedY;
        private float seedRot;

        private Vector3 appliedPositionOffset = Vector3.zero;
        private Quaternion appliedRotationOffset = Quaternion.identity;
        private bool hasAppliedOffset;

        private float baseRampDuration;
        private float baseNoiseFrequency;
        private float baseInitialPositionAmplitude;
        private float baseMaxPositionAmplitude;
        private float baseInitialRotationAmplitude;
        private float baseMaxRotationAmplitude;

        private float shakeStrengthMultiplier = 1f;
        private float shakeFrequencyMultiplier = 1f;
        private float shakeDurationMultiplier = 1f;
        private float scenarioProgressStrength = 0f;
        private bool scenarioTimelineActive;

        // Cached values to reduce per-frame calculations
        private float cachedTime;
        private float cachedAdjustedFrequency;
    private float cachedPositionAmplitude;
    private float cachedRotationAmplitude;
    private float platformBoostFactor = 1f;
    private Camera targetCamera;
    private float initialFov;
    private bool hasCamera;
    private float seedFov;

    private bool rumblePrepared;

    // Haptic vibration state
    private float lastHapticTime;

        void Awake()
        {
            if (shakeTarget == null)
            {
                shakeTarget = transform;
            }

            seedX = Random.value * 100f;
            seedY = Random.value * 100f + 25f;
            seedRot = Random.value * 100f + 50f;
            seedFov = Random.value * 100f + 75f;

            CacheBaseValues();

            // Determine platform boost once
            if (Application.isMobilePlatform)
            {
                platformBoostFactor = Mathf.Max(1f, mobilePlatformBoost);
            }
            else
            {
                platformBoostFactor = 1f;
            }

            // Resolve camera reference for FOV jitter
            targetCamera = GetComponentInChildren<Camera>();
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            if (targetCamera != null)
            {
                initialFov = targetCamera.fieldOfView;
                hasCamera = true;
            }

            PrepareRumbleSourceIfNeeded();
        }

        void OnEnable()
        {
            DisasterTypeManager.OnDisasterTypeChanged += HandleDisasterTypeChanged;
            EarthquakeScenarioManager.OnParametersUpdated += HandleScenarioParameters;
            EarthquakeScenarioManager.OnProgressUpdated += HandleScenarioProgress;
            ApplyScenarioParameters(EarthquakeScenarioManager.CurrentParameters);
            HandleScenarioProgress(EarthquakeScenarioManager.CurrentProgress);
            HandleInitialState();
        }

        void OnDisable()
        {
            DisasterTypeManager.OnDisasterTypeChanged -= HandleDisasterTypeChanged;
            EarthquakeScenarioManager.OnParametersUpdated -= HandleScenarioParameters;
            EarthquakeScenarioManager.OnProgressUpdated -= HandleScenarioProgress;
            StopShake();
            scenarioTimelineActive = false;
            scenarioProgressStrength = 0f;
        }

        void Update()
        {
            if (!shaking)
            {
                if (hasAppliedOffset)
                {
                    ApplyOffsets(Vector3.zero, Quaternion.identity);
                }
                return;
            }

            elapsed += Time.deltaTime;
            float targetRampDuration = baseRampDuration * shakeDurationMultiplier;
            if (targetRampDuration < 0.05f) targetRampDuration = 0.05f;

            float t = targetRampDuration > Mathf.Epsilon ? elapsed / targetRampDuration : 1f;
            if (t > 1f) t = 1f;

            float curve = rampCurve != null ? rampCurve.Evaluate(t) : t;
            if (curve < 0f) curve = 0f;
            if (curve > 1f) curve = 1f;

            // Cache amplitude calculations (include platform boost and minimum visible strength while active)
            float basePositionAmplitude = Mathf.Lerp(baseInitialPositionAmplitude, baseMaxPositionAmplitude, curve);
            float progressFactor = scenarioTimelineActive ? Mathf.Max(scenarioProgressStrength, minActiveStrength) : scenarioProgressStrength;
            cachedPositionAmplitude = basePositionAmplitude * shakeStrengthMultiplier * progressFactor * platformBoostFactor;
            
            float baseRotationAmplitude = Mathf.Lerp(baseInitialRotationAmplitude, baseMaxRotationAmplitude, curve);
            cachedRotationAmplitude = baseRotationAmplitude * shakeStrengthMultiplier * progressFactor * platformBoostFactor * rotationVisibilityBoost;

            // Early exit if shake is negligible
            if (cachedPositionAmplitude < 0.0001f && cachedRotationAmplitude < 0.0001f)
            {
                if (hasAppliedOffset)
                {
                    ApplyOffsets(Vector3.zero, Quaternion.identity);
                }
                return;
            }

            // Cache time calculation
            cachedAdjustedFrequency = baseNoiseFrequency * shakeFrequencyMultiplier;
            cachedTime = Time.time * cachedAdjustedFrequency;

            // Optimized Perlin noise sampling with reduced multiplications
            float offsetX = (Mathf.PerlinNoise(seedX, cachedTime) - 0.5f) * (2f * cachedPositionAmplitude);
            float offsetY = (Mathf.PerlinNoise(seedY, cachedTime * 1.3f) - 0.5f) * (2f * cachedPositionAmplitude);
            float offsetZ = (Mathf.PerlinNoise(seedX + seedY, cachedTime * 0.7f) - 0.5f) * cachedPositionAmplitude;

            float rotX = (Mathf.PerlinNoise(seedRot, cachedTime * 1.1f) - 0.5f) * (2f * cachedRotationAmplitude);
            float rotY = (Mathf.PerlinNoise(seedRot + 33f, cachedTime * 0.9f) - 0.5f) * (1.5f * cachedRotationAmplitude);
            float rotZ = (Mathf.PerlinNoise(seedRot + 66f, cachedTime * 1.4f) - 0.5f) * (1.2f * cachedRotationAmplitude);

            ApplyOffsets(new Vector3(offsetX, offsetY, offsetZ), Quaternion.Euler(rotX, rotY, rotZ));

            // Field of View jitter (subtle zoom wobble)
            if (enableFovJitter && hasCamera)
            {
                float fTime = Time.time * cachedAdjustedFrequency * fovJitterFrequencyScale;
                float fovDelta = (Mathf.PerlinNoise(seedFov, fTime) - 0.5f) * 2f * maxFovJitter * progressFactor * platformBoostFactor;
                targetCamera.fieldOfView = Mathf.Clamp(initialFov + fovDelta, initialFov - 2.5f, initialFov + 2.5f);
            }

            // Audio rumble – adjust volume and pitch based on intensity
            if (enableAudioRumble && rumblePrepared && rumbleSource != null)
            {
                float volT = Mathf.InverseLerp(0f, 1f, progressFactor);
                float targetVol = Mathf.Lerp(rumbleBaseVolume, rumbleMaxVolume, volT) * shakeStrengthMultiplier * platformBoostFactor;

                // ARSafeSettings will apply SFX volume multiplier globally to the AudioSource
                rumbleSource.volume = Mathf.Clamp01(targetVol);

                float freqT = Mathf.Clamp(shakeFrequencyMultiplier, 0.5f, 2f) - 1f; // -0.5..1
                float pitch = rumblePitchCenter + freqT * rumblePitchVariance;
                rumbleSource.pitch = Mathf.Clamp(pitch, 0.6f, 1.4f);
            }

            // Haptic vibration for mobile devices
            UpdateHapticVibration(progressFactor);
        }

        private void HandleInitialState()
        {
            if (DisasterTypeManager.SelectedDisasterType == DisasterType.Earthquake)
            {
                StartShake();
            }
            else
            {
                StopShake();
            }
        }

        private void HandleDisasterTypeChanged(DisasterType disasterType)
        {
            if (disasterType == DisasterType.Earthquake)
            {
                StartShake();
            }
            else
            {
                StopShake();
                scenarioTimelineActive = false;
                scenarioProgressStrength = 0f;
            }
        }

        private void StartShake()
        {
            if (!scenarioTimelineActive)
            {
                // Wait for progress events to activate intensity so we do not jitter during idle states.
                return;
            }

            shaking = true;
            elapsed = 0f;

            // Start rumble audio
            if (enableAudioRumble && rumblePrepared && rumbleSource != null && !rumbleSource.isPlaying)
            {
                rumbleSource.volume = 0f;
                rumbleSource.Play();
            }
        }

        private void StopShake()
        {
            shaking = false;
            elapsed = 0f;
            ApplyOffsets(Vector3.zero, Quaternion.identity);

            // Restore FOV
            if (hasCamera && enableFovJitter)
            {
                targetCamera.fieldOfView = initialFov;
            }

            // Stop rumble audio
            if (enableAudioRumble && rumbleSource != null && rumbleSource.isPlaying)
            {
                rumbleSource.Stop();
                rumbleSource.volume = 0f;
            }

            // Stop haptic vibration
            StopHapticVibration();
        }

        private void HandleScenarioParameters(EarthquakeScenarioParameters parameters)
        {
            ApplyScenarioParameters(parameters);
        }

        private void HandleScenarioProgress(EarthquakeScenarioProgress progress)
        {
            if (!enabled)
            {
                return;
            }

            if (!progress.IsActive)
            {
                scenarioTimelineActive = false;
                scenarioProgressStrength = 0f;

                if (progress.IsComplete)
                {
                    StopShake();
                }
                return;
            }

            scenarioTimelineActive = true;
            scenarioProgressStrength = EvaluateProgressStrength(progress.NormalizedTime);

            if (!shaking)
            {
                StartShake();
            }
        }

        private float EvaluateProgressStrength(float normalized)
        {
            float ramp = Mathf.Clamp(progressRampPortion, 0.05f, 0.5f);
            float fade = Mathf.Clamp(progressFadePortion, 0.05f, 0.5f);

            float strength = 1f;

            if (normalized <= Mathf.Epsilon)
            {
                return 0f;
            }

            if (normalized < ramp)
            {
                strength = Mathf.Clamp01(normalized / Mathf.Max(0.01f, ramp));
            }
            else if (normalized > 1f - fade)
            {
                float fadeT = Mathf.InverseLerp(1f - fade, 1f, normalized);
                strength = Mathf.Lerp(1f, 0f, fadeT);
            }

            return Mathf.Clamp01(strength);
        }

        private void ApplyScenarioParameters(EarthquakeScenarioParameters parameters)
        {
            if (!parameters.IsActive)
            {
                shakeStrengthMultiplier = 1f;
                shakeFrequencyMultiplier = 1f;
                shakeDurationMultiplier = 1f;
                return;
            }

            shakeStrengthMultiplier = Mathf.Max(0.1f, parameters.ShakeMultiplier);
            shakeFrequencyMultiplier = Mathf.Max(0.1f, parameters.FrequencyMultiplier);
            shakeDurationMultiplier = Mathf.Max(0.1f, parameters.DurationMultiplier);
        }

        private void CacheBaseValues()
        {
            baseRampDuration = Mathf.Max(0.05f, rampDuration);
            baseNoiseFrequency = Mathf.Max(0.01f, noiseFrequency);
            baseInitialPositionAmplitude = Mathf.Max(0f, initialPositionAmplitude);
            baseMaxPositionAmplitude = Mathf.Max(baseInitialPositionAmplitude, maxPositionAmplitude);
            baseInitialRotationAmplitude = Mathf.Max(0f, initialRotationAmplitude);
            baseMaxRotationAmplitude = Mathf.Max(baseInitialRotationAmplitude, maxRotationAmplitude);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            CacheBaseValues();
        }
#endif

        private void ApplyOffsets(Vector3 positionOffset, Quaternion rotationOffset)
        {
            if (shakeTarget == null)
            {
                return;
            }

            // Remove previous offset before applying new one so external transforms remain intact
            if (hasAppliedOffset)
            {
                shakeTarget.localPosition -= appliedPositionOffset;
                shakeTarget.localRotation *= Quaternion.Inverse(appliedRotationOffset);
            }

            appliedPositionOffset = positionOffset;
            appliedRotationOffset = rotationOffset;
            hasAppliedOffset = positionOffset != Vector3.zero || rotationOffset != Quaternion.identity;

            if (hasAppliedOffset)
            {
                shakeTarget.localPosition += appliedPositionOffset;
                shakeTarget.localRotation *= appliedRotationOffset;
            }
            else
            {
                appliedPositionOffset = Vector3.zero;
                appliedRotationOffset = Quaternion.identity;
            }
        }

        private void PrepareRumbleSourceIfNeeded()
        {
            if (!enableAudioRumble)
            {
                return;
            }

            if (rumbleSource == null)
            {
                Debug.LogWarning("[EarthquakeCameraShake] Rumble AudioSource not assigned! Please assign an AudioSource with a clip in the Inspector.");
                return;
            }

            if (rumbleSource.clip == null)
            {
                Debug.LogWarning("[EarthquakeCameraShake] Rumble AudioSource has no clip assigned! Please assign a looping rumble AudioClip in the Inspector.");
                return;
            }

            // Validate AudioSource is configured correctly
            if (!rumbleSource.loop)
            {
                Debug.LogWarning("[EarthquakeCameraShake] Rumble AudioSource should have Loop enabled for continuous playback!");
                rumbleSource.loop = true;
            }

            rumbleSource.playOnAwake = false;
            rumbleSource.spatialBlend = 0f; // 2D rumble
            rumbleSource.volume = 0f; // Will be set during shake
            rumbleSource.pitch = rumblePitchCenter;

            rumblePrepared = true;
            Debug.Log($"<color=cyan>[EarthquakeCameraShake] Rumble prepared: {rumbleSource.clip.name} (loop: {rumbleSource.loop})</color>");
        }

        /// <summary>
        /// Update haptic vibration based on earthquake intensity.
        /// Uses Unity's Handheld.Vibrate() for Android devices.
        /// Respects ARSafeSettings.HapticFeedback toggle.
        /// </summary>
        private void UpdateHapticVibration(float progressFactor)
        {
            // CRITICAL: Check settings - respect user's haptic toggle choice
            bool hapticEnabled = enableHapticVibration;
            if (ARSafe.ARSafeSettings.Instance != null)
            {
                hapticEnabled = ARSafe.ARSafeSettings.Instance.HapticFeedback;
            }

            if (!hapticEnabled || !Application.isMobilePlatform)
            {
                return;
            }

            // Only vibrate on mobile platforms
#if UNITY_ANDROID || UNITY_IOS
            // Determine if we should trigger a vibration pulse
            float timeSinceLastHaptic = Time.time - lastHapticTime;

            // Adjust pulse interval based on intensity if scaling is enabled
            float actualInterval = hapticPulseInterval;
            if (scaleHapticWithIntensity)
            {
                // Higher intensity = more frequent vibrations (shorter interval)
                actualInterval = Mathf.Lerp(hapticPulseInterval * 2f, hapticPulseInterval * 0.5f, progressFactor);
            }

            if (timeSinceLastHaptic >= actualInterval)
            {
                // Trigger vibration pulse
                Handheld.Vibrate();
                lastHapticTime = Time.time;
                // Vibration triggered

                if (enableDebugLogs)
                {
                    Debug.Log($"<color=yellow>[EarthquakeCameraShake] Haptic pulse (intensity: {progressFactor:F2}, interval: {actualInterval:F2}s)</color>");
                }
            }
#endif
        }

        /// <summary>
        /// Stop haptic vibration.
        /// Note: Unity's Handheld.Vibrate() doesn't have a stop method, vibration stops automatically.
        /// </summary>
        private void StopHapticVibration()
        {
            // Vibration stopped
            lastHapticTime = 0f;
        }

        private bool enableDebugLogs = false; // Set to true to see haptic debug logs
    }
}
