using RooseLabs.Core;
using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(-96)]
    public class PlayerMovement : MonoBehaviour
    {
        private PlayerCharacter m_character;
        private AvatarMover m_avatarMover;
        private Animator Animator => m_character.Animations.Animator;
        private SoundEmitter m_soundEmitter;
        private float m_nextFootstepTime;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed   = 1.50f; // Average speed from animation: 1.20f;
        [SerializeField] private float sprintSpeed = 5.00f; // Average speed from animation: 5.83f;
        [SerializeField] private float crouchSpeed = 0.75f; // Average speed from animation: 0.67f;
        [SerializeField] private float crawlSpeed  = 0.50f; // Average speed from animation: 0.25f;
        [SerializeField] private float jumpHeight  = 0.50f;
        [SerializeField] private float sprintStaminaUsage = 20f;

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

        private const float Gravity = 9.81f;
        private bool m_isJumping = false;
        private float m_jumpVelocity = 0f;
        private float m_midAirSpeed = 0f;

        private bool m_isNearTable;

        private int m_footstepIndex = -1;
        private int m_runningIndex = -1;
        private int m_crouchingIndex = -1;

        private void Awake()
        {
            m_character = GetComponent<PlayerCharacter>();
            m_avatarMover = GetComponent<AvatarMover>();
            m_soundEmitter = GetComponent<SoundEmitter>();

            m_footstepIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Footstep");
            m_runningIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Running");
            m_crouchingIndex = m_soundEmitter.availableSounds.FindIndex(s => s.type != null && s.type.key == "Crouching");
        }

        private void OnEnable()
        {
            m_avatarMover.OnIsOnGroundChanged += IsOnGroundStateChanged;
        }

        private void OnDisable()
        {
            m_avatarMover.OnIsOnGroundChanged -= IsOnGroundStateChanged;
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
                if (m_isJumping) EndJump();
                return;
            }

            Vector2 moveInput = m_character.Input.movementInput;

            float horizontalMovementSpeed = !m_avatarMover.IsOnGround ? m_midAirSpeed : CurrentStateSpeed;
            m_character.Data.CurrentSpeed = moveInput.sqrMagnitude <= 0.01f ? 0.0f : horizontalMovementSpeed;

            Vector3 lookForward = m_character.Data.lookDirectionFlat;
            Vector3 lookRight = Vector3.Cross(Vector3.up, lookForward).normalized;
            Vector3 moveDirection = (lookRight * moveInput.x + lookForward * moveInput.y).normalized;
            Vector3 deltaMovement = moveDirection * m_character.Data.CurrentSpeed;

            Vector3 velocityJump = UpdateJump(Time.deltaTime);
            m_avatarMover.Move(deltaMovement + velocityJump);

            int soundIndex = -1;
            if (m_character.Data.IsSprinting)
                soundIndex = m_runningIndex;
            else if (m_character.Data.IsCrouching)
                soundIndex = m_crouchingIndex;
            else
                soundIndex = m_footstepIndex;

            bool isMoving = m_character.Data.CurrentSpeed > 0.01f && m_movementValue != 0f && !m_character.Data.IsCrawling;

            if (isMoving && soundIndex >= 0 && Time.time >= m_nextFootstepTime)
            {
                m_soundEmitter.RequestEmitFromClient(soundIndex);

                float interval = m_character.Data.IsSprinting ? 0.3f :
                                 m_character.Data.IsCrouching ? 0.7f : 0.55f;
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
                StartJump(jumpHeight);
            }

            // Handle rotation
            if (m_character.Data.isAiming)
            {
                // While aiming, rotation should always face look direction
                m_character.ModelTransform.rotation = Quaternion.LookRotation(m_character.Data.lookDirectionFlat);
            }
            else if (moveInput.sqrMagnitude <= 0.01f)
            {
                // Not moving, rotate when look direction is above a certain threshold
                float angle = Vector3.Angle(m_character.ModelTransform.forward, m_character.Data.lookDirectionFlat);
                if (angle > 45f)
                {
                    m_character.ModelTransform.rotation = Quaternion.Slerp(
                        m_character.ModelTransform.rotation,
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

                m_character.ModelTransform.rotation = Quaternion.Slerp(
                    m_character.ModelTransform.rotation,
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

        private void StartJump(float height)
        {
            m_jumpVelocity = Mathf.Sqrt(2f * Gravity * height);
            m_isJumping = true;
            m_avatarMover.LeaveGround();
        }

        private Vector3 UpdateJump(float deltaTime)
        {
            if (!m_isJumping) return Vector3.zero;

            if (m_avatarMover.IsTouchingCeiling)
            {
                EndJump();
                return Vector3.zero;
            }

            Vector3 velocityThisFrame = new Vector3(0f, m_jumpVelocity, 0f);
            m_jumpVelocity -= Gravity * deltaTime;
            if (m_jumpVelocity <= 0f) EndJump();

            return velocityThisFrame;
        }

        private void EndJump()
        {
            m_isJumping = false;
            m_jumpVelocity = 0f;
            m_avatarMover.EndLeaveGround();
        }

        private void IsOnGroundStateChanged(bool isGrounded)
        {
            if (isGrounded) return;
            // Player just left the ground, take a snapshot of the current horizontal movement speed
            m_midAirSpeed = CurrentStateSpeed;
        }

        public void SetNearTable(bool value)
        {
            m_isNearTable = value;
        }
    }
}
