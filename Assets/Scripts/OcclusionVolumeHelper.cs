using UnityEngine;

namespace ARSafe.House
{
    /*
     * ARCHITECTURE PLAN: OcclusionVolumeHelper
     *
     * PURPOSE:
     *   - Helper component to create invisible occlusion volumes for Vuforia AR.
     *   - Prevents flood water from showing through walls/objects.
     *   - Generates mesh at runtime or uses assigned mesh.
     *
     * DEPENDENCIES:
     *   - Shader: ARSafe/OcclusionOnly (depth-only rendering)
     *   - Unity: MeshFilter, MeshRenderer
     *
     * DATA FLOW:
     *   - Input: Box dimensions or custom mesh
     *   - Processing: Create mesh, apply occlusion material
     *   - Output: Invisible mesh that writes to depth buffer
     *
     * USAGE:
     *   - Add to GameObject where you want occlusion (wall, floor, furniture)
     *   - Set volume type (Box, Floor, Custom)
     *   - Adjust size/position with gizmos
     *   - Water will be hidden behind this volume
     */
    [ExecuteAlways]
    public class OcclusionVolumeHelper : MonoBehaviour
    {
        public enum VolumeType
        {
            Box,        // General purpose box (walls, furniture)
            Floor,      // Large flat plane (floor occlusion)
            Custom      // Use custom mesh
        }

        [Header("Occlusion Volume Settings")]
        [Tooltip("Type of occlusion volume to generate")]
        public VolumeType volumeType = VolumeType.Box;

        [Tooltip("Size of the occlusion volume (for Box and Floor types)")]
        public Vector3 size = new Vector3(1f, 2f, 0.1f); // Default wall: 1m wide, 2m tall, 10cm thick

        [Tooltip("Custom mesh for occlusion (only used if volumeType = Custom)")]
        public Mesh customMesh;

        [Header("Material")]
        [Tooltip("Occlusion material (uses ARSafe/OcclusionOnly shader). Auto-created if null.")]
        public Material occlusionMaterial;

        [Header("Visualization")]
        [Tooltip("Show occlusion volume in Scene view (gizmos)")]
        public bool showGizmos = true;

        [Tooltip("Color of gizmos in Scene view")]
        public Color gizmoColor = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red

        [Header("Info")]
        [Tooltip("Read-only: Is occlusion mesh set up correctly?")]
        [SerializeField] private bool isSetup = false;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh generatedMesh;

        private void OnEnable()
        {
            SetupOcclusionMesh();
        }

        private void OnValidate()
        {
            // Clamp size to reasonable values
            size.x = Mathf.Max(0.01f, size.x);
            size.y = Mathf.Max(0.01f, size.y);
            size.z = Mathf.Max(0.01f, size.z);

            if (Application.isPlaying || !Application.isEditor)
            {
                SetupOcclusionMesh();
            }
        }

        /// <summary>
        /// Set up occlusion mesh and material
        /// </summary>
        private void SetupOcclusionMesh()
        {
            // Get or add MeshFilter
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Get or add MeshRenderer
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            // Create occlusion material if needed
            if (occlusionMaterial == null)
            {
                occlusionMaterial = CreateOcclusionMaterial();
            }

            // Apply material
            meshRenderer.sharedMaterial = occlusionMaterial;

            // Disable shadow casting/receiving (invisible mesh)
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // Generate or assign mesh
            if (volumeType == VolumeType.Custom && customMesh != null)
            {
                meshFilter.sharedMesh = customMesh;
                isSetup = true;
            }
            else if (volumeType == VolumeType.Box)
            {
                meshFilter.sharedMesh = GenerateBoxMesh();
                isSetup = true;
            }
            else if (volumeType == VolumeType.Floor)
            {
                meshFilter.sharedMesh = GenerateFloorMesh();
                isSetup = true;
            }
            else
            {
                isSetup = false;
            }

            if (!isSetup)
            {
                Debug.LogWarning($"[OcclusionVolume] {name} - Occlusion mesh not set up correctly! Check settings.");
            }
        }

        /// <summary>
        /// Create occlusion material with depth-only shader
        /// </summary>
        private Material CreateOcclusionMaterial()
        {
            // Try to load shader
            Shader occlusionShader = Shader.Find("ARSafe/OcclusionOnly");

            if (occlusionShader == null)
            {
                Debug.LogError("[OcclusionVolume] ARSafe/OcclusionOnly shader not found! Make sure OcclusionOnly.shader is in Assets/Shaders/");
                // Fallback to Hidden shader
                occlusionShader = Shader.Find("Hidden/InternalErrorShader");
            }

            Material mat = new Material(occlusionShader);
            mat.name = "OcclusionMaterial (Runtime)";

            return mat;
        }

