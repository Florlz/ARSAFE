/*
 * PURPOSE: Cache original AudioSource volume before settings adjustments
 * DEPENDENCIES: Unity AudioSource
 * DATA FLOW: ARSafeSettings → Cache original volume → Apply multiplier
 * PERFORMANCE: Lightweight component, minimal memory footprint
 * EDGE CASES: Handles runtime AudioSource creation, DontDestroyOnLoad
 */

using UnityEngine;

namespace ARSafe
{
    /// <summary>
    /// Caches the original volume of an AudioSource before settings adjustments.
    /// Auto-created by ARSafeSettings when needed.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioSourceVolumeCache : MonoBehaviour
    {
        [Tooltip("Original volume before settings multipliers (cached automatically)")]
        public float originalVolume = 1f;

        [Tooltip("Last applied volume multiplier (for debug)")]
        public float lastMultiplier = 1f;

        [Tooltip("Audio type (SFX, UI, or Music)")]
        public string audioType = "SFX";

        private void Awake()
        {
            // Cache the initial volume
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                originalVolume = audioSource.volume;
            }
        }

        /// <summary>
        /// Update the cached original volume (call this if you change volume at runtime)
        /// </summary>
        public void UpdateOriginalVolume(float newVolume)
        {
            originalVolume = newVolume;
        }
    }
}
