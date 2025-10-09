using UnityEngine;

namespace RooseLabs.Player
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform standPosition;
        [SerializeField] private Transform runPosition;
        [SerializeField] private Transform crouchPosition;
        [SerializeField] private Transform crawlPosition;

        public static CameraController Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            Player player = Player.LocalPlayer;

            Vector3 desiredPosition = player.Data.isRunning ? runPosition.position
                : player.Data.isCrouching ? crouchPosition.position
                : player.Data.isCrawling ? crawlPosition.position
                : standPosition.position;
            if (transform.position != desiredPosition)
            {
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 10f);
            }
            transform.rotation = Quaternion.LookRotation(player.Data.lookDirection);
        }
    }
}
