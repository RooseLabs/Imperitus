using UnityEngine;

namespace RooseLabs.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        private Player m_player;
        private CharacterController m_characterController;
        private Vector3 m_velocity;
        private float m_verticalRotation = 0f;

        [Header("Movement Settings")]
        [SerializeField] private float movementSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpForce = 1f;
        [SerializeField] private float groundCheckDistance = 0.2f;

        [Header("Look Settings")]
        [SerializeField] private float lookSensitivity = 1f;
        [SerializeField] private float maxLookAngle = 80f;

        private const float GravityForce = 9.81f;

        private void Awake()
        {
            m_player = GetComponent<Player>();
            m_characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (!m_player.IsOwner)
                return;
            HandleRotation();
            HandleMovement();
        }

        private void HandleMovement()
        {
            bool isGrounded = IsGrounded();
            if (isGrounded && m_velocity.y < 0)
            {
                m_velocity.y = -2f;
            }

            float currentSpeed = movementSpeed;
            if (m_player.Input.sprintIsPressed)
            {
                currentSpeed *= sprintMultiplier;
            }

            Vector3 moveDirection = transform.right * m_player.Input.movementInput.x + transform.forward * m_player.Input.movementInput.y;
            m_characterController.Move(moveDirection * (currentSpeed * Time.deltaTime));

            if (m_player.Input.jumpWasPressed && isGrounded)
            {
                m_velocity.y = Mathf.Sqrt(jumpForce * 2f * GravityForce);
            }

            m_velocity.y -= GravityForce * Time.deltaTime;
            m_characterController.Move(m_velocity * Time.deltaTime);
        }

        private void HandleRotation()
        {
            Vector2 delta = m_player.Input.lookInput * lookSensitivity;

            m_verticalRotation -= delta.y;
            m_verticalRotation = Mathf.Clamp(m_verticalRotation, -maxLookAngle, maxLookAngle);
            m_player.Camera.transform.localRotation = Quaternion.Euler(m_verticalRotation, 0f, 0f);

            transform.Rotate(Vector3.up * delta.x);
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position + Vector3.up * 0.03f, Vector3.down, groundCheckDistance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.03f, Vector3.down * groundCheckDistance);
        }
#endif
    }
}
