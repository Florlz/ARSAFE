using System.Collections.Generic;
using UnityEngine;
using Vuforia;

/// <summary>
/// LEGACY: This script is deprecated. Use ARSafeDebugHelper instead.
/// Kept as a stub to prevent scene reference errors.
/// </summary>
public class AreaTargetDebugHelper : MonoBehaviour
{
    [Tooltip("DEPRECATED: Use ARSafeDebugHelper instead.")]
    [System.Obsolete("Use ARSafeDebugHelper instead")]
    public GameObject manager;

    [Tooltip("DEPRECATED: Functionality moved to ARSafeDebugHelper.")]
    public bool enableVerboseLogging = true;

    private void Start()
    {
        Debug.LogWarning("[AreaTargetDebugHelper] DEPRECATED: This script no longer functions. Use ARSafeDebugHelper instead.", this);
    }
}