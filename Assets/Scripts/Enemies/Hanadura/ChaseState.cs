using UnityEngine;

namespace RooseLabs.Enemies
{
    public class ChaseState : IEnemyState
    {
        private HanaduraAI ai;
        private float updateInterval = 0.2f;
        private float timer = 0f;

        public ChaseState(HanaduraAI ai)
        {
            this.ai = ai;
        }

        public void Enter()
        {
            timer = 0f;
        }

        public void Exit()
        {
        }

        public void Tick()
        {
            Vector3? targetPos = ai.CurrentTarget != null
                ? ai.CurrentTarget.position
                : ai.LastKnownTargetPosition;

            if (targetPos == null) return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = updateInterval;
                ai.MoveTo(targetPos.Value);
            }

            // Optionally, check if we've reached last known position
            if (ai.CurrentTarget == null && ai.LastKnownTargetPosition.HasValue)
            {
                float dist = Vector3.Distance(ai.transform.position, ai.LastKnownTargetPosition.Value);
                if (dist <= ai.navAgent.stoppingDistance + 0.1f)
                {
                    // Reached last known position, wait or revert to patrol
                    ai.LastKnownTargetPosition = null;
                    if (!(ai.CurrentState is PatrolState))
                        ai.EnterState(ai.PatrolState);
                }
            }
        }

    }
}
