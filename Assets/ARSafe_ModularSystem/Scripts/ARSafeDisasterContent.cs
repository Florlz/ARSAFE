using UnityEngine;

namespace ARSafe.Modular
{
    /// <summary>
    /// Tag component for disaster-specific content.
    /// Attach to GameObjects that should only be visible for specific disaster types.
    /// 
    /// USAGE:
    /// 1. Create parent GameObject with ARSafeDisasterFilter component
    /// 2. Add this component to each child that represents disaster-specific content
    /// 3. Set disasterType to match the content (Fire, Earthquake, Flood, etc.)
    /// 4. ARSafeDisasterFilter will automatically show/hide based on current disaster type
    /// 
    /// EXAMPLE HIERARCHY:
    /// Content (ARSafeDisasterFilter)
    ///   ├── Fire_Content (ARSafeDisasterContent: disasterType = Fire)
    ///   ├── Earthquake_Content (ARSafeDisasterContent: disasterType = Earthquake)
    ///   └── GeneralSafety_Content (ARSafeDisasterContent: disasterType = GeneralSafety)
    /// </summary>
    [AddComponentMenu("ARSafe/Modular/ARSafe Disaster Content")]
    public class ARSafeDisasterContent : MonoBehaviour
    {
        [Header("Disaster Type")]
        [Tooltip("Which disaster type this content belongs to")]
        public DisasterType disasterType = DisasterType.GeneralSafety;

        [Header("Debug (Optional)")]
        [Tooltip("Display name for debugging purposes")]
        public string contentDescription = "";
        
        /// <summary>
        /// Editor helper: Auto-set content description from GameObject name
        /// </summary>
        private void Reset()
        {
            if (string.IsNullOrEmpty(contentDescription))
            {
                contentDescription = gameObject.name;
            }
        }
        
        /// <summary>
        /// Editor validation
        /// </summary>
        private void OnValidate()
        {
            // Auto-set description if empty
            if (string.IsNullOrEmpty(contentDescription))
            {
                contentDescription = gameObject.name;
            }
        }
    }
}
