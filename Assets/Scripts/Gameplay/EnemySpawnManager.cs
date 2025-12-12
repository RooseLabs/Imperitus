using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using RooseLabs.Gameplay;
using RooseLabs.Utils;
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

        [Tooltip("Prefab for Grimoire enemy")]
        [SerializeField] private GameObject grimoirePrefab;

        [Header("Initial Spawn Settings")]
        [Tooltip("Number of Hanadura enemies to spawn when heist starts")]
        [SerializeField] private int initialHanaduraCount = 3;

        [Tooltip("Number of Grimoire enemies to spawn when heist starts")]
        [SerializeField] private int initialGrimoireCount = 1;

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

        [Header("Room Limitations")]
        [Tooltip("Maximum number of Hanaduras allowed per room")]
        [SerializeField] private int maxHanaduraPerRoom = 2;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // Runtime data
        private List<EnemySpawner> allSpawners = new List<EnemySpawner>();
        public Dictionary<GameObject, EnemySpawner> activeEnemies = new Dictionary<GameObject, EnemySpawner>();
        private Dictionary<string, List<GameObject>> roomActiveEnemies = new Dictionary<string, List<GameObject>>();
        private Dictionary<string, GameObject> roomActiveGrimoires = new Dictionary<string, GameObject>();
        private Dictionary<string, List<GameObject>> roomActiveHanaduras = new Dictionary<string, List<GameObject>>();
        private Dictionary<string, List<GameObject>> roomRandomPatrollers = new Dictionary<string, List<GameObject>>();
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
            public bool isGrimoire;
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

            this.LogInfo($"Registered {allSpawners.Count} enemy spawners");
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
                this.LogInfo($"Received {patrolZones.Count} patrol zones");
            }

            SpawnInitialEnemies();
        }

        /// <summary>
        /// Register a Hanadura as randomly patrolling a room
        /// </summary>
        public void RegisterRandomPatroller(GameObject hanadura, string roomId)
        {
            if (!roomRandomPatrollers.ContainsKey(roomId))
            {
                roomRandomPatrollers[roomId] = new List<GameObject>();
            }

            if (!roomRandomPatrollers[roomId].Contains(hanadura))
            {
                roomRandomPatrollers[roomId].Add(hanadura);
                this.LogInfo($"Registered {hanadura.name} as random patroller in room '{roomId}'");
            }
        }

        /// <summary>
        /// Unregister a Hanadura from randomly patrolling a room
        /// </summary>
        public void UnregisterRandomPatroller(GameObject hanadura, string roomId)
        {
            if (roomRandomPatrollers.ContainsKey(roomId))
            {
                roomRandomPatrollers[roomId].Remove(hanadura);
                this.LogInfo($"Unregistered {hanadura.name} from random patrol in room '{roomId}'");

                // Clean up null references
                roomRandomPatrollers[roomId].RemoveAll(h => h == null);

                // Remove empty list
                if (roomRandomPatrollers[roomId].Count == 0)
                {
                    roomRandomPatrollers.Remove(roomId);
                }
            }
        }

        /// <summary>
        /// Check if a room already has a random patroller
        /// </summary>
        public bool IsRoomBeingRandomlyPatrolled(string roomId)
        {
            if (roomRandomPatrollers.ContainsKey(roomId))
            {
                // Clean up null references
                roomRandomPatrollers[roomId].RemoveAll(h => h == null);

                if (roomRandomPatrollers[roomId].Count == 0)
                {
                    roomRandomPatrollers.Remove(roomId);
                    return false;
                }

                return true;
            }

            return false;
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
                this.LogInfo($"Grimoire alert triggered spawn at {playerPosition}");

                // Find closest non-current-room spawner
                EnemySpawner spawner = GetClosestSpawnerNotInPlayerRoom(playerPosition);

                if (spawner != null)
                {
                    GameObject enemy = SpawnEnemyAtSpawner(spawner, false, playerPosition, true);
                    if (enemy != null)
                    {
                        this.LogInfo($"Spawned alert Hanadura at {spawner.name}");
                    }
                }
            }
        }

        /// <summary>
        /// Call this when an enemy dies to handle respawn logic
        /// </summary>
        public void OnEnemyDeath(GameObject enemyObject, bool isGrimoire)
        {
            if (!IsServerInitialized || !heistStarted)
                return;

            // Remove from active enemies tracking
            if (!activeEnemies.TryGetValue(enemyObject, out EnemySpawner spawner))
            {
                this.LogInfo("Enemy died but was not tracked by spawn manager");
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

            // Clean up type-specific tracking
            if (isGrimoire && roomActiveGrimoires.ContainsKey(roomId))
            {
                if (roomActiveGrimoires[roomId] == enemyObject)
                {
                    roomActiveGrimoires.Remove(roomId);
                    this.LogInfo($"Grimoire removed from room '{roomId}' tracking");
                }
            }
            else if (!isGrimoire && roomActiveHanaduras.ContainsKey(roomId))
            {
                roomActiveHanaduras[roomId].Remove(enemyObject);
                this.LogInfo($"Hanadura removed from room '{roomId}' tracking (now {roomActiveHanaduras[roomId].Count}/{maxHanaduraPerRoom})");
            }

            this.LogInfo($"{(isGrimoire ? "Grimoire" : "Hanadura")} died at spawner: {spawner.name} (room: {roomId})");

            // Roll for instant respawn
            if (Random.Range(0f, 100f) <= instantRespawnChance)
            {
                this.LogInfo("Instant respawn triggered");
                StartCoroutine(DelayedSpawn(spawner, 0.5f, isGrimoire));
            }
            else
            {
                // Queue for delayed respawn
                float heistProgress = GetHeistProgress();
                float respawnTime = Mathf.Lerp(baseRespawnTime, minRespawnTime, heistProgress);

                PendingRespawn pending = new PendingRespawn
                {
                    spawnTime = Time.time + respawnTime,
                    preferredSpawner = spawner,
                    targetAlertPosition = Vector3.zero,
                    isAlertSpawn = false,
                    isGrimoire = isGrimoire
                };

                respawnQueue.Enqueue(pending);
                this.LogInfo($"Queued {(isGrimoire ? "Grimoire" : "Hanadura")} respawn in {respawnTime:F1} seconds");
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
                this.LogInfo($"Task rune spawn on cooldown ({taskProgressSpawnCooldown - (Time.time - lastTaskProgressSpawnTime):F1}s remaining)");
                return;
            }

            lastTaskProgressSpawnTime = Time.time;
            this.LogInfo($"Task rune collected at {collectionPosition}, spawning enemy");

            // Spawn at closest non-current-room spawner
            EnemySpawner spawner = GetClosestSpawnerNotInPlayerRoom(collectionPosition);

            if (spawner != null)
            {
                StartCoroutine(DelayedSpawn(spawner, 1f, false)); // 1 second delay for task progress spawns
            }
        }
        #endregion

        #region Spawning Logic
        private void SpawnInitialEnemies()
        {
            if (allSpawners.Count == 0)
            {
                this.LogWarning("No spawners available for initial spawn!");
                return;
            }

            int hanaduraSpawned = 0;
            int grimoireSpawned = 0;

            // Spawn Hanaduras
            for (int i = 0; i < initialHanaduraCount; i++)
            {
                // Find best spawner across ALL spawners, prioritizing empty rooms
                EnemySpawner bestSpawner = null;
                int lowestHanaduraCount = int.MaxValue;

                // Create a randomized list to avoid always picking the same spawner in a room
                List<EnemySpawner> shuffledSpawners = allSpawners.OrderBy(x => Random.value).ToList();

                foreach (var spawner in shuffledSpawners)
                {
                    string roomId = spawner.RoomIdentifier;

                    if (!CanSpawnHanaduraInRoom(roomId))
                        continue;

                    int roomCount = GetHanaduraCountInRoom(roomId);

                    // Strict priority: empty rooms (0) > rooms with 1 > rooms with 2 (if allowed)
                    if (roomCount < lowestHanaduraCount)
                    {
                        lowestHanaduraCount = roomCount;
                        bestSpawner = spawner;

                        // If we found an empty room, use it immediately (highest priority)
                        if (roomCount == 0)
                            break;
                    }
                }

                if (bestSpawner != null)
                {
                    GameObject hanadura = SpawnEnemyAtSpawner(bestSpawner, false);
                    if (hanadura != null)
                    {
                        hanaduraSpawned++;
                        this.LogInfo($"Hanadura {hanaduraSpawned}/{initialHanaduraCount} spawned in room '{bestSpawner.RoomIdentifier}' (priority: {lowestHanaduraCount} existing)");
                    }
                }
                else
                {
                    this.LogWarning($"Could not find valid spawner for Hanadura {i + 1} (all rooms at max capacity)");
                    break;
                }
            }

            // Spawn Grimoires
            List<string> roomsWithGrimoires = new List<string>();

            for (int i = 0; i < initialGrimoireCount; i++)
            {
                EnemySpawner validSpawner = null;

                // First Grimoire is always spawned in biggest room (by volume)
                if (i == 0)
                {
                    string biggestRoomId = RoomCalculations.FindBiggestRoom(roomTag);

                    if (!string.IsNullOrEmpty(biggestRoomId) && biggestRoomId != "Unknown")
                    {
                        // Find a spawner in the biggest room
                        List<EnemySpawner> biggestRoomSpawners = allSpawners
                            .Where(s => s.RoomIdentifier == biggestRoomId)
                            .OrderBy(x => Random.value)
                            .ToList();

                        if (biggestRoomSpawners.Count > 0 && CanSpawnGrimoireInRoom(biggestRoomId))
                        {
                            validSpawner = biggestRoomSpawners[0];
                            roomsWithGrimoires.Add(biggestRoomId);
                            this.LogInfo($"First Grimoire assigned to BIGGEST room: '{biggestRoomId}' (volume: {RoomCalculations.GetRoomVolume(biggestRoomId, roomTag):F2})");
                        }
                        else
                        {
                            this.LogWarning($"Biggest room '{biggestRoomId}' already has a Grimoire or no spawners available");
                        }
                    }
                    else
                    {
                        this.LogWarning("Could not determine biggest room");
                    }
                }

                // The remaining Grimoires use randomized algorithm
                if (validSpawner == null)
                {
                    // Randomize to avoid always picking the same rooms
                    List<EnemySpawner> shuffledSpawners = allSpawners.OrderBy(x => Random.value).ToList();

                    foreach (var spawner in shuffledSpawners)
                    {
                        string roomId = spawner.RoomIdentifier;

                        // Skip if this room already has a Grimoire (from this spawn session or previously)
                        if (roomsWithGrimoires.Contains(roomId) || !CanSpawnGrimoireInRoom(roomId))
                            continue;

                        validSpawner = spawner;
                        roomsWithGrimoires.Add(roomId);
                        break;
                    }
                }

                if (validSpawner != null)
                {
                    GameObject grimoire = SpawnEnemyAtSpawner(validSpawner, true);
                    if (grimoire != null)
                    {
                        grimoireSpawned++;
                        this.LogInfo($"Grimoire {grimoireSpawned}/{initialGrimoireCount} spawned in room '{validSpawner.RoomIdentifier}'");
                    }
                }
                else
                {
                    this.LogWarning($"Could not find valid spawner for Grimoire {i + 1} (all rooms occupied or insufficient rooms)");
                    break;
                }
            }

            this.LogInfo($"Initial spawn complete: {hanaduraSpawned}/{initialHanaduraCount} Hanaduras across rooms, {grimoireSpawned}/{initialGrimoireCount} Grimoires");

            // Debug room distribution
            // DebugRoomDistribution();
        }

        private GameObject SpawnEnemyAtSpawner(EnemySpawner spawner, bool isGrimoire, Vector3 alertPosition = default, bool isAlert = false)
        {
            GameObject prefabToSpawn = isGrimoire ? grimoirePrefab : hanaduraPrefab;
            if (prefabToSpawn == null)
            {
                this.LogWarning($"{(isGrimoire ? "Grimoire" : "Hanadura")} prefab not assigned!");
                return null;
            }
            string roomId = spawner.RoomIdentifier;
            // Check room limitations
            if (isGrimoire && !CanSpawnGrimoireInRoom(roomId))
            {
                this.LogInfo($"Cannot spawn Grimoire at {spawner.name} - room '{roomId}' already has one");
                return null;
            }
            if (!isGrimoire && !CanSpawnHanaduraInRoom(roomId))
            {
                this.LogInfo($"Cannot spawn Hanadura at {spawner.name} - room '{roomId}' at max capacity ({maxHanaduraPerRoom})");
                return null;
            }
            // Instantiate enemy
            GameObject enemyObj = Instantiate(prefabToSpawn, spawner.SpawnPoint.position, spawner.SpawnPoint.rotation);

            // Generate unique enemy name
            string enemyTypeName = isGrimoire ? "Grimoire" : "Hanadura";
            int uniqueId = GetNextUniqueIdForEnemyInRoom(roomId, isGrimoire);
            enemyObj.name = $"{enemyTypeName}_{roomId}_{uniqueId:D2}";

            if (isGrimoire)
            {
                if (enemyObj.TryGetComponent(out GrimoireAI grimoire))
                {
                    AssignRoomPatrolRoute(grimoire, spawner);
                    // If this is an alert spawn, alert the enemy to the player position
                    if (isAlert && alertPosition != default)
                    {
                        StartCoroutine(AlertGrimoireAfterSpawn(grimoire, alertPosition));
                    }
                }
            }
            else
            {
                if (enemyObj.TryGetComponent(out HanaduraAI hanadura))
                {
                    AssignRoomPatrolRoute(hanadura, spawner);
                    // If this is an alert spawn, alert the enemy to the player position
                    if (isAlert && alertPosition != default)
                    {
                        StartCoroutine(AlertEnemyAfterSpawn(hanadura, alertPosition));
                    }
                }
            }
            // Network spawn
            Spawn(enemyObj);
            // Track this enemy
            activeEnemies[enemyObj] = spawner;
            spawner.OnEnemySpawned(enemyObj);
            // Track in room
            if (!roomActiveEnemies.ContainsKey(roomId))
            {
                roomActiveEnemies[roomId] = new List<GameObject>();
            }
            roomActiveEnemies[roomId].Add(enemyObj);
            // Track by enemy type
            if (isGrimoire)
            {
                roomActiveGrimoires[roomId] = enemyObj;
                this.LogInfo($"Grimoire spawned in room '{roomId}' as '{enemyObj.name}'");
            }
            else
            {
                if (!roomActiveHanaduras.ContainsKey(roomId))
                {
                    roomActiveHanaduras[roomId] = new List<GameObject>();
                }
                roomActiveHanaduras[roomId].Add(enemyObj);
                this.LogInfo($"Hanadura spawned in room '{roomId}' as '{enemyObj.name}' (now {roomActiveHanaduras[roomId].Count}/{maxHanaduraPerRoom})");
            }
            // Subscribe to death event
            if (enemyObj.TryGetComponent(out BaseEnemy enemyData))
            {
                StartCoroutine(WaitForDeathSubscription(enemyObj, enemyData, isGrimoire));
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
            this.LogInfo($"Alerted spawned enemy to position: {alertPosition}");
        }

        private IEnumerator AlertGrimoireAfterSpawn(GrimoireAI grimoire, Vector3 alertPosition)
        {
            // Wait a frame for network initialization
            yield return null;
            yield return null;

            // Grimoires don't have AlertToPosition method, but we can transition them to tracking state
            // This is handled by their own detection system
            this.LogInfo($"Grimoire spawned (alert spawn at position: {alertPosition})");
        }

        private IEnumerator WaitForDeathSubscription(GameObject enemyObj, BaseEnemy baseEnemy, bool isGrimoire)
        {
            // Wait for enemy to be fully initialized
            yield return new WaitForSeconds(0.5f);

            // Check if enemy still exists
            if (enemyObj == null)
                yield break;

            // Monitor for death
            while (enemyObj != null && !baseEnemy.IsDead)
            {
                yield return new WaitForSeconds(0.5f);
            }

            // Enemy died
            if (enemyObj != null)
            {
                OnEnemyDeath(enemyObj, isGrimoire);
            }
        }

        private IEnumerator DelayedSpawn(EnemySpawner spawner, float delay, bool isGrimoire)
        {
            yield return new WaitForSeconds(delay);

            if (spawner != null)
            {
                SpawnEnemyAtSpawner(spawner, isGrimoire);
            }
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Get next unique ID for an enemy type in a specific room
        /// </summary>
        private int GetNextUniqueIdForEnemyInRoom(string roomId, bool isGrimoire)
        {
            int highestId = 0;

            if (isGrimoire)
            {
                // Grimoires: just check if one exists (max 1 per room)
                if (roomActiveGrimoires.ContainsKey(roomId) && roomActiveGrimoires[roomId] != null)
                {
                    highestId = 1;
                }
            }
            else
            {
                // Hanaduras: check existing ones in the room
                if (roomActiveHanaduras.ContainsKey(roomId))
                {
                    foreach (GameObject hanadura in roomActiveHanaduras[roomId])
                    {
                        if (hanadura != null)
                        {
                            // Parse the ID from the name (format: "Hanadura_RoomName_01")
                            string[] parts = hanadura.name.Split('_');
                            if (parts.Length >= 3)
                            {
                                if (int.TryParse(parts[parts.Length - 1], out int existingId))
                                {
                                    if (existingId > highestId)
                                    {
                                        highestId = existingId;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return highestId + 1;
        }

        /// <summary>
        /// Debug method to show enemy distribution across rooms
        /// </summary>
        private void DebugRoomDistribution()
        {
            this.LogInfo("=== Enemy Distribution by Room ===");

            // Get all unique rooms
            HashSet<string> allRooms = new HashSet<string>();
            foreach (var spawner in allSpawners)
            {
                allRooms.Add(spawner.RoomIdentifier);
            }

            foreach (string roomId in allRooms)
            {
                int hanaduraCount = GetHanaduraCountInRoom(roomId);
                bool hasGrimoire = roomActiveGrimoires.ContainsKey(roomId) && roomActiveGrimoires[roomId] != null;

                this.LogInfo($"Room '{roomId}': {hanaduraCount} Hanaduras, {(hasGrimoire ? "1" : "0")} Grimoire");
            }

            this.LogInfo("================================");
        }

        /// <summary>
        /// Check if a Hanadura can spawn in this room (respects max per room limit)
        /// </summary>
        private bool CanSpawnHanaduraInRoom(string roomId)
        {
            if (!roomActiveHanaduras.ContainsKey(roomId))
                return true;

            // Clean up null references
            roomActiveHanaduras[roomId].RemoveAll(h => h == null);

            int activeCount = roomActiveHanaduras[roomId].Count;

            if (activeCount >= maxHanaduraPerRoom)
            {
                this.LogInfo($"Room '{roomId}' already has {activeCount}/{maxHanaduraPerRoom} Hanaduras");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get number of active Hanaduras in a specific room
        /// </summary>
        private int GetHanaduraCountInRoom(string roomId)
        {
            if (!roomActiveHanaduras.ContainsKey(roomId))
                return 0;

            // Clean up null references
            roomActiveHanaduras[roomId].RemoveAll(h => h == null);
            return roomActiveHanaduras[roomId].Count;
        }

        /// <summary>
        /// Check if a Grimoire can spawn in this room (max 1 per room)
        /// </summary>
        private bool CanSpawnGrimoireInRoom(string roomId)
        {
            if (roomActiveGrimoires.ContainsKey(roomId))
            {
                GameObject existingGrimoire = roomActiveGrimoires[roomId];
                // Check if it's still alive (null check for destroyed objects)
                if (existingGrimoire != null)
                {
                    this.LogInfo($"Room '{roomId}' already has an active Grimoire");
                    return false;
                }
                else
                {
                    // Clean up null reference
                    roomActiveGrimoires.Remove(roomId);
                }
            }
            return true;
        }

        /// <summary>
        /// Assign a room-based patrol route to a Hanadura
        /// </summary>
        private void AssignRoomPatrolRoute(HanaduraAI hanadura, EnemySpawner spawner)
        {
            string roomId = spawner.RoomIdentifier;

            if (string.IsNullOrEmpty(roomId) || roomId == "Unknown")
            {
                this.LogWarning($"Spawner has no room identifier, using closest zone");
                roomId = FindClosestRoomToPosition(spawner.SpawnPoint.position);
            }

            // Get patrol zone for this room
            RoomPatrolZone zone = GameManager.Instance.GetPatrolZone(roomId);

            if (zone == null)
            {
                this.LogWarning($"No patrol zone found for room '{roomId}'");
                return;
            }

            // Generate unique route for this enemy
            PatrolRoute route = zone.GenerateUniqueRoute(hanadura.gameObject);

            if (route != null && route.Count > 0)
            {
                hanadura.patrolRoute = route;
                hanadura.startWaypointIndex = 0; // Always start at beginning of custom route
                hanadura.ReinitializePatrolState();

                this.LogInfo($"Assigned {route.Count}-waypoint patrol route to {hanadura.name} in room '{roomId}'");
            }
            else
            {
                this.LogWarning($"Failed to generate route for room '{roomId}'");
            }
        }

        /// <summary>
        /// Assign a room-based patrol route to a Grimoire
        /// </summary>
        private void AssignRoomPatrolRoute(GrimoireAI grimoire, EnemySpawner spawner)
        {
            string roomId = spawner.RoomIdentifier;

            if (string.IsNullOrEmpty(roomId) || roomId == "Unknown")
            {
                this.LogWarning($"Spawner has no room identifier, using closest zone");
                roomId = FindClosestRoomToPosition(spawner.SpawnPoint.position);
            }

            // Get patrol zone for this room
            RoomPatrolZone zone = GameManager.Instance.GetPatrolZone(roomId);

            if (zone == null)
            {
                this.LogWarning($"No patrol zone found for room '{roomId}'");
                return;
            }

            // Generate unique route for this enemy
            PatrolRoute route = zone.GenerateUniqueRoute(grimoire.gameObject);

            if (route != null && route.Count > 0)
            {
                grimoire.patrolRoute = route;
                grimoire.startWaypointIndex = 0;
                // Grimoire doesn't have ReinitializePatrolState, it initializes in OnStartServer

                this.LogInfo($"Assigned {route.Count}-waypoint patrol route to Grimoire {grimoire.name} in room '{roomId}'");
            }
            else
            {
                this.LogWarning($"Failed to generate route for Grimoire in room '{roomId}'");
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
                this.LogInfo("No valid spawners outside player's room, using any spawner");
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
                this.LogWarning("No rooms found with tag: " + roomTag);
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
                        this.LogInfo($"Position is inside room: {room.name}");
                        return room.name;
                    }

                    // Use bounds center for distance calculation
                    roomCenter = roomBounds.center;
                }
                else
                {
                    // Fallback to pivot point if no bounds found
                    roomCenter = room.transform.position;
                    this.LogWarning($"Room {room.name} has no bounds, using pivot point");
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
                this.LogInfo($"Closest room to position is: {closestRoom.name} (distance: {closestDistance:F1}m)");
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
                    SpawnEnemyAtSpawner(pending.preferredSpawner, pending.isGrimoire, pending.targetAlertPosition, true);
                }
                else
                {
                    SpawnEnemyAtSpawner(pending.preferredSpawner, pending.isGrimoire);
                }

                this.LogInfo($"Processed queued {(pending.isGrimoire ? "Grimoire" : "Hanadura")} respawn");
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
