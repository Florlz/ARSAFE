using UnityEngine;

namespace ARSafe.Content
{
    /// <summary>
    /// Limits particle systems to stay within defined boundaries (prevents smoke going through ceilings)
    /// Attach to any particle system that should respect room boundaries
    /// Supports both cylindrical and box-shaped boundaries with full rotation control
    /// </summary>
    public class ARSafeParticleBoundaryLimiter : MonoBehaviour
    {
        public enum BoundaryShape
        {
            Cylinder,
            Box
        }

        [Header("Boundary Shape")]
        [Tooltip("Shape of the boundary volume")]
        [SerializeField]
        private BoundaryShape boundaryShape = BoundaryShape.Cylinder;

        [Header("Transform Controls")]
        [Tooltip("Offset position from GameObject transform (local space)")]
        [SerializeField]
        private Vector3 positionOffset = Vector3.zero;

        [Tooltip("Rotation offset from GameObject transform (Euler angles)")]
        [SerializeField]
        private Vector3 rotationOffset = Vector3.zero;

        [Header("Boundary Settings")]
        [Tooltip("Maximum height particles can reach (local Y)")]
        [SerializeField]
        private float maxHeight = 3f;

        [Tooltip("Minimum height particles can reach (local Y)")]
        [SerializeField]
        private float minHeight = 0f;

        [Header("Cylinder Settings (when shape = Cylinder)")]
        [Tooltip("Maximum horizontal distance from origin")]
        [SerializeField]
        private float maxRadius = 10f;

        [Header("Box Settings (when shape = Box)")]
        [Tooltip("Box size in X axis (width)")]
        [SerializeField]
        private float boxWidth = 10f;

        [Tooltip("Box size in Z axis (length/depth)")]
        [SerializeField]
        private float boxLength = 10f;

        [Header("Damping Settings")]
        [Tooltip("Distance from boundary where damping starts (prevents jitter)")]
        [SerializeField]
        private float dampingZone = 0.5f;

        [Tooltip("How much to slow particles near boundary (0-1)")]
        [SerializeField]
        private float dampingStrength = 0.85f;

        [Header("Particle Removal (Mobile Optimization)")]
        [Tooltip("Kill particles that reach the boundary")]
        [SerializeField]
        private bool killAtBoundary = false;

        [Tooltip("Reduce lifetime when near boundary (prevents particle buildup)")]
        [SerializeField]
        private bool fadeNearBoundary = true;

        [Tooltip("How much to reduce lifetime per second near boundary")]
        [SerializeField]
        private float lifetimeDrainRate = 2f;

        [Header("Performance")]
        [Tooltip("How often to check particles (seconds)")]
        [SerializeField]
        private float updateInterval = 0.1f;

        private ParticleSystem ps;
        private ParticleSystem.Particle[] particles;
        private float nextUpdateTime;
        private bool isWorldSpace;
        private Quaternion cachedRotation;
        private Quaternion cachedRotationInverse;

        void Start()
        {
            ps = GetComponent<ParticleSystem>();
            UpdateCachedRotation();
            if (ps == null)
            {
                Debug.LogError(
                    $"[ARSafeParticleBoundaryLimiter] No ParticleSystem found on {gameObject.name}"
                );
                enabled = false;
                return;
            }

            // Check simulation space
            var main = ps.main;
            isWorldSpace = (main.simulationSpace == ParticleSystemSimulationSpace.World);

            // Pre-allocate particle array
            particles = new ParticleSystem.Particle[ps.main.maxParticles];

            Debug.Log(
                $"[ARSafeParticleBoundaryLimiter] {gameObject.name} using {(isWorldSpace ? "World" : "Local")} simulation space"
            );
        }

        private void UpdateCachedRotation()
        {
            cachedRotation = transform.rotation * Quaternion.Euler(rotationOffset);
            cachedRotationInverse = Quaternion.Inverse(cachedRotation);
        }

