using UnityEngine;

namespace RooseLabs.Utils
{
    /// <summary>
    /// Utility class providing helper methods for camera-related projections and calculations in Unity,
    /// such as converting viewport or screen coordinates to world points on a plane at a specified depth.
    /// </summary>
    public static class CameraUtils
    {
        /// <summary>
        /// Converts a viewport coordinate to a world point on a plane parallel to the camera's viewport at the specified depth.
        /// </summary>
        /// <param name="camera">The camera to use for the transformation.</param>
        /// <param name="zDepth">The depth from the camera along its forward axis where the plane is located.</param>
        /// <param name="viewportCoord">The viewport coordinate (ranging from (0,0) to (1,1)) to convert.</param>
        /// <returns>The world position on the plane corresponding to the viewport coordinate.</returns>
        public static Vector3 ViewportToWorldPointOnPlane(Camera camera, float zDepth, Vector2 viewportCoord)
        {
            Vector2 angles = ViewportToViewAngles(camera, viewportCoord);
            float xOffset = Mathf.Tan(angles.x) * zDepth;
            float yOffset = Mathf.Tan(angles.y) * zDepth;
            Vector3 localPosition = new Vector3(xOffset, yOffset, zDepth);
            return camera.transform.TransformPoint(localPosition);
        }

        /// <summary>
        /// Converts a screen coordinate to a world point on a plane parallel to the camera's viewport at the specified depth.
        /// </summary>
        /// <param name="camera">The camera to use for the transformation.</param>
        /// <param name="zDepth">The depth from the camera along its forward axis where the plane is located.</param>
        /// <param name="screenCoord">The screen coordinate to convert.</param>
        /// <returns>The world position on the plane corresponding to the screen coordinate.</returns>
        public static Vector3 ScreenToWorldPointOnPlane(Camera camera, float zDepth, Vector3 screenCoord)
        {
            Vector3 viewportPoint = camera.ScreenToViewportPoint(screenCoord);
            return ViewportToWorldPointOnPlane(camera, zDepth, viewportPoint);
        }

        /// <summary>
        /// Calculates the view angles (in radians) from the camera to the given viewport coordinate.
        /// The X component is the horizontal angle, and Y is the vertical angle.
        /// </summary>
        /// <param name="camera">The camera to use for the calculation.</param>
        /// <param name="viewportCoord">The viewport coordinate (ranging from (0,0) to (1,1)) for which to compute the angles.</param>
        /// <returns>A Vector2 containing the horizontal (X) and vertical (Y) angles in radians.</returns>
        public static Vector2 ViewportToViewAngles(Camera camera, Vector2 viewportCoord)
        {
            float horizontalFov = GetProportionalAngle(camera.fieldOfView / 2f, camera.aspect) * 2f;
            float xProportion = (viewportCoord.x - 0.5f) / 0.5f;
            float yProportion = (viewportCoord.y - 0.5f) / 0.5f;
            float xAngle = GetProportionalAngle(horizontalFov / 2f, xProportion) * Mathf.Deg2Rad;
            float yAngle = GetProportionalAngle(camera.fieldOfView / 2f, yProportion) * Mathf.Deg2Rad;
            return new Vector2(xAngle, yAngle);
        }

        /// <summary>
        /// Calculates the depth (distance along the camera's forward axis) from the camera to a given world point.
        /// </summary>
        /// <param name="camera">The camera from which to measure the depth.</param>
        /// <param name="point">The world point to measure the depth to.</param>
        /// <returns>The depth value (positive if in front of the camera).</returns>
        public static float GetDepthToPoint(Camera camera, Vector3 point)
        {
            Vector3 localPosition = camera.transform.InverseTransformPoint(point);
            return localPosition.z;
        }

        /// <summary>
        /// Determines if a given renderer is visible from the specified camera's perspective.
        /// </summary>
        /// <param name="camera">The camera to check visibility from.</param>
        /// <param name="renderer">The renderer to check for visibility.</param>
        /// <returns>True if the renderer is visible from the camera; otherwise, false.</returns>
        public static bool VisibleFromCamera(Camera camera, Renderer renderer)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, renderer.bounds);
        }

        /// <summary>
        /// Computes a proportional angle based on the given base angle and proportion.
        /// This is useful for calculating field-of-view adjustments or sub-angles.
        /// </summary>
        /// <param name="angle">The base angle in degrees.</param>
        /// <param name="proportion">The proportion to apply (e.g., -1 to 1 for full range).</param>
        /// <returns>The proportional angle in degrees.</returns>
        private static float GetProportionalAngle(float angle, float proportion)
        {
            float opposite = Mathf.Tan(angle * Mathf.Deg2Rad);
            float oppositeProportion = opposite * proportion;
            return Mathf.Atan(oppositeProportion) * Mathf.Rad2Deg;
        }
    }
}
