using RooseLabs.Core;
using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(-96)]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private Transform modelTransform;

        private PlayerCharacter m_character;
        private AvatarMover m_avatarMover;
        private Rigidbody m_rigidbody;
        private Animator Animator => m_character.Animations.Animator;
        private SoundEmitter m_soundEmitter;
        private float m_nextFootstepTime;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed   = 1.50f; // Average speed from animation: 1.20f;
        [SerializeField] private float sprintSpeed = 5.00f; // Average speed from animation: 5.83f;
        [SerializeField] private float crouchSpeed = 0.75f; // Average speed from animation: 0.67f;
        [SerializeField] private float crawlSpeed  = 0.50f; // Average speed from animation: 0.25f;
        [SerializeField] private float jumpHeight  = 0.50f;
        [SerializeField] private float sprintStaminaUsage = 25f;

        [Header("Object Colliders")]
        [SerializeField] private Collider standingCollider;
        [SerializeField] private Collider crouchingCollider;
        [SerializeField] private Collider crawlingCollider;

        public float CurrentStateSpeed
        {
            get
            {
                if (m_character.Data.IsCrawling) return crawlSpeed;
                if (m_character.Data.IsCrouching) return crouchSpeed;
                if (m_character.Data.IsSprinting) return sprintSpeed;
                return walkSpeed;
            }
        }

        private float m_movementValue; // -1 to 1, where negative is backwards, positive is forwards, and 0 is idle
        private float m_lastVerticalInputSignal = 1f;

        private bool m_jumpIsQueued;
        private bool m_isJumping;

        private bool m_isNearTable;

        private int footstepIndex = -1;
        private int runningIndex = -1;
        private int crouchingIndex = -1;

        private void Start()
        {
            m_character = GetComponent<PlayerCharacter>();
            m_avatarMover = GetComponent<AvatarMover>();
            m_rigidbody = GetComponent<Rigidbody>();
            m_soundEmitter = GetComponent<SoundEmitter>();

            footstepIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Footstep");
            runningIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Running");
            crouchingIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Crouching");
        }

        private void Update()
        {
            if (!m_character.IsOwner) return;

            HandleLookInput();
            HandleMovementAndRotation();
            UpdateColliders();

            if (m_character.Data.IsSprinting)
            {
                m_character.UseStamina(sprintStaminaUsage * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (!m_character.IsOwner) return;

            if (m_character.Data.IsRagdollActive)
            {
                m_character.Data.CurrentSpeed = 0.0f;
                if (m_isJumping)
                {
                    m_avatarMover.EndLeaveGround();
                    m_isJumping = false;
                }
                return;
            }

            Vector2 moveInput = m_character.Input.movementInput;
            m_character.Data.CurrentSpeed = moveInput.sqrMagnitude <= 0.01f ? 0.0f : CurrentStateSpeed;
            Vector3 lookForward = m_character.Data.lookDirectionFlat;
            Vector3 lookRight = Vector3.Cross(Vector3.up, lookForward).normalized;
            Vector3 moveDirection = (lookRight * moveInput.x + lookForward * moveInput.y).normalized;
            Vector3 deltaMovement = moveDirection * m_character.Data.CurrentSpeed;

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

            int soundIndex = -1;
            if (m_character.Data.IsSprinting)
                soundIndex = runningIndex;
            else if (m_character.Data.IsCrouching)
                soundIndex = crouchingIndex;
            else
                soundIndex = footstepIndex;

            bool isMoving = m_character.Data.CurrentSpeed > 0.01f && m_movementValue != 0f && !m_character.Data.IsCrawling;

            if (isMoving && soundIndex >= 0 && Time.time >= m_nextFootstepTime)
            {
                m_soundEmitter.RequestEmitFromClient(soundIndex);

                float interval = m_character.Data.IsSprinting ? 0.1f :
                                 m_character.Data.IsCrouching ? 0.5f : 0.4f;
                m_nextFootstepTime = Time.time + interval;
            }
        }

        private void HandleMovementAndRotation()
        {
            if (m_character.Data.IsRagdollActive) return;
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

            if (moveInput == Vector2.zero && Mathf.Abs(Animator.GetFloat(PlayerAnimations.F_Movement)) <= 0.01f)
            {
                Animator.SetFloat(PlayerAnimations.F_Movement, 0.0f);
            }
            else
            {
                Animator.SetFloat(PlayerAnimations.F_Movement, m_movementValue, 0.1f, Time.deltaTime);
            }

            #region Crawl Logic
            if (m_character.Data.IsCrouching && m_isNearTable && !m_character.Data.IsCrawling)
            {
                // Drop any held item
                // if (m_pickup != null && m_pickup.HasItemInHand())
                // {
                //     m_pickup.Drop();
                // }
                m_character.Data.IsCrawling = true;
                m_character.Data.IsCrouching = false;
            }
            else if (m_character.Data.IsCrawling && !m_isNearTable)
            {
                m_character.Data.IsCrawling = false;
                m_character.Data.IsCrouching = true;
            }
            #endregion

            #region Crouch Logic
            if (m_character.Input.crouchWasPressed && !m_character.Data.IsCrawling)
            {
                m_character.Data.IsCrouching = !m_character.Data.IsCrouching;
                // Move pickup spot for crouching
                // if (m_pickup != null)
                //     m_pickup.SetPickupPositionForCrouch(m_character.Data.IsCrouching);
            }
            #endregion

            #region Sprint Logic
            bool canSprint = m_movementValue > 0.0f && m_character.Data.Stamina > 0.0f &&
                             !m_character.Data.IsCrouching && !m_character.Data.IsCrawling;
            if (!canSprint)
            {
                m_character.Data.IsSprinting = false;
            }
            else if (InputHandler.CurrentInputScheme == InputScheme.KeyboardMouse)
            {
                m_character.Data.IsSprinting = m_character.Input.sprintIsPressed;
            }
            else if (m_character.Input.sprintWasPressed)
            {
                m_character.Data.IsSprinting = !m_character.Data.IsSprinting;
            }
            #endregion

            if (m_character.Input.jumpWasPressed && CanJump())
            {
                m_jumpIsQueued = true;
            }

            // Handle rotation
            if (m_character.Data.isAiming)
            {
                // While aiming, rotation should always face look direction
                modelTransform.rotation = Quaternion.LookRotation(m_character.Data.lookDirectionFlat);
            }
            else if (moveInput.sqrMagnitude <= 0.01f)
            {
                // Not moving, rotate when look direction is above a certain threshold
                float angle = Vector3.Angle(modelTransform.forward, m_character.Data.lookDirectionFlat);
                if (angle > 45f)
                {
                    modelTransform.rotation = Quaternion.Slerp(
                        modelTransform.rotation,
                        Quaternion.LookRotation(m_character.Data.lookDirectionFlat),
                        Mathf.InverseLerp(45f, 80f, angle) * 10f * Time.deltaTime
                    );
                }
            }
            else
            {
                // Moving, rotate based on movement direction and look direction
                Vector3 lookDirection = m_character.Data.lookDirectionFlat;

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
            float lookSensitivity = InputHandler.CurrentInputScheme == InputScheme.KeyboardMouse ? 0.1f : 0.4f;
            Vector2 delta = m_character.Input.lookInput * lookSensitivity;
            m_character.Data.lookValues += delta;
            m_character.Data.lookValues.y = Mathf.Clamp(m_character.Data.lookValues.y, -85f, 85f);
            m_character.UpdateLookDirection();
        }

        private void UpdateColliders()
        {
            const float standingHeight = 1.70f;
            const float crouchHeight   = 1.10f;
            const float crawlHeight    = 0.85f;

            if (m_character.Data.IsCrawling)
            {
                standingCollider.enabled = false;
                crouchingCollider.enabled = false;
                crawlingCollider.enabled = true;
                m_avatarMover.SetColliderHeight(crawlHeight);
            }
            else if (m_character.Data.IsCrouching)
            {
                standingCollider.enabled = false;
                crouchingCollider.enabled = true;
                crawlingCollider.enabled = false;
                m_avatarMover.SetColliderHeight(crouchHeight);
            }
            else
            {
                standingCollider.enabled = true;
                crouchingCollider.enabled = false;
                crawlingCollider.enabled = false;
                m_avatarMover.SetColliderHeight(standingHeight);
            }
        }

        private bool CanJump() => m_avatarMover.IsOnGround && !m_character.Data.IsCrouching && !m_character.Data.IsCrawling;

        public void SetNearTable(bool value)
        {
            m_isNearTable = value;
        }
    }
}