        void LateUpdate()
        {
            if (Time.time < nextUpdateTime)
                return;
            nextUpdateTime = Time.time + updateInterval;

            // Update rotation if it changed
            UpdateCachedRotation();

            int particleCount = ps.GetParticles(particles);
            bool modified = false;
            Matrix4x4 worldToLocal = default;
            Matrix4x4 localToWorld = default;
            Vector3 boundaryCenter = transform.position + transform.TransformDirection(positionOffset);

            if (isWorldSpace)
            {
                // Create custom matrices that include position and rotation offset
                worldToLocal = Matrix4x4.TRS(boundaryCenter, cachedRotation, Vector3.one).inverse;
                localToWorld = Matrix4x4.TRS(boundaryCenter, cachedRotation, Vector3.one);
            }
            float maxRadiusSqr = maxRadius * maxRadius;
            float dampingZoneCeilStart = maxHeight - dampingZone;
            float dampingZoneFloorEnd = minHeight + dampingZone;
            float dampingRadius = Mathf.Max(0f, maxRadius - dampingZone);
            float dampingRadiusSqr = dampingRadius * dampingRadius;
            float fadeAmount = fadeNearBoundary ? lifetimeDrainRate * updateInterval : 0f;

            for (int i = 0; i < particleCount; i++)
            {
                // Get particle position in local space (accounting for offset)
                Vector3 localPos;
                if (isWorldSpace)
                {
                    // World space: convert world position to boundary-local space
                    localPos = worldToLocal.MultiplyPoint3x4(particles[i].position);
                }
                else
                {
                    // Local space: convert from GameObject local to boundary-local space
                    Vector3 worldPos = transform.TransformPoint(particles[i].position);
                    Vector3 relativePos = worldPos - boundaryCenter;
                    localPos = cachedRotationInverse * relativePos;
                }

                Vector3 originalLocalPos = localPos;
                bool needsVelocityDamping = false;
                float dampingFactor = 1f;

                // Clamp vertical position with damping zone
                if (localPos.y > maxHeight)
                {
                    if (killAtBoundary)
                    {
                        // Kill particle at ceiling
                        particles[i].remainingLifetime = 0f;
                        modified = true;
                        continue; // Skip rest of processing for dead particle
                    }
                    localPos.y = maxHeight;
                    needsVelocityDamping = true;
                    modified = true;
                }
                else if (localPos.y > dampingZoneCeilStart)
                {
                    // In damping zone near ceiling
                    float distanceFromCeiling = maxHeight - localPos.y;
                    dampingFactor = Mathf.Lerp(
                        dampingStrength,
                        1f,
                        distanceFromCeiling / dampingZone
                    );
                    needsVelocityDamping = true;

                    // Fade particle near boundary
                    if (fadeNearBoundary)
                    {
                        particles[i].remainingLifetime = Mathf.Max(
                            0f,
                            particles[i].remainingLifetime - fadeAmount
                        );
                        modified = true;
                    }
                }

                if (localPos.y < minHeight)
                {
                    if (killAtBoundary)
                    {
                        // Kill particle at floor
                        particles[i].remainingLifetime = 0f;
                        modified = true;
                        continue;
                    }
                    localPos.y = minHeight;
                    needsVelocityDamping = true;
                    modified = true;
                }
                else if (localPos.y < dampingZoneFloorEnd)
                {
                    // In damping zone near floor
                    float distanceFromFloor = localPos.y - minHeight;
                    dampingFactor = Mathf.Min(
                        dampingFactor,
                        Mathf.Lerp(dampingStrength, 1f, distanceFromFloor / dampingZone)
                    );
                    needsVelocityDamping = true;

                    // Fade particle near boundary
                    if (fadeNearBoundary)
                    {
                        particles[i].remainingLifetime = Mathf.Max(
                            0f,
                            particles[i].remainingLifetime - fadeAmount
                        );
                        modified = true;
                    }
                }

                // Apply velocity damping for vertical boundaries
                if (needsVelocityDamping)
                {
                    Vector3 vel = particles[i].velocity;
                    if (isWorldSpace)
                    {
                        // Damp vertical velocity
                        vel.y *= dampingFactor;
                        // Kill upward velocity at ceiling
                        if (localPos.y >= maxHeight)
                        {
                            vel.y = Mathf.Min(vel.y, 0);
                        }
                    }
                    else
                    {
                        // Local space velocity
                        Vector3 localVel = transform.InverseTransformDirection(vel);
                        localVel.y *= dampingFactor;
                        // Kill upward velocity at ceiling
                        if (localPos.y >= maxHeight)
                        {
                            localVel.y = Mathf.Min(localVel.y, 0);
                        }
                        vel = transform.TransformDirection(localVel);
                    }
                    particles[i].velocity = vel;
                    modified = true;
                }

                // Clamp horizontal distance based on shape
                if (boundaryShape == BoundaryShape.Cylinder)
                {
                    // Cylinder: radial distance check
                    float horizontalDistSqr = localPos.x * localPos.x + localPos.z * localPos.z;
                    if (horizontalDistSqr > maxRadiusSqr)
                    {
                        if (killAtBoundary)
                        {
                            particles[i].remainingLifetime = 0f;
                            modified = true;
                            continue;
                        }

                        // Hard clamp at boundary
                        float horizontalDist = Mathf.Sqrt(horizontalDistSqr);
                        float scale = maxRadius / horizontalDist;
                        localPos.x *= scale;
                        localPos.z *= scale;

                        // Kill velocity toward boundary
                        Vector3 vel = particles[i].velocity;
                        Vector3 velDir = isWorldSpace ? vel : transform.InverseTransformDirection(vel);
                        Vector2 horizontalVel = new Vector2(velDir.x, velDir.z);
                        Vector2 horizontalPos = new Vector2(localPos.x, localPos.z);

                        if (Vector2.Dot(horizontalVel, horizontalPos) > 0)
                        {
                            Vector2 outwardDir = horizontalPos.normalized;
                            float outwardVel = Vector2.Dot(horizontalVel, outwardDir);
                            horizontalVel -= outwardDir * outwardVel;
                            velDir.x = horizontalVel.x;
                            velDir.z = horizontalVel.y;
                            particles[i].velocity = isWorldSpace
                                ? velDir
                                : transform.TransformDirection(velDir);
                        }

                        modified = true;
                    }
                    else if (horizontalDistSqr > dampingRadiusSqr)
                    {
                        // In damping zone near horizontal boundary
                        float horizontalDist = Mathf.Sqrt(horizontalDistSqr);
                        float distanceFromEdge = maxRadius - horizontalDist;
                        float radialDamping = Mathf.Lerp(
                            dampingStrength,
                            1f,
                            distanceFromEdge / dampingZone
                        );

                        // Damp horizontal velocity
                        Vector3 vel = particles[i].velocity;
                        Vector3 velDir = isWorldSpace ? vel : transform.InverseTransformDirection(vel);
                        velDir.x *= radialDamping;
                        velDir.z *= radialDamping;
                        particles[i].velocity = isWorldSpace
                            ? velDir
                            : transform.TransformDirection(velDir);

                        if (fadeNearBoundary)
                        {
                            particles[i].remainingLifetime = Mathf.Max(
                                0f,
                                particles[i].remainingLifetime - fadeAmount
                            );
                        }

                        modified = true;
                    }
                }
                else // Box shape
                {
                    float halfWidth = boxWidth * 0.5f;
                    float halfLength = boxLength * 0.5f;
                    bool outsideBoundary = false;
                    bool inDampingZone = false;
                    float minDistanceFromEdge = float.MaxValue;

                    // Check X axis
                    if (Mathf.Abs(localPos.x) > halfWidth)
                    {
                        outsideBoundary = true;
                        if (killAtBoundary)
                        {
                            particles[i].remainingLifetime = 0f;
                            modified = true;
                            continue;
                        }
                        localPos.x = Mathf.Clamp(localPos.x, -halfWidth, halfWidth);
                        modified = true;
                    }
                    else
                    {
                        float distFromXEdge = halfWidth - Mathf.Abs(localPos.x);
                        minDistanceFromEdge = Mathf.Min(minDistanceFromEdge, distFromXEdge);
                        if (distFromXEdge < dampingZone)
                            inDampingZone = true;
                    }

                    // Check Z axis
                    if (Mathf.Abs(localPos.z) > halfLength)
                    {
                        outsideBoundary = true;
                        if (killAtBoundary)
                        {
                            particles[i].remainingLifetime = 0f;
                            modified = true;
                            continue;
                        }
                        localPos.z = Mathf.Clamp(localPos.z, -halfLength, halfLength);
                        modified = true;
                    }
                    else
                    {
                        float distFromZEdge = halfLength - Mathf.Abs(localPos.z);
                        minDistanceFromEdge = Mathf.Min(minDistanceFromEdge, distFromZEdge);
                        if (distFromZEdge < dampingZone)
                            inDampingZone = true;
                    }

                    // Apply damping in the damping zone
                    if (inDampingZone && !outsideBoundary)
                    {
                        float boxDamping = Mathf.Lerp(
                            dampingStrength,
                            1f,
                            minDistanceFromEdge / dampingZone
                        );

                        // Damp horizontal velocity
                        Vector3 vel = particles[i].velocity;
                        Vector3 velDir = isWorldSpace ? vel : transform.InverseTransformDirection(vel);
                        velDir.x *= boxDamping;
                        velDir.z *= boxDamping;
                        particles[i].velocity = isWorldSpace
                            ? velDir
                            : transform.TransformDirection(velDir);

                        if (fadeNearBoundary)
                        {
                            particles[i].remainingLifetime = Mathf.Max(
                                0f,
                                particles[i].remainingLifetime - fadeAmount
                            );
                        }

                        modified = true;
                    }
                }

                // Apply clamped position back
                if (localPos != originalLocalPos)
                {
                    if (isWorldSpace)
                    {
                        // Convert back to world space
                        particles[i].position = localToWorld.MultiplyPoint3x4(localPos);
                    }
                    else
                    {
                        // Convert from boundary-local back to GameObject-local space
                        Vector3 rotatedPos = cachedRotation * localPos;
                        Vector3 worldPos = boundaryCenter + rotatedPos;
                        particles[i].position = transform.InverseTransformPoint(worldPos);
                    }
                }
            }

            if (modified)
            {
                ps.SetParticles(particles, particleCount);
            }
        }

