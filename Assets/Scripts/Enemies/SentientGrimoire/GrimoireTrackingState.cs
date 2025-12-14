using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Tracking state for Grimoire - continuously tracks player with spotlight
    /// </summary>
    public class GrimoireTrackingState : IEnemyState
    {
        private readonly GrimoireAI m_ai;

        public GrimoireTrackingState(GrimoireAI ai)
        {
            m_ai = ai;
        }

        public void OnEnter()
        {
            m_ai.navAgent.isStopped = false;
            m_ai.navAgent.speed = m_ai.trackingSpeed;

            //Debug.Log("[GrimoireTrackingState] Entered - actively tracking player");
        }

        public void OnExit()
        {
            m_ai.navAgent.speed = m_ai.patrolSpeed;
        }

        public void Update()
        {
            Transform detectedPlayer = m_ai.DetectedPlayer;

            // Continue tracking player while in sight
            if (detectedPlayer)
            {
                m_ai.navAgent.SetDestination(detectedPlayer.position);
            }
            else
            {
                // Lost player, return to patrol
                m_ai.ChangeState(m_ai.PatrolState);
            }
        }
    }
}
