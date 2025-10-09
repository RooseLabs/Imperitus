using RooseLabs.Core;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovementCC : MonoBehaviour
    {
        private Player m_player;
        private CharacterController m_cc;
        private Animator m_animator;
        private InputHandler m_inputHandler;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed   = 1.50f; // Average speed from animation: 1.20f;
        [SerializeField] private float runSpeed    = 5.00f; // Average speed from animation: 5.83f;
        [SerializeField] private float crouchSpeed = 0.67f; // Average speed from animation: 0.67f;
        [SerializeField] private float crawlSpeed  = 0.25f; // Average speed from animation: 0.25f;
        [SerializeField] private float jumpHeight  = 0.50f;
        [SerializeField] private float groundCheckDistance = 0.1f;

        private float m_lastVerticalInputSignal = 1f;
        private float m_verticalVelocity = 0f;

        private const float GravityForce = 9.81f;

        public float CurrentStateSpeed
        {
            get
            {
                if (m_player.Data.isCrawling) return crawlSpeed;
                if (m_player.Data.isCrouching) return crouchSpeed;
                if (m_player.Data.isRunning) return runSpeed;
                return walkSpeed;
            }
        }

        private void Start()
        {
            m_player = GetComponent<Player>();
            m_cc = GetComponent<CharacterController>();
            m_animator = GetComponent<Animator>();
            m_inputHandler = InputHandler.Instance;
        }

        private void Update()
        {
            if (!m_player.IsOwner) return;
            if (!m_cc.enabled) return;

            HandleLook();
            HandleMovement();
        }

        private void HandleMovement()
        {
            Vector2 moveInput = m_player.Input.movementInput;

            // Note: The deadzone and m_lastVerticalInputSignal related code here is an attempt to reduce a "flipping"
            // effect that is most often noticeable when using a controller where the player quickly switches from
            // forward to backward movement. This is by no means a perfect solution, but it does help somewhat.
            float verticalInput = Mathf.Abs(moveInput.y) < 0.2f ? 0.0f : moveInput.y;
            bool isOnDeadzone = moveInput.y != 0.0f && verticalInput == 0.0f;

            bool hasHorizontal = !Mathf.Approximately(moveInput.x, 0.0f);
            bool hasVertical = !Mathf.Approximately(verticalInput, 0.0f);

            float movementValue; // -1 to 1, where negative is backwards, positive is forwards, and 0 is idle
            if (hasVertical)
            {
                // Moving forwards or backwards (with or without sideways movement), use magnitude of input vector for movement value
                m_lastVerticalInputSignal = Mathf.Sign(verticalInput);
                movementValue = moveInput.magnitude * m_lastVerticalInputSignal;
            }
            else if (hasHorizontal)
            {
                // Moving purely sideways, use absolute x value for movement value
                movementValue = Mathf.Abs(moveInput.x);
                if (isOnDeadzone) movementValue *= m_lastVerticalInputSignal;
            }
            else
            {
                movementValue = 0.0f;
            }

            if (moveInput == Vector2.zero && Mathf.Abs(m_animator.GetFloat(PlayerAnimations.F_Movement)) <= 0.01f)
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, 0.0f);
            }
            else
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, movementValue, 0.1f, Time.deltaTime);
            }

            if (m_player.Input.crouchWasPressed)
            {
                m_player.Data.isCrouching = !m_player.Data.isCrouching;
                m_animator.SetBool(PlayerAnimations.B_IsCrouching, m_player.Data.isCrouching);
            }

            bool canRun = movementValue > 0.0f && !m_player.Data.isCrouching && !m_player.Data.isCrawling;
            if (!canRun)
            {
                m_player.Data.isRunning = false;
            }
            else if (m_inputHandler.IsCurrentDeviceKBM())
            {
                m_player.Data.isRunning = m_player.Input.sprintIsPressed;
            }
            else if (m_player.Input.sprintWasPressed)
            {
                m_player.Data.isRunning = !m_player.Data.isRunning;
            }
            m_animator.SetBool(PlayerAnimations.B_IsRunning, m_player.Data.isRunning);

            // Handle player rotation - this ensures the player faces the movement direction
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(m_player.Data.lookDirection_Flat);
                if (hasHorizontal)
                {
                    // Moving sideways as well, rotate in relation to look direction based on x input
                    float maxTurnAngle = isOnDeadzone
                        ? moveInput.x * m_lastVerticalInputSignal * 90f
                        : !hasVertical
                            ? moveInput.x > 0.0f ? 90f : -90f // Purely sideways
                            : moveInput.x * (verticalInput >= 0.0f ? 90f : -90f);
                    float angleOffset = Mathf.Lerp(0f, maxTurnAngle, Mathf.Abs(moveInput.x));
                    targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y + angleOffset, 0f);
                }
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            }

            if (IsGrounded() && m_verticalVelocity < 0f)
            {
                m_verticalVelocity = -2f;
            }
            if (m_player.Input.jumpWasPressed && CanJump())
            {
                m_verticalVelocity = Mathf.Sqrt(2f * jumpHeight * GravityForce);
            }
            m_verticalVelocity -= GravityForce * Time.deltaTime;

            Vector3 deltaMovement = transform.rotation * new Vector3(0.0f, 0.0f, movementValue) * (CurrentStateSpeed * Time.deltaTime);
            deltaMovement.y = m_verticalVelocity * Time.deltaTime;
            m_cc.Move(deltaMovement);
        }

        private void HandleLook()
        {
            float lookSensitivity = m_inputHandler.IsCurrentDeviceKBM() ? 0.1f : 0.4f;
            Vector2 delta = m_player.Input.lookInput * lookSensitivity;

            m_player.Data.lookValues += delta;
            m_player.Data.lookValues.y = Mathf.Clamp(m_player.Data.lookValues.y, -85f, 85f);

            Vector3 normalized = HelperFunctions.LookToDirection(m_player.Data.lookValues, Vector3.forward).normalized;
            m_player.Data.lookDirection = normalized;
            normalized.y = 0.0f;
            normalized.Normalize();
            m_player.Data.lookDirection_Flat = normalized;

            // Rotate the player if look direction is above a certain threshold (this should only happen when stationary)
            if (m_player.Input.movementInput.sqrMagnitude <= 0.01f)
            {
                float angle = Vector3.Angle(transform.forward, m_player.Data.lookDirection_Flat);
                if (angle > 45f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(m_player.Data.lookDirection_Flat);
                    float lerpSpeed = Time.deltaTime * 10f * Mathf.InverseLerp(45f, 80f, angle);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, lerpSpeed);
                }
            }
        }

        private bool CanJump() => IsGrounded() && !m_player.Data.isCrouching && !m_player.Data.isCrawling;

        private bool IsGrounded()
        {
            return m_cc.isGrounded;
            // return Physics.Raycast(transform.position + Vector3.up * 0.03f, Vector3.down, groundCheckDistance);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody hitRigidbody = hit.rigidbody;

            // Don't push objects that are kinematic or too heavy
            if (hitRigidbody == null || hitRigidbody.isKinematic)
                return;

            // Don't push vertically (prevents pushing objects when jumping on them)
            if (hit.moveDirection.y > 0.1f)
                return;

            hitRigidbody.AddForce(hit.moveDirection * 1.5f, ForceMode.Impulse);
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Gizmos.color = Color.red;
            // Gizmos.DrawRay(transform.position + Vector3.up * 0.03f, Vector3.down * groundCheckDistance);
        }
        #endif
    }
}