        // Gizmos for visualization in editor
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;

            // Calculate effective rotation and position with offsets
            Quaternion effectiveRotation = transform.rotation * Quaternion.Euler(rotationOffset);
            Vector3 boundaryCenter = transform.position + transform.TransformDirection(positionOffset);
            Vector3 upDir = effectiveRotation * Vector3.up;

            // Calculate height center in world space (respecting rotation and offsets)
            Vector3 center = boundaryCenter + upDir * ((maxHeight + minHeight) * 0.5f);
            float height = maxHeight - minHeight;

            if (boundaryShape == BoundaryShape.Cylinder)
            {
                // Draw cylinder representing boundary
                DrawWireCylinder(center, maxRadius, height, effectiveRotation, upDir);
            }
            else
            {
                // Draw box representing boundary
                DrawWireBox(center, boxWidth, height, boxLength, effectiveRotation, upDir);
            }

            // Draw floor and ceiling planes
            Gizmos.color = Color.red;
            Vector3 floorCenter = boundaryCenter + upDir * minHeight;
            Vector3 ceilingCenter = boundaryCenter + upDir * maxHeight;

            if (boundaryShape == BoundaryShape.Cylinder)
            {
                DrawCircle(floorCenter, maxRadius, upDir, effectiveRotation);
                DrawCircle(ceilingCenter, maxRadius, upDir, effectiveRotation);
            }
            else
            {
                DrawRectangle(floorCenter, boxWidth, boxLength, upDir, effectiveRotation);
                DrawRectangle(ceilingCenter, boxWidth, boxLength, upDir, effectiveRotation);
            }
        }

