using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using FishNet.Utility.Performance;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        private bool m_hasEnteredLibrary = false;
        private bool m_isEndingHeist = false;

        private void HandleHeistSceneLoaded()
        {
            m_hasEnteredLibrary = false;
            m_heistTimer.ToggleTimerVisibility(true);
            if (IsServerInitialized)
            {
                SpawnHeistRuneContainerObjects();
                m_heistTimer.StartTimer(m_heistTimer.defaultTime);
            }
            else
            {
                DestroyRuneObjectSpawnPoints();
            }
        }

        /// <summary>
        /// Called when exiting the lobby to start a heist. Loads a random heist scene.
        /// </summary>
        public void StartHeist()
        {
            if (!IsServerInitialized) return;
            int randomIndex = Random.Range(0, heistScenes.Length);
            string selectedSceneName = GetSceneName(heistScenes[randomIndex]);
            SceneManagement.SceneManager.Instance.LoadScene(selectedSceneName, PlayerHandler.CharacterNetworkObjects);
            m_isEndingHeist = false;
        }

        /// <summary>
        /// Called to end the heist and return to the lobby.
        /// </summary>
        /// <param name="successful">If true, the heist was completed successfully; otherwise, it failed.</param>
        public void EndHeist(bool successful)
        {
            m_heistTimer.ToggleTimerVisibility(false);
            if (!IsServerInitialized) return;
            if (m_isEndingHeist) return;
            m_isEndingHeist = true;
            m_heistTimer.PauseTimer();
            SceneManagement.SceneManager.Instance.LoadScene(GetSceneName(lobbyScene), PlayerHandler.CharacterNetworkObjects);
            if (!successful)
            {
                m_aboutToLearnSpells.Clear();
                // Mark all tasks as incomplete
                foreach (var taskId in CurrentAssignment.tasks)
                {
                    var task = TaskDatabase[taskId];
                    task.IsCompleted = false;
                }
            }
            else
            {
                foreach (var spell in m_aboutToLearnSpells)
                {
                    int spellIndex = SpellDatabase.IndexOf(spell);
                    if (!LearnedSpellsIndices.Contains(spellIndex))
                    {
                        LearnedSpellsIndices.Add(spellIndex);
                    }
                }
                m_aboutToLearnSpells.Clear();
            }
            foreach (var player in PlayerHandler.AllCharacters)
            {
                player.OnReturnToLobby_TargetRPC(player.Owner);
            }
        }

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
                if (Random.value <= 0.1f)
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

        [ServerRpc(RequireOwnership = false)]
        public void NotifyEnteredLibrary()
        {
            if (m_hasEnteredLibrary) return;
            m_hasEnteredLibrary = true;
            this.LogInfo("Player has entered the library for the first time in this heist.");
        }

        [ServerRpc(RequireOwnership = false)]
        public void NotifyReturnToLobby()
        {
            if (!m_hasEnteredLibrary) return;
            EndHeist(true);
        }
    }
}
