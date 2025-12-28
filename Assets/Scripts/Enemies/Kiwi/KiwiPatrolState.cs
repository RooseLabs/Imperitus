using UnityEngine;

namespace RooseLabs.Enemies
{
    public class KiwiPatrolState : IEnemyState
    {
        private readonly KiwiAI m_ai;
        private bool m_hasSetInitialDestination = false;
        private Vector3 m_currentDestination = Vector3.zero;

        public KiwiPatrolState(KiwiAI ai)
        {
            m_ai = ai;
        }

        public void OnEnter()
        {
            m_ai.NavAgent.isStopped = false;
            m_ai.NavAgent.speed = m_ai.PatrolSpeed;
            m_ai.Animator.SetBool("IsPatrolling", true);
            m_ai.Animator.SetBool("IsChasing", false);
            m_ai.Animator.SetBool("IsScreaming", false);
            m_ai.Animator.SetBool("IsFleeing", false);

            m_hasSetInitialDestination = false;
            m_currentDestination = Vector3.zero;
        }

        public void Update()
        {
            if (!m_hasSetInitialDestination)
            {
                m_currentDestination = m_ai.CurrentTargetWaypoint;
                m_ai.NavAgent.SetDestination(m_currentDestination);
                m_hasSetInitialDestination = true;
                return;
            }

            if (Vector3.Distance(m_currentDestination, m_ai.CurrentTargetWaypoint) > 0.5f)
            {
                m_currentDestination = m_ai.CurrentTargetWaypoint;
                m_ai.NavAgent.SetDestination(m_currentDestination);
                return;
            }

            if (m_ai.NavAgent.hasPath && !m_ai.NavAgent.pathPending)
            {
                float remainingDistance = m_ai.NavAgent.remainingDistance;
                float velocity = m_ai.NavAgent.velocity.magnitude;

                bool hasReachedDestination = remainingDistance <= m_ai.StoppingDistance + 0.2f;
                bool hasStopped = velocity < 1f;

                if (hasReachedDestination && hasStopped)
                {
                    Debug.Log($"[KiwiPatrolState] {m_ai.gameObject.name} reached waypoint in room '{m_ai.CurrentRoomId}'");
                    m_ai.SelectNextWaypoint();
                    m_hasSetInitialDestination = false;
                }
            }
        }

        public void OnExit()
        {
            m_ai.Animator.SetBool("IsPatrolling", false);
            Debug.Log($"[KiwiPatrolState] {m_ai.gameObject.name} exiting patrol state");
        }
    }
}
