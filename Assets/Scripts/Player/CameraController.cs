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
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            Vector3 desiredPosition = character.Data.isRunning ? runPosition.position
                : character.Data.isCrouching ? crouchPosition.position
                : character.Data.isCrawling ? crawlPosition.position
                : standPosition.position;
            if (transform.position != desiredPosition)
            {
                transform.position = Vector3.MoveTowards(transform.position, desiredPosition, Time.deltaTime * 2.5f);
            }
            transform.rotation = Quaternion.LookRotation(character.Data.lookDirection);
        }
    }
}