        /// <summary>
        /// Generate box mesh for walls/furniture
        /// </summary>
        private Mesh GenerateBoxMesh()
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedMesh);
                }
                else
                {
                    DestroyImmediate(generatedMesh);
                }
            }

            generatedMesh = new Mesh();
            generatedMesh.name = "OcclusionBox";

            // Generate box vertices
            Vector3 halfSize = size * 0.5f;

            Vector3[] vertices = new Vector3[24]; // 4 vertices per face, 6 faces
            int[] triangles = new int[36]; // 2 triangles per face, 6 faces

            // Front face
            vertices[0] = new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            vertices[1] = new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[2] = new Vector3(halfSize.x, halfSize.y, halfSize.z);
            vertices[3] = new Vector3(-halfSize.x, halfSize.y, halfSize.z);

            // Back face
            vertices[4] = new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[5] = new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            vertices[6] = new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            vertices[7] = new Vector3(halfSize.x, halfSize.y, -halfSize.z);

            // Left face
            vertices[8] = new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            vertices[9] = new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            vertices[10] = new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            vertices[11] = new Vector3(-halfSize.x, halfSize.y, -halfSize.z);

            // Right face
            vertices[12] = new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[13] = new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[14] = new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            vertices[15] = new Vector3(halfSize.x, halfSize.y, halfSize.z);

            // Top face
            vertices[16] = new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            vertices[17] = new Vector3(halfSize.x, halfSize.y, halfSize.z);
            vertices[18] = new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            vertices[19] = new Vector3(-halfSize.x, halfSize.y, -halfSize.z);

            // Bottom face
            vertices[20] = new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            vertices[21] = new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            vertices[22] = new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            vertices[23] = new Vector3(-halfSize.x, -halfSize.y, halfSize.z);

            // Generate triangles (2 per face)
            for (int i = 0; i < 6; i++)
            {
                int vertexOffset = i * 4;
                int triangleOffset = i * 6;

                triangles[triangleOffset + 0] = vertexOffset + 0;
                triangles[triangleOffset + 1] = vertexOffset + 1;
                triangles[triangleOffset + 2] = vertexOffset + 2;

                triangles[triangleOffset + 3] = vertexOffset + 0;
                triangles[triangleOffset + 4] = vertexOffset + 2;
                triangles[triangleOffset + 5] = vertexOffset + 3;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();

            return generatedMesh;
        }

        /// <summary>
        /// Generate large floor plane
        /// </summary>
        private Mesh GenerateFloorMesh()
        {
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedMesh);
                }
                else
                {
                    DestroyImmediate(generatedMesh);
                }
            }

            generatedMesh = new Mesh();
            generatedMesh.name = "OcclusionFloor";

            // Simple quad for floor
            Vector3 halfSize = size * 0.5f;

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-halfSize.x, 0, -halfSize.z),
                new Vector3(halfSize.x, 0, -halfSize.z),
                new Vector3(halfSize.x, 0, halfSize.z),
                new Vector3(-halfSize.x, 0, halfSize.z)
            };

            int[] triangles = new int[6]
            {
                0, 1, 2,
                0, 2, 3
            };

            generatedMesh.vertices = vertices;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateNormals();
            generatedMesh.RecalculateBounds();

            return generatedMesh;
        }

        /// <summary>
        /// Force regenerate mesh (call after changing size in code)
        /// </summary>
        public void RegenerateMesh()
        {
            SetupOcclusionMesh();
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
            {
                return;
            }

            Gizmos.color = gizmoColor;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (volumeType == VolumeType.Box)
            {
                Gizmos.DrawCube(Vector3.zero, size);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
            else if (volumeType == VolumeType.Floor)
            {
                // Draw floor as flat box
                Vector3 floorSize = new Vector3(size.x, 0.01f, size.z);
                Gizmos.DrawCube(Vector3.zero, floorSize);
                Gizmos.DrawWireCube(Vector3.zero, floorSize);
            }
            else if (volumeType == VolumeType.Custom && customMesh != null)
            {
                Gizmos.DrawMesh(customMesh, Vector3.zero, Quaternion.identity, Vector3.one);
                Gizmos.DrawWireMesh(customMesh, Vector3.zero, Quaternion.identity, Vector3.one);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos)
            {
                return;
            }

            // Draw brighter when selected
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;

            if (volumeType == VolumeType.Box)
            {
                Gizmos.DrawCube(Vector3.zero, size);
            }
            else if (volumeType == VolumeType.Floor)
            {
                Vector3 floorSize = new Vector3(size.x, 0.01f, size.z);
                Gizmos.DrawCube(Vector3.zero, floorSize);
            }
        }

        private void OnDestroy()
        {
            // Clean up generated mesh
            if (generatedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedMesh);
                }
                else
                {
                    DestroyImmediate(generatedMesh);
                }
            }
        }
    }
}
