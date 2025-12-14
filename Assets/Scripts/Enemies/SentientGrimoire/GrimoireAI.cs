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
        public Animator animator;
        public Rigidbody rb;
        public Transform modelTransform;
        public Light spotlight;
        public Transform spotlightTransform;
        public SpotlightConeVisualizer coneVisualizer;
        public PatrolRoute patrolRoute;

        [Header("Movement & Patrol")]
        public float patrolSpeed = 2f;
        public float trackingSpeed = 1.5f;
        public int startWaypointIndex;
        public bool loopPatrol = true;
        public float waypointReachThreshold = 1.5f;

        [Header("Spotlight Detection")]
        public float spotlightRange = 15f;
        public float spotlightAngle = 60f;
        public float detectionCheckInterval = 0.2f;
        public LayerMask playerMask;
        public LayerMask obstructionMask;

        [Header("Visual Settings")]
        public Color normalSpotlightColor = Color.white;
        public Color alertSpotlightColor = Color.red;
        public float colorTransitionSpeed = 2f;
        [Tooltip("Rotation speed in degrees per second")]
        public float spotlightRotationSpeed = 180f;
        [Tooltip("Rotation speed in degrees per second")]
        public float modelRotationSpeed = 180f;

        [Header("Alert & Reinforcements")]
        public float alertDuration = 5f;
        public float callReinforcementsCooldown = 10f;
        public float reinforcementSearchRadius = 50f;
        public int maxReinforcementsToCall = 3;
        [Tooltip("How often to send position updates to alerted Hanaduras (seconds)")]
        public float reinforcementUpdateInterval = 1f;

        [Header("Debug")]
        public bool showDebugRay = true;
        public float debugRayLength = 3f;

        #region Animation Parameters
        private static readonly int AnimParamIsPatrolling = Animator.StringToHash("isPatrolling");
        private static readonly int AnimParamIsAlert = Animator.StringToHash("isAlert");
        #endregion

        #region Private Fields
        private Quaternion m_defaultSpotlightRotation;
        private float m_reinforcementUpdateTimer;
        private readonly List<HanaduraAI> m_alertedHanaduras = new();
        private Transform m_detectedPlayer;
        private float m_reinforcementTimer;
        private float m_detectionTimer;
        private bool m_hasHandledDeath;
        #endregion

        #region Network Synchronized Variables
        private readonly SyncVar<Transform> m_syncedSpotlightTarget = new();
        #endregion

        #region Public Properties
        public Transform DetectedPlayer => m_detectedPlayer;
        #endregion

        // FSM States
        public GrimoirePatrolState PatrolState { get; private set; }
        public GrimoireAlertState AlertState { get; private set; }
        public GrimoireTrackingState TrackingState { get; private set; }

        protected override void Initialize()
        {
            TryGetComponent(out navAgent);
            modelTransform.TryGetComponent(out animator);
            TryGetComponent(out rb);

            // Store initial spotlight rotation
            if (spotlightTransform)
            {
                m_defaultSpotlightRotation = spotlightTransform.rotation;
            }
        }

        public override void OnStartServer()
        {
            navAgent.speed = patrolSpeed;

            // Create states
            PatrolState = new GrimoirePatrolState(this, patrolRoute, loopPatrol, startWaypointIndex, waypointReachThreshold);
            AlertState = new GrimoireAlertState(this, alertDuration);
            TrackingState = new GrimoireTrackingState(this);

            // Start in patrol state
            ChangeState(PatrolState);
        }

        public override void OnStartClient()
        {
            // Initialize cone visualizer
            if (coneVisualizer)
            {
                coneVisualizer.SetSpotlightOrigin(spotlightTransform);
                coneVisualizer.SetConeAngle(spotlightAngle);
            }
        }

        private void Update()
        {
            if (!IsServerInitialized) return;
            if (IsDead) return;

            if (showDebugRay)
            {
                Debug.DrawRay(transform.position, transform.forward * debugRayLength, Color.purple);
            }

            m_detectionTimer -= Time.deltaTime;
            m_reinforcementTimer -= Time.deltaTime;
            m_reinforcementUpdateTimer -= Time.deltaTime;

            // Update current state
            currentState?.Update();

            // Periodic detection check
            if (m_detectionTimer <= 0f)
            {
                m_detectionTimer = detectionCheckInterval;
                CheckSpotlightDetection();
            }

            // Send position updates to alerted Hanaduras
            if (m_reinforcementUpdateTimer <= 0f)
            {
                m_reinforcementUpdateTimer = reinforcementUpdateInterval;
                UpdateAlertedHanaduras();
            }

            // Update animator parameters (NetworkAnimator handles the syncing)
            UpdateAnimatorParameters();
        }

        private void LateUpdate()
        {
            if (IsDead) return;

            // Update all visual elements (spotlight, model rotation) on both server and clients
            UpdateVisuals();
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
                        }
                    }
                }
            }

            // If player detected
            if ((bool)closestPlayer)
            {
                OnPlayerDetected(closestPlayer);
            }
            else if ((bool)m_detectedPlayer && currentState is not GrimoirePatrolState)
            {
                // Lost sight of player
                m_detectedPlayer = null;
                SetSpotlightTarget(null);
            }
        }

        private void OnPlayerDetected(Transform player)
        {
            bool isNewDetection = !m_detectedPlayer;
            m_detectedPlayer = player;

            // Set spotlight to track the detected player
            SetSpotlightTarget(player);

            // Call reinforcements if cooldown is ready
            if (isNewDetection && m_reinforcementTimer <= 0f)
            {
                CallReinforcements();
                m_reinforcementTimer = callReinforcementsCooldown;
            }
        }
        #endregion

        #region Spotlight Control (Helper methods for states)
        /// <summary>
        /// Set the spotlight to track a specific transform (usually the detected player)
        /// </summary>
        /// <param name="target">Transform to track, or null to return to default rotation</param>
        public void SetSpotlightTarget(Transform target)
        {
            if (!IsServerInitialized) return;
            m_syncedSpotlightTarget.Value = target;
        }

        /// <summary>
        /// Update all visual elements including spotlight color, spotlight rotation, and model rotation.
        /// Called in LateUpdate on both server and clients to ensure synchronized visuals.
        /// </summary>
        private void UpdateVisuals()
        {
            // Update spotlight rotation
            UpdateSpotlightRotation();

            // Update spotlight color based on target
            if (spotlight)
            {
                Color targetColor = (bool)m_syncedSpotlightTarget.Value
                    ? alertSpotlightColor
                    : normalSpotlightColor;

                Color finalColor = Color.Lerp(spotlight.color, targetColor, Time.deltaTime * colorTransitionSpeed);
                spotlight.color = finalColor;
                coneVisualizer?.SetConeColor(finalColor);
            }

            // Update model rotation
            UpdateModelRotation();
        }

        /// <summary>
        /// Continuously update spotlight rotation toward target or default direction.
        /// Called by UpdateVisuals in LateUpdate on both server and clients.
        /// </summary>
        private void UpdateSpotlightRotation()
        {
            if (!spotlightTransform) return;

            Quaternion targetRotation;

            // Determine target rotation based on synced target
            Transform currentTarget = m_syncedSpotlightTarget.Value;

            if (currentTarget)
            {
                // Track the target transform
                Vector3 targetPoint = currentTarget.position;

                // Try to get player character for better center targeting
                if (currentTarget.TryGetComponentInParent(out PlayerCharacter playerChar))
                {
                    targetPoint = playerChar.Center;
                }

                Vector3 direction = (targetPoint - spotlightTransform.position).normalized;
                targetRotation = Quaternion.LookRotation(direction);
            }
            else
            {
                // Return to default rotation
                targetRotation = m_defaultSpotlightRotation;
            }

            // Smoothly rotate toward target using RotateTowards
            spotlightTransform.rotation = Quaternion.RotateTowards(
                spotlightTransform.rotation,
                targetRotation,
                spotlightRotationSpeed * Time.deltaTime
            );
        }

        /// <summary>
        /// Update model rotation based on spotlight target.
        /// Called by UpdateVisuals in LateUpdate on both server and clients.
        /// </summary>
        private void UpdateModelRotation()
        {
            if (!modelTransform) return;

            Quaternion targetRotation;
            Transform currentTarget = m_syncedSpotlightTarget.Value;

            if (currentTarget)
            {
                // Rotate model to face the target
                Vector3 targetPoint = currentTarget.position;

                // Try to get player character for better center targeting
                if (currentTarget.TryGetComponentInParent(out PlayerCharacter playerChar))
                {
                    targetPoint = playerChar.Center;
                }

                Vector3 directionToTarget = targetPoint - transform.position;
                directionToTarget.y = 0;

                if (directionToTarget != Vector3.zero)
                {
                    targetRotation = Quaternion.LookRotation(directionToTarget);
                    targetRotation = Quaternion.Inverse(transform.rotation) * targetRotation;
                }
                else
                {
                    targetRotation = Quaternion.identity;
                }
            }
            else
            {
                targetRotation = Quaternion.identity;
            }

            modelTransform.localRotation = Quaternion.RotateTowards(
                modelTransform.localRotation,
                targetRotation,
                modelRotationSpeed * Time.deltaTime
            );
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

            m_alertedHanaduras.Clear();

            int called = 0;
            foreach (HanaduraAI hanadura in availableHanaduras)
            {
                if (called >= maxReinforcementsToCall) break;
                hanadura.AlertToPosition(m_detectedPlayer.position);
                m_alertedHanaduras.Add(hanadura);
                called++;
            }

            EnemySpawnManager.Instance?.OnGrimoireAlert(m_detectedPlayer.position);

            // Notify all clients of reinforcement call
            RPC_PlayReinforcementCallEffect();
        }

        /// <summary>
        /// Updates alerted Hanaduras with the current player position
        /// </summary>
        private void UpdateAlertedHanaduras()
        {
            if (!m_detectedPlayer || currentState is GrimoirePatrolState)
            {
                // Clear the list if we're not tracking anymore
                if (m_alertedHanaduras.Count > 0)
                {
                    m_alertedHanaduras.Clear();
                }
                return;
            }

            // Remove any dead or null Hanaduras from the list
            m_alertedHanaduras.RemoveAll(h => !h || h.IsDead);

            // Send updated position to all alerted Hanaduras
            foreach (HanaduraAI hanadura in m_alertedHanaduras)
            {
                hanadura.AlertToPosition(m_detectedPlayer.position, m_detectedPlayer);
                this.LogInfo($"Updated Hanadura {hanadura.gameObject.name} with new player position.");
            }
        }
        #endregion

        #region Visual Effects & Network Sync
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
