using UnityEngine;

namespace RooseLabs.Enemies
{
    public class AttackState : IEnemyState
    {
        private HanaduraAI ai;

        public AttackState(HanaduraAI ai)
        {
            this.ai = ai;
        }

        public void Enter()
        {
            ai.StopMovement();
        }

        public void Exit()
        {
        }

        public void Tick()
        {
            if (ai.CurrentTarget == null) return;

            Vector3 dir = (ai.CurrentTarget.position - ai.transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion look = Quaternion.LookRotation(dir);
                ai.transform.rotation = Quaternion.Slerp(ai.transform.rotation, look, Time.deltaTime * 10f);
            }

            ai.TryPerformAttack();
        }
    }
}
