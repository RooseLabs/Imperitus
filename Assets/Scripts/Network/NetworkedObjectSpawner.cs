using System;
using System.Linq;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet;
using UnityEngine;

namespace RooseLabs.Network
{
    public class SceneSpawnManager : MonoBehaviour
    {
        [Tooltip("Scene name where prefabs should spawn")]
        public string targetSceneName;

        [Serializable]
        public class SpawnEntry
        {
            public NetworkObject prefab;
            public Vector3 position;
            public Quaternion rotation = Quaternion.identity;
        }

        [Tooltip("Objects to spawn")]
        public SpawnEntry[] spawnEntries;

        private void OnEnable()
        {
            InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoadEnd;
        }

        private void OnDisable()
        {
            InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoadEnd;
        }

        private void HandleSceneLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!InstanceFinder.ServerManager.Started)
                return;

            if (args.LoadedScenes.Any(scene => scene.name == targetSceneName))
            {
                Debug.Log($"Scene {targetSceneName} loaded. Spawning {spawnEntries.Length} prefabs...");

                foreach (var entry in spawnEntries)
                {
                    if (entry.prefab == null) continue;

                    NetworkObject obj = Instantiate(entry.prefab, entry.position, entry.rotation);
                    InstanceFinder.ServerManager.Spawn(obj);
                }
            }
        }
    }
}
