using System;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Network
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawns = Array.Empty<Transform>();

        private int m_nextSpawn;

        public override void OnStartServer()
        {
            SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
        }

        public override void OnStopServer()
        {
            if (SceneManager != null)
                SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
        }

        private void OnClientLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            // Check if this connection is observing this object (and thus in this scene)
            if (asServer && Observers.Contains(connection))
                SpawnPlayer(connection);
        }

        public override void OnSpawnServer(NetworkConnection connection)
        {
            if (connection.LoadedStartScenes(true))
                SpawnPlayer(connection);
        }

        private void SpawnPlayer(NetworkConnection connection)
        {
            Vector3 position;
            Quaternion rotation;

            // Check if player has already been spawned for this connection
            // If that's the case we're probably transitioning scenes, try to set new position for existing character
            if (PlayerHandler.GetPlayer(connection) != null)
            {
                var playerCharacter = PlayerHandler.GetCharacter(connection);
                if (playerCharacter != null)
                {
                    SetSpawn(playerCharacter.transform, out position, out rotation);
                    playerCharacter.SetPositionAndRotation(connection, position, rotation);
                }
                return;
            }

            if (playerPrefab == null)
            {
                Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {connection.ClientId}.");
                return;
            }

            SetSpawn(playerPrefab.transform, out position, out rotation);
            NetworkObject playerObject = Instantiate(playerPrefab, position, rotation);
            Spawn(playerObject, connection);
        }

        private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            // No spawns specified.
            if (spawns.Length == 0)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
                return;
            }

            Transform result = spawns[m_nextSpawn];
            if (result == null)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
            }
            else
            {
                pos = result.position;
                rot = result.rotation;
            }

            // Advance to next spawn point or loop back to the first one.
            m_nextSpawn = (m_nextSpawn + 1) % spawns.Length;
        }

        private void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            pos = prefab.position;
            rot = prefab.rotation;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Only draw gizmos if this object or one of its children is selected
            GameObject selectedObject = UnityEditor.Selection.activeGameObject;
            if (!selectedObject || (selectedObject != gameObject && (!selectedObject.transform.parent || selectedObject.transform.parent.gameObject != gameObject)))
                return;
            foreach (Transform spawn in spawns)
            {
                if (!spawn) continue;
                DrawCapsuleGizmo(spawn.position, spawn.rotation, 0.25f, 1.7f);
            }
        }

        private void DrawCapsuleGizmo(Vector3 position, Quaternion rotation, float radius, float height)
        {
            Gizmos.color = Color.green;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;

            // Bottom sphere center is at ground + radius (the lowest point touches ground)
            Vector3 bottomSphereCenter = position + up * radius;
            // Top sphere center is at height - radius (the highest point is at height)
            Vector3 topSphereCenter = position + up * (height - radius);

            // Draw bottom sphere
            Gizmos.DrawWireSphere(bottomSphereCenter, radius);

            // Draw top sphere
            Gizmos.DrawWireSphere(topSphereCenter, radius);

            // Vertical lines connecting spheres
            Gizmos.DrawLine(bottomSphereCenter + forward * radius, topSphereCenter + forward * radius);
            Gizmos.DrawLine(bottomSphereCenter - forward * radius, topSphereCenter - forward * radius);
            Gizmos.DrawLine(bottomSphereCenter + right * radius, topSphereCenter + right * radius);
            Gizmos.DrawLine(bottomSphereCenter - right * radius, topSphereCenter - right * radius);

            // Draw arrow at eye level to indicate forward direction
            Vector3 arrowPosition = position + up * (height * 0.85f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(arrowPosition, forward * 0.5f);
        }
        #endif
    }
}
