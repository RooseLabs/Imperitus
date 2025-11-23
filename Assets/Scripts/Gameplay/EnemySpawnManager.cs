using FishNet.Object;
using RooseLabs.Gameplay;
using RooseLabs.Player;
using RooseLabs.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Manages dynamic enemy spawning throughout the heist based on various triggers.
    /// </summary>
    public class EnemySpawnManager : NetworkBehaviour
    {
        public static EnemySpawnManager Instance { get; private set; }

        [Header("Enemy Prefabs")]
        [Tooltip("Prefab for Hanadura enemy")]
        [SerializeField] private GameObject hanaduraPrefab;

        [Header("Initial Spawn Settings")]
        [Tooltip("Number of enemies to spawn when heist starts")]
        [SerializeField] private int initialSpawnCount = 3;

        [Tooltip("Number of spawners to use for initial spawn (0 = use all available)")]
        [SerializeField] private int initialSpawnerCount = 0;

        [Header("Grimoire Alert Spawn Settings")]
        [Tooltip("Chance (0-100%) to spawn an enemy when Grimoire alerts")]
        [SerializeField][Range(0f, 100f)] private float grimoireAlertSpawnChance = 50f;

        [Header("Enemy Death Respawn Settings")]
        [Tooltip("Chance (0-100%) to instantly spawn replacement when non-Hanadura enemy dies")]
        [SerializeField][Range(0f, 100f)] private float instantRespawnChance = 30f;

        [Tooltip("Base respawn timer in seconds when instant spawn fails")]
        [SerializeField] private float baseRespawnTime = 60f;

        [Tooltip("Minimum respawn time in seconds (scaled as heist progresses)")]
        [SerializeField] private float minRespawnTime = 20f;

        [Header("Task Progress Spawn Settings")]
        [Tooltip("Enable spawning when task-required runes are collected")]
        [SerializeField] private bool enableTaskProgressSpawns = true;

        [Tooltip("Cooldown between task-progress spawns (0 = no cooldown)")]
        [SerializeField] private float taskProgressSpawnCooldown = 30f;

        [Header("Room Detection")]
        [Tooltip("Tag used to identify room GameObjects")]
        [SerializeField] private string roomTag = "Room";

        [Tooltip("Maximum distance to consider a spawner as 'in' a room")]
        [SerializeField] private float roomProximityThreshold = 50f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private bool showDebugGizmos = true;

        // Runtime data
        private List<EnemySpawner> allSpawners = new List<EnemySpawner>();
        private Dictionary<GameObject, EnemySpawner> activeEnemies = new Dictionary<GameObject, EnemySpawner>();
        private Dictionary<string, List<GameObject>> roomActiveEnemies = new Dictionary<string, List<GameObject>>();
        private Queue<PendingRespawn> respawnQueue = new Queue<PendingRespawn>();
        private PatrolRoute currentPatrolRoute;
        private float lastTaskProgressSpawnTime = -999f;
        private bool heistStarted = false;

        private class PendingRespawn
        {
            public float spawnTime;
            public EnemySpawner preferredSpawner;
            public Vector3 targetAlertPosition;
            public bool isAlertSpawn;
        }

        #region Initialization

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Register all EnemySpawner components in the scene
        /// </summary>
        public void RegisterAllSpawners()
        {
            allSpawners.Clear();
            EnemySpawner[] spawners = FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None);

            foreach (var spawner in spawners)
            {
                if (spawner != null)
                {
                    allSpawners.Add(spawner);
                    spawner.AssignRoomIdentifier(roomTag, roomProximityThreshold);
                }
            }

            LogDebug($"Registered {allSpawners.Count} enemy spawners");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Call this when the heist starts to spawn initial enemies
        /// </summary>
        public void OnHeistStart(Dictionary<string, RoomPatrolZone> patrolZones = null)
        {
            if (!IsServerInitialized)
                return;

            heistStarted = true;

            // Store reference to zones
            if (patrolZones != null)
            {
                LogDebug($"Received {patrolZones.Count} patrol zones");
            }

            SpawnInitialEnemies();
        }

        /// <summary>
        /// Call this when a Grimoire alerts to potentially spawn reinforcement
        /// </summary>
        public void OnGrimoireAlert(Vector3 playerPosition)
        {
            if (!IsServerInitialized || !heistStarted)
                return;

            if (Random.Range(0f, 100f) <= grimoireAlertSpawnChance)
            {
                LogDebug($"Grimoire alert triggered spawn at {playerPosition}");

                // Find closest non-current-room spawner
                EnemySpawner spawner = GetClosestSpawnerNotInPlayerRoom(playerPosition);

                if (spawner != null)
                {
                    // Spawn immediately and alert to player position
                    GameObject enemy = SpawnEnemyAtSpawner(spawner, playerPosition, true);
                    if (enemy != null)
                    {
                        LogDebug($"Spawned alert enemy at {spawner.name}");
                    }
                }
            }
        }

        /// <summary>
        /// Call this when an enemy dies to handle respawn logic
        /// </summary>
        public void OnEnemyDeath(GameObject enemyObject, bool isHanadura)
        {
            if (!IsServerInitialized || !heistStarted)
                return;

            // Remove from active enemies tracking
            if (!activeEnemies.TryGetValue(enemyObject, out EnemySpawner spawner))
            {
                LogDebug("Enemy died but was not tracked by spawn manager");
                return;
            }

            activeEnemies.Remove(enemyObject);

            // Clean up room tracking
            string roomId = spawner.RoomIdentifier;
            if (roomActiveEnemies.ContainsKey(roomId))
            {
                roomActiveEnemies[roomId].Remove(enemyObject);

                // Release route from patrol zone
                RoomPatrolZone zone = GameManager.Instance.GetPatrolZone(roomId);
                zone?.ReleaseRoute(enemyObject);
            }

            LogDebug($"Enemy died at spawner: {spawner.name} (room: {roomId})");

            // Roll for instant respawn (existing logic)
            if (Random.Range(0f, 100f) <= instantRespawnChance)
            {
                LogDebug("Instant respawn triggered");
                StartCoroutine(DelayedSpawn(spawner, 0.5f));
            }
            else
            {
                // Queue for delayed respawn (existing logic)
                float heistProgress = GetHeistProgress();
                float respawnTime = Mathf.Lerp(baseRespawnTime, minRespawnTime, heistProgress);

                PendingRespawn pending = new PendingRespawn
                {
                    spawnTime = Time.time + respawnTime,
                    preferredSpawner = spawner,
                    targetAlertPosition = Vector3.zero,
                    isAlertSpawn = false
                };

                respawnQueue.Enqueue(pending);
                LogDebug($"Queued respawn in {respawnTime:F1} seconds");
            }
        }

        /// <summary>
        /// Call this when a task-required rune is collected
        /// </summary>
        public void OnTaskRuneCollected(Vector3 collectionPosition)
        {
            if (!IsServerInitialized || !heistStarted || !enableTaskProgressSpawns)
                return;

            // Check cooldown
            if (Time.time - lastTaskProgressSpawnTime < taskProgressSpawnCooldown)
            {
                LogDebug($"Task rune spawn on cooldown ({taskProgressSpawnCooldown - (Time.time - lastTaskProgressSpawnTime):F1}s remaining)");
                return;
            }

            lastTaskProgressSpawnTime = Time.time;
            LogDebug($"Task rune collected at {collectionPosition}, spawning enemy");

            // Spawn at closest non-current-room spawner
            EnemySpawner spawner = GetClosestSpawnerNotInPlayerRoom(collectionPosition);

            if (spawner != null)
            {
                StartCoroutine(DelayedSpawn(spawner, 1f)); // 1 second delay for task progress spawns
            }
        }

        #endregion

        #region Spawning Logic

        private void SpawnInitialEnemies()
        {
            if (allSpawners.Count == 0)
            {
                LogDebug("No spawners available for initial spawn!", true);
                return;
            }

            // Determine how many spawners to use
            int spawnersToUse = initialSpawnerCount > 0
                ? Mathf.Min(initialSpawnerCount, allSpawners.Count)
                : allSpawners.Count;

            // Select random spawners
            List<EnemySpawner> selectedSpawners = allSpawners
                .OrderBy(x => Random.value)
                .Take(spawnersToUse)
                .ToList();

            // Spawn enemies
            int spawned = 0;
            for (int i = 0; i < initialSpawnCount && i < selectedSpawners.Count; i++)
            {
                GameObject enemy = SpawnEnemyAtSpawner(selectedSpawners[i % selectedSpawners.Count]);
                if (enemy != null)
                    spawned++;
            }

            LogDebug($"Initial spawn complete: {spawned}/{initialSpawnCount} enemies spawned across {selectedSpawners.Count} spawners");
        }

        private GameObject SpawnEnemyAtSpawner(EnemySpawner spawner, Vector3 alertPosition = default, bool isAlert = false)
        {
            if (hanaduraPrefab == null)
            {
                LogDebug("Hanadura prefab not assigned!", true);
                return null;
            }

            // Instantiate enemy
            GameObject enemyObj = Instantiate(hanaduraPrefab, spawner.SpawnPoint.position, spawner.SpawnPoint.rotation);

            // Configure Hanadura with ROOM-BASED patrol
            if (enemyObj.TryGetComponent(out HanaduraAI hanadura))
            {
                AssignRoomPatrolRoute(hanadura, spawner);

                // If this is an alert spawn, alert the enemy to the player position
                if (isAlert && alertPosition != default)
                {
                    StartCoroutine(AlertEnemyAfterSpawn(hanadura, alertPosition));
                }
            }

            // Network spawn
            Spawn(enemyObj);

            // Track this enemy
            activeEnemies[enemyObj] = spawner;
            spawner.OnEnemySpawned(enemyObj);

            // Track in room
            string roomId = spawner.RoomIdentifier;
            if (!roomActiveEnemies.ContainsKey(roomId))
            {
                roomActiveEnemies[roomId] = new List<GameObject>();
            }
            roomActiveEnemies[roomId].Add(enemyObj);

            // Subscribe to death event
            if (enemyObj.TryGetComponent(out EnemyData enemyData))
            {
                StartCoroutine(WaitForDeathSubscription(enemyObj, enemyData));
            }

            return enemyObj;
        }

        private IEnumerator AlertEnemyAfterSpawn(HanaduraAI hanadura, Vector3 alertPosition)
        {
            // Wait a frame for network initialization
            yield return null;
            yield return null;

            // Alert the enemy to the player position
            hanadura.AlertToPosition(alertPosition);
            LogDebug($"Alerted spawned enemy to position: {alertPosition}");
        }

        private IEnumerator WaitForDeathSubscription(GameObject enemyObj, EnemyData enemyData)
        {
            // Wait for enemy to be fully initialized
            yield return new WaitForSeconds(0.5f);

            // Check if enemy still exists
            if (enemyObj == null)
                yield break;

            // Monitor for death
            while (enemyObj != null && !enemyData.IsDead)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // Enemy died
            if (enemyObj != null)
            {
                OnEnemyDeath(enemyObj, enemyData.EnemyType.name.Contains("Hanadura"));
            }
        }

        private IEnumerator DelayedSpawn(EnemySpawner spawner, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (spawner != null)
            {
                SpawnEnemyAtSpawner(spawner);
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Assign a room-based patrol route to a Hanadura
        /// </summary>
        private void AssignRoomPatrolRoute(HanaduraAI hanadura, EnemySpawner spawner)
        {
            string roomId = spawner.RoomIdentifier;

            if (string.IsNullOrEmpty(roomId) || roomId == "Unknown")
            {
                LogDebug($"Spawner has no room identifier, using closest zone", true);
                roomId = FindClosestRoomToPosition(spawner.SpawnPoint.position);
            }

            // Get patrol zone for this room
            RoomPatrolZone zone = GameManager.Instance.GetPatrolZone(roomId);

            if (zone == null)
            {
                LogDebug($"No patrol zone found for room '{roomId}'", true);
                return;
            }

            // Generate unique route for this enemy
            PatrolRoute route = zone.GenerateUniqueRoute(hanadura.gameObject);

            if (route != null && route.Count > 0)
            {
                hanadura.patrolRoute = route;
                hanadura.startWaypointIndex = 0; // Always start at beginning of custom route
                hanadura.ReinitializePatrolState();

                LogDebug($"Assigned {route.Count}-waypoint patrol route to {hanadura.name} in room '{roomId}'");
            }
            else
            {
                LogDebug($"Failed to generate route for room '{roomId}'", true);
            }
        }

        /// <summary>
        /// Find closest room to a position (fallback when spawner has no room ID)
        /// </summary>
        private string FindClosestRoomToPosition(Vector3 position)
        {
            RoomPatrolZone closest = GameManager.Instance.GetClosestPatrolZone(position);
            return closest?.roomIdentifier ?? "Unknown";
        }

        private EnemySpawner GetClosestSpawnerNotInPlayerRoom(Vector3 playerPosition)
        {
            // Find which room the player is in
            string playerRoom = GetRoomForPosition(playerPosition);

            // Filter spawners not in player's room
            List<EnemySpawner> validSpawners = allSpawners
                .Where(s => s.RoomIdentifier != playerRoom)
                .ToList();

            if (validSpawners.Count == 0)
            {
                LogDebug("No valid spawners outside player's room, using any spawner");
                validSpawners = allSpawners;
            }

            if (validSpawners.Count == 0)
                return null;

            // Find closest
            EnemySpawner closest = validSpawners
                .OrderBy(s => Vector3.Distance(s.SpawnPoint.position, playerPosition))
                .FirstOrDefault();

            return closest;
        }

        /// <summary>
        /// Get number of active enemies in a specific room
        /// </summary>
        public int GetActiveEnemyCountInRoom(string roomId)
        {
            if (!roomActiveEnemies.ContainsKey(roomId))
                return 0;

            // Clean up null references
            roomActiveEnemies[roomId].RemoveAll(e => e == null);
            return roomActiveEnemies[roomId].Count;
        }

        private string GetRoomForPosition(Vector3 position)
        {
            GameObject[] rooms = GameObject.FindGameObjectsWithTag(roomTag);

            if (rooms.Length == 0)
            {
                LogDebug("No rooms found with tag: " + roomTag, true);
                return "Unknown";
            }

            float closestDistance = float.MaxValue;
            GameObject closestRoom = null;

            foreach (GameObject room in rooms)
            {
                // Try bounds-based detection first (more accurate)
                Bounds roomBounds = CalculateRoomBounds(room);
                Vector3 roomCenter;

                if (roomBounds.size != Vector3.zero)
                {
                    // Check if position is inside room bounds
                    if (roomBounds.Contains(position))
                    {
                        LogDebug($"Position is inside room: {room.name}");
                        return room.name;
                    }

                    // Use bounds center for distance calculation
                    roomCenter = roomBounds.center;
                }
                else
                {
                    // Fallback to pivot point if no bounds found
                    roomCenter = room.transform.position;
                    LogDebug($"Room {room.name} has no bounds, using pivot point", true);
                }

                float distance = Vector3.Distance(roomCenter, position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoom = room;
                }
            }

            if (closestRoom != null)
            {
                LogDebug($"Closest room to position is: {closestRoom.name} (distance: {closestDistance:F1}m)");
            }

            return closestRoom != null ? closestRoom.name : "Unknown";
        }

        /// <summary>
        /// Calculate combined bounds of a room GameObject and all its children
        /// </summary>
        private Bounds CalculateRoomBounds(GameObject room)
        {
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            Collider[] colliders = room.GetComponentsInChildren<Collider>();

            if (renderers.Length == 0 && colliders.Length == 0)
                return new Bounds(room.transform.position, Vector3.zero);

            Bounds bounds = new Bounds(room.transform.position, Vector3.zero);
            bool boundsInitialized = false;

            foreach (Renderer r in renderers)
            {
                if (!boundsInitialized)
                {
                    bounds = r.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            foreach (Collider c in colliders)
            {
                if (!boundsInitialized)
                {
                    bounds = c.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return bounds;
        }

        private float GetHeistProgress()
        {
            return GameManager.Instance.GetHeistTimerValue();
        }

        private void LogDebug(string message, bool isWarning = false)
        {
            if (!showDebugLogs)
                return;

            string formatted = $"[EnemySpawnManager] {message}";

            if (isWarning)
                Debug.LogWarning(formatted);
            else
                Debug.Log(formatted);
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (!IsServerInitialized || !heistStarted)
                return;

            // Process respawn queue
            while (respawnQueue.Count > 0 && respawnQueue.Peek().spawnTime <= Time.time)
            {
                PendingRespawn pending = respawnQueue.Dequeue();

                if (pending.isAlertSpawn)
                {
                    SpawnEnemyAtSpawner(pending.preferredSpawner, pending.targetAlertPosition, true);
                }
                else
                {
                    SpawnEnemyAtSpawner(pending.preferredSpawner);
                }

                LogDebug("Processed queued respawn");
            }
        }

        #endregion

        #region Debug Visualization

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || allSpawners.Count == 0)
                return;

            // Draw connections between spawners
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            for (int i = 0; i < allSpawners.Count - 1; i++)
            {
                if (allSpawners[i] != null && allSpawners[i + 1] != null)
                {
                    Gizmos.DrawLine(
                        allSpawners[i].SpawnPoint.position,
                        allSpawners[i + 1].SpawnPoint.position
                    );
                }
            }
        }

        #endregion
    }
}