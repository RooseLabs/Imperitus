using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Marks a location where enemies can spawn in a room.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("The actual spawn point transform (uses this GameObject if null)")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Optionally specify which room this spawner belongs to (auto-detected if empty)")]
        [SerializeField] private string roomIdentifier = "";

        [Header("Visualization")]
        [SerializeField] private bool showGizmo = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.5f, 0f, 0.8f);
        [SerializeField] private float gizmoSize = 1f;
        [SerializeField] private bool showRoomRadius = true;
        [SerializeField] private float roomRadiusVisualization = 10f;

        // Runtime data
        private GameObject currentSpawnedEnemy;

        public Transform SpawnPoint => spawnPoint != null ? spawnPoint : transform;
        public string RoomIdentifier => roomIdentifier;
        public bool HasActiveEnemy => currentSpawnedEnemy != null;

        private void Reset()
        {
            // Auto-assign spawn point to this GameObject
            if (spawnPoint == null)
                spawnPoint = transform;
        }

        /// <summary>
        /// Automatically assign room identifier based on nearby room GameObjects
        /// Uses multiple detection methods in order of reliability
        /// </summary>
        public void AssignRoomIdentifier(string roomTag, float proximityThreshold)
        {
            // If already manually assigned, skip
            if (!string.IsNullOrEmpty(roomIdentifier))
                return;

            GameObject[] rooms = GameObject.FindGameObjectsWithTag(roomTag);

            if (rooms.Length == 0)
            {
                roomIdentifier = "Unknown";
                Debug.LogWarning($"[EnemySpawner] No GameObjects found with tag '{roomTag}'. Spawner: {gameObject.name}");
                return;
            }

            // Method 1: Check if spawner is child of a room (hierarchy-based)
            Transform parent = transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag(roomTag))
                {
                    roomIdentifier = parent.gameObject.name;
                    Debug.Log($"[EnemySpawner] {gameObject.name} assigned to room '{parent.gameObject.name}' (hierarchy-based)");
                    return;
                }
                parent = parent.parent;
            }

            // Method 2: Raycast-based detection (check what's beneath the spawner)
            GameObject roomBelow = DetectRoomByRaycast(rooms);
            if (roomBelow != null)
            {
                roomIdentifier = roomBelow.name;
                Debug.Log($"[EnemySpawner] {gameObject.name} assigned to room '{roomBelow.name}' (raycast-based)");
                return;
            }

            // Method 3: Bounds-based detection (most accurate for separated rooms)
            GameObject roomByBounds = DetectRoomByBounds(rooms);
            if (roomByBounds != null)
            {
                roomIdentifier = roomByBounds.name;
                Debug.Log($"[EnemySpawner] {gameObject.name} assigned to room '{roomByBounds.name}' (bounds-based)");
                return;
            }

            // Method 4: Closest room by child bounds (fallback)
            float closestDistance = float.MaxValue;
            GameObject closestRoom = null;

            foreach (GameObject room in rooms)
            {
                Bounds roomBounds = RoomCalculations.CalculateRoomBounds(room);

                if (roomBounds.size != Vector3.zero)
                {
                    Vector3 closestPoint = roomBounds.ClosestPoint(transform.position);
                    float distance = Vector3.Distance(transform.position, closestPoint);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestRoom = room;
                    }
                }
            }

            if (closestRoom != null && closestDistance < proximityThreshold)
            {
                roomIdentifier = closestRoom.name;
                Debug.Log($"[EnemySpawner] {gameObject.name} assigned to room '{closestRoom.name}' (proximity: {closestDistance:F1}m)");
            }
            else
            {
                roomIdentifier = "Unknown";
                Debug.LogWarning($"[EnemySpawner] {gameObject.name} couldn't find nearby room. Closest: {closestDistance:F1}m. Please assign manually.");
            }
        }

        /// <summary>
        /// Detect room by raycasting down to see what floor/collider we're above
        /// </summary>
        private GameObject DetectRoomByRaycast(GameObject[] rooms)
        {
            // Raycast downward from spawner position
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, 100f))
            {
                // Check if the hit collider belongs to any of the room hierarchies
                Transform hitTransform = hit.collider.transform;

                foreach (GameObject room in rooms)
                {
                    // Check if hit object is child of this room
                    if (IsChildOf(hitTransform, room.transform))
                    {
                        return room;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Detect room by checking if spawner position is within room's bounds
        /// </summary>
        private GameObject DetectRoomByBounds(GameObject[] rooms)
        {
            foreach (GameObject room in rooms)
            {
                Bounds roomBounds = RoomCalculations.CalculateRoomBounds(room);

                if (roomBounds.size != Vector3.zero && roomBounds.Contains(transform.position))
                {
                    return room;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a transform is a child of another transform
        /// </summary>
        private bool IsChildOf(Transform child, Transform parent)
        {
            Transform current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Called by EnemySpawnManager when an enemy is spawned here
        /// </summary>
        public void OnEnemySpawned(GameObject enemy)
        {
            currentSpawnedEnemy = enemy;
        }

        /// <summary>
        /// Called when the spawned enemy dies or is destroyed
        /// </summary>
        public void OnEnemyDestroyed()
        {
            currentSpawnedEnemy = null;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo)
                return;

            Transform spawnTransform = SpawnPoint;

            // Draw spawn point indicator
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(spawnTransform.position, gizmoSize * 0.3f);

            // Draw direction arrow
            Gizmos.DrawLine(
                spawnTransform.position,
                spawnTransform.position + spawnTransform.forward * gizmoSize
            );

            // Draw cone to show forward direction
            Vector3 forward = spawnTransform.forward * gizmoSize;
            Vector3 right = spawnTransform.right * (gizmoSize * 0.3f);

            Gizmos.DrawLine(
                spawnTransform.position + forward,
                spawnTransform.position + forward * 0.7f + right
            );
            Gizmos.DrawLine(
                spawnTransform.position + forward,
                spawnTransform.position + forward * 0.7f - right
            );

            // Draw room radius
            if (showRoomRadius)
            {
                Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.2f);
                DrawCircle(spawnTransform.position, roomRadiusVisualization, 32);
            }

            // Draw label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                spawnTransform.position + Vector3.up * (gizmoSize + 0.5f),
                $"Spawner\n{(string.IsNullOrEmpty(roomIdentifier) ? "Room: Auto" : $"Room: {roomIdentifier}")}"
            );
#endif
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo)
                return;

            Transform spawnTransform = SpawnPoint;

            // Draw more detailed visualization when selected
            Gizmos.color = Color.yellow;
            DrawCircle(spawnTransform.position, gizmoSize * 2f, 32);

            // Draw vertical line
            Gizmos.DrawLine(
                spawnTransform.position,
                spawnTransform.position + Vector3.up * 3f
            );
        }

        private void DrawCircle(Vector3 center, float radius, int segments)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0, 0);

            for (int i = 1; i <= segments; i++)
            {
                float angle = angleStep * i * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    0,
                    Mathf.Sin(angle) * radius
                );

                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }
    }
}