using UnityEngine;

namespace RooseLabs.Enemies
{
    public class PatrolState : IEnemyState
    {
        private HanaduraAI ai;
        private PatrolRoute route;
        private int currentIndex;
        private bool loop;

        public PatrolState(HanaduraAI ai, PatrolRoute route, bool loop, int startIndex = 0)
        {
            this.ai = ai;
            this.route = route;
            this.loop = loop;
            this.currentIndex = startIndex;
        }

        public void Enter()
        {
            ai.SetAnimatorBool("IsChasing", false);
            ai.SetAnimatorBool("IsLookingAround", false);

            if (route == null || route.Count == 0)
            {
                ai.StopMovement();
                return;
            }

            // Find nearest waypoint index
            float minDist = float.MaxValue;
            int nearestIndex = 0;
            Vector3 enemyPos = ai.transform.position;

            for (int i = 0; i < route.Count; i++)
            {
                Transform wp = route.GetWaypoint(i);
                if (wp == null) continue;

                float dist = Vector3.Distance(enemyPos, wp.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIndex = i;
                }
            }

            currentIndex = nearestIndex;
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

            //if (dist <= ai.navAgent.stoppingDistance + 1.2f)
            if (dist <= ai.navAgent.stoppingDistance)
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