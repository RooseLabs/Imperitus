using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

namespace RooseLabs
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class SpotlightConeVisualizer : NetworkBehaviour
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
        [SerializeField] private float colorTransitionSpeed = 10f;

        // Network synced
        private readonly SyncVar<Color> syncedConeColor = new SyncVar<Color>(
            new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers)
        );

        // Runtime data
        private Mesh coneMesh;
        private float currentConeHeight;
        private float targetConeHeight;
        private Material coneMaterial;
        private static readonly int ColorPropertyID = Shader.PropertyToID("_ConeColor");

        // Cache to avoid recreating mesh every frame if height hasn't changed much
        private float lastMeshHeight = -1f;
        private const float MeshUpdateThreshold = 0.1f;

        private void Awake()
        {
            if (meshFilter == null)
                meshFilter = GetComponent<MeshFilter>();

            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            // Create the cone mesh
            coneMesh = new Mesh();
            coneMesh.name = "SpotlightCone";
            meshFilter.mesh = coneMesh;

            // Get material instance
            coneMaterial = meshRenderer.material;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            syncedConeColor.OnChange += OnConeColorChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServerInitialized)
            {
                syncedConeColor.OnChange += OnConeColorChanged;

                // Apply initial synced color
                if (coneMaterial != null)
                    coneMaterial.SetColor(ColorPropertyID, syncedConeColor.Value);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            syncedConeColor.OnChange -= OnConeColorChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            syncedConeColor.OnChange -= OnConeColorChanged;
        }

        private void LateUpdate()
        {
            if (spotlightOrigin == null) return;

            // Update cone height based on ground distance
            UpdateConeHeight();

            // Smoothly interpolate to target height
            currentConeHeight = Mathf.Lerp(currentConeHeight, targetConeHeight, Time.deltaTime * heightUpdateSpeed);

            // Only regenerate mesh if height changed significantly
            if (Mathf.Abs(currentConeHeight - lastMeshHeight) > MeshUpdateThreshold)
            {
                GenerateConeMesh(currentConeHeight);
                lastMeshHeight = currentConeHeight;
            }

            // Update material color (client-side interpolation)
            if (coneMaterial != null && !IsServerInitialized)
            {
                Color currentColor = coneMaterial.GetColor(ColorPropertyID);
                Color targetColor = syncedConeColor.Value;
                coneMaterial.SetColor(ColorPropertyID, Color.Lerp(currentColor, targetColor, Time.deltaTime * colorTransitionSpeed));
            }
        }

        /// <summary>
        /// Raycast down to find ground and set target cone height
        /// Uses a separate raycast that won't interfere with player detection
        /// </summary>
        private void UpdateConeHeight()
        {
            if (spotlightOrigin == null) return;

            // Cast from spotlight position in its forward direction
            // Use ONLY groundLayer to avoid interfering with player detection
            RaycastHit hit;
            Vector3 origin = spotlightOrigin.position;
            Vector3 direction = spotlightOrigin.forward;

            if (Physics.Raycast(origin, direction, out hit, maxConeHeight, groundLayer, QueryTriggerInteraction.Ignore))
            {
                targetConeHeight = hit.distance;
            }
            else
            {
                // No ground found, use max height
                targetConeHeight = maxConeHeight;
            }
        }

        /// <summary>
        /// Procedurally generate the frustum cone mesh with open top and proper subdivisions
        /// Mesh is generated in LOCAL space, extending along +Z (forward)
        /// </summary>
        private void GenerateConeMesh(float height)
        {
            if (coneMesh == null || height <= 0.01f) return;

            coneMesh.Clear();

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

            coneMesh.vertices = vertices;
            coneMesh.uv = uv;
            coneMesh.triangles = triangles;
            coneMesh.RecalculateNormals();
            coneMesh.RecalculateBounds();
        }

        /// <summary>
        /// Set cone color (SERVER ONLY - will sync to clients)
        /// </summary>
        public void SetConeColor(Color color)
        {
            if (!IsServerInitialized)
            {
                return;
            }

            syncedConeColor.Value = color;

            // Apply immediately on server
            if (coneMaterial != null)
                coneMaterial.SetColor(ColorPropertyID, color);
        }

        /// <summary>
        /// SyncVar callback when color changes
        /// </summary>
        private void OnConeColorChanged(Color prev, Color next, bool asServer)
        {
            if (!asServer && coneMaterial != null)
            {
                coneMaterial.SetColor(ColorPropertyID, next);
            }
        }

        /// <summary>
        /// Set the spotlight origin transform (usually the spotlight itself)
        /// </summary>
        public void SetSpotlightOrigin(Transform origin)
        {
            spotlightOrigin = origin;

            // Initialize height immediately
            if (spotlightOrigin != null)
            {
                UpdateConeHeight();
                currentConeHeight = targetConeHeight;
                GenerateConeMesh(currentConeHeight);
                lastMeshHeight = currentConeHeight;
            }
        }

        /// <summary>
        /// Update cone angle to match spotlight
        /// </summary>
        public void SetConeAngle(float angle)
        {
            coneAngle = angle;
            lastMeshHeight = -1f; // Force mesh regeneration
        }

        private void OnDestroy()
        {
            if (coneMesh != null)
                Destroy(coneMesh);

            if (coneMaterial != null)
                Destroy(coneMaterial);
        }
    }
}