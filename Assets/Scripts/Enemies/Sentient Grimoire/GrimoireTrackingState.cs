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
            Debug.Log("[GrimoireTrackingState] Entered - actively tracking player");
        }

        public void Exit()
        {
            // Nothing special needed on exit
        }

        public void Tick()
        {
            Transform detectedPlayer = ai.DetectedPlayer;

            // Continue tracking player while in sight
            if (detectedPlayer != null)
            {
                ai.RotateSpotlightToTarget(detectedPlayer, 5f);
            }
            else
            {
                // Lost player, return to patrol
                ai.EnterState(ai.patrolState);
            }
        }
    }
}