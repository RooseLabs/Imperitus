using RooseLabs.Enemies;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        [Header("Enemy Spawn Manager")]
        [SerializeField] private EnemySpawnManager enemySpawnManager;

        // Track which runes are required for tasks to detect task-progress
        public HashSet<RuneSO> currentRequiredRunes = new HashSet<RuneSO>();

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
        private System.Collections.IEnumerator WaitForPatrolRouteAndStartSpawning()
        {
            // Wait for patrol route to be generated
            int maxWait = 60;
            int waited = 0;

            while (currentMapPatrolRoute == null && waited < maxWait)
            {
                yield return null;
                waited++;
            }

            if (currentMapPatrolRoute == null)
            {
                this.LogWarning("Patrol route not ready after waiting, spawning enemies anyway...");
            }
            else
            {
                this.LogInfo($"Patrol route ready with {currentMapPatrolRoute.Count} waypoints, starting enemy spawns");
            }

            // Start enemy spawning
            if (enemySpawnManager != null)
            {
                enemySpawnManager.OnHeistStart(currentMapPatrolRoute);
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

            this.LogInfo($"Cached {currentRequiredRunes.Count} required runes for spawn system");
        }
    }
}