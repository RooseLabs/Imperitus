using System;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class ObjectSpawnPoint : MonoBehaviour
    {
        [Tooltip("Prefabs of the objects that are allowed to be spawned at this spawn point.")]
        [field: SerializeField] public GameObject[] AllowedObjects { get; private set; } = Array.Empty<GameObject>();

        #if UNITY_EDITOR
        private Bounds m_maxLocalBounds = new(Vector3.zero, Vector3.zero);
        private readonly Collider[] m_overlaps = new Collider[2];

        protected virtual void OnValidate()
        {
            ComputeMaxBounds();
        }

        private void ComputeMaxBounds()
        {
            Vector3 overallMin = Vector3.one * float.MaxValue;
            Vector3 overallMax = Vector3.one * float.MinValue;

            foreach (var prefab in AllowedObjects)
            {
                if (!prefab) continue;

                var prefabBounds = GetPrefabBounds(prefab);
                if (prefabBounds.size == Vector3.zero) continue;

                overallMin = Vector3.Min(overallMin, prefabBounds.min);
                overallMax = Vector3.Max(overallMax, prefabBounds.max);
            }

            m_maxLocalBounds.SetMinMax(overallMin, overallMax);
        }

        private Bounds GetPrefabBounds(GameObject prefab)
        {
            var col = prefab.GetComponent<BoxCollider>();
            if (col == null) return new Bounds(Vector3.zero, Vector3.zero);

            return new Bounds(col.center, col.size);
        }

        private void OnDrawGizmos()
        {
            if (AllowedObjects == null || AllowedObjects.Length == 0 || m_maxLocalBounds.size == Vector3.zero)
            {
                return;
            }

            Camera sceneCam = Camera.current;
            if (sceneCam == null) return;
            float dist = Vector3.Distance(transform.position, sceneCam.transform.position);
            if (dist > 20f) return;

            Matrix4x4 originalMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            Vector3 center = m_maxLocalBounds.center;
            Vector3 halfExtents = m_maxLocalBounds.extents;

            Vector3 worldCenter = transform.TransformPoint(center);
            int layerMask = LayerMask.GetMask("Default", "Ground", "Draggable");

            int nOverlaps = Physics.OverlapBoxNonAlloc(worldCenter, halfExtents, m_overlaps, transform.rotation, layerMask, QueryTriggerInteraction.Ignore);

            // First overlap is always self, so we check for more than 1
            Gizmos.color = nOverlaps > 1 ? new Color(1, 0, 0, 0.3f) : new Color(0, 1, 0, 0.3f);
            Gizmos.DrawCube(center, m_maxLocalBounds.size);
            // Draw wireframe
            Gizmos.color = nOverlaps > 1 ? Color.red : Color.green;
            Gizmos.DrawWireCube(center, m_maxLocalBounds.size);

            Gizmos.matrix = originalMatrix;
        }
        #endif
    }
}
