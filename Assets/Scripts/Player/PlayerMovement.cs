using RooseLabs.Core;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Transform modelTransform;

        private PlayerCharacter m_character;
        private AvatarMover m_avatarMover;
        private Rigidbody m_rigidbody;
        private Animator m_animator;
        private InputHandler m_inputHandler;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed   = 1.50f; // Average speed from animation: 1.20f;
        [SerializeField] private float runSpeed    = 5.00f; // Average speed from animation: 5.83f;
        [SerializeField] private float crouchSpeed = 0.75f; // Average speed from animation: 0.67f;
        [SerializeField] private float crawlSpeed  = 0.50f; // Average speed from animation: 0.25f;
        [SerializeField] private float jumpHeight  = 0.50f;

        public float CurrentStateSpeed
        {
            get
            {
                if (m_character.Data.isCrawling) return crawlSpeed;
                if (m_character.Data.isCrouching) return crouchSpeed;
                if (m_character.Data.isRunning) return runSpeed;
                return walkSpeed;
            }
        }

        private float m_movementValue; // -1 to 1, where negative is backwards, positive is forwards, and 0 is idle
        private float m_lastVerticalInputSignal = 1f;

        private bool m_jumpIsQueued;
        private bool m_isJumping;

        private bool m_isNearTable;

        private void Start()
        {
            m_character = GetComponent<PlayerCharacter>();
            m_avatarMover = GetComponent<AvatarMover>();
            m_rigidbody = GetComponent<Rigidbody>();
            m_animator = m_character.Animations.Animator;
            m_inputHandler = InputHandler.Instance;
        }

        private void Update()
        {
            if (!m_character.IsOwner) return;

            HandleLookInput();
            HandleMovementAndRotation();
            UpdateColliderHeight();
        }

        private void FixedUpdate()
        {
            if (!m_character.IsOwner) return;

            Vector2 moveInput = m_character.Input.movementInput;
            m_character.Data.currentSpeed = moveInput.sqrMagnitude <= 0.01f ? 0.0f : CurrentStateSpeed;
            Vector3 lookForward = m_character.Data.lookDirection_Flat;
            Vector3 lookRight = Vector3.Cross(Vector3.up, lookForward).normalized;
            Vector3 moveDirection = (lookRight * moveInput.x + lookForward * moveInput.y).normalized;
            Vector3 deltaMovement = moveDirection * m_character.Data.currentSpeed;

            if (m_isJumping)
            {
                m_avatarMover.EndLeaveGround();
                m_isJumping = false;
            }
            if (m_jumpIsQueued)
            {
                m_avatarMover.LeaveGround();
                float jumpVelocity = Mathf.Sqrt(2f * jumpHeight * 9.81f);
                m_avatarMover._velocityLeaveGround = m_avatarMover.Up * jumpVelocity;
                m_isJumping = true;
                m_jumpIsQueued = false;
            }

            m_avatarMover.Move(deltaMovement);
        }

        private void HandleMovementAndRotation()
        {
            Vector2 moveInput = m_character.Input.movementInput;

            // Note: The deadzone and m_lastVerticalInputSignal related code here is an attempt to reduce a "flipping"
            // effect that is most often noticeable when using a controller where the player quickly switches from
            // forward to backward movement. This is by no means a perfect solution, but it does help somewhat.
            float verticalInput = Mathf.Abs(moveInput.y) < 0.2f ? 0.0f : moveInput.y;
            bool isOnDeadzone = moveInput.y != 0.0f && verticalInput == 0.0f;

            bool hasHorizontal = !Mathf.Approximately(moveInput.x, 0.0f);
            bool hasVertical = !Mathf.Approximately(verticalInput, 0.0f);

            if (hasVertical)
            {
                // Moving forwards or backwards (with or without sideways movement), use magnitude of input vector for movement value
                m_lastVerticalInputSignal = Mathf.Sign(verticalInput);
                m_movementValue = moveInput.magnitude * m_lastVerticalInputSignal;
            }
            else if (hasHorizontal) // (and no vertical movement)
            {
                // Moving purely sideways, use absolute x value for movement value
                m_movementValue = Mathf.Abs(moveInput.x);
                if (isOnDeadzone) m_movementValue *= m_lastVerticalInputSignal;
            }
            else // No movement input
            {
                m_movementValue = 0.0f;
            }

            if (moveInput == Vector2.zero && Mathf.Abs(m_animator.GetFloat(PlayerAnimations.F_Movement)) <= 0.01f)
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, 0.0f);
            }
            else
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, m_movementValue, 0.1f, Time.deltaTime);
            }

            #region Crawl Logic
            if (m_character.Data.isCrouching && m_isNearTable && !m_character.Data.isCrawling)
            {
                // Drop any held item
                // if (m_pickup != null && m_pickup.HasItemInHand())
                // {
                //     m_pickup.Drop();
                // }
                m_character.Data.isCrawling = true;
                m_character.Data.isCrouching = false;
            }
            else if (m_character.Data.isCrawling && !m_isNearTable)
            {
                m_character.Data.isCrawling = false;
                m_character.Data.isCrouching = true;
            }
            #endregion

            #region Crouch Logic
            if (m_character.Input.crouchWasPressed && !m_character.Data.isCrawling)
            {
                m_character.Data.isCrouching = !m_character.Data.isCrouching;
                // Move pickup spot for crouching
                // if (m_pickup != null)
                //     m_pickup.SetPickupPositionForCrouch(m_character.Data.isCrouching);
            }
            #endregion

            #region Run Logic
            bool canRun = m_movementValue > 0.0f && !m_character.Data.isCrouching && !m_character.Data.isCrawling;
            if (!canRun)
            {
                m_character.Data.isRunning = false;
            }
            else if (m_inputHandler.IsCurrentDeviceKBM())
            {
                m_character.Data.isRunning = m_character.Input.sprintIsPressed;
            }
            else if (m_character.Input.sprintWasPressed)
            {
                m_character.Data.isRunning = !m_character.Data.isRunning;
            }
            #endregion

            if (m_character.Input.jumpWasPressed && CanJump())
            {
                m_jumpIsQueued = true;
            }

            // Handle rotation
            if (moveInput.sqrMagnitude <= 0.01f)
            {
                // Not moving, rotate when look direction is above a certain threshold
                float angle = Vector3.Angle(modelTransform.forward, m_character.Data.lookDirection_Flat);
                if (angle > 45f)
                {
                    modelTransform.rotation = Quaternion.Slerp(
                        modelTransform.rotation,
                        Quaternion.LookRotation(m_character.Data.lookDirection_Flat),
                        Mathf.InverseLerp(45f, 80f, angle) * 10f * Time.deltaTime
                    );
                }
            }
            else
            {
                // Moving, rotate based on movement direction and look direction
                Vector3 lookDirection = m_character.Data.lookDirection_Flat;

                float forwardAmount = hasVertical ? Mathf.Abs(verticalInput) : 0.0f;
                float sideAmount = Mathf.Abs(moveInput.x);
                float sideSign = Mathf.Sign(moveInput.x);
                if (verticalInput < 0.0f || (isOnDeadzone && !hasVertical && m_lastVerticalInputSignal < 0.0f))
                {
                    // Moving backwards, invert side sign
                    sideSign = -sideSign;
                }

                float angle = 0.0f;
                if (forwardAmount > 0.0f || sideAmount > 0.0f)
                {
                    angle = forwardAmount == 0.0f ? 90.0f : Mathf.Atan(sideAmount / forwardAmount) * Mathf.Rad2Deg;
                    angle *= sideSign;
                }

                modelTransform.rotation = Quaternion.Slerp(
                    modelTransform.rotation,
                    Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0f, angle, 0f),
                    Time.deltaTime * 10f
                );
            }
        }

        private void HandleLookInput()
        {
            float lookSensitivity = m_inputHandler.IsCurrentDeviceKBM() ? 0.1f : 0.4f;
            Vector2 delta = m_character.Input.lookInput * lookSensitivity;
            m_character.Data.lookValues += delta;
            m_character.Data.lookValues.y = Mathf.Clamp(m_character.Data.lookValues.y, -85f, 85f);
            m_character.UpdateLookDirection();
        }

        private void UpdateColliderHeight()
        {
            const float standingHeight = 1.70f;
            const float crouchHeight   = 1.10f;
            const float crawlHeight    = 0.85f;

            float colliderHeight = m_character.Data.isCrawling ? crawlHeight :
                                   m_character.Data.isCrouching ? crouchHeight :
                                   standingHeight;

            m_avatarMover.SetColliderHeight(colliderHeight);
        }

        private bool CanJump() => m_avatarMover.IsOnGround && !m_character.Data.isCrouching && !m_character.Data.isCrawling;

        public void SetNearTable(bool value)
        {
            m_isNearTable = value;
        }
    }
}
