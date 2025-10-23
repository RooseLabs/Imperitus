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
        [Range(0f, 180f)]
        public float verticalViewAngle = 60f;

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

                // Horizontal angle
                float horizontalAngle = Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(dirToTarget, transform.up));
                // Vertical angle
                float verticalAngle = Vector3.Angle(dirToTarget, Vector3.ProjectOnPlane(dirToTarget, transform.right));

                if (horizontalAngle < viewAngle * 0.5f && verticalAngle < verticalViewAngle * 0.5f)
                {
                    float dstToTarget = Vector3.Distance(transform.position, target.position);
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
            {
                DetectedTarget = best;
                Debug.Log($"[EnemyDetection] Target detected: {best.name}");
            }

            #if UNITY_EDITOR
            if (best != null)
            {
                // Draw green line to the detected target
                Debug.DrawLine(transform.position + Vector3.up * 0.5f, best.position, Color.green, checkInterval);
            }
            else
            {
                // Debug rays to all potential targets to visualize what�s blocking view
                foreach (Collider col in targetsInViewRadius)
                {
                    Vector3 dirToTarget = (col.transform.position - transform.position).normalized;
                    float dstToTarget = Vector3.Distance(transform.position, col.transform.position);

                    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToTarget, out RaycastHit hit, dstToTarget, obstructionMask))
                    {
                        // Red: something blocks the view (like a table)
                        Debug.DrawLine(transform.position + Vector3.up * 0.5f, hit.point, Color.red, checkInterval);
                    }
                    else
                    {
                        // Yellow: line of sight is clear but outside FOV angles
                        Debug.DrawLine(transform.position + Vector3.up * 0.5f, col.transform.position, Color.yellow, checkInterval);
                    }
                }
            }
            #endif
        }

        #region Debug - FOV Mesh/Gizmo

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

        public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
        {
            if (!angleIsGlobal)
                angleInDegrees += transform.eulerAngles.y;
            float rad = angleInDegrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0, Mathf.Cos(rad));
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawFOV) return;

            Gizmos.color = new Color(0, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, viewRadius);

            Vector3 forward = transform.forward * viewRadius;
            Quaternion leftRot = Quaternion.Euler(0, -viewAngle / 2f, 0);
            Quaternion rightRot = Quaternion.Euler(0, viewAngle / 2f, 0);
            Vector3 leftDir = leftRot * forward;
            Vector3 rightDir = rightRot * forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + leftDir);
            Gizmos.DrawLine(transform.position, transform.position + rightDir);

            Gizmos.color = Color.cyan;

            forward = transform.forward;
            Vector3 up = transform.up;
            Quaternion upRot = Quaternion.AngleAxis(-verticalViewAngle / 2f, transform.right);
            Quaternion downRot = Quaternion.AngleAxis(verticalViewAngle / 2f, transform.right);
            Vector3 topDir = upRot * forward;
            Vector3 bottomDir = downRot * forward;
            Gizmos.DrawLine(transform.position, transform.position + topDir * viewRadius);
            Gizmos.DrawLine(transform.position, transform.position + bottomDir * viewRadius);
            Gizmos.DrawWireSphere(transform.position + topDir * viewRadius, 0.1f);
            Gizmos.DrawWireSphere(transform.position + bottomDir * viewRadius, 0.1f);
        }
        #endif

        #endregion
    }
}
