using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Tracking state for Grimoire - continuously tracks player with spotlight
    /// </summary>
    public class GrimoireTrackingState : IEnemyState
    {
        private GrimoireAI ai;

        public GrimoireTrackingState(GrimoireAI ai)
        {
            this.ai = ai;
        }

        public void Enter()
        {
            ai.navAgent.isStopped = false;
            ai.navAgent.speed = ai.trackingSpeed;

            //Debug.Log("[GrimoireTrackingState] Entered - actively tracking player");
        }

        public void Exit()
        {
            ai.navAgent.speed = ai.patrolSpeed;
        }

        public void Tick()
        {
            Transform detectedPlayer = ai.DetectedPlayer;

            // Continue tracking player while in sight
            if (detectedPlayer != null)
            {
                ai.RotateSpotlightToTarget(detectedPlayer, 5f);
                ai.navAgent.SetDestination(detectedPlayer.position);
            }
            else
            {
                // Lost player, return to patrol
                ai.EnterState(ai.patrolState);
            }
        }
    }
}