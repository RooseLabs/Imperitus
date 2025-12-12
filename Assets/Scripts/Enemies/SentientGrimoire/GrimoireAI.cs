using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Sentient Grimoire - A floating book that detects players with a spotlight
    /// and calls Hanadura reinforcements when a player is spotted
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GrimoireAI : BaseEnemy
    {
        [Header("References")]
        public NavMeshAgent navAgent;
        public PatrolRoute patrolRoute;
        public Animator animator;
        public Light spotlight;
        public Transform spotlightTransform;
        private Quaternion defaultSpotlightRotation;
        public Transform modelTransform;
        private Quaternion defaultModelRotation;
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
        [Tooltip("How often to send position updates to alerted Hanaduras (seconds)")]
        public float reinforcementUpdateInterval = 1f;
        private float reinforcementUpdateTimer = 0f;
        private List<HanaduraAI> alertedHanaduras = new();

        [Header("Alert Visual")]
        public Color normalSpotlightColor = Color.white;
        public Color alertSpotlightColor = Color.red;
        public float colorTransitionSpeed = 2f;

        [Header("Model Rotation")]
        public float modelRotationSpeed = 5f;
        public bool showDebugRay = true;
        public float debugRayLength = 3f;

        #region Animation Parameters
        private static readonly int AnimParamIsPatrolling = Animator.StringToHash("isPatrolling");
        private static readonly int AnimParamIsAlert = Animator.StringToHash("isAlert");
        #endregion

        // FSM States
        public GrimoirePatrolState PatrolState { get; private set; }
        public GrimoireAlertState AlertState { get; private set; }
        public GrimoireTrackingState TrackingState { get; private set; }

        // Detection
        private Transform detectedPlayer;
        private PlayerCharacter detectedPlayerCharacter;
        private float reinforcementTimer = 0f;
        private float detectionTimer = 0f;

        // Network synchronized variables
        private readonly SyncVar<Color> syncedSpotlightColor = new();
        private readonly SyncVar<Quaternion> syncedSpotlightRotation = new();
        private readonly SyncVar<Quaternion> syncedModelRotation = new();

        // Public properties for states to access
        public Transform DetectedPlayer => detectedPlayer;

        private bool m_hasHandledDeath = false;

        protected override void Initialize()
        {
            TryGetComponent(out navAgent);
            modelTransform.TryGetComponent(out animator);
            TryGetComponent(out rb);
        }

        public override void OnStartServer()
        {
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
            PatrolState = new GrimoirePatrolState(this, patrolRoute, loopPatrol, startWaypointIndex, waypointReachThreshold);
            AlertState = new GrimoireAlertState(this, alertDuration);
            TrackingState = new GrimoireTrackingState(this);

            // Start in patrol state
            ChangeState(PatrolState);
        }

        public override void OnStartClient()
        {
            // Subscribe to SyncVar changes on clients
            if (IsServerInitialized) return;
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
        }

        public override void OnStopClient()
        {
            syncedSpotlightColor.OnChange -= OnSpotlightColorChanged;
            syncedSpotlightRotation.OnChange -= OnSpotlightRotationChanged;
            syncedModelRotation.OnChange -= OnModelRotationChanged;
        }

        public override void OnStopServer()
        {
            syncedSpotlightColor.OnChange -= OnSpotlightColorChanged;
            syncedSpotlightRotation.OnChange -= OnSpotlightRotationChanged;
            syncedModelRotation.OnChange -= OnModelRotationChanged;
        }

        private void Update()
        {
            if (IsDead) return;

            if (!IsServerInitialized)
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
            reinforcementUpdateTimer -= Time.deltaTime;

            // Update current state
            currentState?.Update();

            // Periodic detection check
            if (detectionTimer <= 0f)
            {
                detectionTimer = detectionCheckInterval;
                CheckSpotlightDetection();
            }

            // Send position updates to alerted Hanaduras
            if (reinforcementUpdateTimer <= 0f)
            {
                reinforcementUpdateTimer = reinforcementUpdateInterval;
                UpdateAlertedHanaduras();
            }

            // Update spotlight visuals and sync to network
            UpdateSpotlightVisualsServer();

            UpdateModelRotation();

            // Update animator parameters (NetworkAnimator handles the syncing)
            UpdateAnimatorParameters();
        }

        #region Animation Control
        /// <summary>
        /// Updates animator parameters based on current state.
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (!animator) return;

            // Set state bools based on current state
            bool isInPatrolState = currentState is GrimoirePatrolState;
            bool isInAlertOrTracking = currentState is GrimoireAlertState or GrimoireTrackingState;

            animator.SetBool(AnimParamIsPatrolling, isInPatrolState);
            animator.SetBool(AnimParamIsAlert, isInAlertOrTracking);
        }
        #endregion

        #region Detection
        private void CheckSpotlightDetection()
        {
            if (!spotlight || !spotlightTransform) return;

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
                    if (Physics.Raycast(spotlightPos, dirToTarget, out var hit, dist, obstructionMask))
                    {
                        // this.LogInfo($"Line of sight BLOCKED by: {hit.collider.name}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}, Distance: {hit.distance}");
                        Debug.DrawRay(spotlightPos, dirToTarget * hit.distance, Color.red, 0.5f);
                    }
                    else
                    {
                        // this.LogInfo("Line of sight CLEAR!");
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
            if ((bool)closestPlayer)
            {
                OnPlayerDetected(closestPlayer);
            }
            else if ((bool)detectedPlayer && currentState is not GrimoirePatrolState)
            {
                // Lost sight of player
                detectedPlayer = null;
            }
        }

        private void OnPlayerDetected(Transform player)
        {
            bool isNewDetection = !detectedPlayer;
            detectedPlayer = player;
            player.TryGetComponentInParent(out detectedPlayerCharacter);

            // Call reinforcements if cooldown is ready
            if (isNewDetection && reinforcementTimer <= 0f)
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
            if (!IsServerInitialized) return;
            if (!target || !spotlightTransform) return;

            Vector3 targetPoint = (bool)detectedPlayerCharacter
                ? detectedPlayerCharacter.Center
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
            if (!IsServerInitialized) return;
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
            // Find all Hanadura enemies in range
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, reinforcementSearchRadius);
            List<HanaduraAI> availableHanaduras = new List<HanaduraAI>();

            foreach (Collider col in nearbyColliders)
            {
                if (col.TryGetComponent(out HanaduraAI hanadura))
                {
                    availableHanaduras.Add(hanadura);
                }
            }

            if (availableHanaduras.Count == 0) return;

            // Sort by distance and call the closest ones
            availableHanaduras.Sort((a, b) =>
            {
                float distA = Vector3.Distance(transform.position, a.transform.position);
                float distB = Vector3.Distance(transform.position, b.transform.position);
                return distA.CompareTo(distB);
            });

            alertedHanaduras.Clear();

            int called = 0;
            foreach (HanaduraAI hanadura in availableHanaduras)
            {
                if (called >= maxReinforcementsToCall) break;
                hanadura.AlertToPosition(detectedPlayer.position);
                alertedHanaduras.Add(hanadura);
                called++;
            }

            EnemySpawnManager.Instance?.OnGrimoireAlert(detectedPlayer.position);

            // Notify all clients of reinforcement call
            RPC_PlayReinforcementCallEffect();
        }

        /// <summary>
        /// Updates alerted Hanaduras with the current player position
        /// </summary>
        private void UpdateAlertedHanaduras()
        {
            if (!detectedPlayer || currentState is GrimoirePatrolState)
            {
                // Clear the list if we're not tracking anymore
                if (alertedHanaduras.Count > 0)
                {
                    alertedHanaduras.Clear();
                }
                return;
            }

            // Remove any dead or null Hanaduras from the list
            alertedHanaduras.RemoveAll(h => !h || h.IsDead);

            // Send updated position to all alerted Hanaduras
            foreach (HanaduraAI hanadura in alertedHanaduras)
            {
                hanadura.AlertToPosition(detectedPlayer.position, detectedPlayer);
                this.LogInfo($"Updated Hanadura {hanadura.gameObject.name} with new player position.");
            }
        }
        #endregion

        #region Visual Effects & Network Sync
        /// <summary>
        /// Server: Update spotlight visuals and sync to network
        /// </summary>
        private void UpdateSpotlightVisualsServer()
        {
            if (!spotlight) return;

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
            if (spotlight)
            {
                spotlight.color = Color.Lerp(spotlight.color, syncedSpotlightColor.Value, Time.deltaTime * colorTransitionSpeed);
            }

            if (spotlightTransform)
            {
                spotlightTransform.rotation = Quaternion.Slerp(
                    spotlightTransform.rotation,
                    syncedSpotlightRotation.Value,
                    Time.deltaTime * 10f
                );
            }

            if (modelTransform)
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
            // this.LogInfo("Alert RPC received");
        }

        [ObserversRpc]
        private void RPC_PlayReinforcementCallEffect()
        {
            // Play special effect when reinforcements are called
            // e.g., magic circle, sound effect, screen shake, etc.
            // this.LogInfo("Reinforcement call effect RPC received");
        }

        private void UpdateModelRotation()
        {
            if (!modelTransform) return;

            Quaternion targetRotation;

            if (currentState is GrimoireAlertState or GrimoireTrackingState && (bool)detectedPlayer)
            {
                Vector3 directionToPlayer = detectedPlayerCharacter.Center - transform.position;
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

        protected override void OnDeath()
        {
            if (animator)
            {
                HandleDeath_ObserversRPC();
            }
            else
            {
                this.LogWarning($"No Animator found on {gameObject.name}, cannot play death animation.");
                Despawn(gameObject);
            }
        }

        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
        private void HandleDeath_ObserversRPC()
        {
            if (!animator || m_hasHandledDeath) return;
            currentState = null;
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.enabled = false;
            rb.useGravity = true;
            rb.isKinematic = false;
            animator.Play("Death");
            m_hasHandledDeath = true;

            this.LogWarning($"{gameObject.name} death sequence executed on observer");

            // StartCoroutine(DespawnAfterDeath());
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

        #if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            TryGetComponent(out navAgent);
        }

        #region Debug
        private void OnDrawGizmosSelected()
        {
            if (!spotlightTransform) return;

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
        #endif
    }
}
