using UnityEngine;

namespace RooseLabs.Enemies
{
    public class ChaseState : IEnemyState
    {
        private EnemyAI ai;
        private float updateInterval = 0.2f;
        private float timer = 0f;

        public ChaseState(EnemyAI ai)
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
            if (ai.CurrentTarget == null)
                return;

            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = updateInterval;
                ai.MoveTo(ai.CurrentTarget.position);
            }
        }
    }
}
