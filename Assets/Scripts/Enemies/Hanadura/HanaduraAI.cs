using FishNet.Object;
using RooseLabs.Gameplay;
using RooseLabs.ScriptableObjects;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NetworkObject))]
    public class HanaduraAI : NetworkBehaviour, ISoundListener, IEnemyAI
    {
        private static Logger Logger => Logger.GetLogger("Hanadura");

        [Header("References")]
        public NavMeshAgent navAgent;
        public EnemyDetection detection;
        public PatrolRoute patrolRoute;
        public Animator animator;
        public WeaponCollider weaponCollider;
        public Transform RaycastOrigin;
        public Transform modelTransform;
        public EnemyData enemyData;
        public Rigidbody rb;

        [Header("Combat")]
        public float attackRange = 4f;
        public float attackCooldown = 1.2f;
        public int attackDamage = 0;
        // This is just a reference to the duration of the hanadura attack so
        // I can lock state changes during the attack animation...
        public float attackAnimationDuration = 2.3f;

        [Header("Patrol")]
        public int startWaypointIndex = 0;
        public bool loopPatrol = true;

        [Header("Detection")]
        public float detectionExpiryTime = 10f;
        public float minSoundIntensity = 0.1f;
        public float visualLostSightGracePeriod = 6f;

        private float visualLostSightTimer = 0f;

        [Header("Random Room Patrol")]
        [Tooltip("Chance (0-100%) to patrol a random room outside assigned room")]
        [Range(0f, 100f)]
        public float randomRoomPatrolChance = 15f;

        [Tooltip("How often to check if Hanadura should patrol random room (seconds)")]
        public float randomRoomCheckInterval = 30f;

        [Tooltip("How long to patrol the random room before returning (seconds)")]
        public float randomRoomPatrolDuration = 45f;

        // Random room patrol tracking
        private float randomRoomCheckTimer = 0f;
        private bool isPatrollingRandomRoom = false;
        private float randomRoomPatrolTimer = 0f;
        private PatrolRoute originalPatrolRoute;
        private string originalRoomId;
        private string currentRandomRoomId;

        // FSM states
        private IEnemyState currentState;
        private PatrolState patrolState;
        private ChaseState chaseState;
        private AttackState attackState;
        private InvestigateState investigateState;

        public IEnemyState CurrentState => currentState;
        public PatrolState PatrolState => patrolState;
        public ChaseState ChaseState => chaseState;
        public AttackState AttackState => attackState;
        public InvestigateState InvestigateState => investigateState;

        // Server-controlled target reference (only used server-side)
        public Transform CurrentTarget { get; private set; }
        public Vector3? LastKnownTargetPosition { get; set; }

        // Priority-based detection system
        private DetectionInfo currentDetection = null;

        // for attack cooldown
        private float attackTimer = 0f;

        private bool hasTriggeredDetectedAnimation = false;
        private bool isPlayingDetectedAnimation = false;

        private bool isAttackLocked = false;
        private float attackLockDuration = 0f;

        private bool hasPlayedDeathAnimation = false;

        #region Detection Priority System

        public enum DetectionPriority
        {
            None = 0,
            Patrol = 1,
            SoundOther = 2,      // Footsteps, items, etc.
            AlertFromAI = 3,     // Another AI reported something
            SoundVoice = 4,      // Player voice chat
            Visual = 5           // Direct line of sight
        }

        public class DetectionInfo
        {
            public DetectionPriority priority;
            public Vector3 position;
            public Transform target;        // null for sounds/alerts without direct target
            public float metric;            // intensity for sounds, distance for visual/alerts
            public float timestamp;

            public DetectionInfo(DetectionPriority priority, Vector3 position, Transform target = null, float metric = 0f)
            {
                this.priority = priority;
                this.position = position;
                this.target = target;
                this.metric = metric;
                this.timestamp = Time.time;
            }
        }

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            weaponCollider = GetComponentInChildren<WeaponCollider>();
            enemyData = GetComponent<EnemyData>();
            rb = GetComponent<Rigidbody>();

            if (enemyData != null)
            {
                attackDamage = enemyData.AttackDamage;
            }
        }

        private bool ShouldSwitchToNewDetection(DetectionInfo newDetection)
        {
            // No current detection, always switch
            if (currentDetection == null)
                return true;

            // Check if current detection has expired
            if (IsDetectionStale(currentDetection))
            {
                //DebugManager.Log("[HanaduraAI] Current detection expired, switching to new detection.");
                return true;
            }

            // Higher priority always wins
            if (newDetection.priority > currentDetection.priority)
            {
                //DebugManager.Log($"[HanaduraAI] New detection priority ({newDetection.priority}) > current ({currentDetection.priority}), switching.");
                return true;
            }

            if (newDetection.priority < currentDetection.priority)
                return false;

            // Same priority - use tie-breaker
            if (newDetection.priority == DetectionPriority.Visual ||
                newDetection.priority == DetectionPriority.AlertFromAI)
            {
                // Use distance for visual/alerts (lower distance wins)
                float currentDist = Vector3.Distance(transform.position, currentDetection.position);
                float newDist = Vector3.Distance(transform.position, newDetection.position);

                if (newDist < currentDist)
                {
                    //DebugManager.Log($"[HanaduraAI] Same priority, new detection closer ({newDist:F2}m vs {currentDist:F2}m), switching.");
                    return true;
                }
                return false;
            }
            else if (newDetection.priority == DetectionPriority.SoundVoice ||
                     newDetection.priority == DetectionPriority.SoundOther)
            {
                // Use intensity for sounds (higher intensity wins)
                if (newDetection.metric > currentDetection.metric)
                {
                    //DebugManager.Log($"[HanaduraAI] Same priority, new sound louder ({newDetection.metric:F2} vs {currentDetection.metric:F2}), switching.");
                    return true;
                }
                return false;
            }

            return false; // Default: don't switch
        }

        private bool IsDetectionStale(DetectionInfo detection)
        {
            if (detection == null) return true;

            // Visual detections don't go stale if we still see the target
            if (detection.priority == DetectionPriority.Visual && detection.target != null)
            {
                // Don't check staleness here, let ProcessVisualDetection() handle it
                return false;
            }

            return Time.time - detection.timestamp > detectionExpiryTime;
        }

        private void ClearCurrentDetection()
        {
            currentDetection = null;
            CurrentTarget = null;
            LastKnownTargetPosition = null;
            hasTriggeredDetectedAnimation = false;
            isPlayingDetectedAnimation = false;
            //DebugManager.Log("[HanaduraAI] Cleared current detection.");
        }

        #endregion

        private void Reset()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Ensure components exist
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
            if (detection == null) detection = GetComponent<EnemyDetection>();

            // Create states
            patrolState = new PatrolState(this, patrolRoute, loopPatrol, startWaypointIndex);
            chaseState = new ChaseState(this);
            attackState = new AttackState(this);
            investigateState = new InvestigateState(this);

            EnterState(patrolState);

            // Initialize random room patrol timer with random offset to stagger checks
            randomRoomCheckTimer = Random.Range(0f, randomRoomCheckInterval);
        }

        /// <summary>
        /// Call this after assigning a patrol route to reinitialize the patrol state
        /// </summary>
        public void ReinitializePatrolState()
        {
            if (!IsServerInitialized) return;

            // Recreate patrol state with new route
            patrolState = new PatrolState(this, patrolRoute, loopPatrol, startWaypointIndex);

            // If currently patrolling, re-enter the state to apply changes
            if (currentState is PatrolState)
            {
                EnterState(patrolState);
            }

            Logger.Info($"[HanaduraAI] Patrol state reinitialized with {patrolRoute?.Count ?? 0} waypoints");
        }

        private void Update()
        {
            if (!base.IsServerInitialized)
                return;

            if (!hasPlayedDeathAnimation && enemyData.IsDead)
            {
                HandleDeath_ServerRPC();
                return;
            }
            else if (enemyData.IsDead)
                return;

            // Check if Detected animation finished
            if (isPlayingDetectedAnimation)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                if (!stateInfo.IsName("Detected"))
                {
                    isPlayingDetectedAnimation = false;
                }
            }

            // Only tick state and update if not playing detected animation
            if (!isPlayingDetectedAnimation)
            {
                currentState?.Tick();
            }

            attackTimer -= Time.deltaTime;

            if (isAttackLocked)
            {
                attackLockDuration -= Time.deltaTime;
                if (attackLockDuration <= 0f)
                {
                    isAttackLocked = false;
                }
            }

            ProcessVisualDetection();

            // Only check for stale detections if NOT currently investigating
            if (!(currentState is InvestigateState))
            {
                if (currentDetection != null && IsDetectionStale(currentDetection))
                {
                    ClearCurrentDetection();
                }
            }

            // Only update state if not playing detected animation
            if (!isPlayingDetectedAnimation)
            {
                UpdateStateFromDetection();
            }

            // Handle random room patrol system (only when in patrol state and no active detection)
            if (currentState is PatrolState && currentDetection == null)
            {
                UpdateRandomRoomPatrol();
            }
        }

        private void ProcessVisualDetection()
        {
            Transform detected = detection.DetectedTarget;

            if (detected != null)
            {
                if (!hasTriggeredDetectedAnimation && currentState is PatrolState)
                {
                    SetAnimatorTrigger("Detected");
                    hasTriggeredDetectedAnimation = true;
                    isPlayingDetectedAnimation = true;
                    StopMovement(); // Stop the enemy
                    Logger.Info("[HanaduraAI] First visual detection - playing Detected animation");
                }

                // Reset lost sight timer
                visualLostSightTimer = visualLostSightGracePeriod;

                float dist = Vector3.Distance(transform.position, detected.position);
                DetectionInfo visualDetection = new DetectionInfo(
                    DetectionPriority.Visual,
                    detected.position,
                    detected,
                    dist
                );

                if (ShouldSwitchToNewDetection(visualDetection))
                {
                    currentDetection = visualDetection;
                    CurrentTarget = detected;
                    LastKnownTargetPosition = detected.position;
                }
                else if (currentDetection?.priority == DetectionPriority.Visual && currentDetection.target == detected)
                {
                    // Refresh existing visual detection
                    currentDetection.timestamp = Time.time;
                    currentDetection.position = detected.position;
                    currentDetection.metric = dist;
                    CurrentTarget = detected;
                    LastKnownTargetPosition = detected.position;
                }
            }
            else if (currentDetection?.priority == DetectionPriority.Visual)
            {
                // Lost sight but still have a grace period
                visualLostSightTimer -= Time.deltaTime;

                if (visualLostSightTimer <= 0f)
                {
                    // Grace period expired, mark as stale
                    ClearCurrentDetection();
                }
            }
        }

        private void UpdateStateFromDetection()
        {
            if (currentDetection == null)
            {
                // No detection, return to patrol
                if (!(currentState is PatrolState))
                {
                    EnterState(patrolState);
                }
                return;
            }

            // Handle based on detection priority
            switch (currentDetection.priority)
            {
                case DetectionPriority.Visual:
                    HandleVisualDetection();
                    break;

                case DetectionPriority.SoundVoice:
                case DetectionPriority.SoundOther:
                case DetectionPriority.AlertFromAI:
                    HandleInvestigationDetection();
                    break;
            }
        }

        private void HandleVisualDetection()
        {
            if (CurrentTarget == null) return;
            if (isAttackLocked) return;

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);

            // Check if we can actually attack (distance AND line of sight)
            bool canAttack = dist <= attackRange &&
                             detection.HasLineOfSightOfHitbox(CurrentTarget, RaycastOrigin);

            if (canAttack)
            {
                if (!(currentState is AttackState))
                    EnterState(attackState);
            }
            else
            {
                // Either too far OR no line of sight -> chase
                if (!(currentState is ChaseState))
                    EnterState(chaseState);
            }
        }

        private void HandleInvestigationDetection()
        {
            // If we have a direct target from alert, chase it
            if (currentDetection.target != null)
            {
                CurrentTarget = currentDetection.target;
                LastKnownTargetPosition = currentDetection.target.position;

                float dist = Vector3.Distance(transform.position, CurrentTarget.position);
                if (dist <= attackRange)
                {
                    if (!(currentState is AttackState))
                        EnterState(attackState);
                }
                else
                {
                    if (!(currentState is ChaseState))
                        EnterState(chaseState);
                }
            }
            else
            {
                // No direct target, investigate the position
                CurrentTarget = null;
                LastKnownTargetPosition = currentDetection.position;

                // Only transition to investigate if not already investigating, 
                // or if already investigating and it's complete
                if (!(currentState is InvestigateState))
                {
                    EnterState(investigateState);
                }
                else if (investigateState.IsInvestigationComplete)
                {
                    // Investigation complete, clear detection to return to patrol
                    ClearCurrentDetection();
                }
            }
        }

        public void EnterState(IEnemyState newState)
        {
            if (currentState != null)
                currentState.Exit();

            currentState = newState;

            if (currentState != null)
            {
                Logger.Info($"[HanaduraAI] Entered state: {currentState.GetType().Name}");
                currentState.Enter();
            }
        }

        /// <summary>
        /// Called by Sentient Grimoire to alert this Hanadura
        /// </summary>
        public void AlertToPosition(Vector3 position, Transform target = null)
        {
            if (!base.IsServerInitialized) return;

            float dist = Vector3.Distance(transform.position, position);
            DetectionInfo alertDetection = new DetectionInfo(
                DetectionPriority.AlertFromAI,
                position,
                target,
                dist  // Use distance as metric for tie-breaking
            );

            if (ShouldSwitchToNewDetection(alertDetection))
            {
                currentDetection = alertDetection;

                if (target != null)
                {
                    //DebugManager.Log($"[HanaduraAI] AI Alert with target: {target.name} at {position}");
                }
                else
                {
                    //DebugManager.Log($"[HanaduraAI] AI Alert to investigate: {position}");
                }
            }
            else
            {
                //DebugManager.Log($"[HanaduraAI] AI Alert ignored (current priority: {currentDetection?.priority})");
            }
        }

        public void SetCurrentTarget(Transform target)
        {
            CurrentTarget = target;
            if (target != null)
            {
                LastKnownTargetPosition = target.position;
            }
        }

        /// <summary>
        /// Called server-side by the sound system when a sound is detected nearby.
        /// </summary>
        public void OnSoundHeard(Vector3 position, SoundType type, float intensity)
        {
            if (!base.IsServerInitialized) return;

            // Only react to sufficiently strong sounds
            if (intensity < minSoundIntensity)
                return;

            // Determine priority based on sound type
            DetectionPriority priority = type.key == "Voice"
                ? DetectionPriority.SoundVoice
                : DetectionPriority.SoundOther;

            DetectionInfo soundDetection = new DetectionInfo(
                priority,
                position,
                null,  // Sounds don't have direct targets
                intensity  // Use intensity as metric for tie-breaking
            );

            if (ShouldSwitchToNewDetection(soundDetection))
            {
                currentDetection = soundDetection;
                //DebugManager.Log($"[HanaduraAI] Sound detected: '{type.key}' ({priority}) with intensity {intensity:F2} at {position}");
            }
            else
            {
                //DebugManager.Log($"[HanaduraAI] Sound '{type.key}' ignored (current priority: {currentDetection?.priority}, intensity: {intensity:F2})");
            }
        }

        #region Movement & Attack APIs (Server-side)

        public void MoveTo(Vector3 position)
        {
            if (!base.IsServerInitialized) return;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
        }

        public void StopMovement()
        {
            if (!base.IsServerInitialized) return;
            navAgent.isStopped = true;
        }
        public bool TryPerformAttack()
        {
            if (!base.IsServerInitialized) return false;
            if (attackTimer > 0f) return false;
            if (CurrentTarget == null) return false;

            float dist = Vector3.Distance(transform.position, CurrentTarget.position);
            if (dist > attackRange) return false;

            if (!detection.HasLineOfSightOfHitbox(CurrentTarget, RaycastOrigin))
            {
                //Debug.Log("[HanaduraAI] Cannot attack - no current line of sight to target hitbox");
                return false;
            }

            // Start attack cooldown
            attackTimer = attackCooldown;

            // Enable weapon collider for this attack
            if (weaponCollider != null)
            {
                weaponCollider.EnableWeapon();
            }
            else
            {
                Debug.LogWarning("[HanaduraAI] WeaponCollider reference is missing!");
            }

            return true;
        }
        #endregion

        public void SetAnimatorBool(string paramName, bool value)
        {
            if (animator != null)
            {
                animator.SetBool(paramName, value);
                //DebugManager.Log($"[HanaduraAI] Set animator bool '{paramName}' to {value}");
            }

            // Sync to all clients
            if (IsServerInitialized)
            {
                Rpc_SetAnimatorBool(paramName, value);
            }
        }

        public void SetAnimatorTrigger(string paramName)
        {
            if (animator != null)
            {
                animator.SetTrigger(paramName);
                //DebugManager.Log($"[HanaduraAI] Triggered animator '{paramName}'");
            }

            // Sync to all clients
            if (IsServerInitialized)
            {
                Rpc_SetAnimatorTrigger(paramName);
            }
        }

        [ObserversRpc]
        private void Rpc_SetAnimatorBool(string paramName, bool value)
        {
            if (animator != null && !base.IsServerInitialized) // Only clients execute this
            {
                animator.SetBool(paramName, value);
            }
        }

        [ObserversRpc]
        private void Rpc_SetAnimatorTrigger(string paramName)
        {
            if (animator != null && !base.IsServerInitialized) // Only clients execute this
            {
                animator.SetTrigger(paramName);
            }
        }


        public void SyncModelRotation(Quaternion localRotation)
        {
            if (!base.IsServerInitialized) return;
            if (modelTransform != null)
            {
                modelTransform.localRotation = localRotation;

                Rpc_SyncModelRotation(localRotation);
            }
        }

        [ObserversRpc]
        private void Rpc_SyncModelRotation(Quaternion localRotation)
        {
            if (!base.IsServerInitialized && modelTransform != null)
            {
                modelTransform.localRotation = localRotation;
            }
        }

        public void LockIntoAttack()
        {
            isAttackLocked = true;
            attackLockDuration = attackAnimationDuration;
        }

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeath_ServerRPC()
        {
            if (!IsServerInitialized) 
                return;

            if (animator != null)
            {
                HandleDeath_ObserversRPC();
            }
            else
            {
                Debug.LogWarning($"No Animator found on {gameObject.name}, cannot play death animation.");
                Despawn(gameObject);
            }
        }

        [ObserversRpc]
        private void HandleDeath_ObserversRPC()
        {
            if (animator != null && !hasPlayedDeathAnimation)
            {
                StopMovement();
                currentState = null;
                ClearCurrentDetection();

                // Clean up random room patrol if active
                if (isPatrollingRandomRoom && !string.IsNullOrEmpty(currentRandomRoomId))
                {
                    EnemySpawnManager.Instance.UnregisterRandomPatroller(gameObject, currentRandomRoomId);

                    RoomPatrolZone randomZone = GameManager.Instance.GetPatrolZone(currentRandomRoomId);
                    randomZone?.ReleaseRoute(gameObject);
                    isPatrollingRandomRoom = false;
                }

                rb.isKinematic = true;
                weaponCollider.DisableWeapon();

                Collider col = gameObject.GetComponent<Collider>();
                if (col != null)
                    col.enabled = false;

                animator.Play("Death");
                hasPlayedDeathAnimation = true;

                if (navAgent != null)
                    navAgent.enabled = false;

                Debug.Log($"{gameObject.name} death sequence executed on observer");
            }
        }

        public void OnEnemyDeath()
        {
            if (IsServerInitialized)
            {
                HandleDeath_ServerRPC();
            }
        }

        private IEnumerator DespawnAfterDeath()
        {
            // Wait for death animation to finish
            yield return new WaitForSeconds(10f); 

            if (IsServerInitialized)
            {
                Despawn(gameObject);
            }
        }

        #region Random Room Patrol System

        private void UpdateRandomRoomPatrol()
        {
            // If currently patrolling random room
            if (isPatrollingRandomRoom)
            {
                randomRoomPatrolTimer -= Time.deltaTime;

                if (randomRoomPatrolTimer <= 0f)
                {
                    // Time to return to original room
                    ReturnToOriginalRoom();
                }
            }
            else
            {
                // Check if should start random room patrol
                randomRoomCheckTimer -= Time.deltaTime;

                if (randomRoomCheckTimer <= 0f)
                {
                    randomRoomCheckTimer = randomRoomCheckInterval;

                    // Roll chance to patrol random room
                    if (Random.Range(0f, 100f) <= randomRoomPatrolChance)
                    {
                        StartRandomRoomPatrol();
                    }
                }
            }
        }

        private void StartRandomRoomPatrol()
        {
            // Get current room from spawner
            if (!EnemySpawnManager.Instance.activeEnemies.TryGetValue(gameObject, out EnemySpawner spawner))
            {
                Debug.Log("[HanaduraAI] Cannot start random patrol - enemy not tracked by spawn manager");
                return;
            }

            originalRoomId = spawner.RoomIdentifier;

            // Get all available patrol zones
            var allZones = GameManager.Instance.GetAllPatrolZones();
            if (allZones == null || allZones.Count <= 1)
            {
                Debug.Log("[HanaduraAI] Not enough rooms for random patrol");
                return;
            }

            // Filter valid rooms (not current room, has waypoints, not being randomly patrolled)
            List<string> validRoomIds = new List<string>();

            foreach (var kvp in allZones)
            {
                // Skip current assigned room
                if (kvp.Key == originalRoomId)
                    continue;

                RoomPatrolZone zone = kvp.Value;

                // Must have waypoints
                if (zone.waypoints == null || zone.waypoints.Count == 0)
                    continue;

                // Skip if room already has a random patroller
                if (EnemySpawnManager.Instance.IsRoomBeingRandomlyPatrolled(kvp.Key))
                {
                    Debug.Log($"[HanaduraAI] Skipping room '{kvp.Key}' - already has a random patroller");
                    continue;
                }

                validRoomIds.Add(kvp.Key);
            }

            if (validRoomIds.Count == 0)
            {
                Debug.Log("[HanaduraAI] No valid rooms found for random patrol (all occupied or unavailable)");
                return;
            }

            // Pick random room
            currentRandomRoomId = validRoomIds[Random.Range(0, validRoomIds.Count)];
            RoomPatrolZone randomZone = allZones[currentRandomRoomId];

            // Store original patrol route
            originalPatrolRoute = patrolRoute;

            // Generate new route for random room
            PatrolRoute newRoute = randomZone.GenerateUniqueRoute(gameObject);

            if (newRoute != null && newRoute.Count > 0)
            {
                patrolRoute = newRoute;
                startWaypointIndex = 0;
                ReinitializePatrolState();

                isPatrollingRandomRoom = true;
                randomRoomPatrolTimer = randomRoomPatrolDuration;

                // Register this Hanadura as randomly patrolling this room
                EnemySpawnManager.Instance.RegisterRandomPatroller(gameObject, currentRandomRoomId);

                Debug.Log($"[HanaduraAI] '{gameObject.name}' started random room patrol: '{originalRoomId}' -> '{currentRandomRoomId}' for {randomRoomPatrolDuration}s");
            }
            else
            {
                Debug.Log($"[HanaduraAI] '{gameObject.name}' failed to generate route for random room '{currentRandomRoomId}'");
            }
        }

        private void ReturnToOriginalRoom()
        {
            if (originalPatrolRoute == null)
            {
                Debug.Log("[HanaduraAI] Cannot return to original room - no original route stored");
                isPatrollingRandomRoom = false;
                return;
            }

            // Unregister from random patrol tracking
            if (!string.IsNullOrEmpty(currentRandomRoomId))
            {
                EnemySpawnManager.Instance.UnregisterRandomPatroller(gameObject, currentRandomRoomId);

                // Release the random room route
                RoomPatrolZone randomZone = GameManager.Instance.GetPatrolZone(currentRandomRoomId);
                randomZone?.ReleaseRoute(gameObject);
            }

            // Restore original patrol route
            patrolRoute = originalPatrolRoute;
            startWaypointIndex = 0;
            ReinitializePatrolState();

            isPatrollingRandomRoom = false;
            originalPatrolRoute = null;

            Debug.Log($"[HanaduraAI] '{gameObject.name}' returned to original room: '{originalRoomId}'");
        }

        public bool IsPatrollingRandomRoom()
        {
            return isPatrollingRandomRoom;
        }

        public string GetCurrentPatrolRoomId()
        {
            return isPatrollingRandomRoom ? currentRandomRoomId : originalRoomId;
        }

        #endregion
    }
}
