using System.Collections;
using System.Collections.Generic;
using RooseLabs.Enemies;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        [Header("Enemy Spawn Manager")]
        [SerializeField] private EnemySpawnManager enemySpawnManager;

        // Track which runes are required for tasks to detect task-progress
        public readonly HashSet<RuneSO> currentRequiredRunes = new();

        /// <summary>
        /// Initialize the enemy spawn manager with the patrol route
        /// </summary>
        private void InitializeEnemySpawnManager()
        {
            if (!IsServerInitialized) return;

            // Find or create spawn manager
            if (enemySpawnManager == null)
            {
                enemySpawnManager = FindFirstObjectByType<EnemySpawnManager>();

                if (enemySpawnManager == null)
                {
                    this.LogWarning("No EnemySpawnManager found in scene!");
                    return;
                }
            }

            // Wait for patrol route to be ready, then start spawning
            StartCoroutine(WaitForPatrolRouteAndStartSpawning());
        }

        /// <summary>
        /// Wait for patrol route generation before starting enemy spawns
        /// </summary>
        private IEnumerator WaitForPatrolRouteAndStartSpawning()
        {
            // Wait for patrol zones
            int maxWait = 60;
            int waited = 0;
            while (roomPatrolZones == null && waited < maxWait)
            {
                yield return null;
                waited++;
            }

            if (roomPatrolZones == null)
            {
                this.LogWarning("Patrol zones not ready, spawning anyway...");
            }

            // Start enemy spawning with zones
            if (enemySpawnManager != null)
            {
                enemySpawnManager.OnHeistStart(roomPatrolZones);
            }
        }

        /// <summary>
        /// Cache required runes for current assignment to detect task progress
        /// </summary>
        private void CacheRequiredRunesForAssignment()
        {
            currentRequiredRunes.Clear();

            if (CurrentAssignment == null)
                return;

            foreach (var taskId in CurrentAssignment.tasks)
            {
                var task = TaskDatabase[taskId];
                if (task.CompletionCondition is CastSpellCondition csc)
                {
                    foreach (var rune in csc.Spell.Runes)
                    {
                        currentRequiredRunes.Add(rune);
                    }
                }
            }

            Debug.Log($"Cached {currentRequiredRunes.Count} required runes for spawn system");
        }
    }
}
