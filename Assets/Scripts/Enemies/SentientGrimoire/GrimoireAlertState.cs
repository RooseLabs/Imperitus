using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Alert state for Grimoire - stops and tracks player with spotlight after initial detection
    /// </summary>
    public class GrimoireAlertState : IEnemyState
    {
        private GrimoireAI ai;
        private float alertDuration;
        private float alertTimer;

        public GrimoireAlertState(GrimoireAI ai, float alertDuration)
        {
            this.ai = ai;
            this.alertDuration = alertDuration;
        }

        public void Enter()
        {
            ai.navAgent.isStopped = true;
            alertTimer = alertDuration;

            // Trigger alert animation/effects
            if (ai.animator != null)
            {
                ai.animator.SetTrigger("Alert");
            }

            // RPC to show visual alert to all clients
            ai.RPC_ShowAlert();

            Debug.Log("[GrimoireAlertState] Player detected! Entering Alert state");
        }

        public void Exit()
        {
            // Nothing special needed on exit
        }

        public void Tick()
        {
            alertTimer -= Time.deltaTime;

            // Rotate spotlight to track player
            Transform detectedPlayer = ai.DetectedPlayer;
            if (detectedPlayer != null)
            {
                ai.RotateSpotlightToTarget(detectedPlayer, 5f);
            }

            // After alert duration, transition
            if (alertTimer <= 0f)
            {
                if (detectedPlayer != null)
                {
                    // Still have target, go to tracking
                    ai.EnterState(ai.trackingState);
                }
                else
                {
                    // Lost target, return to patrol
                    ai.EnterState(ai.patrolState);
                }
            }
        }
    }
}