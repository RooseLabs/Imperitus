using FishNet.Object;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    public class EnemyBehavior : NetworkBehaviour
    {
        public NavMeshAgent agent;

        public LayerMask whatIsGround, whatIsPlayer;
        public float sightFOV = 90f;
        public float verticalFOV = 45f;
        public float attackFOV = 30f;

        // Patrol
        public Transform[] patrolPoints;
        private int currentPatrolIndex = 0;

        // Chasing
        private Vector3 lastKnownPlayerPosition;
        private bool hasLastKnownPosition = false;
        private bool isSearching = false;
        private float searchArrivalThreshold = 1.0f;

        // Attacking
        public float timeBetweenAttacks;
        bool alreadyAttacked;

        // States
        public float sightRange, attackRange;
        public bool playerInSightRange, playerInAttackRange;

        private Transform currentTargetPlayer;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Update()
        {
            if (!IsServerInitialized)
                return;

            currentTargetPlayer = GetClosestVisiblePlayer(sightRange, sightFOV, verticalFOV);
            playerInSightRange = currentTargetPlayer != null;

            bool attackTargetFound = false;
            Transform attackTarget = GetClosestVisiblePlayer(attackRange, attackFOV, verticalFOV);
            playerInAttackRange = attackTarget != null;
            if (playerInAttackRange)
            {
                currentTargetPlayer = attackTarget;
                attackTargetFound = true;
            }

            if (playerInSightRange)
            {
                lastKnownPlayerPosition = currentTargetPlayer.position;
                hasLastKnownPosition = true;
                isSearching = false;
                ChasePlayer();
            }
            else if (hasLastKnownPosition)
            {
                SearchLastKnownPosition();
            }
            else
            {
                Patroling();
            }

            if (playerInAttackRange && attackTargetFound)
            {
                AttackPlayer();
            }
        }

        private void SearchLastKnownPosition()
        {
            if (!isSearching)
            {
                agent.SetDestination(lastKnownPlayerPosition);
                isSearching = true;
            }

            float distanceToLastKnown = Vector3.Distance(transform.position, lastKnownPlayerPosition);

            if (distanceToLastKnown < searchArrivalThreshold)
            {
                hasLastKnownPosition = false;
                isSearching = false;
            }
        }

        private void Patroling()
        {
            if (patrolPoints == null || patrolPoints.Length == 0)
                return;

            agent.SetDestination(patrolPoints[currentPatrolIndex].position);

            Vector3 distanceToPoint = transform.position - patrolPoints[currentPatrolIndex].position;
            if (distanceToPoint.magnitude < 1f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            }
        }

        private void ChasePlayer()
        {
            if (currentTargetPlayer != null)
                agent.SetDestination(currentTargetPlayer.position);
        }

        private void AttackPlayer()
        {
            agent.SetDestination(transform.position);

            if (currentTargetPlayer != null)
                transform.LookAt(currentTargetPlayer);

            if (!alreadyAttacked)
            {
                Debug.Log("Enemy attacked");
                alreadyAttacked = true;
                Invoke(nameof(ResetAttack), timeBetweenAttacks);
            }
        }

        private void ResetAttack()
        {
            alreadyAttacked = false;
        }

        private bool IsPlayerInVisibleFOV(Transform candidate, float range, float horizontalFOV, float verticalFOV)
        {
            Vector3 toPlayer = candidate.position - transform.position;
            float distanceToPlayer = toPlayer.magnitude;

            if (distanceToPlayer > range)
                return false;

            Vector3 forwardXZ = new Vector3(transform.forward.x, 0, transform.forward.z).normalized;
            Vector3 toPlayerXZ = new Vector3(toPlayer.x, 0, toPlayer.z);

            if (toPlayerXZ.sqrMagnitude < 0.0001f)
                return false;

            toPlayerXZ.Normalize();
            float horizontalAngle = Vector3.Angle(forwardXZ, toPlayerXZ);
            if (horizontalAngle > horizontalFOV * 0.5f)
                return false;

            float verticalAngle = Mathf.Abs(Mathf.Atan2(toPlayer.y, new Vector2(toPlayer.x, toPlayer.z).magnitude) * Mathf.Rad2Deg);
            if (verticalAngle > verticalFOV * 0.5f)
                return false;

            if (Physics.Raycast(transform.position, toPlayer.normalized, out RaycastHit hit, distanceToPlayer))
            {
                if (hit.transform == candidate)
                    return true;
            }

            return false;
        }

        private Transform GetClosestVisiblePlayer(float range, float horizontalFOV, float verticalFOV)
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            Transform closestPlayer = null;
            float closestDistance = Mathf.Infinity;

            foreach (GameObject playerObj in players)
            {
                Transform candidate = playerObj.transform;
                if (IsPlayerInVisibleFOV(candidate, range, horizontalFOV, verticalFOV))
                {
                    float dist = Vector3.Distance(transform.position, candidate.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        closestPlayer = candidate;
                    }
                }
            }
            return closestPlayer;
        }
    }
}
