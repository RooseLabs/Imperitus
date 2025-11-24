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
    }
}
