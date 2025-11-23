using RooseLabs.Enemies;
using RooseLabs.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        [Header("Enemy Patrol Settings")]
        private PatrolPointGenerator patrolGenerator;

        private Dictionary<string, RoomPatrolZone> roomPatrolZones;
        private PatrolRoute currentMapPatrolRoute;

        private void InitializeEnemyPatrolSystem()
        {
            if (!IsServerInitialized) return;

            StartCoroutine(WaitForNavMeshAndGenerate());
        }

        private IEnumerator WaitForNavMeshAndGenerate()
        {
            yield return null;

            // Wait for NavMesh
            int maxAttempts = 60;
            int attempts = 0;

            while (attempts < maxAttempts)
            {
                if (NavMesh.SamplePosition(Vector3.zero, out NavMeshHit hit, 100f, NavMesh.AllAreas))
                {
                    Debug.Log("NavMesh is ready, generating patrol zones...");
                    break;
                }

                attempts++;
                yield return null;
            }

            if (attempts >= maxAttempts)
            {
                this.LogWarning("NavMesh not ready after waiting! Attempting generation anyway...");
            }

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

                    patrolGenerator.mapContainerTag = "Map";
                    patrolGenerator.groundLayer = LayerMask.GetMask("Ground");
                    patrolGenerator.obstacleLayer = LayerMask.GetMask("Default");
                    patrolGenerator.useRoomBasedPatrolling = true; // NEW
                }
            }

            // Generate room-based patrol zones
            Debug.Log("Generating room-based enemy patrol zones...");
            roomPatrolZones = patrolGenerator.GenerateRoomPatrolZones();

            if (roomPatrolZones != null && roomPatrolZones.Count > 0)
            {
                Debug.Log($"Patrol zones generated: {roomPatrolZones.Count} rooms");

                // This routing is still not used
                // Grimoires are not spawned by the spawn manager yet
                //GenerateGrimoirePatrolRoutes();
            }
            else
            {
                this.LogWarning("Failed to generate patrol zones!");
            }
        }

        private void GenerateGrimoirePatrolRoutes()
        {
            // TODO
        }

        /// <summary>
        /// Get patrol zone for a specific room
        /// </summary>
        public RoomPatrolZone GetPatrolZone(string roomIdentifier)
        {
            if (roomPatrolZones != null && roomPatrolZones.TryGetValue(roomIdentifier, out RoomPatrolZone zone))
            {
                return zone;
            }

            this.LogWarning($"No patrol zone found for room: {roomIdentifier}");
            return null;
        }

        /// <summary>
        /// Get all patrol zones
        /// </summary>
        public Dictionary<string, RoomPatrolZone> GetAllPatrolZones()
        {
            return roomPatrolZones;
        }

        /// <summary>
        /// Find closest patrol zone to a position
        /// </summary>
        public RoomPatrolZone GetClosestPatrolZone(Vector3 position)
        {
            if (roomPatrolZones == null || roomPatrolZones.Count == 0)
                return null;

            RoomPatrolZone closest = null;
            float closestDist = float.MaxValue;

            foreach (var zone in roomPatrolZones.Values)
            {
                float dist = Vector3.Distance(position, zone.roomBounds.center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }

            return closest;
        }

        // This is in theory not going to be used, however in case i want
        // the global patrol route for debugging or other purposes...
        public PatrolRoute GetCurrentPatrolRoute()
        {
            return currentMapPatrolRoute;
        }
    }
}