        private void DrawWireCylinder(Vector3 center, float radius, float height, Quaternion rotation, Vector3 upDir)
        {
            int segments = 32;
            float angleStep = 360f / segments;

            Vector3 top = center + upDir * height * 0.5f;
            Vector3 bottom = center - upDir * height * 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = Mathf.Deg2Rad * (i * angleStep);
                float angle2 = Mathf.Deg2Rad * ((i + 1) * angleStep);

                Vector3 p1 = new Vector3(Mathf.Cos(angle1) * radius, 0, Mathf.Sin(angle1) * radius);
                Vector3 p2 = new Vector3(Mathf.Cos(angle2) * radius, 0, Mathf.Sin(angle2) * radius);

                // Top circle
                Gizmos.DrawLine(top + rotation * p1, top + rotation * p2);
                // Bottom circle
                Gizmos.DrawLine(bottom + rotation * p1, bottom + rotation * p2);
                // Vertical lines
                if (i % 4 == 0)
                {
                    Gizmos.DrawLine(top + rotation * p1, bottom + rotation * p1);
                }
            }
        }

        private void DrawWireBox(Vector3 center, float width, float height, float length, Quaternion rotation, Vector3 upDir)
        {
            float halfWidth = width * 0.5f;
            float halfHeight = height * 0.5f;
            float halfLength = length * 0.5f;

            // Define 8 corners in local space
            Vector3[] corners = new Vector3[8];
            corners[0] = new Vector3(-halfWidth, -halfHeight, -halfLength);
            corners[1] = new Vector3(halfWidth, -halfHeight, -halfLength);
            corners[2] = new Vector3(halfWidth, -halfHeight, halfLength);
            corners[3] = new Vector3(-halfWidth, -halfHeight, halfLength);
            corners[4] = new Vector3(-halfWidth, halfHeight, -halfLength);
            corners[5] = new Vector3(halfWidth, halfHeight, -halfLength);
            corners[6] = new Vector3(halfWidth, halfHeight, halfLength);
            corners[7] = new Vector3(-halfWidth, halfHeight, halfLength);

            // Transform corners to world space using offset rotation
            for (int i = 0; i < 8; i++)
            {
                corners[i] = center + rotation * corners[i];
            }

            // Draw bottom face
            Gizmos.DrawLine(corners[0], corners[1]);
            Gizmos.DrawLine(corners[1], corners[2]);
            Gizmos.DrawLine(corners[2], corners[3]);
            Gizmos.DrawLine(corners[3], corners[0]);

            // Draw top face
            Gizmos.DrawLine(corners[4], corners[5]);
            Gizmos.DrawLine(corners[5], corners[6]);
            Gizmos.DrawLine(corners[6], corners[7]);
            Gizmos.DrawLine(corners[7], corners[4]);

            // Draw vertical edges
            Gizmos.DrawLine(corners[0], corners[4]);
            Gizmos.DrawLine(corners[1], corners[5]);
            Gizmos.DrawLine(corners[2], corners[6]);
            Gizmos.DrawLine(corners[3], corners[7]);
        }

        private void DrawCircle(Vector3 center, float radius, Vector3 normal, Quaternion rotation)
        {
            int segments = 32;
            float angleStep = 360f / segments;
            Vector3 right = Vector3.Cross(normal, Vector3.forward).normalized;
            if (right.magnitude < 0.1f)
                right = Vector3.Cross(normal, Vector3.up).normalized;
            Vector3 forward = Vector3.Cross(right, normal).normalized;

            // Apply rotation to axes
            right = rotation * right.normalized;
            forward = rotation * forward.normalized;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = Mathf.Deg2Rad * (i * angleStep);
                float angle2 = Mathf.Deg2Rad * ((i + 1) * angleStep);

                Vector3 p1 = center + (right * Mathf.Cos(angle1) + forward * Mathf.Sin(angle1)) * radius;
                Vector3 p2 = center + (right * Mathf.Cos(angle2) + forward * Mathf.Sin(angle2)) * radius;

                Gizmos.DrawLine(p1, p2);
            }
        }

        private void DrawRectangle(Vector3 center, float width, float length, Vector3 normal, Quaternion rotation)
        {
            float halfWidth = width * 0.5f;
            float halfLength = length * 0.5f;

            // Calculate local axes using offset rotation
            Vector3 right = rotation * Vector3.right;
            Vector3 forward = rotation * Vector3.forward;

            Vector3 corner1 = center + right * -halfWidth + forward * -halfLength;
            Vector3 corner2 = center + right * halfWidth + forward * -halfLength;
            Vector3 corner3 = center + right * halfWidth + forward * halfLength;
            Vector3 corner4 = center + right * -halfWidth + forward * halfLength;

            Gizmos.DrawLine(corner1, corner2);
            Gizmos.DrawLine(corner2, corner3);
            Gizmos.DrawLine(corner3, corner4);
            Gizmos.DrawLine(corner4, corner1);
        }
    }
}
