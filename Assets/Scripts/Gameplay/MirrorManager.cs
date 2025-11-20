using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs
{
    public class MirrorManager : MonoBehaviour
    {
        public Transform MirrorCam;
        private Transform PlayerCam;

        void OnEnable()
        {
            if (PlayerCharacter.LocalCharacter != null)
            {
                AssignPlayerCamera();
            }
            else
            {
                StartCoroutine(WaitForLocalCharacter());
            }
        }

        void OnDisable()
        {
            if (PlayerCharacter.LocalCharacter != null)
            {
                PlayerCharacter.LocalCharacter.onPlayerCameraReady -= AssignPlayerCamera;
            }
        }

        private System.Collections.IEnumerator WaitForLocalCharacter()
        {
            while (PlayerCharacter.LocalCharacter == null)
            {
                yield return null;
            }

            PlayerCharacter.LocalCharacter.onPlayerCameraReady += AssignPlayerCamera;
        }

        private void AssignPlayerCamera()
        {
            if (PlayerCharacter.LocalCharacter != null && PlayerCharacter.LocalCharacter.Camera != null)
            {
                PlayerCam = PlayerCharacter.LocalCharacter.Camera.transform;
            }
        }

        void Update()
        {
            if (PlayerCam == null) return;

            Vector3 PosY = new Vector3(transform.position.x, PlayerCam.position.y, transform.position.z);
            Vector3 side1 = PlayerCam.transform.position - PosY;
            Vector3 side2 = transform.forward;
            float angle = Vector3.SignedAngle(side1, side2, Vector3.up);

            MirrorCam.localEulerAngles = new Vector3(0, angle, 0);
        }
    }
}