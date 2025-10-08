using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        private Player m_player;
        private CharacterController m_characterController;
        // private Animator m_animator;
        private Vector3 m_velocity;

        [Header("Movement Settings")]
        [SerializeField] private float movementSpeed = 5f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float jumpForce = 1f;
        [SerializeField] private float groundCheckDistance = 0.2f;

        private const float GravityForce = 9.81f;

        private void Start()
        {
            m_player = GetComponent<Player>();
            m_characterController = GetComponent<CharacterController>();
            // m_animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (!m_player.IsOwner) return;
            if (!m_characterController.enabled) return;
            HandleLook();
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

            if (m_player.Input.movementInput.magnitude > 0f)
            {
                Vector3 forward = new Vector3(m_player.Data.lookDirection_Flat.x, 0, m_player.Data.lookDirection_Flat.z).normalized;
                Vector3 right = new Vector3(forward.z, 0, -forward.x).normalized;
                Vector3 moveDirection = right * m_player.Input.movementInput.x + forward * m_player.Input.movementInput.y;
                m_characterController.Move(moveDirection * (currentSpeed * Time.deltaTime));

                // Rotate player towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(m_player.Data.lookDirection_Flat);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }

            if (m_player.Input.crouchWasPressed)
            {
                m_player.Data.isCrouching = !m_player.Data.isCrouching;
                // m_animator.SetBool("IsCrouching", m_player.Data.isCrouching);
            }
            if (m_player.Input.jumpWasPressed && CanJump())
            {
                m_velocity.y = Mathf.Sqrt(jumpForce * 2f * GravityForce);
            }

            m_velocity.y -= GravityForce * Time.deltaTime;
            m_characterController.Move(m_velocity * Time.deltaTime);
        }

        private void HandleLook()
        {
            Vector2 delta = m_player.Input.lookInput * 0.1f;

            m_player.Data.lookValues += delta;
            m_player.Data.lookValues.y = Mathf.Clamp(m_player.Data.lookValues.y, -85f, 85f);

            Vector3 normalized = HelperFunctions.LookToDirection(m_player.Data.lookValues, Vector3.forward).normalized;
            m_player.Data.lookDirection = normalized;
            normalized.y = 0.0f;
            normalized.Normalize();
            m_player.Data.lookDirection_Flat = normalized;

            // If look direction is above a certain threshold, rotate the player
            float angle = Vector3.Angle(transform.forward, m_player.Data.lookDirection_Flat);
            if (angle > 45f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(m_player.Data.lookDirection_Flat);
                float lerpSpeed = Time.deltaTime * 10f * (angle / 90f);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lerpSpeed);
            }
        }

        private bool CanJump() => IsGrounded() && !m_player.Data.isCrouching && !m_player.Data.isCrawling;

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
