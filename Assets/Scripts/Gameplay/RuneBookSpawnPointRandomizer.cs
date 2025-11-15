using FishNet;
using FishNet.Object;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.ScriptableObjects;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class RuneBookSpawnPointRandomizer : NetworkBehaviour
    {
        [Tooltip("Number of books to spawn. This is used as the default if no parameter is passed to SpawnBooks().")]
        [SerializeField] private int defaultBooksToSpawn = 20;

        public void SpawnBooks(int numberOfBooks = -1, SpellSO[] requiredSpells = null)
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[RuneBookSpawnPointRandomizer] SpawnBooks() can only be called on the server!");
                return;
            }

            int booksToSpawn = numberOfBooks == -1 ? defaultBooksToSpawn : numberOfBooks;
            RuneObjectSpawnPoint[] allSpawnPoints = FindObjectsByType<RuneObjectSpawnPoint>(FindObjectsSortMode.None);
            if (allSpawnPoints.Length == 0)
            {
                Debug.LogWarning("[RuneBookSpawnPointRandomizer] No RuneObjectSpawnPoint found in the scene!");
                return;
            }
            if (booksToSpawn > allSpawnPoints.Length)
            {
                Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Requested {booksToSpawn} books but only {allSpawnPoints.Length} spawn points available. Spawning {allSpawnPoints.Length} books instead.");
                booksToSpawn = allSpawnPoints.Length;
            }

            List<RuneSO> requiredRunes = new List<RuneSO>();
            if (requiredSpells != null)
            {
                foreach (var spell in requiredSpells)
                {
                    if (spell != null && spell.Runes != null)
                    {
                        foreach (var rune in spell.Runes)
                        {
                            if (rune != null && !requiredRunes.Contains(rune))
                                requiredRunes.Add(rune);
                        }
                    }
                }
            }

            int minimumBooks = requiredRunes.Count;
            if (booksToSpawn < minimumBooks)
            {
                Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Requested {booksToSpawn} books but need at least {minimumBooks} for required spells. Spawning {minimumBooks} books instead.");
                booksToSpawn = minimumBooks;
            }

            List<RuneObjectSpawnPoint> selectedSpawnPoints = GetRandomSpawnPoints(allSpawnPoints, booksToSpawn);

            for (int i = 0; i < requiredRunes.Count && i < selectedSpawnPoints.Count; i++)
            {
                SpawnBookAtPoint(selectedSpawnPoints[i], requiredRunes[i]);
            }
            for (int i = requiredRunes.Count; i < selectedSpawnPoints.Count; i++)
            {
                SpawnBookAtPoint(selectedSpawnPoints[i]);
            }

            //Debug.Log($"[RuneBookSpawnPointRandomizer] Successfully spawned {booksToSpawn} books at random locations.");
        }

        private List<RuneObjectSpawnPoint> GetRandomSpawnPoints(RuneObjectSpawnPoint[] allSpawnPoints, int count)
        {
            List<RuneObjectSpawnPoint> shuffled = allSpawnPoints.OrderBy(x => Random.value).ToList();
            return shuffled.Take(count).ToList();
        }

        private void SpawnBookAtPoint(RuneObjectSpawnPoint spawnPoint, RuneSO forceRune = null)
        {
            if (spawnPoint.AllowedObjects == null || spawnPoint.AllowedObjects.Length == 0)
            {
                Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Spawn point '{spawnPoint.name}' has no AllowedObjects assigned!", spawnPoint);
                return;
            }
            GameObject[] validObjects = spawnPoint.AllowedObjects.Where(obj => obj != null).ToArray();
            if (validObjects.Length == 0)
            {
                Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Spawn point '{spawnPoint.name}' has only null AllowedObjects!", spawnPoint);
                return;
            }
            GameObject bookPrefab = validObjects[Random.Range(0, validObjects.Length)];

            // This logic selects a rune for the book instance.
            // Might cause trouble whenever we have the definitive assignment data coming from gameplay flow.
            // Currently guarantees that the assignment spell(s) runes are spawned first, the rest are randomized
            // Might have to revisit this logic later
            RuneSO selectedRune;
            if (forceRune != null)
            {
                selectedRune = forceRune;
            }
            else
            {
                List<RuneSO> possibleRunes = spawnPoint.GetPossibleRunes().ToList();
                if (possibleRunes.Count == 0)
                {
                    Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Spawn point '{spawnPoint.name}' has no possible runes to assign!", spawnPoint);
                    return;
                }
                selectedRune = possibleRunes[Random.Range(0, possibleRunes.Count)];
            }

            GameObject bookInstance = Instantiate(bookPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);
            Book bookComponent = bookInstance.GetComponent<Book>();
            if (bookComponent != null)
            {
                var runeField = typeof(Book).GetField("rune", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (runeField != null)
                {
                    runeField.SetValue(bookComponent, selectedRune);
                    //Debug.Log($"[RuneBookSpawnPointRandomizer] Spawned book with rune '{selectedRune.name}' at '{spawnPoint.name}'");
                }
                else
                    Debug.LogError("[RuneBookSpawnPointRandomizer] Could not find 'rune' field in Book script!");
            }
            else
                Debug.LogWarning($"[RuneBookSpawnPointRandomizer] Book prefab '{bookPrefab.name}' does not have a Book component!", bookPrefab);

            Spawn(bookInstance);
        }
    }
}