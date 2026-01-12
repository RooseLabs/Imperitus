using System.Collections.Generic;
using System.Linq;
using RooseLabs.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    public class KiwiAI : BaseEnemy
    {
        #region Serialized Fields

        [Header("References")]
        public SoundEmitter soundEmitter;

        [Header("Movement Settings")]
        [SerializeField] private float patrolSpeed = 3f;
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float fleeSpeed = 6.5f;
        [SerializeField] private float desiredDistanceFromPlayer = 2f;
        [SerializeField] private float stoppingDistance = 0.5f;

        [Header("Detection Settings")]
        [SerializeField] private EnemyDetection enemyDetection;

        [Header("Scream State Settings")]
        [SerializeField] private float screamDuration = 5f;
        [SerializeField] private float alertFrequency = 2f;
        [SerializeField] private float alertRadius = 20f;
        [SerializeField] private float alertCooldown = 10f;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        // State machine
        private Animator animator;
        private NavMeshAgent navAgent;

        // Patrol state
        private Vector3 currentTargetWaypoint;
        private string currentRoomId;

        // Chase state
        private Transform targetPlayer;

        // Scream state
        private Vector3 lastDetectedPlayerPosition;

        // Alert cooldown
        private float alertCooldownEndTime;
        private bool onAlertCooldown;

        // Scream lock
        private bool isScreaming = false;

        // Patrol zones reference
        private Dictionary<string, RoomPatrolZone> patrolZones;

        // State instances
        private KiwiPatrolState patrolState;
        private KiwiChaseState chaseState;
        private KiwiScreamState screamState;
        private KiwiFleeState fleeState;

        #endregion

        #region Properties

        public Vector3 CurrentTargetWaypoint => currentTargetWaypoint;
        public string CurrentRoomId => currentRoomId;
        public bool IsOnAlertCooldown => onAlertCooldown;
        public NavMeshAgent NavAgent => navAgent;
        public EnemyDetection EnemyDetection => enemyDetection;
        public Animator Animator => animator;
        public float PatrolSpeed => patrolSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float FleeSpeed => fleeSpeed;
        public float DesiredDistanceFromPlayer => desiredDistanceFromPlayer;
        public float StoppingDistance => stoppingDistance;
        public float AlertRadius => alertRadius;
        public float AlertFrequency => alertFrequency;
        public Dictionary<string, RoomPatrolZone> PatrolZones => patrolZones;
        public KiwiPatrolState PatrolState => patrolState;
        public KiwiChaseState ChaseState => chaseState;
        public KiwiScreamState ScreamState => screamState;
        public KiwiFleeState FleeState => fleeState;
        public bool IsScreaming => isScreaming;

        #endregion

        #region Unity Lifecycle

        protected override void Initialize()
        {
            animator = GetComponentInChildren<Animator>();
            navAgent = GetComponent<NavMeshAgent>();

            if (enemyDetection == null)
            {
                enemyDetection = GetComponent<EnemyDetection>();
            }

            if (animator == null)
            {
                Debug.LogError($"[KiwiAI] {gameObject.name} requires an Animator component!");
            }

            if (navAgent == null)
            {
                Debug.LogError($"[KiwiAI] {gameObject.name} requires a NavMeshAgent component!");
            }

            onAlertCooldown = false;
            alertCooldownEndTime = 0f;
        }

        public override void OnStartServer()
        {
            PatrolPointGenerator generator = FindFirstObjectByType<PatrolPointGenerator>();
            if (generator != null)
            {
                patrolZones = generator.GetAllPatrolZones();
            }
            else
            {
                Debug.LogWarning("[KiwiAI] Could not find PatrolPointGenerator in scene!");
            }

            patrolState = new KiwiPatrolState(this);
            chaseState = new KiwiChaseState(this);
            screamState = new KiwiScreamState(this);
            fleeState = new KiwiFleeState(this);

            ChangeState(patrolState);
            SelectInitialWaypoint();
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;

            if (enemyDetection != null && !onAlertCooldown)
            {
                CheckForPlayerDetection();
            }

            if (onAlertCooldown && Time.time >= alertCooldownEndTime)
            {
                onAlertCooldown = false;
            }

            currentState?.Update();
        }

        #endregion

        #region Waypoint Selection

        private void SelectInitialWaypoint()
        {
            if (patrolZones == null || patrolZones.Count == 0)
            {
                Debug.LogWarning("[KiwiAI] No patrol zones available, cannot select initial waypoint");
                return;
            }

            string kiwiSpawnRoom = currentRoomId;

            List<string> availableRooms = patrolZones.Keys
                    .Where(roomId => roomId != kiwiSpawnRoom)
             .ToList();

            if (availableRooms.Count == 0)
            {
                Debug.LogWarning("[KiwiAI] No other rooms available besides spawn room, using any room");
                availableRooms = patrolZones.Keys.ToList();
            }

            string selectedRoom = availableRooms[Random.Range(0, availableRooms.Count)];
            RoomPatrolZone selectedZone = patrolZones[selectedRoom];

            if (selectedZone.waypoints.Count == 0)
            {
                Debug.LogWarning($"[KiwiAI] Room '{selectedRoom}' has no waypoints");
                return;
            }

            currentTargetWaypoint = selectedZone.waypoints[Random.Range(0, selectedZone.waypoints.Count)];
            currentRoomId = selectedRoom;
        }

        public void SelectNextWaypoint()
        {
            if (patrolZones == null || patrolZones.Count == 0)
            {
                Debug.LogWarning("[KiwiAI] No patrol zones available for waypoint selection");
                return;
            }

            List<string> availableRooms = patrolZones.Keys
                    .Where(roomId => roomId != currentRoomId)
             .ToList();

            if (availableRooms.Count == 0)
            {
                Debug.LogWarning($"[KiwiAI] No other rooms available besides current room '{currentRoomId}'");
                return;
            }

            string selectedRoom = availableRooms[Random.Range(0, availableRooms.Count)];
            RoomPatrolZone selectedZone = patrolZones[selectedRoom];

            if (selectedZone.waypoints.Count == 0)
            {
                Debug.LogWarning($"[KiwiAI] Room '{selectedRoom}' has no waypoints");
                return;
            }

            currentTargetWaypoint = selectedZone.waypoints[Random.Range(0, selectedZone.waypoints.Count)];
            currentRoomId = selectedRoom;
        }

        #endregion

        #region Detection

        private void CheckForPlayerDetection()
        {
            Transform detectedTarget = enemyDetection.DetectedTarget;

            if (detectedTarget != null)
            {
                targetPlayer = detectedTarget;
                lastDetectedPlayerPosition = targetPlayer.position;

                if (!isScreaming)
                {
                    ChangeState(chaseState);
                }
            }
        }

        #endregion

        #region Alert System

        public void AlertNearbyHanaduras(Vector3 playerPosition)
        {
            Collider[] collidersInRadius = Physics.OverlapSphere(transform.position, alertRadius);

            foreach (Collider collider in collidersInRadius)
            {
                if (collider.TryGetComponent(out HanaduraAI hanadura))
                {
                    hanadura.AlertByKiwi(playerPosition);
                }
            }
        }

        public void StartAlertCooldown()
        {
            onAlertCooldown = true;
            alertCooldownEndTime = Time.time + alertCooldown;
        }

        #endregion

        #region Utility Methods

        public Transform GetTargetPlayer()
        {
            return targetPlayer;
        }

        public Vector3 GetLastDetectedPlayerPosition()
        {
            return lastDetectedPlayerPosition;
        }

        public void SetLastDetectedPlayerPosition(Vector3 position)
        {
            lastDetectedPlayerPosition = position;
        }

        public float GetScreamDuration()
        {
            return screamDuration;
        }

        public void SetSpawnRoom(string roomId)
        {
            currentRoomId = roomId;
        }

        public void SetTargetWaypoint(Vector3 waypoint)
        {
            currentTargetWaypoint = waypoint;
        }

        public void SetScreamingState(bool screaming)
        {
            isScreaming = screaming;
        }

        public void StopScreamSound()
        {
            SoundManager.Instance.StopSoundByKey("Kiwi_Scream");
        }

        #endregion

        protected override void OnDeath()
        {
            return;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, alertRadius);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, alertRadius);
        }
    }
}
