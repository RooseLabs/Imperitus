using UnityEngine;

namespace RooseLabs.Utils
{
    /// <summary>
    /// Utility class for explosion mechanics.
    /// Provides functions to detect colliders affected by an explosion with obstruction checking.
    /// </summary>
    public static class ExplosionUtils
    {
        private const int MaxIterations = 10; // Max iterations for projection convergence
        private const float Epsilon = 1E-6f;
        private const int RaycastCount = 12;
        private const float AngularSpacing = 2 * Mathf.PI / RaycastCount; // Precomputed angular spacing in radians

        /// <summary>
        /// Tests if a collider is effectively hit by an explosion with LOS obstruction checking.
        /// It is recommended to first use Physics.OverlapSphere to get candidate colliders within the outer radius.
        /// </summary>
        /// <param name="center">The center position of the explosion</param>
        /// <param name="targetCollider">The collider to test</param>
        /// <param name="innerRadius">Inner radius from which rays are cast</param>
        /// <param name="outerRadius">Outer radius that defines the maximum explosion range</param>
        /// <param name="layerMask">Layer mask for physics checks</param>
        /// <param name="queryTriggerInteraction">Whether to include trigger colliders in the checks</param>
        /// <returns>True if the collider is effectively hit, false otherwise</returns>
        public static bool IsColliderHitByExplosion(
            Vector3 center,
            Collider targetCollider,
            float innerRadius,
            float outerRadius,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide)
        {
            Vector3 toTarget = (targetCollider.transform.position - center).normalized;

            // Calculate perpendicular basis for sphere distribution
            Vector3 perpendicular1 = Vector3.Cross(toTarget, Vector3.up).normalized;
            if (perpendicular1.sqrMagnitude < 0.01f)
                perpendicular1 = Vector3.Cross(toTarget, Vector3.right).normalized;
            Vector3 perpendicular2 = Vector3.Cross(toTarget, perpendicular1).normalized;

            // Cast from center
            if (ExplosionRaycast(center, targetCollider, center, outerRadius, layerMask))
                return true;

            // Cast from surface points on inner sphere
            for (int i = 0; i < RaycastCount; ++i)
            {
                float angle = i * AngularSpacing;

                // Position on inner sphere surface
                Vector3 rayOrigin = center + (perpendicular1 * Mathf.Cos(angle) + perpendicular2 * Mathf.Sin(angle)) * innerRadius;

                if (ExplosionRaycast(rayOrigin, targetCollider, center, outerRadius, layerMask, queryTriggerInteraction))
                    return true;
            }

            return false;
        }

        private static bool ExplosionRaycast(
            Vector3 rayOrigin,
            Collider targetCollider,
            Vector3 center,
            float outerRadius,
            int layerMask,
            QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Collide)
        {
            // Get target point using intersection projection
            Vector3 targetPoint = ClosestPointInSphereIntersection(
                rayOrigin,
                center,
                outerRadius,
                targetCollider
            );

            float rawDist = Vector3.Distance(rayOrigin, targetPoint);

            // If origin is inside collider, treat as hit
            if (rawDist <= Epsilon)
                return true;

            // Skip if target point is outside explosion radius
            if (Vector3.Distance(center, targetPoint) > outerRadius + 0.01f)
                return false;

            float rayDistance = rawDist + 0.01f;
            Vector3 rayDirection = (targetPoint - rayOrigin).normalized;

            if (Physics.Raycast(rayOrigin, rayDirection, out var rayHitInfo, rayDistance, layerMask, queryTriggerInteraction))
            {
                if (rayHitInfo.collider == targetCollider)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the closest point in the intersection of a sphere and a collider volume.
        /// </summary>
        private static Vector3 ClosestPointInSphereIntersection(
            Vector3 origin,
            Vector3 sphereCenter,
            float sphereRadius,
            Collider targetCollider)
        {
            Vector3 point = origin;
            Vector3 targetPos = targetCollider.transform.position;
            Quaternion targetRot = targetCollider.transform.rotation;

            for (int i = 0; i < MaxIterations; ++i)
            {
                // Project onto collider volume (works for supported types)
                point = Physics.ClosestPoint(point, targetCollider, targetPos, targetRot);

                // Project onto sphere volume (analytical, zero alloc)
                point = ProjectToSphereVolume(point, sphereCenter, sphereRadius);

                // Early exit if converged
                if ((point - origin).magnitude < Epsilon) break;
            }

            return point;
        }

        /// <summary>
        /// Projects a point onto the volume of a sphere.
        /// </summary>
        private static Vector3 ProjectToSphereVolume(Vector3 point, Vector3 center, float radius)
        {
            Vector3 vec = point - center;
            float dist = vec.magnitude;
            if (dist <= radius || dist < Epsilon)
            {
                return point; // Already inside or degenerate
            }
            return center + vec * (radius / dist);
        }
    }
}
