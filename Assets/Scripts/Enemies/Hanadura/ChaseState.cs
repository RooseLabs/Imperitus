using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Enemies
{
    public class ChaseState : IEnemyState
    {
        private static Logger Logger => Logger.GetLogger("Hanadura");

        private HanaduraAI ai;
        private float updateInterval = 0.2f;
        private float updateTimer = 0f;

        public ChaseState(HanaduraAI ai)
        {
            this.ai = ai;
        }

        public void Enter()
        {
            updateTimer = 0f;
            ai.SetAnimatorBool("IsChasing", true);
            ai.SetAnimatorBool("IsLookingAround", false);
        }

        public void Exit()
        {
            ai.SetAnimatorBool("IsChasing", false);
        }

        public void Tick()
        {
            // Determine target position
            Vector3? targetPos = ai.CurrentTarget != null
                ? ai.CurrentTarget.position
                : ai.LastKnownTargetPosition;

            if (!targetPos.HasValue)
            {
                Logger.Warning("[ChaseState] No target position available!");
                return;
            }

            // Update path periodically
            updateTimer -= Time.deltaTime;
            if (updateTimer <= 0f)
            {
                updateTimer = updateInterval;
                ai.MoveTo(targetPos.Value);
            }

            // Check if reached last known position (when no direct target)
            if (ai.CurrentTarget == null && ai.LastKnownTargetPosition.HasValue)
            {
                float dist = Vector3.Distance(ai.transform.position, ai.LastKnownTargetPosition.Value);
                if (dist <= ai.navAgent.stoppingDistance + 0.5f)
                {
                    // Reached last known position, let the priority system decide next action
                    // (Could transition to investigate or patrol automatically)
                    Logger.Info("[ChaseState] Reached last known position.");
                }
            }
        }
    }
}
