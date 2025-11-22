using RooseLabs.Enemies;
using RooseLabs.Utils;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        [Header("Enemy Patrol Settings")]
        private PatrolPointGenerator patrolGenerator;

        // Cache generated patrol route for spawning enemies
        private PatrolRoute currentMapPatrolRoute;

        private void InitializeEnemyPatrolSystem()
        {
            if (!IsServerInitialized) return;

            // Start coroutine to wait for NavMesh before generating
            StartCoroutine(WaitForNavMeshAndGenerate());
        }

        /// <summary>
        /// Wait for NavMesh to be ready before generating patrol points
        /// </summary>
        private IEnumerator WaitForNavMeshAndGenerate()
        {
            // Wait a frame to ensure scene is fully loaded
            yield return null;

            // Wait for NavMesh to be built/loaded
            int maxAttempts = 60; // 60 frames = ~1 second at 60fps
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                // Check if NavMesh exists by sampling a position
                if (NavMesh.SamplePosition(Vector3.zero, out NavMeshHit hit, 100f, NavMesh.AllAreas))
                {
                    this.LogInfo("NavMesh is ready, generating patrol points...");
                    break;
                }

                attempts++;
                yield return null;
            }

            if (attempts >= maxAttempts)
            {
                this.LogWarning("NavMesh not ready after waiting! Attempting generation anyway...");
            }

            // Additional small delay to be safe
            yield return new WaitForSeconds(0.1f);

            // Find or create patrol generator
            if (patrolGenerator == null)
            {
                patrolGenerator = FindFirstObjectByType<PatrolPointGenerator>();

                if (patrolGenerator == null)
                {
                    this.LogWarning("No PatrolPointGenerator found in scene! Creating one...");
                    GameObject generatorObj = new GameObject("PatrolPointGenerator");
                    patrolGenerator = generatorObj.AddComponent<PatrolPointGenerator>();

                    // Configure default settings
                    patrolGenerator.mapContainerTag = "Map";
                    patrolGenerator.groundLayer = LayerMask.GetMask("Ground");
                    patrolGenerator.obstacleLayer = LayerMask.GetMask("Default");
                }
            }

            // Generate patrol points
            this.LogInfo("Generating enemy patrol points...");
            currentMapPatrolRoute = patrolGenerator.GeneratePatrolPoints();

            if (currentMapPatrolRoute != null && currentMapPatrolRoute.Count > 0)
            {
                this.LogInfo($"Patrol route generated with {currentMapPatrolRoute.Count} waypoints");

                // Now assign the route to existing enemies or spawn new ones
                AssignPatrolRoutesToEnemies();
            }
            else
            {
                this.LogWarning("Failed to generate patrol route!");
            }
        }

        /// <summary>
        /// Assign the generated patrol route to all enemies in the scene
        /// </summary>
        private void AssignPatrolRoutesToEnemies()
        {
            // Find all Hanadura enemies
            HanaduraAI[] enemies = FindObjectsByType<HanaduraAI>(FindObjectsSortMode.None);

            foreach (var enemy in enemies)
            {
                // Assign the generated patrol route
                enemy.patrolRoute = currentMapPatrolRoute;

                // Set a random starting waypoint for variety
                if (currentMapPatrolRoute.Count > 0)
                {
                    enemy.startWaypointIndex = Random.Range(0, currentMapPatrolRoute.Count);
                }

                enemy.ReinitializePatrolState();

                this.LogInfo($"Assigned patrol route to {enemy.name} (starting at waypoint {enemy.startWaypointIndex})");
            }
        }

        /// <summary>
        /// Get the current patrol route for the loaded map
        /// </summary>
        public PatrolRoute GetCurrentPatrolRoute()
        {
            return currentMapPatrolRoute;
        }
    }
}