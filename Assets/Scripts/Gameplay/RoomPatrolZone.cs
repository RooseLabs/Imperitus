using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Enemies
{
    public class RoomPatrolZone
    {
        private static Logger Logger => Logger.GetLogger("RoomPatrolZone");

        public string roomIdentifier;
        public List<Vector3> waypoints = new();
        public Bounds roomBounds;

        // Track active routes to avoid assigning same route to multiple enemies
        private readonly List<PatrolRouteAssignment> m_activeAssignments = new();

        // Different route strategies for the same room
        public enum RouteStrategy
        {
            Perimeter,      // Follow waypoints near room edges
            Interior,       // Focus on central waypoints
            Zigzag,         // Alternate between sides
            Random          // Random subset of waypoints
        }

        private class PatrolRouteAssignment
        {
            public GameObject enemy;
            public PatrolRoute route;
            public RouteStrategy strategy;
        }

        /// <summary>
        /// Generate a unique patrol route for an enemy, avoiding collision with existing routes
        /// </summary>
        public PatrolRoute GenerateUniqueRoute(GameObject enemy, RouteStrategy? preferredStrategy = null)
        {
            // Clean up null references
            m_activeAssignments.RemoveAll(a => a.enemy == null);

            // Determine strategy based on how many enemies already patrol here
            RouteStrategy strategy = preferredStrategy ?? DetermineStrategy();

            // Generate waypoint sequence based on strategy
            List<Vector3> routeWaypoints = GenerateRouteWaypoints(strategy);

            // Create patrol route
            PatrolRoute route = CreatePatrolRoute(routeWaypoints);

            // Track assignment
            m_activeAssignments.Add(new PatrolRouteAssignment
            {
                enemy = enemy,
                route = route,
                strategy = strategy
            });

            Logger.Info($"Generated {strategy} route for room '{roomIdentifier}' with {routeWaypoints.Count} waypoints");

            return route;
        }

        /// <summary>
        /// Remove an enemy's route assignment when they die or leave permanently
        /// </summary>
        public void ReleaseRoute(GameObject enemy)
        {
            m_activeAssignments.RemoveAll(a => a.enemy == enemy);
        }

        /// <summary>
        /// Get number of enemies currently patrolling this zone
        /// </summary>
        public int GetActiveEnemyCount()
        {
            m_activeAssignments.RemoveAll(a => a.enemy == null);
            return m_activeAssignments.Count;
        }

        /// <summary>
        /// Determine route strategy based on existing assignments
        /// </summary>
        private RouteStrategy DetermineStrategy()
        {
            int count = GetActiveEnemyCount();

            // I assumed we won't have more than 3 enemies per room often
            // So the logic caters to first 2 enemies specifically
            if (count == 0)
            {
                // First enemy: perimeter patrol
                return RouteStrategy.Perimeter;
            }
            else if (count == 1)
            {
                // Second enemy: interior patrol
                return RouteStrategy.Interior;
            }
            else
            {
                // Additional enemies: random to avoid patterns
                return RouteStrategy.Random;
            }
        }

        /// <summary>
        /// Generate waypoint sequence based on strategy
        /// </summary>
        private List<Vector3> GenerateRouteWaypoints(RouteStrategy strategy)
        {
            if (waypoints.Count == 0)
            {
                Logger.Warning($"No waypoints in room '{roomIdentifier}'");
                return new List<Vector3>();
            }

            switch (strategy)
            {
                case RouteStrategy.Perimeter:
                    return GeneratePerimeterRoute();

                case RouteStrategy.Interior:
                    return GenerateInteriorRoute();

                case RouteStrategy.Zigzag:
                    return GenerateZigzagRoute();

                case RouteStrategy.Random:
                default:
                    return GenerateRandomRoute();
            }
        }

        /// <summary>
        /// Generate route following room perimeter
        /// </summary>
        private List<Vector3> GeneratePerimeterRoute()
        {
            // Sort waypoints by distance to room bounds edges
            var perimeterPoints = waypoints
                .Select(wp => new
                {
                    position = wp,
                    edgeDistance = GetDistanceToNearestEdge(wp)
                })
                .OrderBy(p => p.edgeDistance)
                .Take(Mathf.Max(4, waypoints.Count / 2)) // Use top 50% closest to edges
                .Select(p => p.position)
                .ToList();

            // Order by angle around room center for smooth perimeter patrol
            Vector3 center = roomBounds.center;
            perimeterPoints = perimeterPoints
                .OrderBy(wp => Mathf.Atan2(wp.z - center.z, wp.x - center.x))
                .ToList();

            return perimeterPoints;
        }

        /// <summary>
        /// Generate route focusing on interior waypoints
        /// </summary>
        private List<Vector3> GenerateInteriorRoute()
        {
            Vector3 center = roomBounds.center;

            // Get waypoints closer to room center
            var interiorPoints = waypoints
                .Select(wp => new
                {
                    position = wp,
                    centerDistance = Vector3.Distance(wp, center)
                })
                .OrderBy(p => p.centerDistance)
                .Take(Mathf.Max(4, waypoints.Count / 2))
                .Select(p => p.position)
                .ToList();

            // Use nearest-neighbor for smooth interior patrol
            return OptimizeRouteOrder(interiorPoints);
        }

        /// <summary>
        /// Generate zigzag pattern (alternates between room sides)
        /// </summary>
        private List<Vector3> GenerateZigzagRoute()
        {
            Vector3 center = roomBounds.center;

            // Split waypoints by which side of room center they're on (X-axis)
            var leftSide = waypoints.Where(wp => wp.x < center.x).OrderBy(wp => wp.z).ToList();
            var rightSide = waypoints.Where(wp => wp.x >= center.x).OrderBy(wp => wp.z).ToList();

            List<Vector3> zigzagPoints = new List<Vector3>();

            // Alternate between sides
            int maxCount = Mathf.Max(leftSide.Count, rightSide.Count);
            for (int i = 0; i < maxCount; i++)
            {
                if (i < leftSide.Count) zigzagPoints.Add(leftSide[i]);
                if (i < rightSide.Count) zigzagPoints.Add(rightSide[i]);
            }

            return zigzagPoints.Take(Mathf.Max(6, waypoints.Count / 2)).ToList();
        }

        /// <summary>
        /// Generate random subset of waypoints
        /// </summary>
        private List<Vector3> GenerateRandomRoute()
        {
            int waypointCount = Mathf.Max(4, waypoints.Count / 2);

            var randomPoints = waypoints
                .OrderBy(x => Random.value)
                .Take(waypointCount)
                .ToList();

            // Optimize order to avoid excessive backtracking
            return OptimizeRouteOrder(randomPoints);
        }

        /// <summary>
        /// Get distance from waypoint to nearest room edge
        /// </summary>
        private float GetDistanceToNearestEdge(Vector3 point)
        {
            float distToMinX = Mathf.Abs(point.x - roomBounds.min.x);
            float distToMaxX = Mathf.Abs(point.x - roomBounds.max.x);
            float distToMinZ = Mathf.Abs(point.z - roomBounds.min.z);
            float distToMaxZ = Mathf.Abs(point.z - roomBounds.max.z);

            return Mathf.Min(distToMinX, distToMaxX, distToMinZ, distToMaxZ);
        }

        /// <summary>
        /// Optimize waypoint order using nearest-neighbor (for smooth paths)
        /// </summary>
        private List<Vector3> OptimizeRouteOrder(List<Vector3> points)
        {
            if (points.Count <= 2) return points;

            List<Vector3> optimized = new List<Vector3>();
            List<Vector3> remaining = new List<Vector3>(points);

            // Start with first point
            Vector3 current = remaining[0];
            optimized.Add(current);
            remaining.RemoveAt(0);

            // Build path using nearest neighbor
            while (remaining.Count > 0)
            {
                int nearestIndex = 0;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < remaining.Count; i++)
                {
                    float dist = Vector3.Distance(current, remaining[i]);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIndex = i;
                    }
                }

                current = remaining[nearestIndex];
                optimized.Add(current);
                remaining.RemoveAt(nearestIndex);
            }

            return optimized;
        }

        /// <summary>
        /// Create actual PatrolRoute component from waypoint list
        /// </summary>
        private PatrolRoute CreatePatrolRoute(List<Vector3> routeWaypoints)
        {
            // Create a container GameObject for this route
            GameObject routeContainer = new GameObject($"PatrolRoute_{roomIdentifier}_{GetActiveEnemyCount()}");

            PatrolRoute route = routeContainer.AddComponent<PatrolRoute>();
            route.waypoints = new List<Transform>();

            // Create waypoint GameObjects
            for (int i = 0; i < routeWaypoints.Count; i++)
            {
                GameObject wpObj = new GameObject($"Waypoint_{i}");
                wpObj.transform.position = routeWaypoints[i];
                wpObj.transform.SetParent(routeContainer.transform);

                route.waypoints.Add(wpObj.transform);
            }

            return route;
        }
    }
}
