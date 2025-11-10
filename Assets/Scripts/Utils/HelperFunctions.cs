using UnityEngine;

namespace RooseLabs.Utils
{
    public static class HelperFunctions
    {
        public static readonly int AllPhysicalLayerMask = LayerMask.GetMask("Default", "Ground", "PlayerHitbox", "Draggable");

        /// <summary>
        /// Converts look rotation values (pitch, yaw) to Euler angles.
        /// </summary>
        /// <param name="lookRotationValues">The look rotation values (x: yaw, y: pitch).</param>
        /// <returns>The corresponding Euler angles (x: pitch, y: yaw, z: 0).</returns>
        public static Vector3 LookToEuler(Vector2 lookRotationValues)
        {
            // The Y value is negated because moving the mouse up should make the camera look up, but in Unity’s
            // coordinate system, a positive X rotation tilts the camera down. The negation fixes that inversion.
            return new Vector3(-lookRotationValues.y, lookRotationValues.x, 0.0f);
        }

        /// <summary>
        /// Converts a look vector (pitch, yaw) to a direction vector based on a target direction.
        /// Example: LookToDirection(new Vector3(30, 45, 0), Vector3.forward) will return a direction vector
        /// that represents a 30 degree pitch and 45 degree yaw from the forward direction.
        /// This is useful for converting mouse look input into a direction vector for movement or aiming.
        /// </summary>
        /// <param name="look">The look vector (pitch, yaw).</param>
        /// <param name="targetDir">The target direction vector.</param>
        /// <returns>The resulting direction vector.</returns>
        public static Vector3 LookToDirection(Vector3 look, Vector3 targetDir)
        {
            return EulerToDirection(LookToEuler(look), targetDir);
        }

        /// <summary>
        /// Converts Euler angles to a direction vector based on a target direction.
        /// </summary>
        /// <param name="euler">The Euler angles (x: pitch, y: yaw, z: roll).</param>
        /// <param name="targetDir">The target direction vector.</param>
        /// <returns>The resulting direction vector.</returns>
        public static Vector3 EulerToDirection(Vector3 euler, Vector3 targetDir)
        {
            return Quaternion.Euler(euler) * targetDir;
        }
    }
}
