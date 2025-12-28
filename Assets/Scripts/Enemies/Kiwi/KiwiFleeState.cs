using UnityEngine;

namespace RooseLabs.Enemies
{
    public class KiwiFleeState : IEnemyState
    {
        private readonly KiwiAI m_ai;
        private bool m_hasSetInitialDestination = false;
        private Vector3 m_currentDestination = Vector3.zero;

        public KiwiFleeState(KiwiAI ai)
        {
            m_ai = ai;
        }

        public void OnEnter()
        {
            m_ai.NavAgent.isStopped = false;
            m_ai.NavAgent.speed = m_ai.FleeSpeed;
            m_ai.Animator.SetBool("IsPatrolling", false);
            m_ai.Animator.SetBool("IsChasing", false);
            m_ai.Animator.SetBool("IsScreaming", false);
            m_ai.Animator.SetBool("IsFleeing", true);

            m_hasSetInitialDestination = false;
            m_currentDestination = Vector3.zero;

            SelectFurthestRoomWaypoint();
        }

        public void Update()
        {
            if (!m_hasSetInitialDestination)
            {
                m_currentDestination = m_ai.CurrentTargetWaypoint;
                m_ai.NavAgent.SetDestination(m_currentDestination);
                m_hasSetInitialDestination = true;
                return;
            }

            Vector3 targetWaypoint = m_ai.CurrentTargetWaypoint;
            
            if (Vector3.Distance(m_currentDestination, targetWaypoint) > 0.5f)
            {
                m_currentDestination = targetWaypoint;
                m_ai.NavAgent.SetDestination(m_currentDestination);
                return;
            }

            float directDistance = Vector3.Distance(m_ai.transform.position, targetWaypoint);
            float velocity = m_ai.NavAgent.velocity.magnitude;

            if (directDistance <= m_ai.StoppingDistance + 0.5f && velocity < 0.5f)
            {
                m_ai.ChangeState(m_ai.PatrolState);
            }
        }

        public void OnExit()
        {
            m_ai.Animator.SetBool("IsFleeing", false);
            m_ai.SelectNextWaypoint();
            Debug.Log($"[KiwiFleeState] {m_ai.gameObject.name} exiting flee state");
        }

        private void SelectFurthestRoomWaypoint()
        {
            if (m_ai.PatrolZones == null || m_ai.PatrolZones.Count == 0)
            {
                Debug.LogWarning("[KiwiFleeState] No patrol zones available, cannot select flee destination");
                return;
            }

            string furthestRoomId = null;
            float maxDistance = -1f;

            foreach (var roomEntry in m_ai.PatrolZones)
            {
                string roomId = roomEntry.Key;
                RoomPatrolZone zone = roomEntry.Value;

                if (roomId == m_ai.CurrentRoomId)
                    continue;

                if (zone.waypoints.Count == 0)
                    continue;

                float totalDistance = 0f;
                foreach (var waypoint in zone.waypoints)
                {
                    totalDistance += Vector3.Distance(m_ai.transform.position, waypoint);
                }
                float averageDistance = totalDistance / zone.waypoints.Count;

                if (averageDistance > maxDistance)
                {
                    maxDistance = averageDistance;
                    furthestRoomId = roomId;
                }
            }

            if (furthestRoomId == null)
            {
                Debug.LogWarning("[KiwiFleeState] Could not find a suitable room to flee to");
                return;
            }

            RoomPatrolZone furthestZone = m_ai.PatrolZones[furthestRoomId];

            if (furthestZone.waypoints.Count == 0)
            {
                Debug.LogWarning($"[KiwiFleeState] Furthest room '{furthestRoomId}' has no waypoints");
                return;
            }

            Vector3 selectedWaypoint = furthestZone.waypoints[Random.Range(0, furthestZone.waypoints.Count)];
            m_ai.SetTargetWaypoint(selectedWaypoint);
            m_ai.SetSpawnRoom(furthestRoomId);
        }
    }
}
