using UnityEngine;

namespace RooseLabs.Enemies
{
    public class KiwiChaseState : IEnemyState
    {
        private readonly KiwiAI m_ai;

        public KiwiChaseState(KiwiAI ai)
        {
            m_ai = ai;
        }

        public void OnEnter()
        {
            m_ai.NavAgent.isStopped = false;
            m_ai.NavAgent.speed = m_ai.ChaseSpeed;
            m_ai.Animator.SetBool("IsPatrolling", false);
            m_ai.Animator.SetBool("IsChasing", true);
            m_ai.Animator.SetBool("IsScreaming", false);
            m_ai.Animator.SetBool("IsFleeing", false);
        }

        public void Update()
        {
            Transform targetPlayer = m_ai.GetTargetPlayer();

            if (targetPlayer == null || m_ai.EnemyDetection.DetectedTarget == null || m_ai.EnemyDetection.DetectedTarget != targetPlayer)
            {
                m_ai.ChangeState(m_ai.PatrolState);
                return;
            }

            m_ai.NavAgent.SetDestination(targetPlayer.position);

            float distanceToPlayer = Vector3.Distance(m_ai.transform.position, targetPlayer.position);

            if (distanceToPlayer <= m_ai.DesiredDistanceFromPlayer)
            {
                m_ai.SetLastDetectedPlayerPosition(targetPlayer.position);
                m_ai.ChangeState(m_ai.ScreamState);
            }
        }

        public void OnExit()
        {
            m_ai.Animator.SetBool("IsChasing", false);
        }
    }
}
