using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using RooseLabs.Enemies;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        private const float HeistMaxTime = 30f * 60f; // 30 minutes (base time)
        private const float HeistMinTime = 15f * 60f; // 15 minutes (minimum time)
        private const float HeistTimeReductionPerAdditionalPlayer = 5f * 60f; // Less 5 minutes per additional player

        private bool m_isEndingHeist = false;
        private bool m_isHeistOngoing = false;

        private void HandleHeistSceneLoaded()
        {
            m_heistTimer.ToggleTimerVisibility(true);
            if (IsServerInitialized)
            {
                EnemySpawnManager.Instance.RegisterAllSpawners();
                CacheRequiredRunesForAssignment();
                SpawnHeistRuneContainerObjects();
                InitializeEnemyPatrolSystem();
                InitializeEnemySpawnManager();

                // Determine time limit based on number of players
                float timeLimit = HeistMaxTime - (PlayerHandler.AllCharacters.Count - 1) * HeistTimeReductionPerAdditionalPlayer;
                timeLimit = Mathf.Clamp(timeLimit, HeistMinTime, HeistMaxTime);
                m_heistTimer.StartTimer(timeLimit);
                m_isHeistOngoing = true;
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

        private void UpdateHeist()
        {
            if (!IsServerInitialized) return;
            if (!m_isHeistOngoing) return;
            if (PlayerHandler.AllCharacters.All(player => player.Data.isDead))
            {
                EndHeist(false);
            }
        }

        /// <summary>
        /// Called to end the heist and return to the lobby.
        /// </summary>
        /// <param name="successful">If true, the heist was completed successfully; otherwise, it failed.</param>
        public void EndHeist(bool successful)
        {
            if (!IsServerInitialized) return;
            if (m_isEndingHeist) return;
            m_isEndingHeist = true;
            m_isHeistOngoing = false;
            m_heistTimer.StopTimer();
            if (!successful)
            {
                // Mark all tasks as incomplete
                foreach (var taskId in CurrentAssignment.tasks)
                {
                    var task = TaskDatabase[taskId];
                    task.IsCompleted = false;
                }
            }
            else
            {
                // Permanently learn spells from completed tasks
                foreach (var taskId in CurrentAssignment.tasks)
                {
                    var task = TaskDatabase[taskId];
                    if (!task.IsCompleted) continue;
                    if (task.CompletionCondition is CastSpellCondition csc)
                    {
                        int spellIndex = SpellDatabase.IndexOf(csc.Spell);
                        if (!LearnedSpellsIndices.Contains(spellIndex))
                        {
                            LearnedSpellsIndices.Add(spellIndex);
                        }
                    }
                }
            }
            foreach (var player in PlayerHandler.AllCharacters)
            {
                player.ResetState();
            }
            SceneManagement.SceneManager.Instance.LoadScene(GetSceneName(lobbyScene), PlayerHandler.CharacterNetworkObjects);
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
                this.LogWarning("No RuneObjectSpawnPoint found in the scene!");
                return;
            }
            if (requiredRunes.Count > allSpawnPoints.Length)
            {
                this.LogWarning("Not enough RuneObjectSpawnPoints to spawn all required runes!");
            }
            allSpawnPoints.Shuffle();

            // Spawn required runes first
            foreach (var rune in requiredRunes)
            {
                int selectedSpawnPointIndex = -1;
                for (int i = 0; i < allSpawnPoints.Length; ++i)
                {
                    var spawnPoint = allSpawnPoints[i];
                    if (!spawnPoint) continue;
                    if (spawnPoint.GetPossibleRunes().Contains(rune))
                    {
                        selectedSpawnPointIndex = i;
                        break;
                    }
                }
                if (selectedSpawnPointIndex == -1)
                {
                    this.LogWarning($"No spawn point allows required rune '{rune.name}'!");
                    if (!RuneDatabase.Contains(rune))
                    {
                        this.LogWarning($"Required rune '{rune.name}' is not in the RuneDatabase!");
                    }
                    this.LogWarning("Forcing spawn at next available point.");
                    for (int i = 0; i < allSpawnPoints.Length; ++i)
                    {
                        if (!allSpawnPoints[i]) continue;
                        selectedSpawnPointIndex = i;
                        break;
                    }
                }
                if (selectedSpawnPointIndex == -1)
                {
                    this.LogWarning($"No available spawn point found for required rune '{rune.name}'!");
                }
                else
                {
                    var spawnPoint = allSpawnPoints[selectedSpawnPointIndex];
                    SpawnRuneContainerObjectAtPoint(spawnPoint, rune);
                    Destroy(spawnPoint.gameObject);
                    allSpawnPoints[selectedSpawnPointIndex] = null;
                }
            }

            // Spawn additional books with 10% chance
            foreach (var spawnPoint in allSpawnPoints)
            {
                if (!spawnPoint) continue;
                if (Random.value <= 0.1f)
                    SpawnRuneContainerObjectAtPoint(spawnPoint);
                Destroy(spawnPoint.gameObject);
            }
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
                var possibleRunes = spawnPoint.GetPossibleRunes();
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

        public void ReturnToLobby()
        {
            if (IsServerInitialized)
            {
                EndHeist(true);
            }
            else
            {
                EndHeist_ServerRPC(true);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void EndHeist_ServerRPC(bool successful)
        {
            EndHeist(successful);
        }
    }
}
