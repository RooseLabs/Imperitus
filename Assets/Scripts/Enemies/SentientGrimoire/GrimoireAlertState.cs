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

        public void OnEnter()
        {
            ai.navAgent.isStopped = true;
            alertTimer = alertDuration;

            // RPC to show visual alert to all clients
            ai.RPC_ShowAlert();

            // Debug.Log("[GrimoireAlertState] Player detected! Entering Alert state");
        }

        public void OnExit()
        {

        }

        public void Update()
        {
            alertTimer -= Time.deltaTime;

            // Rotate spotlight to track player
            Transform detectedPlayer = ai.DetectedPlayer;
            if (detectedPlayer)
            {
                ai.RotateSpotlightToTarget(detectedPlayer, 5f);
            }

            // After alert duration, transition
            if (alertTimer <= 0f)
            {
                if (detectedPlayer)
                {
                    // Still have target, go to tracking
                    ai.ChangeState(ai.TrackingState);
                }
                else
                {
                    // Lost target, return to patrol
                    ai.ChangeState(ai.PatrolState);
                }
            }
        }
    }
}
