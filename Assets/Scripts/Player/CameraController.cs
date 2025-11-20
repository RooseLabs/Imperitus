using UnityEngine;

namespace RooseLabs.Player
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform standPosition;
        [SerializeField] private Transform sprintPosition;
        [SerializeField] private Transform crouchPosition;
        [SerializeField] private Transform crawlPosition;

        public static CameraController Instance { get; private set; }

        private const float RagdollTransitionTime = 0.25f;
        private float m_ragdollTransitionTimer;
        private Quaternion m_ragdollRotation;

        private void OnEnable()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            if (character.Data.IsRagdollActive)
            {
                // When ragdoll is active, camera should follow head position and orientation
                Transform headTransform = character.GetBodypart(HumanBodyBones.Head);
                Vector3 targetPosition = new Vector3(headTransform.position.x, headTransform.position.y + 0.1f, headTransform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * 10f);
                Quaternion targetRotation = Quaternion.LookRotation(headTransform.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                m_ragdollTransitionTimer = RagdollTransitionTime;
                m_ragdollRotation = transform.rotation;
            }
            else
            {
                Vector3 desiredPosition = character.Data.IsSprinting ? sprintPosition.position
                    : character.Data.IsCrouching ? crouchPosition.position
                    : character.Data.IsCrawling ? crawlPosition.position
                    : standPosition.position;
                if (transform.position != desiredPosition)
                {
                    transform.position = Vector3.MoveTowards(transform.position, desiredPosition, Time.deltaTime * 2.5f);
                }

                if (m_ragdollTransitionTimer > 0.0f)
                {
                    m_ragdollTransitionTimer -= Time.deltaTime;
                    float progress = Mathf.Clamp01(1 - (m_ragdollTransitionTimer / RagdollTransitionTime));
                    transform.rotation = Quaternion.Slerp(m_ragdollRotation, Quaternion.LookRotation(character.Data.lookDirection), progress);
                }
                else
                {
                    transform.rotation = Quaternion.LookRotation(character.Data.lookDirection);
                }
            }
        }

        public void ResetPosition()
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            Vector3 desiredPosition = character.Data.IsSprinting ? sprintPosition.position
                : character.Data.IsCrouching ? crouchPosition.position
                : character.Data.IsCrawling ? crawlPosition.position
                : standPosition.position;

            transform.position = desiredPosition;
        }
    }
}
