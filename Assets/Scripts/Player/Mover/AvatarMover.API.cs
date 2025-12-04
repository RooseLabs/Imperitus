using UnityEngine;

namespace RooseLabs.Player
{
    public partial class AvatarMover : MonoBehaviour
    {
        /// <summary>
        /// Set the intended movement velocity for one physics frame.
        /// </summary>
        /// <param name="velocity">The desired velocity.</param>
        public void Move(Vector3 velocity)
        {
            _velocityInput = velocity;
        }

        /// <summary>
        /// Pause ground-related functionalities.
        /// </summary>
        public void LeaveGround()
        {
            if (_isTouchingCeiling) return;
            _shouldLeaveGround = true;
        }

        /// <summary>
        /// Resume ground-related functionalities.
        /// </summary>
        public void EndLeaveGround()
        {
            _shouldLeaveGround = false;
            _isOnGroundChangedThisFrame = true;
            _velocityGravity = Vector3.zero;
        }

        /// <summary>
        /// Set the collider height.
        /// </summary>
        /// <param name="height"></param>
        public void SetColliderHeight(float height)
        {
            if (Mathf.Approximately(_height, height)) return;
            _height = height;
            InitColliderDimensions();
        }
    }
}
