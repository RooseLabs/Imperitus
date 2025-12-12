using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using RooseLabs.Utils;
using UnityEngine;
using UnityEngine.AI;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Generates patrol waypoints dynamically at runtime by scanning the map geometry.
    /// </summary>
    public class PatrolPointGenerator : NetworkBehaviour
    {
        [Header("Map Configuration")]
        [Tooltip("Tag of the GameObject(s) that contain the map. Will search within these bounds.")]
        public string mapContainerTag = "Map";

        [Tooltip("Tag for individual room GameObjects (different from map container tag)")]
        public string roomTag = "Room";

        [Tooltip("Layer mask for ground detection")]
        public LayerMask groundLayer;

        [Tooltip("Layer mask for obstacles to avoid")]
        public LayerMask obstacleLayer;

        [Header("Generation Settings")]
        [Tooltip("Horizontal spacing between raycast samples (smaller = more points, slower)")]
        [Range(1f, 10f)]
        public float gridSpacing = 3f;

        [Tooltip("Minimum clearance radius around each point to avoid obstacles")]
        [Range(0.5f, 5f)]
        public float obstacleClearanceRadius = 1.5f;

        [Tooltip("Height above map bounds to start raycasting from")]
        public float raycastStartHeight = 50f;

        [Tooltip("Maximum raycast distance downward")]
        public float maxRaycastDistance = 100f;

        [Tooltip("Minimum height clearance above patrol points (checks for low ceilings)")]
        [Range(1f, 5f)]
        public float minHeightClearance = 2.5f;

        [Tooltip("NavMesh sample distance for validation")]
        public float navMeshSampleDistance = 2f;

        [Tooltip("Parent object for waypoint GameObjects (leave null to use this transform)")]
        public Transform waypointParent;

        [Header("Patrol Route Setup")]
        [Tooltip("Automatically create PatrolRoute component and assign waypoints")]
        public bool autoCreatePatrolRoute = true;

        [Header("Room-Based Patrol Zones")]
        [Tooltip("Generate separate patrol zones per room instead of single global route")]
        public bool useRoomBasedPatrolling = true;

        [Header("Debug Visualization")]
        [Tooltip("Show generated waypoints as gizmos in Scene view")]
        public bool showDebugGizmos = true;

        [Tooltip("Color for valid waypoint gizmos")]
        public Color validPointColor = Color.green;

        [Tooltip("Color for rejected point gizmos (shown during generation)")]
        public Color rejectedPointColor = Color.red;

        [Tooltip("Size of waypoint gizmo spheres")]
        public float gizmoSize = 0.3f;

        [Tooltip("Draw lines connecting waypoints in order")]
        public bool showPatrolPath = true;

        [Header("Exclusion Zones")]
        [Tooltip("Tag for GameObjects that define areas where NO waypoints should spawn")]
        public string exclusionZoneTag = "PatrolExclusionZone";

        [Tooltip("Show exclusion zones as red wireframe boxes in Scene view")]
        public bool showExclusionZones = true;

        // Generated waypoints
        private List<Vector3> generatedPoints = new List<Vector3>();
        private List<Vector3> rejectedPoints = new List<Vector3>();

        // Reference to created patrol route
        private PatrolRoute patrolRoute;

        private Dictionary<string, RoomPatrolZone> roomPatrolZones = new Dictionary<string, RoomPatrolZone>();

        /// <summary>
        /// Generate patrol points across the map.
        /// </summary>
        public PatrolRoute GeneratePatrolPoints()
        {
            if (!IsServerInitialized)
            {
                this.LogWarning("GeneratePatrolPoints() should only be called on server!");
                return null;
            }

            this.LogInfo("Starting patrol point generation...");

            // Clear previous data
            generatedPoints.Clear();
            rejectedPoints.Clear();

            // Find map bounds
            Bounds mapBounds = CalculateMapBounds();
            if (mapBounds.size == Vector3.zero)
            {
                this.LogError("No map container found with tag: " + mapContainerTag);
                return null;
            }

            this.LogInfo($"Map bounds: {mapBounds.size}, Center: {mapBounds.center}");

            // Generate grid of sample points
            int pointsGenerated = 0;
            int pointsRejected = 0;

            for (float x = mapBounds.min.x; x <= mapBounds.max.x; x += gridSpacing)
            {
                for (float z = mapBounds.min.z; z <= mapBounds.max.z; z += gridSpacing)
                {
                    Vector3 samplePoint = new Vector3(x, mapBounds.max.y + raycastStartHeight, z);

                    if (TryGenerateWaypoint(samplePoint, out Vector3 validPoint))
                    {
                        generatedPoints.Add(validPoint);
                        pointsGenerated++;
                    }
                    else
                    {
                        pointsRejected++;
                    }
                }
            }

            this.LogInfo($"Generation complete! Valid points: {pointsGenerated}, Rejected: {pointsRejected}");

            // Optimize waypoint order for efficient patrol routes
            if (generatedPoints.Count > 1)
            {
                OptimizeWaypointOrder();
            }

            // Create patrol route
            if (autoCreatePatrolRoute)
            {
                patrolRoute = CreatePatrolRoute();
            }

            return patrolRoute;
        }

        /// <summary>
        /// Try to generate a valid waypoint at the given position
        /// </summary>
        private bool TryGenerateWaypoint(Vector3 startPosition, out Vector3 validPoint)
        {
            validPoint = Vector3.zero;

            // Raycast down to find ground
            if (!Physics.Raycast(startPosition, Vector3.down, out RaycastHit hit, maxRaycastDistance, groundLayer))
            {
                return false;
            }

            Vector3 groundPoint = hit.point;

            // Is this point inside an exclusion zone?
            if (IsPointInExclusionZone(groundPoint))
            {
                rejectedPoints.Add(groundPoint);
                return false;
            }

            // Check obstacle clearance (sphere check around the point)
            if (Physics.CheckSphere(groundPoint + Vector3.up * obstacleClearanceRadius, obstacleClearanceRadius, obstacleLayer))
            {
                rejectedPoints.Add(groundPoint); // For debug visualization
                return false;
            }

            // Check height clearance (raycast up to detect low ceilings)
            if (Physics.Raycast(groundPoint, Vector3.up, minHeightClearance, obstacleLayer))
            {
                rejectedPoints.Add(groundPoint);
                return false;
            }

            // Validate point is on NavMesh (skip in Edit Mode since NavMesh may not be loaded)
            if (Application.isPlaying)
            {
                if (NavMesh.SamplePosition(groundPoint, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
                {
                    validPoint = navHit.position;
                    return true;
                }

                rejectedPoints.Add(groundPoint);
                return false;
            }
            else
            {
                // In Edit Mode, skip NavMesh validation (just use ground point)
                // This is for preview purposes only
                validPoint = groundPoint;
                return true;
            }
        }

        /// <summary>
        /// Calculate combined bounds of all GameObjects with the map tag
        /// </summary>
        private Bounds CalculateMapBounds()
        {
            GameObject[] mapObjects = GameObject.FindGameObjectsWithTag(mapContainerTag);

            if (mapObjects.Length == 0)
                return new Bounds();

            Bounds combinedBounds = new Bounds(mapObjects[0].transform.position, Vector3.zero);

            foreach (GameObject obj in mapObjects)
            {
                // Include all renderers in children
                Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
                foreach (Renderer r in renderers)
                {
                    combinedBounds.Encapsulate(r.bounds);
                }

                // Include all colliders in children
                Collider[] colliders = obj.GetComponentsInChildren<Collider>();
                foreach (Collider c in colliders)
                {
                    combinedBounds.Encapsulate(c.bounds);
                }
            }

            return combinedBounds;
        }

        /// <summary>
        /// Optimize waypoint order using nearest-neighbor algorithm for smoother patrol routes
        /// </summary>
        private void OptimizeWaypointOrder()
        {
            if (generatedPoints.Count <= 2)
                return;

            List<Vector3> optimized = new List<Vector3>();
            List<Vector3> remaining = new List<Vector3>(generatedPoints);

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

            generatedPoints = optimized;
            if (Application.isPlaying)
                this.LogInfo("Waypoint order optimized for smoother patrol paths");
            else
                Debug.Log("[PatrolPointGenerator] Waypoint order optimized for smoother patrol paths");
        }

        /// <summary>
        /// Create PatrolRoute component and spawn waypoint GameObjects
        /// </summary>
        private PatrolRoute CreatePatrolRoute()
        {
            // Get or create patrol route component
            PatrolRoute route = GetComponent<PatrolRoute>();
            if (route == null)
            {
                route = gameObject.AddComponent<PatrolRoute>();
            }

            // Clear existing waypoints
            if (route.waypoints != null)
            {
                foreach (Transform wp in route.waypoints)
                {
                    if (wp != null && wp.gameObject != null)
                        Destroy(wp.gameObject);
                }
                route.waypoints.Clear();
            }
            else
            {
                route.waypoints = new List<Transform>();
            }

            // Create waypoint GameObjects
            Transform parent = waypointParent != null ? waypointParent : transform;

            for (int i = 0; i < generatedPoints.Count; i++)
            {
                GameObject waypointObj = new GameObject($"Waypoint_{i}");
                waypointObj.transform.position = generatedPoints[i];
                waypointObj.transform.SetParent(parent);

                route.waypoints.Add(waypointObj.transform);
            }

            this.LogInfo($"Created PatrolRoute with {route.waypoints.Count} waypoints");
            return route;
        }

        /// <summary>
        /// Get the generated patrol route (null if not yet generated)
        /// </summary>
        public PatrolRoute GetPatrolRoute()
        {
            return patrolRoute;
        }

        /// <summary>
        /// Clear all generated waypoints and route
        /// </summary>
        public void ClearPatrolPoints()
        {
            if (patrolRoute != null && patrolRoute.waypoints != null)
            {
                foreach (Transform wp in patrolRoute.waypoints)
                {
                    if (wp != null && wp.gameObject != null)
                        Destroy(wp.gameObject);
                }
                patrolRoute.waypoints.Clear();
            }

            generatedPoints.Clear();
            rejectedPoints.Clear();

            this.LogInfo("Cleared all patrol points");
        }

        /// <summary>
        /// Check if a point is inside any exclusion zone
        /// </summary>
        private bool IsPointInExclusionZone(Vector3 point)
        {
            GameObject[] exclusionZones = GameObject.FindGameObjectsWithTag(exclusionZoneTag);

            foreach (GameObject zone in exclusionZones)
            {
                // Check box colliders
                BoxCollider boxCollider = zone.GetComponent<BoxCollider>();
                if (boxCollider != null)
                {
                    if (IsPointInBoxCollider(point, boxCollider))
                    {
                        return true;
                    }
                }

                // Check sphere colliders
                SphereCollider sphereCollider = zone.GetComponent<SphereCollider>();
                if (sphereCollider != null)
                {
                    if (IsPointInSphereCollider(point, sphereCollider))
                    {
                        return true;
                    }
                }

                // Check all child colliders too
                Collider[] childColliders = zone.GetComponentsInChildren<Collider>();
                foreach (Collider col in childColliders)
                {
                    if (col is BoxCollider box && IsPointInBoxCollider(point, box))
                        return true;
                    if (col is SphereCollider sphere && IsPointInSphereCollider(point, sphere))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check if a point is inside a box collider
        /// </summary>
        private bool IsPointInBoxCollider(Vector3 point, BoxCollider box)
        {
            // Transform point to local space of the collider
            Vector3 localPoint = box.transform.InverseTransformPoint(point);

            // Check if point is within bounds
            Vector3 halfSize = box.size * 0.5f;
            Vector3 center = box.center;

            return Mathf.Abs(localPoint.x - center.x) <= halfSize.x &&
                   Mathf.Abs(localPoint.y - center.y) <= halfSize.y &&
                   Mathf.Abs(localPoint.z - center.z) <= halfSize.z;
        }

        /// <summary>
        /// Check if a point is inside a sphere collider
        /// </summary>
        private bool IsPointInSphereCollider(Vector3 point, SphereCollider sphere)
        {
            Vector3 worldCenter = sphere.transform.TransformPoint(sphere.center);
            float worldRadius = sphere.radius * Mathf.Max(
                sphere.transform.lossyScale.x,
                sphere.transform.lossyScale.y,
                sphere.transform.lossyScale.z
            );

            return Vector3.Distance(point, worldCenter) <= worldRadius;
        }

        /// <summary>
        /// Generate patrol zones grouped by room
        /// </summary>
        public Dictionary<string, RoomPatrolZone> GenerateRoomPatrolZones()
        {
            if (!IsServerInitialized)
            {
                this.LogWarning("GenerateRoomPatrolZones() should only be called on server!");
                return null;
            }

            this.LogInfo("Starting room-based patrol zone generation...");

            // Clear previous data
            generatedPoints.Clear();
            rejectedPoints.Clear();
            roomPatrolZones.Clear();

            // Find map bounds
            Bounds mapBounds = CalculateMapBounds();
            if (mapBounds.size == Vector3.zero)
            {
                this.LogError("No map container found with tag: " + mapContainerTag);
                return null;
            }

            this.LogInfo($"Map bounds: {mapBounds.size}, Center: {mapBounds.center}");

            // Generate grid of sample points (same as before)
            int pointsGenerated = 0;
            int pointsRejected = 0;

            for (float x = mapBounds.min.x; x <= mapBounds.max.x; x += gridSpacing)
            {
                for (float z = mapBounds.min.z; z <= mapBounds.max.z; z += gridSpacing)
                {
                    Vector3 samplePoint = new Vector3(x, mapBounds.max.y + raycastStartHeight, z);

                    if (TryGenerateWaypoint(samplePoint, out Vector3 validPoint))
                    {
                        generatedPoints.Add(validPoint);
                        pointsGenerated++;
                    }
                    else
                    {
                        pointsRejected++;
                    }
                }
            }

            this.LogInfo($"Waypoint generation complete! Valid: {pointsGenerated}, Rejected: {pointsRejected}");

            // Group waypoints by room
            GroupWaypointsByRoom();

            this.LogInfo($"Created {roomPatrolZones.Count} patrol zones");

            return roomPatrolZones;
        }

        /// <summary>
        /// Group generated waypoints into room-based patrol zones
        /// </summary>
        private void GroupWaypointsByRoom()
        {
            // Find all GameObjects tagged as "Room" (not map containers)
            GameObject[] rooms = GameObject.FindGameObjectsWithTag(roomTag);

            if (rooms.Length == 0)
            {
                this.LogWarning($"No rooms found with tag '{roomTag}', creating single patrol zone");
                CreateSinglePatrolZone();
                return;
            }

            this.LogInfo($"Found {rooms.Length} rooms with tag '{roomTag}'");

            // Create a patrol zone for each room
            foreach (GameObject room in rooms)
            {
                RoomPatrolZone zone = new RoomPatrolZone
                {
                    roomIdentifier = room.name,
                    roomBounds = RoomCalculations.CalculateRoomBounds(room)
                };

                roomPatrolZones[room.name] = zone;
                this.LogInfo($"Created zone for room: {room.name}");
            }

            // Assign each waypoint to nearest room
            foreach (Vector3 waypoint in generatedPoints)
            {
                string closestRoom = FindClosestRoom(waypoint, rooms);

                if (roomPatrolZones.ContainsKey(closestRoom))
                {
                    roomPatrolZones[closestRoom].waypoints.Add(waypoint);
                }
            }

            // Log results
            foreach (var zone in roomPatrolZones.Values)
            {
                this.LogInfo($"Room '{zone.roomIdentifier}': {zone.waypoints.Count} waypoints");
            }

            // Remove empty zones
            var emptyZones = roomPatrolZones.Where(kvp => kvp.Value.waypoints.Count == 0).Select(kvp => kvp.Key).ToList();
            foreach (var emptyZone in emptyZones)
            {
                roomPatrolZones.Remove(emptyZone);
                this.LogWarning($"Removed empty zone: {emptyZone}");
            }
        }

        /// <summary>
        /// Find which room a waypoint belongs to
        /// </summary>
        private string FindClosestRoom(Vector3 waypoint, GameObject[] rooms)
        {
            string closestRoom = "Unknown";
            float closestDistance = float.MaxValue;

            foreach (GameObject room in rooms)
            {
                Bounds roomBounds = RoomCalculations.CalculateRoomBounds(room);

                // Check if point is inside room bounds
                if (roomBounds.Contains(waypoint))
                {
                    return room.name;
                }

                // Otherwise, use distance to room center
                float distance = Vector3.Distance(waypoint, roomBounds.center);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoom = room.name;
                }
            }

            return closestRoom;
        }

        /// <summary>
        /// Fallback: create single patrol zone if no rooms found
        /// </summary>
        private void CreateSinglePatrolZone()
        {
            RoomPatrolZone zone = new RoomPatrolZone
            {
                roomIdentifier = "Global",
                waypoints = new List<Vector3>(generatedPoints),
                roomBounds = CalculateMapBounds()
            };

            roomPatrolZones["Global"] = zone;
        }

        /// <summary>
        /// Get patrol zone for a specific room
        /// </summary>
        public RoomPatrolZone GetPatrolZone(string roomIdentifier)
        {
            if (roomPatrolZones.TryGetValue(roomIdentifier, out RoomPatrolZone zone))
            {
                return zone;
            }

            this.LogWarning($"No patrol zone found for room: {roomIdentifier}");
            return null;
        }

        /// <summary>
        /// Get all patrol zones
        /// </summary>
        public Dictionary<string, RoomPatrolZone> GetAllPatrolZones()
        {
            return roomPatrolZones;
        }

        /// <summary>
        /// Find closest patrol zone to a position (useful for enemy respawning)
        /// </summary>
        public RoomPatrolZone GetClosestPatrolZone(Vector3 position)
        {
            RoomPatrolZone closest = null;
            float closestDist = float.MaxValue;

            foreach (var zone in roomPatrolZones.Values)
            {
                float dist = Vector3.Distance(position, zone.roomBounds.center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = zone;
                }
            }

            return closest;
        }

        #region Debug Visualization

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos)
                return;

            // Draw valid waypoints
            Gizmos.color = validPointColor;
            foreach (Vector3 point in generatedPoints)
            {
                Gizmos.DrawSphere(point, gizmoSize);

                // Draw small upward line to show orientation
                Gizmos.DrawLine(point, point + Vector3.up * (gizmoSize * 2));
            }

            // Draw patrol path connections
            if (showPatrolPath && generatedPoints.Count > 1)
            {
                Gizmos.color = validPointColor * 0.6f;
                for (int i = 0; i < generatedPoints.Count - 1; i++)
                {
                    Gizmos.DrawLine(generatedPoints[i], generatedPoints[i + 1]);
                }

                // Draw line back to start for looping patrols
                Gizmos.DrawLine(generatedPoints[generatedPoints.Count - 1], generatedPoints[0]);
            }

            // Draw room zones with different colors
            if (roomPatrolZones != null && roomPatrolZones.Count > 0)
            {
                int colorIndex = 0;
                Color[] zoneColors = new Color[]
                {
                    Color.cyan, Color.magenta, Color.yellow,
                    new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f),
                    new Color(0f, 1f, 0.5f)
                };

                foreach (var zone in roomPatrolZones.Values)
                {
                    Color zoneColor = zoneColors[colorIndex % zoneColors.Length];
                    Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);

                    // Draw room bounds
                    Gizmos.DrawWireCube(zone.roomBounds.center, zone.roomBounds.size);

                    // Draw waypoints in this zone
                    Gizmos.color = zoneColor;
                    foreach (Vector3 wp in zone.waypoints)
                    {
                        Gizmos.DrawWireSphere(wp, gizmoSize * 0.7f);
                    }

                    colorIndex++;
                }
            }

            // Draw rejected points (only visible during/after generation)
            if (rejectedPoints.Count > 0)
            {
                Gizmos.color = rejectedPointColor;
                foreach (Vector3 point in rejectedPoints)
                {
                    Gizmos.DrawWireSphere(point, gizmoSize * 0.5f);
                }
            }

            // Draw exclusion zones
            if (showExclusionZones)
            {
                GameObject[] exclusionZones = GameObject.FindGameObjectsWithTag(exclusionZoneTag);

                Gizmos.color = new Color(1f, 0f, 0f, 0.3f); // Semi-transparent red

                foreach (GameObject zone in exclusionZones)
                {
                    // Draw box colliders
                    BoxCollider boxCollider = zone.GetComponent<BoxCollider>();
                    if (boxCollider != null)
                    {
                        Matrix4x4 oldMatrix = Gizmos.matrix;
                        Gizmos.matrix = zone.transform.localToWorldMatrix;
                        Gizmos.DrawCube(boxCollider.center, boxCollider.size);
                        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
                        Gizmos.matrix = oldMatrix;
                    }

                    // Draw sphere colliders
                    SphereCollider sphereCollider = zone.GetComponent<SphereCollider>();
                    if (sphereCollider != null)
                    {
                        Vector3 worldCenter = zone.transform.TransformPoint(sphereCollider.center);
                        float worldRadius = sphereCollider.radius * Mathf.Max(
                            zone.transform.lossyScale.x,
                            zone.transform.lossyScale.y,
                            zone.transform.lossyScale.z
                        );
                        Gizmos.DrawSphere(worldCenter, worldRadius);
                        Gizmos.DrawWireSphere(worldCenter, worldRadius);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw obstacle clearance radius for selected generator
            if (!showDebugGizmos || generatedPoints.Count == 0)
                return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            foreach (Vector3 point in generatedPoints)
            {
                Gizmos.DrawWireSphere(point + Vector3.up * obstacleClearanceRadius, obstacleClearanceRadius);
            }
        }

        #endregion

        #region Editor-Time Generation
        /// <summary>
        /// Generate patrol points in Edit Mode for preview/testing
        /// Called by custom editor
        /// </summary>
        public bool EditorGeneratePatrolPoints()
        {
            generatedPoints.Clear();
            rejectedPoints.Clear();

            Bounds mapBounds = CalculateMapBounds();
            if (mapBounds.size == Vector3.zero)
            {
                this.LogError("No map container found with tag: " + mapContainerTag);
                return false;
            }

            int pointsGenerated = 0;
            int pointsRejected = 0;

            for (float x = mapBounds.min.x; x <= mapBounds.max.x; x += gridSpacing)
            {
                for (float z = mapBounds.min.z; z <= mapBounds.max.z; z += gridSpacing)
                {
                    Vector3 samplePoint = new Vector3(x, mapBounds.max.y + raycastStartHeight, z);

                    if (TryGenerateWaypoint(samplePoint, out Vector3 validPoint))
                    {
                        generatedPoints.Add(validPoint);
                        pointsGenerated++;
                    }
                    else
                    {
                        pointsRejected++;
                    }
                }
            }

            Debug.Log($"[PatrolPointGenerator] Generation complete! Valid points: {pointsGenerated}, Rejected: {pointsRejected}");

            // Optimize waypoint order
            if (generatedPoints.Count > 1)
            {
                OptimizeWaypointOrder();
            }

            return pointsGenerated > 0;
        }

        /// <summary>
        /// Clear patrol points in Edit Mode
        /// </summary>
        public void EditorClearPatrolPoints()
        {
            generatedPoints.Clear();
            rejectedPoints.Clear();
        }

        /// <summary>
        /// Get number of valid points for editor display
        /// </summary>
        public int GetEditorPointCount()
        {
            return generatedPoints.Count;
        }

        /// <summary>
        /// Get number of rejected points for editor display
        /// </summary>
        public int GetEditorRejectedCount()
        {
            return rejectedPoints.Count;
        }

        /// <summary>
        /// Get bounds of all generated points for scene view framing
        /// </summary>
        public Bounds GetEditorPointsBounds()
        {
            if (generatedPoints.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = new Bounds(generatedPoints[0], Vector3.zero);
            foreach (Vector3 point in generatedPoints)
            {
                bounds.Encapsulate(point);
            }

            // Add some padding
            bounds.Expand(10f);
            return bounds;
        }

        #endregion
    }
}
