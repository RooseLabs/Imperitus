using UnityEngine;
using PlayerController = RooseLabs.Player.Player;

namespace RooseLabs.UI
{
    public class NameTag : MonoBehaviour
    {
        private Transform localCamera;

        void LateUpdate()
        {
            if (localCamera == null && PlayerController.LocalPlayer != null && PlayerController.LocalPlayer.Camera != null)
            {
                localCamera = PlayerController.LocalPlayer.Camera.transform;
            }

            if (localCamera != null)
            {
                transform.forward = localCamera.forward;
            }
        }
    }
}
