using UnityEngine;

namespace RooseLabs.Enemies
{
    public class PatrolState : IEnemyState
    {
        private EnemyAI ai;
        private PatrolRoute route;
        private int currentIndex;
        private bool loop;

        public PatrolState(EnemyAI ai, PatrolRoute route, bool loop, int startIndex = 0)
        {
            this.ai = ai;
            this.route = route;
            this.loop = loop;
            this.currentIndex = startIndex;
        }

        public void Enter()
        {
            if (route == null || route.Count == 0)
            {
                ai.StopMovement();
                return;
            }
            MoveToCurrentWaypoint();
        }

        public void Exit()
        {
        }

        public void Tick()
        {
            if (route == null || route.Count == 0) return;

            Transform wp = route.GetWaypoint(currentIndex);
            if (wp == null) return;

            float dist = Vector3.Distance(ai.transform.position, wp.position);
            if (dist <= ai.navAgent.stoppingDistance + 0.2f)
            {
                currentIndex++;
                if (currentIndex >= route.Count)
                {
                    if (loop)
                        currentIndex = 0;
                    else
                    {
                        ai.StopMovement();
                        return;
                    }
                }
                MoveToCurrentWaypoint();
            }
        }

        private void MoveToCurrentWaypoint()
        {
            Transform wp = route.GetWaypoint(currentIndex);
            if (wp != null)
                ai.MoveTo(wp.position);
        }
    }
}
