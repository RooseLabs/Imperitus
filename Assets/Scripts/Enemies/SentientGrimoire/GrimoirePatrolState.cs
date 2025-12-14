using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Patrol state for Grimoire - moves between waypoints with spotlight in default position
    /// </summary>
    public class GrimoirePatrolState : IEnemyState
    {
        private readonly GrimoireAI m_ai;
        private readonly PatrolRoute m_route;
        private int m_currentWaypointIndex;
        private readonly bool m_loop;
        private readonly float m_waypointReachThreshold;

        public GrimoirePatrolState(GrimoireAI ai, PatrolRoute route, bool loop, int startIndex = 0, float reachThreshold = 1.5f)
        {
            m_ai = ai;
            m_route = route;
            m_loop = loop;
            m_currentWaypointIndex = startIndex;
            m_waypointReachThreshold = reachThreshold;
        }

        public void OnEnter()
        {
            if (m_route == null || m_route.Count == 0)
            {
                m_ai.navAgent.isStopped = true;
                return;
            }

            // Find nearest waypoint
            FindNearestWaypoint();
            MoveToWaypoint(m_currentWaypointIndex);

            // Debug.Log("[GrimoirePatrolState] Entered - starting patrol");
        }

        public void OnExit()
        {

        }

        public void Update()
        {
            if (m_ai.DetectedPlayer)
            {
                m_ai.ChangeState(m_ai.AlertState);
                return;
            }

            if (m_route == null || m_route.Count == 0) return;

            // Check if reached current waypoint
            if (!m_ai.navAgent.pathPending && m_ai.navAgent.remainingDistance <= m_waypointReachThreshold)
            {
                // Move to next waypoint
                m_currentWaypointIndex++;
                if (m_currentWaypointIndex >= m_route.Count)
                {
                    if (m_loop)
                    {
                        m_currentWaypointIndex = 0;
                    }
                    else
                    {
                        m_ai.navAgent.isStopped = true;
                        return;
                    }
                }

                MoveToWaypoint(m_currentWaypointIndex);
            }
        }

        private void MoveToWaypoint(int index)
        {
            Transform waypoint = m_route.GetWaypoint(index);
            if (waypoint != null)
            {
                m_ai.navAgent.isStopped = false;
                m_ai.navAgent.SetDestination(waypoint.position);
                //Debug.Log($"[GrimoirePatrolState] Moving to waypoint {index}");
            }
        }

        private void FindNearestWaypoint()
        {
            if (m_route == null || m_route.Count == 0) return;

            float minDist = float.MaxValue;
            int nearestIndex = 0;

            for (int i = 0; i < m_route.Count; i++)
            {
                Transform wp = m_route.GetWaypoint(i);
                if (wp == null) continue;

                float dist = Vector3.Distance(m_ai.transform.position, wp.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestIndex = i;
                }
            }

            m_currentWaypointIndex = nearestIndex;
        }

        public int CurrentWaypointIndex => m_currentWaypointIndex;
    }
}
