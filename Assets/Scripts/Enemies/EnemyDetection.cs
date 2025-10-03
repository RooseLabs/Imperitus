using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Enemies
{
    [RequireComponent(typeof(Collider))]
    public class EnemyDetection : MonoBehaviour
    {
        [Header("FOV Settings")]
        public float viewRadius = 10f;
        [Range(0f, 360f)]
        public float viewAngle = 90f;

        [Header("Detection")]
        public LayerMask targetMask;
        public LayerMask obstructionMask;
        public float checkInterval = 0.2f;

        [Header("Debug")]
        public bool drawFOV = true;
        public int meshResolution = 30;
        public float edgeResolveIterations = 4;
        public float edgeDstThreshold = 0.5f;
        public Material fovMaterial;
        public float fovAlpha = 0.15f;

        public Transform DetectedTarget { get; private set; }

        private float checkTimer = 0f;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh viewMesh;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;

            if (drawFOV)
            {
                GameObject go = new GameObject("FOV_Mesh");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                meshFilter = go.AddComponent<MeshFilter>();
                meshRenderer = go.AddComponent<MeshRenderer>();
                if (fovMaterial != null)
                {
                    meshRenderer.sharedMaterial = fovMaterial;
                }
                viewMesh = new Mesh();
                viewMesh.name = "View Mesh";
                meshFilter.mesh = viewMesh;
            }
        }

        private void Update()
        {
            checkTimer -= Time.deltaTime;
            if (checkTimer <= 0f)
            {
                checkTimer = checkInterval;
                RunDetection();
            }

            if (drawFOV)
                DrawFieldOfView();
        }

        private void RunDetection()
        {
            DetectedTarget = null;

            Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);
            float bestDistance = float.MaxValue;
            Transform best = null;

            foreach (Collider col in targetsInViewRadius)
            {
                Transform target = col.transform;
                Vector3 dirToTarget = (target.position - transform.position).normalized;

                // angle check
                if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle * 0.5f)
                {
                    float dstToTarget = Vector3.Distance(transform.position, target.position);

                    // obstruction check
                    if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToTarget, dstToTarget, obstructionMask))
                    {
                        if (dstToTarget < bestDistance)
                        {
                            bestDistance = dstToTarget;
                            best = target;
                        }
                    }
                }
            }

            if (best != null)
                DetectedTarget = best;
        }

        #region Debug - FOV Mesh
        void DrawFieldOfView()
        {
            int stepCount = Mathf.RoundToInt(viewAngle * meshResolution);
            float stepAngleSize = viewAngle / stepCount;

            List<Vector3> viewPoints = new List<Vector3>();
            for (int i = 0; i <= stepCount; i++)
            {
                float angle = transform.eulerAngles.y - viewAngle / 2 + stepAngleSize * i;
                ViewCastInfo newViewCast = ViewCast(angle);
                viewPoints.Add(newViewCast.point);
            }

            int vertexCount = viewPoints.Count + 1;
            Vector3[] vertices = new Vector3[vertexCount];
            int[] triangles = new int[(vertexCount - 2) * 3];

            vertices[0] = Vector3.zero;
            for (int i = 0; i < viewPoints.Count; i++)
            {
                // Convert world point to local
                vertices[i + 1] = transform.InverseTransformPoint(viewPoints[i]);
                if (i < viewPoints.Count - 1)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = i + 1;
                    triangles[i * 3 + 2] = i + 2;
                }
            }

            viewMesh.Clear();
            viewMesh.vertices = vertices;
            viewMesh.triangles = triangles;
            viewMesh.RecalculateNormals();

            if (meshRenderer)
            {
                if (meshRenderer.sharedMaterial != null)
                {
                    Color c = meshRenderer.sharedMaterial.color;
                    c.a = fovAlpha;
                    meshRenderer.sharedMaterial.color = c;
                }
            }
        }

        ViewCastInfo ViewCast(float globalAngle)
        {
            Vector3 dir = DirFromAngle(globalAngle, true);
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out hit, viewRadius, obstructionMask))
            {
                return new ViewCastInfo(true, hit.point, hit.distance, globalAngle);
            }
            else
            {
                return new ViewCastInfo(false, transform.position + dir * viewRadius, viewRadius, globalAngle);
            }
        }

        public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
        {
            if (!angleIsGlobal)
                angleInDegrees += transform.eulerAngles.y;
            float rad = angleInDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
        }

        public struct ViewCastInfo
        {
            public bool hit;
            public Vector3 point;
            public float dist;
            public float angle;
            public ViewCastInfo(bool _hit, Vector3 _point, float _dist, float _angle)
            {
                hit = _hit; point = _point; dist = _dist; angle = _angle;
            }
        }
        #endregion
    }
}
