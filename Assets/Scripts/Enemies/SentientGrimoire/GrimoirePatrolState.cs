using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Patrol state for Grimoire - moves between waypoints with spotlight in default position
    /// </summary>
    public class GrimoirePatrolState : IEnemyState
    {
        private GrimoireAI ai;
        private PatrolRoute route;
        private int currentWaypointIndex;
        private bool loop;
        private float waypointReachThreshold;

        public GrimoirePatrolState(GrimoireAI ai, PatrolRoute route, bool loop, int startIndex = 0, float reachThreshold = 1.5f)
        {
            this.ai = ai;
            this.route = route;
            this.loop = loop;
            this.currentWaypointIndex = startIndex;
            this.waypointReachThreshold = reachThreshold;
        }

        public void Enter()
        {
            if (route == null || route.Count == 0)
            {
                ai.navAgent.isStopped = true;
                return;
            }

            // Find nearest waypoint
            FindNearestWaypoint();
            MoveToWaypoint(currentWaypointIndex);

            //Debug.Log("[GrimoirePatrolState] Entered - starting patrol");
        }

        public void Exit()
        {

        }

        public void Tick()
        {
            if (route == null || route.Count == 0) return;

            // Check if reached current waypoint
            if (!ai.navAgent.pathPending && ai.navAgent.remainingDistance <= waypointReachThreshold)
            {
                // Move to next waypoint
                currentWaypointIndex++;
                if (currentWaypointIndex >= route.Count)
                {
                    if (loop)
                    {
                        currentWaypointIndex = 0;
                    }
                    else
                    {
                        ai.navAgent.isStopped = true;
                        return;
                    }
                }

                MoveToWaypoint(currentWaypointIndex);
            }

            // Rotate spotlight back to default smoothly
            ai.RotateSpotlightToDefault(2f);
        }

        private void MoveToWaypoint(int index)
        {
            Transform waypoint = route.GetWaypoint(index);
            if (waypoint != null)
            {
                ai.navAgent.isStopped = false;
                ai.navAgent.SetDestination(waypoint.position);
                //Debug.Log($"[GrimoirePatrolState] Moving to waypoint {index}");
            }
        }

        private void FindNearestWaypoint()
        {
            if (route == null || route.Count == 0) return;

            float minDist = float.MaxValue;
            int nearestIndex = 0;

            for (int i = 0; i < route.Count; i++)
            {
                Transform wp = route.GetWaypoint(i);
                if (wp == null) continue;

                float dist = Vector3.Distance(ai.transform.position, wp.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIndex = i;
                }
            }

            currentWaypointIndex = nearestIndex;
        }

        public int CurrentWaypointIndex => currentWaypointIndex;
    }
}