using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Sentient Grimoire - A floating book that detects players with a spotlight
    /// and calls Hanadura reinforcements when a player is spotted
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NetworkObject))]
    public class GrimoireAI : NetworkBehaviour, IEnemyAI
    {
        [Header("References")]
        public NavMeshAgent navAgent;
        public PatrolRoute patrolRoute;
        public Animator animator;
        public Light spotlight;
        public Transform spotlightTransform;
        private Quaternion defaultSpotlightRotation;
        private Collider detectedPlayerCollider;
        public Transform modelTransform;
        private Quaternion defaultModelRotation;
        public EnemyData enemyData;
        public Rigidbody rb;

        [Header("Patrol")]
        public int startWaypointIndex = 0;
        public bool loopPatrol = true;
        public float patrolSpeed = 2f;
        public float waypointReachThreshold = 1.5f;

        [Header("Detection - Spotlight")]
        public float spotlightRange = 15f;
        public float spotlightAngle = 60f;
        public LayerMask playerMask;
        public LayerMask obstructionMask;
        public float detectionCheckInterval = 0.2f;

        [Header("Alert Behavior")]
        public float alertDuration = 5f;
        public float callReinforcementsCooldown = 10f;
        public float reinforcementSearchRadius = 50f;
        public int maxReinforcementsToCall = 3;
        public float trackingSpeed = 1.5f;

        [Header("Alert Visual")]
        public Color normalSpotlightColor = Color.white;
        public Color alertSpotlightColor = Color.red;
        public float colorTransitionSpeed = 2f;

        [Header("Model Rotation")]
        public float modelRotationSpeed = 5f;
        public bool showDebugRay = true;
        public float debugRayLength = 3f;

        // FSM States
        private IEnemyState currentState;
        public GrimoirePatrolState patrolState;
        public GrimoireAlertState alertState;
        public GrimoireTrackingState trackingState;

        // Detection
        private Transform detectedPlayer;
        private float reinforcementTimer = 0f;
        private float detectionTimer = 0f;

        // Network synchronized variables
        private readonly SyncVar<Color> syncedSpotlightColor = new SyncVar<Color>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));
        private readonly SyncVar<Quaternion> syncedSpotlightRotation = new SyncVar<Quaternion>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));
        private readonly SyncVar<Quaternion> syncedModelRotation = new SyncVar<Quaternion>(new SyncTypeSettings(WritePermission.ServerOnly, ReadPermission.Observers));

        // Public properties for states to access
        public Transform DetectedPlayer => detectedPlayer;
        public Animator Animator => animator;

        private string whatWasPreviousState = "";

        private bool hasHandledDeath = false;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (modelTransform == null && animator != null)
            {
                modelTransform = animator.transform;
            }

            if (enemyData == null)
            {
                enemyData = GetComponent<EnemyData>();
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
        }

        private void Reset()
        {
            navAgent = GetComponent<NavMeshAgent>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

            navAgent.speed = patrolSpeed;

            // Store initial spotlight rotation
            if (spotlightTransform != null)
            {
                defaultSpotlightRotation = spotlightTransform.rotation;
                syncedSpotlightRotation.Value = defaultSpotlightRotation;
            }

            if (modelTransform != null)
            {
                defaultModelRotation = modelTransform.localRotation;
                syncedModelRotation.Value = defaultModelRotation;
            }


            syncedSpotlightColor.Value = normalSpotlightColor;

            // Subscribe to SyncVar changes
            syncedSpotlightColor.OnChange += OnSpotlightColorChanged;
            syncedSpotlightRotation.OnChange += OnSpotlightRotationChanged;
            syncedModelRotation.OnChange += OnModelRotationChanged;

            // Create states
            patrolState = new GrimoirePatrolState(this, patrolRoute, loopPatrol, startWaypointIndex, waypointReachThreshold);
            alertState = new GrimoireAlertState(this, alertDuration);
            trackingState = new GrimoireTrackingState(this);

            // Start in patrol state
            EnterState(patrolState);

            //Debug.Log("[GrimoireAI] OnStartServer complete - beginning patrol");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Subscribe to SyncVar changes on clients
            if (!base.IsServerInitialized)
            {
                syncedSpotlightColor.OnChange += OnSpotlightColorChanged;
                syncedSpotlightRotation.OnChange += OnSpotlightRotationChanged;
                syncedModelRotation.OnChange += OnModelRotationChanged;

                // Apply initial synced values
                if (spotlight != null)
                    spotlight.color = syncedSpotlightColor.Value;

                if (spotlightTransform != null)
                    spotlightTransform.rotation = syncedSpotlightRotation.Value;

                if (modelTransform != null)
                    modelTransform.localRotation = syncedModelRotation.Value;

                //Debug.Log("[GrimoireAI] OnStartClient - applied initial spotlight state");
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Unsubscribe from events to prevent memory leaks
            syncedSpotlightColor.OnChange -= OnSpotlightColorChanged;
            syncedSpotlightRotation.OnChange -= OnSpotlightRotationChanged;
            syncedModelRotation.OnChange -= OnModelRotationChanged;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            // Unsubscribe from events
            syncedSpotlightColor.OnChange -= OnSpotlightColorChanged;
            syncedSpotlightRotation.OnChange -= OnSpotlightRotationChanged;
            syncedModelRotation.OnChange -= OnModelRotationChanged;
        }

        private void Update()
        {
            if (!hasHandledDeath && enemyData.IsDead)
            {
                HandleDeath_ServerRPC();
                return;
            }
            else if (enemyData.IsDead)
                return;

            if (!base.IsServerInitialized)
            {
                UpdateSpotlightVisualsClient();
                return;
            }

            if (showDebugRay)
            {
                Debug.DrawRay(transform.position, transform.forward * debugRayLength, Color.purple);
            }

            // Server logic only
            detectionTimer -= Time.deltaTime;
            reinforcementTimer -= Time.deltaTime;

            // Periodic detection check
            if (detectionTimer <= 0f)
            {
                detectionTimer = detectionCheckInterval;
                CheckSpotlightDetection();
            }

            // Tick current state
            currentState?.Tick();

            //if (whatWasPreviousState != (currentState != null ? currentState.GetType().Name : "null"))
            //{
            //    Debug.Log("currentState is " + (currentState != null ? currentState.GetType().Name : "null"));
            //}

            whatWasPreviousState = currentState != null ? currentState.GetType().Name : "null";
               
            // Update spotlight visuals and sync to network
            UpdateSpotlightVisualsServer();

            UpdateModelRotation();

            // Update animator parameters (NetworkAnimator handles the syncing)
            UpdateAnimatorParameters();
        }

        #region State Management

        public void EnterState(IEnemyState newState)
        {
            if (currentState != null)
                currentState.Exit();

            currentState = newState;

            if (currentState != null)
            {
                //Debug.Log($"[GrimoireAI] Entered state: {currentState.GetType().Name}");
                currentState.Enter();
            }
        }

        #endregion

        #region Animation Control

        /// <summary>
        /// Updates animator parameters based on current state.
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (animator == null) return;

            // Set state bools based on current state
            bool isInPatrolState = currentState is GrimoirePatrolState;
            bool isInAlertOrTracking = currentState is GrimoireAlertState || currentState is GrimoireTrackingState;

            animator.SetBool("isPatrolling", isInPatrolState);
            animator.SetBool("isAlert", isInAlertOrTracking);
        }

        #endregion

        #region Detection

        private void CheckSpotlightDetection()
        {
            if (spotlight == null || spotlightTransform == null) return;

            Vector3 spotlightPos = spotlightTransform.position;
            Vector3 spotlightDir = spotlightTransform.forward;

            // Find all potential players in range
            Collider[] potentialTargets = Physics.OverlapSphere(spotlightPos, spotlightRange, playerMask);

            Transform closestPlayer = null;
            float closestDist = float.MaxValue;
            Collider closestPlayerCollider = null;

            foreach (Collider col in potentialTargets)
            {
                Transform target = col.transform;
                Vector3 targetPoint = col.bounds.center;
                Vector3 dirToTarget = (targetPoint - spotlightPos).normalized;
                float angleToTarget = Vector3.Angle(spotlightDir, dirToTarget);

                // Check if within spotlight cone
                if (angleToTarget <= spotlightAngle * 0.5f)
                {
                    float dist = Vector3.Distance(spotlightPos, targetPoint);

                    // Raycast to check line of sight
                    RaycastHit hit;
                    if (Physics.Raycast(spotlightPos, dirToTarget, out hit, dist, obstructionMask))
                    {
                        //Debug.Log($"[GrimoireAI] Line of sight BLOCKED by: {hit.collider.name}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}, Distance: {hit.distance}");
                        Debug.DrawRay(spotlightPos, dirToTarget * hit.distance, Color.red, 0.5f);
                    }
                    else
                    {
                        //Debug.Log("[GrimoireAI] Line of sight CLEAR!");
                        Debug.DrawRay(spotlightPos, dirToTarget * dist, Color.green, 0.5f);

                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestPlayer = target;
                            closestPlayerCollider = col;
                        }
                    }
                }
            }

            // If player detected
            if (closestPlayer != null)
            {
                OnPlayerDetected(closestPlayer, closestPlayerCollider);
            }
            else if (detectedPlayer != null && !(currentState is GrimoirePatrolState))
            {
                // Lost sight of player
                detectedPlayer = null;
            }
        }

        private void OnPlayerDetected(Transform player, Collider playerCollider)
        {
            bool wasNewDetection = (detectedPlayer == null);
            detectedPlayer = player;
            detectedPlayerCollider = playerCollider;

            //Debug.Log("[GrimoireAI] Player detected!");

            // Transition to alert state if currently patrolling
            if (currentState is GrimoirePatrolState)
            {
                EnterState(alertState);
            }

            // Call reinforcements if cooldown is ready
            if (wasNewDetection && reinforcementTimer <= 0f)
            {
                CallReinforcements();
                reinforcementTimer = callReinforcementsCooldown;
            }
        }

        #endregion

        #region Spotlight Control (Helper methods for states)

        /// <summary>
        /// Rotate spotlight to track a target (SERVER ONLY)
        /// </summary>
        public void RotateSpotlightToTarget(Transform target, float speed)
        {
            if (!base.IsServerInitialized) return;
            if (target == null || spotlightTransform == null) return;

            Vector3 targetPoint = detectedPlayerCollider != null
                ? detectedPlayerCollider.bounds.center
                : target.position + Vector3.up * 1f;

            Vector3 dirToTarget = (targetPoint - spotlightTransform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
            spotlightTransform.rotation = Quaternion.Slerp(
                spotlightTransform.rotation,
                targetRot,
                Time.deltaTime * speed
            );

            syncedSpotlightRotation.Value = spotlightTransform.rotation;
        }

        /// <summary>
        /// Rotate spotlight back to default forward position (SERVER ONLY)
        /// </summary>
        public void RotateSpotlightToDefault(float speed)
        {
            if (!base.IsServerInitialized) return;
            if (spotlightTransform == null) return;

            spotlightTransform.rotation = Quaternion.Slerp(
                spotlightTransform.rotation,
                defaultSpotlightRotation,
                Time.deltaTime * speed
            );

            // Update synced rotation
            syncedSpotlightRotation.Value = spotlightTransform.rotation;
        }

        #endregion

        #region Reinforcements

        private void CallReinforcements()
        {
            if (!base.IsServerInitialized)
            {
                //Debug.LogWarning("[GrimoireAI] Tried to call reinforcements on client - ignored");
                return;
            }

            // Find all Hanadura enemies in range
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, reinforcementSearchRadius);
            List<HanaduraAI> availableHanaduras = new List<HanaduraAI>();

            foreach (Collider col in nearbyColliders)
            {
                HanaduraAI hanadura = col.GetComponent<HanaduraAI>();
                if (hanadura != null)
                {
                    availableHanaduras.Add(hanadura);
                }
            }

            if (availableHanaduras.Count == 0)
            {
                //Debug.Log("[GrimoireAI] No Hanadura reinforcements found nearby");
                return;
            }

            // Sort by distance and call the closest ones
            availableHanaduras.Sort((a, b) =>
            {
                float distA = Vector3.Distance(transform.position, a.transform.position);
                float distB = Vector3.Distance(transform.position, b.transform.position);
                return distA.CompareTo(distB);
            });

            int called = 0;
            foreach (HanaduraAI hanadura in availableHanaduras)
            {
                if (called >= maxReinforcementsToCall) break;

                if (detectedPlayer != null)
                {
                    hanadura.AlertToPosition(detectedPlayer.position);
                    called++;
                }
            }

            //Debug.Log($"[GrimoireAI] Called {called} Hanadura reinforcements!");

            // Notify all clients of reinforcement call
            RPC_PlayReinforcementCallEffect();
        }

        #endregion

        #region Visual Effects & Network Sync

        /// <summary>
        /// Server: Update spotlight visuals and sync to network
        /// </summary>
        private void UpdateSpotlightVisualsServer()
        {
            if (spotlight == null) return;

            Color targetColor = (currentState is GrimoirePatrolState)
                ? normalSpotlightColor
                : alertSpotlightColor;

            spotlight.color = Color.Lerp(spotlight.color, targetColor, Time.deltaTime * colorTransitionSpeed);

            // Sync color to network
            syncedSpotlightColor.Value = spotlight.color;
        }

        /// <summary>
        /// Client: Smoothly interpolate spotlight to synced values
        /// </summary>
        private void UpdateSpotlightVisualsClient()
        {
            if (spotlight != null)
            {
                spotlight.color = Color.Lerp(spotlight.color, syncedSpotlightColor.Value, Time.deltaTime * colorTransitionSpeed);
            }

            if (spotlightTransform != null)
            {
                spotlightTransform.rotation = Quaternion.Slerp(
                    spotlightTransform.rotation,
                    syncedSpotlightRotation.Value,
                    Time.deltaTime * 10f
                );
            }

            if (modelTransform != null)
            {
                modelTransform.localRotation = Quaternion.Slerp(
                    modelTransform.localRotation,
                    syncedModelRotation.Value,
                    Time.deltaTime * modelRotationSpeed
                );
            }
        }

        /// <summary>
        /// SyncVar callback when spotlight color changes
        /// </summary>
        private void OnSpotlightColorChanged(Color prev, Color next, bool asServer)
        {
            if (!asServer && spotlight != null)
            {
                // Client received color update - apply immediately
                spotlight.color = next;
            }
        }

        /// <summary>
        /// SyncVar callback when spotlight rotation changes
        /// </summary>
        private void OnSpotlightRotationChanged(Quaternion prev, Quaternion next, bool asServer)
        {
            if (!asServer && spotlightTransform != null)
            {
                // Client received rotation update - will interpolate in Update
                spotlightTransform.rotation = next;
            }
        }

        [ObserversRpc]
        public void RPC_ShowAlert()
        {
            // Play alert sound, particle effects, etc.
            // Animation is handled by NetworkAnimator automatically
            //Debug.Log("[GrimoireAI] Alert RPC received");
        }

        [ObserversRpc]
        private void RPC_PlayReinforcementCallEffect()
        {
            // Play special effect when reinforcements are called
            // e.g., magic circle, sound effect, screen shake, etc.
            //Debug.Log("[GrimoireAI] Reinforcement call effect RPC received");
        }

        private void UpdateModelRotation()
        {
            if (!base.IsServerInitialized) return;
            if (modelTransform == null) return;

            Quaternion targetRotation;

            if ((currentState is GrimoireAlertState || currentState is GrimoireTrackingState) && detectedPlayer != null)
            {
                Vector3 directionToPlayer = (detectedPlayer.GetComponentInParent<PlayerCharacter>().RaycastTarget.position - transform.position);
                directionToPlayer.y = 0;

                if (directionToPlayer != Vector3.zero)
                {
                    targetRotation = Quaternion.LookRotation(directionToPlayer);
                    targetRotation = Quaternion.Inverse(transform.rotation) * targetRotation;
                }
                else
                {
                    targetRotation = defaultModelRotation;
                }
            }
            else
            {
                targetRotation = defaultModelRotation;
            }

            modelTransform.localRotation = Quaternion.Slerp(
                modelTransform.localRotation,
                targetRotation,
                Time.deltaTime * modelRotationSpeed
            );

            syncedModelRotation.Value = modelTransform.localRotation;
        }

        private void OnModelRotationChanged(Quaternion prev, Quaternion next, bool asServer)
        {
            if (!asServer && modelTransform != null)
            {
                modelTransform.localRotation = next;
            }
        }

        #endregion

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
            if (animator != null && !hasHandledDeath)
            {
                currentState = null;
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
                navAgent.enabled = false;
                rb.useGravity = true;
                rb.isKinematic = false;
                animator.Play("Death");
                hasHandledDeath = true;

                Debug.Log($"{gameObject.name} death sequence executed on observer");

                // StartCoroutine(DespawnAfterDeath());
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

        #region Debug

        private void OnDrawGizmosSelected()
        {
            if (spotlightTransform == null) return;

            // Draw spotlight cone
            bool isPatrolling = (currentState is GrimoirePatrolState);
            Gizmos.color = isPatrolling ? Color.yellow : Color.red;

            Vector3 forward = spotlightTransform.forward * spotlightRange;
            Quaternion leftRot = Quaternion.Euler(0, -spotlightAngle * 0.5f, 0);
            Quaternion rightRot = Quaternion.Euler(0, spotlightAngle * 0.5f, 0);

            Vector3 leftDir = spotlightTransform.rotation * leftRot * Vector3.forward * spotlightRange;
            Vector3 rightDir = spotlightTransform.rotation * rightRot * Vector3.forward * spotlightRange;

            Gizmos.DrawLine(spotlightTransform.position, spotlightTransform.position + forward);
            Gizmos.DrawLine(spotlightTransform.position, spotlightTransform.position + leftDir);
            Gizmos.DrawLine(spotlightTransform.position, spotlightTransform.position + rightDir);

            // Draw reinforcement radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, reinforcementSearchRadius);
        }

        #endregion
    }
}