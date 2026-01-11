using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Alert state for Grimoire - stops and tracks player with spotlight after initial detection
    /// </summary>
    public class GrimoireAlertState : IEnemyState
    {
        private readonly GrimoireAI m_ai;
        private readonly float m_alertDuration;
        private float m_alertTimer;

        public GrimoireAlertState(GrimoireAI ai, float alertDuration)
        {
            m_ai = ai;
            m_alertDuration = alertDuration;
        }

        public void OnEnter()
        {
            m_ai.navAgent.isStopped = true;
            m_alertTimer = m_alertDuration;

            // Play alert sound (one-shot)
            m_ai.PlayOneShotSound(m_ai.AlertSoundKey);

            // RPC to show visual alert to all clients
            m_ai.RPC_ShowAlert();

            // Debug.Log("[GrimoireAlertState] Player detected! Entering Alert state");
        }

        public void OnExit()
        {

        }

        public void Update()
        {
            m_alertTimer -= Time.deltaTime;

            Transform detectedPlayer = m_ai.DetectedPlayer;

            // After alert duration, transition
            if (m_alertTimer <= 0f)
            {
                if (detectedPlayer)
                {
                    // Still have target, go to tracking
                    m_ai.ChangeState(m_ai.TrackingState);
                }
                else
                {
                    // Lost target, return to patrol
                    m_ai.ChangeState(m_ai.PatrolState);
                }
            }
        }
    }
}
