using System;

namespace RooseLabs.Network
{
    using FishNet.Connection;
    using FishNet.Object;
    using UnityEngine;

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
            if (playerPrefab == null)
            {
                Debug.LogWarning($"Player prefab is empty and cannot be spawned for connection {connection.ClientId}.");
                return;
            }
            SetSpawn(playerPrefab.transform, out Vector3 position, out Quaternion rotation);
            NetworkObject playerObject = NetworkManager.GetPooledInstantiated(playerPrefab, position, rotation, asServer: true);
            Spawn(playerObject, connection, gameObject.scene);
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
    }
}
