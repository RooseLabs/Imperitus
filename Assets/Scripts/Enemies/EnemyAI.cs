using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NetworkObject))]
    public class EnemyAI : NetworkBehaviour
    {
        [Header("References")]
        public NavMeshAgent navAgent;
        public EnemyDetection detection;
        public PatrolRoute patrolRoute;
        public Animator animator;

        [Header("Combat")]
        public float attackRange = 2f;
        public float attackCooldown = 1.2f;
        public int attackDamage = 10;

        [Header("Patrol")]
        public int startWaypointIndex = 0;
        public bool loopPatrol = true;  

        // FSM states
        private IEnemyState currentState;
        private PatrolState patrolState;
        private ChaseState chaseState;
        private AttackState attackState;

        // Server-controlled target reference (only used server-side)
        public Transform CurrentTarget { get; private set; }

        // for attack cooldown
        private float attackTimer = 0f;

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

            EnterState(patrolState);
        }

        private void Update()
        {
            // Only run AI on server (server authoritative)
            if (!base.IsServerInitialized) // FishNet: IsServerInitialized indicates running as server
                return;

            // Tick state
            currentState?.Tick();

            // Common per-frame
            attackTimer -= Time.deltaTime;

            // detection -> transitions
            Transform detected = detection.DetectedTarget;
            if (detected != null)
            {
                // set current target
                CurrentTarget = detected;

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
                CurrentTarget = null;
                // if no target -> return to patrol
                if (!(currentState is PatrolState))
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
                Debug.Log($"[EnemyAI] Entered state: {currentState.GetType().Name}");
                currentState.Enter();
            }
        }

        #region Movement & Attack APIs (Server-side)
        public void MoveTo(Vector3 position)
        {
            if (!base.IsServerInitialized) return;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
            Debug.Log($"[EnemyAI] MoveTo called. Destination: {position}, PathStatus: {navAgent.pathStatus}");
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

            // notify clients to play attack animation (ObserversRpc will run on observing clients)
            Rpc_PlayAttackAnimation();

            return true;
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
