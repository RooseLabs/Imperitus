using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RooseLabs.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NetworkObject))]
    public class HanaduraAI : NetworkBehaviour, ISoundListener
    {
        [Header("References")]
        public NavMeshAgent navAgent;
        public EnemyDetection detection;
        public PatrolRoute patrolRoute;
        public Animator animator;
        public Volume volume;

        [Header("Combat")]
        public float attackRange = 2f;
        public float attackCooldown = 1.2f;
        public int attackDamage = 10;

        [Header("Patrol")]
        public int startWaypointIndex = 0;
        public bool loopPatrol = true;

        [Header("Chase")]
        public float forgetTargetTime = 3f; // time to forget target after losing sight
        private float forgetTimer = 0f;

        public Vector3? LastKnownTargetPosition { get; set; }

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

        // for attack cooldown
        private float attackTimer = 0f;

        private bool isInvestigating = false;

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
        }

        private void Update()
        {
            if (!base.IsServerInitialized)
                return;

            currentState?.Tick();

            attackTimer -= Time.deltaTime;

            Transform detected = detection.DetectedTarget;

            if (detected != null)
            {
                // Player detected
                CurrentTarget = detected;
                LastKnownTargetPosition = CurrentTarget.position;
                forgetTimer = forgetTargetTime;

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
            else if (CurrentTarget != null)
            {
                // Lost sight but still have a last known position
                forgetTimer -= Time.deltaTime;
                if (forgetTimer > 0f && LastKnownTargetPosition.HasValue)
                {
                    // Keep chasing to last known position
                    if (!(currentState is ChaseState))
                        EnterState(chaseState);
                }
                else
                {
                    // Forget the target
                    CurrentTarget = null;
                    LastKnownTargetPosition = null;
                    if (!(currentState is PatrolState))
                        EnterState(patrolState);
                }
            }
            else
            {
                // Only revert to patrol if not investigating
                if (!isInvestigating && !(currentState is PatrolState))
                    EnterState(patrolState);
            }
        }

        public void EnterState(IEnemyState newState)
        {
            if (currentState != null)
                currentState.Exit();

            currentState = newState;

            if (currentState != null)
            {
                Debug.Log($"[HanaduraAI] Entered state: {currentState.GetType().Name}");
                currentState.Enter();
            }
        }

        /// <summary>
        /// Called by Sentient Grimoire to alert this Hanadura
        /// </summary>
        public void AlertToPosition(Vector3 position, Transform target = null)
        {
            if (!base.IsServerInitialized) return;

            //Debug.Log("[HanaduraAI] Received alert from Grimoire.");

            if (target != null)
            {
                // Direct player reference -> chase right away
                CurrentTarget = target;
                LastKnownTargetPosition = target.position;
                forgetTimer = forgetTargetTime;
                isInvestigating = false;

                if (!(currentState is ChaseState))
                    EnterState(chaseState);

                Debug.Log($"[HanaduraAI] Alerted by Grimoire! Direct chase: {target.name}");
            }
            else
            {
                // No direct target, just a suspicious area -> investigate
                CurrentTarget = null;
                LastKnownTargetPosition = position;
                forgetTimer = forgetTargetTime;
                isInvestigating = true;

                if (!(currentState is InvestigateState))
                    EnterState(investigateState);

                Debug.Log($"[HanaduraAI] Alerted by Grimoire! Investigating area: {position}");
            }

            //Debug.Log("[HanaduraAI] Alert processing complete.");
        }


        public void SetCurrentTarget(Transform target)
        {
            CurrentTarget = target;
            if (target != null)
            {
                LastKnownTargetPosition = target.position;
                forgetTimer = forgetTargetTime;
            }
        }
        public void SetIsInvestigatingFlag()
        {
            isInvestigating = !isInvestigating;
            Debug.Log("[HanaduraAI] isInvestigating set to: " + isInvestigating);
        }

        /// <summary>
        /// Called server-side by the sound system when a sound is detected nearby.
        /// </summary>
        public void OnSoundHeard(Vector3 position, SoundType type, float intensity)
        {
            //Debug.Log("Before Server check");

            if (!base.IsServerInitialized) return;

            //Debug.Log("[HanaduraAI] OnSoundHeard called.");

            // If enemy is already chasing or attacking, ignore sound
            if (currentState is ChaseState || currentState is AttackState)
                return;

            // Only react to sufficiently strong sounds
            if (intensity < 0.1f)
                return;

            // Set investigation data
            LastKnownTargetPosition = position;
            CurrentTarget = null;
            isInvestigating = true;

            Debug.Log($"[HanaduraAI] Heard sound '{type.name}' with intensity {intensity:F2}. Investigating at {position}");

            // Enter investigation state if not already investigating
            if (!(currentState is InvestigateState))
                EnterState(investigateState);
        }

        #region Movement & Attack APIs (Server-side)
        public void MoveTo(Vector3 position)
        {
            if (!base.IsServerInitialized) return;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
            //Debug.Log($"[EnemyAI] MoveTo called. Destination: {position}, PathStatus: {navAgent.pathStatus}");
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

            // perform attack: call damage on the target if it has IDamageable
            //IDamageable dmg = CurrentTarget.GetComponent<IDamageable>();
            //if (dmg != null)
            //{
            //    dmg.ApplyDamage(attackDamage);
            //}
            attackTimer = attackCooldown;

            NetworkObject nobTarget = CurrentTarget.GetComponent<NetworkBehaviour>();
            FlashVignette_TargetRPC(nobTarget.LocalConnection);

            // notify clients to play attack animation (ObserversRpc will run on observing clients)
            //Rpc_PlayAttackAnimation();

            return true;
        }

        [TargetRpc]
        private void FlashVignette_TargetRPC(NetworkConnection _)
        {
            StartCoroutine(FlashVignette());
        }

        private bool isFlashingVignette = false;
        private IEnumerator FlashVignette()
        {
            if (!volume.profile.TryGet(out Vignette vignette)) yield break;
            if (isFlashingVignette) yield break;
            isFlashingVignette = true;
            float originalIntensity = vignette.intensity.value;
            IEnumerator FadeToColor(Color targetColor, float intensity, float duration)
            {
                float initialIntensity = vignette.intensity.value;
                Color initialColor = vignette.color.value;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    vignette.intensity.value = Mathf.Lerp(initialIntensity, intensity, elapsed / duration);
                    vignette.color.value = Color.Lerp(initialColor, targetColor, elapsed / duration);
                    yield return null;
                }
                vignette.color.value = targetColor;
            }
            yield return FadeToColor(Color.red, 0.35f, 0.5f);
            yield return FadeToColor(Color.black, originalIntensity, 0.5f);
            isFlashingVignette = false;
        }

        [ObserversRpc]
        private void Rpc_PlayAttackAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
        }

        #endregion
    }
}
