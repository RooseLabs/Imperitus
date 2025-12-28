using UnityEngine;

namespace RooseLabs.Enemies
{
    public class KiwiScreamState : IEnemyState
    {
        private readonly KiwiAI m_ai;
        private float m_screamEndTime;
        private float m_nextAlertTime;

        public KiwiScreamState(KiwiAI ai)
        {
            m_ai = ai;
        }

        public void OnEnter()
        {
            m_ai.NavAgent.isStopped = true;
            m_ai.NavAgent.velocity = Vector3.zero;
            m_ai.NavAgent.ResetPath();

            m_ai.SetScreamingState(true);

            m_ai.Animator.SetBool("IsPatrolling", false);
            m_ai.Animator.SetBool("IsChasing", false);
            m_ai.Animator.SetBool("IsScreaming", true);
            m_ai.Animator.SetBool("IsFleeing", false);

            m_screamEndTime = Time.time + m_ai.GetScreamDuration();
            m_nextAlertTime = Time.time;

            m_ai.soundEmitter.RequestEmitFromClient("Kiwi_Scream");
        }

        public void Update()
        {
            if (Time.time >= m_screamEndTime)
            {
                EndScream();
                return;
            }

            if (Time.time >= m_nextAlertTime)
            {
                m_ai.AlertNearbyHanaduras(m_ai.GetLastDetectedPlayerPosition());
                m_nextAlertTime = Time.time + (1f / m_ai.AlertFrequency);
            }
        }

        public void OnExit()
        {
            m_ai.NavAgent.isStopped = false;
            m_ai.SetScreamingState(false);

            m_ai.StopScreamSound();

            m_ai.Animator.SetBool("IsScreaming", false);
        }

        private void EndScream()
        {
            m_ai.StartAlertCooldown();
            m_ai.ChangeState(m_ai.FleeState);
        }
    }
}
