using RooseLabs.ScriptableObjects;
using System.Collections.Generic;
using System.Linq;
using FishNet.Utility.Performance;
using UnityEngine;
using RooseLabs.Utils;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        private void SpawnHeistRuneContainerObjects()
        {
            // Get required runes for current assignment
            HashSet<RuneSO> requiredRunes = new HashSet<RuneSO>();
            foreach (var taskId in CurrentAssignment.tasks)
            {
                var task = TaskDatabase[taskId];
                if (task.CompletionCondition is CastSpellCondition csc)
                {
                    requiredRunes.AddRange(csc.Spell.Runes);
                }
            }

            // Get and shuffle all spawn points
            RuneObjectSpawnPoint[] allSpawnPoints = FindObjectsByType<RuneObjectSpawnPoint>(FindObjectsSortMode.None);
            if (allSpawnPoints.Length == 0)
            {
                Debug.LogWarning("No RuneObjectSpawnPoint found in the scene!");
                return;
            }
            allSpawnPoints.Shuffle();

            // Spawn required runes first
            int spawnedCount = 0;
            foreach (var rune in requiredRunes)
            {
                if (spawnedCount >= allSpawnPoints.Length)
                    break;
                SpawnRuneContainerObjectAtPoint(allSpawnPoints[spawnedCount], rune);
                Destroy(allSpawnPoints[spawnedCount].gameObject);
                ++spawnedCount;
            }

            // Spawn additional books with 10% chance
            for (int i = spawnedCount; i < allSpawnPoints.Length; ++i)
            {
                if (Random.Range(0f, 1f) <= 0.1f)
                    SpawnRuneContainerObjectAtPoint(allSpawnPoints[i]);
                Destroy(allSpawnPoints[i].gameObject);
            }
            (NetworkManager.ObjectPool as DefaultObjectPool)?.ClearPool();
        }

        private void SpawnRuneContainerObjectAtPoint(RuneObjectSpawnPoint spawnPoint, RuneSO rune = null)
        {
            if (spawnPoint.AllowedObjects == null || spawnPoint.AllowedObjects.Length == 0)
            {
                this.LogWarning($"Rune Object Spawn point '{spawnPoint.name}' has no AllowedObjects assigned!");
                return;
            }
            GameObject[] validObjects = spawnPoint.AllowedObjects.Where(obj => obj != null).ToArray();
            if (validObjects.Length == 0)
            {
                this.LogWarning($"Rune Object Spawn point '{spawnPoint.name}' has only null AllowedObjects!");
                return;
            }
            GameObject objPrefab = validObjects[Random.Range(0, validObjects.Length)];

            RuneSO selectedRune = rune;
            if (!selectedRune)
            {
                List<RuneSO> possibleRunes = spawnPoint.GetPossibleRunes().ToList();
                if (possibleRunes.Count == 0)
                {
                    this.LogWarning($"Spawn point '{spawnPoint.name}' has no possible runes to assign!");
                    return;
                }
                selectedRune = possibleRunes[Random.Range(0, possibleRunes.Count)];
            }

            GameObject objInstance = Instantiate(objPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
            if (objInstance.TryGetComponent(out IRuneContainer runeContainer))
            {
                runeContainer.SetContainedRune(selectedRune);
            }

            Spawn(objInstance, scene: spawnPoint.gameObject.scene);
        }

        private void DestroyRuneObjectSpawnPoints()
        {
            foreach (var s in FindObjectsByType<RuneObjectSpawnPoint>(FindObjectsSortMode.None))
            {
                Destroy(s.gameObject);
            }
        }
    }
}
