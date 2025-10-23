using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs
{
    public class WaypointNavMeshSnapper : MonoBehaviour
    {
        public float maxDistance = 10f; // Try a larger value

        [ContextMenu("Snap To NavMesh")]
        public void SnapToNavMesh()
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, maxDistance, NavMesh.AllAreas))
            {
                Debug.Log($"NavMeshHit position: {hit.position}, NavMesh surface Y: {hit.position.y}");
                transform.position = hit.position;
                Debug.Log($"{gameObject.name} snapped to NavMesh at {hit.position}");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name} could not find NavMesh nearby!");
            }
        }
    }
}
