using UnityEngine;

namespace RooseLabs.Enemies
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SpotlightConeVisualizer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform spotlightOrigin;
        [SerializeField] private MeshFilter meshFilter;
        [SerializeField] private MeshRenderer meshRenderer;

        [Header("Cone Settings")]
        [SerializeField] private float maxConeHeight = 20f;
        [SerializeField] private float coneAngle = 65f;
        [SerializeField] private int radialSegments = 32; // Circumference segments
        [SerializeField] private int heightSegments = 16; // Vertical segments
        [SerializeField] private LayerMask groundLayer;

        [Header("Top Opening Settings")]
        [SerializeField] private float topOpeningRadius = 0.05f;
        [Tooltip("Whether to cap the top opening with a circle")]
        [SerializeField] private bool capTopOpening = true;
        [Tooltip("Whether to cap the bottom opening with a circle")]
        [SerializeField] private bool capBottomOpening = false;

        [Header("Visual Settings")]
        [SerializeField] private float heightUpdateSpeed = 10f;

        // Runtime data
        private Mesh m_coneMesh;
        private float m_currentConeHeight;
        private float m_targetConeHeight;
        private Material m_coneMaterial;
        private static readonly int ColorPropertyID = Shader.PropertyToID("_ConeColor");

        // Cache to avoid recreating mesh every frame if height hasn't changed much
        private float m_lastMeshHeight = -1f;
        private const float MeshUpdateThreshold = 0.1f;

        private void Awake()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            // Create the cone mesh
            m_coneMesh = new Mesh
            {
                name = "SpotlightCone"
            };
            meshFilter.mesh = m_coneMesh;

            // Get material instance
            m_coneMaterial = meshRenderer.material;
        }

        private void LateUpdate()
        {
            if (!spotlightOrigin) return;

            // Update cone height based on ground distance
            UpdateConeHeight();

            // Smoothly interpolate to target height
            m_currentConeHeight = Mathf.Lerp(m_currentConeHeight, m_targetConeHeight, Time.deltaTime * heightUpdateSpeed);

            // Only regenerate mesh if height changed significantly
            if (Mathf.Abs(m_currentConeHeight - m_lastMeshHeight) > MeshUpdateThreshold)
            {
                GenerateConeMesh(m_currentConeHeight);
                m_lastMeshHeight = m_currentConeHeight;
            }
        }

        /// <summary>
        /// Raycast down to find ground and set target cone height
        /// Uses a separate raycast that won't interfere with player detection
        /// </summary>
        private void UpdateConeHeight()
        {
            if (!spotlightOrigin) return;

            // Cast from spotlight position in its forward direction
            // Use ONLY groundLayer to avoid interfering with player detection
            RaycastHit hit;
            Vector3 origin = spotlightOrigin.position;
            Vector3 direction = spotlightOrigin.forward;

            if (Physics.Raycast(origin, direction, out hit, maxConeHeight, groundLayer, QueryTriggerInteraction.Ignore))
            {
                m_targetConeHeight = hit.distance;
            }
            else
            {
                // No ground found, use max height
                m_targetConeHeight = maxConeHeight;
            }
        }

        /// <summary>
        /// Procedurally generate the frustum cone mesh with open top and proper subdivisions
        /// Mesh is generated in LOCAL space, extending along +Z (forward)
        /// </summary>
        private void GenerateConeMesh(float height)
        {
            if (!m_coneMesh || height <= 0.01f) return;

            m_coneMesh.Clear();

            // Calculate radius at the base using spotlight angle
            float baseRadius = height * Mathf.Tan(coneAngle * 0.5f * Mathf.Deg2Rad);

            int radial = Mathf.Max(radialSegments, 8);
            int vertical = Mathf.Max(heightSegments, 2);

            // Vertex counts: rings along height + duplicate seam vertices + cap centers
            // We need radial+1 vertices per ring to close the UV seam properly
            int ringsCount = vertical + 1;
            int verticesPerRing = radial + 1; // +1 for seam closure
            int bodyVertexCount = ringsCount * verticesPerRing;
            
            int topCapCenterIndex = -1;
            int bottomCapCenterIndex = -1;
            int vertexCount = bodyVertexCount;

            // Separate vertices for caps (these use radial segments, not radial+1)
            int topCapRingStart = -1;
            int bottomCapRingStart = -1;
            
            if (capTopOpening)
            {
                topCapRingStart = vertexCount;
                vertexCount += radial;
                topCapCenterIndex = vertexCount;
                vertexCount++;
            }

            if (capBottomOpening)
            {
                bottomCapRingStart = vertexCount;
                vertexCount += radial;
                bottomCapCenterIndex = vertexCount;
                vertexCount++;
            }

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            // Generate rings from top (0) to bottom (height)
            // Each ring has radial+1 vertices (first and last are same position, different UVs)
            for (int ring = 0; ring < ringsCount; ring++)
            {
                float t = (float)ring / vertical; // 0 at top, 1 at bottom
                float currentHeight = t * height;
                float currentRadius = Mathf.Lerp(topOpeningRadius, baseRadius, t);

                for (int seg = 0; seg <= radial; seg++) // Note: <= to include duplicate vertex
                {
                    float angle = (float)seg / radial * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * currentRadius;
                    float y = Mathf.Sin(angle) * currentRadius;

                    int vertIndex = ring * verticesPerRing + seg;
                    vertices[vertIndex] = new Vector3(x, y, currentHeight);
                    uv[vertIndex] = new Vector2((float)seg / radial, 1f - t); // UV.x goes 0->1 smoothly
                }
            }

            // Cap vertices (separate from body to have proper UVs centered at 0.5)
            if (capTopOpening)
            {
                for (int seg = 0; seg < radial; seg++)
                {
                    float angle = (float)seg / radial * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * topOpeningRadius;
                    float y = Mathf.Sin(angle) * topOpeningRadius;

                    vertices[topCapRingStart + seg] = new Vector3(x, y, 0);
                    uv[topCapRingStart + seg] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
                }

                vertices[topCapCenterIndex] = Vector3.zero;
                uv[topCapCenterIndex] = new Vector2(0.5f, 0.5f);
            }

            // Bottom cap
            if (capBottomOpening)
            {
                for (int seg = 0; seg < radial; seg++)
                {
                    float angle = (float)seg / radial * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * baseRadius;
                    float y = Mathf.Sin(angle) * baseRadius;

                    vertices[bottomCapRingStart + seg] = new Vector3(x, y, height);
                    uv[bottomCapRingStart + seg] = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
                }

                vertices[bottomCapCenterIndex] = new Vector3(0, 0, height);
                uv[bottomCapCenterIndex] = new Vector2(0.5f, 0.5f);
            }

            // Build triangles
            int bodyTriCount = vertical * radial * 2 * 3;
            int topCapTriCount = capTopOpening ? radial * 3 : 0;
            int bottomCapTriCount = capBottomOpening ? radial * 3 : 0;
            int totalTriCount = bodyTriCount + topCapTriCount + bottomCapTriCount;

            int[] triangles = new int[totalTriCount];
            int triIndex = 0;

            // Body faces (quads split into 2 triangles)
            for (int ring = 0; ring < vertical; ring++)
            {
                for (int seg = 0; seg < radial; seg++) // Note: < radial, not <=
                {
                    int current = ring * verticesPerRing + seg;
                    int next = ring * verticesPerRing + seg + 1; // Next is always seg+1 now
                    int currentBelow = (ring + 1) * verticesPerRing + seg;
                    int nextBelow = (ring + 1) * verticesPerRing + seg + 1;

                    // Triangle 1
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = currentBelow;
                    triangles[triIndex++] = next;

                    // Triangle 2
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = currentBelow;
                    triangles[triIndex++] = nextBelow;
                }
            }

            // Top cap
            if (capTopOpening)
            {
                for (int seg = 0; seg < radial; seg++)
                {
                    int current = topCapRingStart + seg;
                    int next = topCapRingStart + (seg + 1) % radial;

                    triangles[triIndex++] = topCapCenterIndex;
                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                }
            }

            // Bottom cap
            if (capBottomOpening)
            {
                for (int seg = 0; seg < radial; seg++)
                {
                    int current = bottomCapRingStart + seg;
                    int next = bottomCapRingStart + (seg + 1) % radial;

                    triangles[triIndex++] = bottomCapCenterIndex;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current;
                }
            }

            m_coneMesh.vertices = vertices;
            m_coneMesh.uv = uv;
            m_coneMesh.triangles = triangles;
            m_coneMesh.RecalculateNormals();
            m_coneMesh.RecalculateBounds();
        }

        /// <summary>
        /// Set cone color target (works on both server and clients)
        /// </summary>
        public void SetConeColor(Color color)
        {
            m_coneMaterial?.SetColor(ColorPropertyID, color);
        }

        /// <summary>
        /// Set the spotlight origin transform (usually the spotlight itself)
        /// </summary>
        public void SetSpotlightOrigin(Transform origin)
        {
            spotlightOrigin = origin;

            // Initialize height immediately
            if (spotlightOrigin)
            {
                UpdateConeHeight();
                m_currentConeHeight = m_targetConeHeight;
                GenerateConeMesh(m_currentConeHeight);
                m_lastMeshHeight = m_currentConeHeight;
            }
        }

        /// <summary>
        /// Update cone angle to match spotlight
        /// </summary>
        public void SetConeAngle(float angle)
        {
            coneAngle = angle;
            m_lastMeshHeight = -1f; // Force mesh regeneration
        }

        private void OnDestroy()
        {
            if (m_coneMesh != null)
                Destroy(m_coneMesh);

            if (m_coneMaterial != null)
                Destroy(m_coneMaterial);
        }
    }
}
