using System;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Network
{
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawns = Array.Empty<Transform>();

        private void Awake()
        {
            // If playerCharacter exists, then it was already registered, which means we must be transitioning scenes
            // In this case we reposition the local player to the correct spawn point
            var clientManager = FishNet.InstanceFinder.ClientManager;
            if (!clientManager || !clientManager.Started) return;

            var localConnection = clientManager.Connection;
            if (localConnection == null) return;

            var playerCharacter = PlayerHandler.GetCharacter(localConnection);
            if (!playerCharacter) return;

            GetSpawnForClient(GetConnectionSpawnIndex(localConnection), out Vector3 position, out Quaternion rotation);
            playerCharacter.SetPositionAndRotation(position, rotation);
        }

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
            // Check if player has already been spawned for this connection
            // If so, we're transitioning scenes - the player will reposition themselves locally in Awake
            if (PlayerHandler.GetPlayer(connection) != null)
                return;

            if (playerPrefab == null)
            {
                Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {connection.ClientId}.");
                return;
            }

            GetSpawnForClient(connection.ClientId, out Vector3 position, out Quaternion rotation);
            NetworkObject playerObject = Instantiate(playerPrefab, position, rotation);
            Spawn(playerObject, connection);
        }

        private void GetSpawnForClient(int clientId, out Vector3 position, out Quaternion rotation)
        {
            // No spawns specified, use prefab position
            if (spawns.Length == 0)
            {
                SetSpawnUsingPrefab(playerPrefab.transform, out position, out rotation);
                return;
            }

            // Use client ID to deterministically select a spawn point
            int spawnIndex = clientId % spawns.Length;
            Transform spawnPoint = spawns[spawnIndex];

            if (!spawnPoint)
            {
                SetSpawnUsingPrefab(playerPrefab.transform, out position, out rotation);
            }
            else
            {
                position = spawnPoint.position;
                rotation = spawnPoint.rotation;
            }
        }

        private void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            pos = prefab.position;
            rot = prefab.rotation;
        }

        private int GetConnectionSpawnIndex(NetworkConnection connection)
        {
            int index = 0;
            foreach (var player in PlayerHandler.AllConnectedPlayers.OrderBy(p => p.Owner.ClientId))
            {
                if (player.Owner == connection)
                    return index;
                index++;
            }
            // Fallback to the client ID
            return connection.ClientId;
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
