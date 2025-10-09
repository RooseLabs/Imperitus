using RooseLabs.Core;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerMovementRootMotion : MonoBehaviour
    {
        private Player m_player;
        private Rigidbody m_rb;
        private Animator m_animator;
        private InputHandler m_inputHandler;

        [Header("Movement Settings")]
        [SerializeField] private float jumpHeight  = 0.50f;
        [SerializeField] private float groundCheckDistance = 0.1f;

        [Header("Colliders")]
        [SerializeField] private CapsuleCollider standingCollider;
        [SerializeField] private BoxCollider runningCollider;
        [SerializeField] private BoxCollider crouchingCollider;
        [SerializeField] private BoxCollider crawlingCollider;

        public Collider CurrentCollider
        {
            get
            {
                if (m_player.Data.isCrawling) return crawlingCollider;
                if (m_player.Data.isCrouching) return crouchingCollider;
                if (m_player.Data.isRunning) return runningCollider;
                return standingCollider;
            }
        }

        private int m_playerLayerMask;
        private readonly Vector3[] m_groundRayOrigins = new Vector3[5];
        private bool m_isGrounded = true;

        private float m_lastVerticalInputSignal = 1f;
        private Quaternion m_targetMovementRotation;
        private Quaternion m_targetLookRotation;
        private float m_lookRotationLerpSpeed;
        private bool m_hasLookRotation;
        private bool m_hasMovementRotation;

        private bool m_jumpIsQueued;

        private void Awake()
        {
            m_playerLayerMask = LayerMask.GetMask("Player");
        }

        private void Start()
        {
            m_player = GetComponent<Player>();
            m_rb = GetComponent<Rigidbody>();
            m_animator = GetComponent<Animator>();
            m_inputHandler = InputHandler.Instance;
        }

        private void Update()
        {
            if (!m_player.IsOwner) return;
            if (m_rb.isKinematic) return;

            UpdateGrounded();
            HandleLook();
            HandleMovement();
        }

        private void FixedUpdate()
        {
            if (!m_player.IsOwner) return;
            if (m_rb.isKinematic) return;

            if (m_hasMovementRotation)
            {
                m_rb.MoveRotation(Quaternion.Lerp(m_rb.rotation, m_targetMovementRotation, Time.fixedDeltaTime * 10f));
            }
            else if (m_hasLookRotation)
            {
                m_rb.MoveRotation(Quaternion.Lerp(m_rb.rotation, m_targetLookRotation, Time.fixedDeltaTime * m_lookRotationLerpSpeed));
            }
            m_hasMovementRotation = false;
            m_hasLookRotation = false;

            if (m_jumpIsQueued)
            {
                float jumpVelocity = Mathf.Sqrt(2f * jumpHeight * Physics.gravity.magnitude);
                m_rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
                m_jumpIsQueued = false;
            }
        }

        private void OnAnimatorMove()
        {
            Vector3 deltaPos = m_animator.deltaPosition;
            m_rb.MovePosition(m_rb.position + deltaPos);
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
            else if (hasHorizontal) // (and no vertical movement)
            {
                // Moving purely sideways, use absolute x value for movement value
                movementValue = Mathf.Abs(moveInput.x);
                if (isOnDeadzone) movementValue *= m_lastVerticalInputSignal;
            }
            else // No movement input
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

            if (m_player.Input.jumpWasPressed && CanJump())
            {
                m_jumpIsQueued = true;
            }

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
                m_targetMovementRotation = targetRotation;
                m_hasMovementRotation = true;
            }
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
                    m_targetLookRotation = Quaternion.LookRotation(m_player.Data.lookDirection_Flat);
                    m_lookRotationLerpSpeed = 10f * Mathf.InverseLerp(45f, 80f, angle);
                    m_hasLookRotation = true;
                }
            }
        }

        private bool CanJump() => m_isGrounded && !m_player.Data.isCrouching && !m_player.Data.isCrawling;

        private void UpdateGrounded()
        {
            float radius = standingCollider.radius;
            Vector3 bottomCenter = transform.position + Vector3.up * (standingCollider.height * 0.5f);
            m_groundRayOrigins[0] = bottomCenter;
            m_groundRayOrigins[1] = bottomCenter + transform.right * radius;
            m_groundRayOrigins[2] = bottomCenter - transform.right * radius;
            m_groundRayOrigins[3] = bottomCenter + transform.forward * radius;
            m_groundRayOrigins[4] = bottomCenter - transform.forward * radius;
            float rayLength = Vector3.Distance(bottomCenter, transform.position + Vector3.down * groundCheckDistance);
            foreach (Vector3 origin in m_groundRayOrigins)
            {
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, ~m_playerLayerMask))
                    continue;
                if (Vector3.Angle(Vector3.up, hit.normal) < 45f)
                {
                    m_isGrounded = true;
                    return;
                }
            }
            m_isGrounded = false;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = Color.red;
            float groundCheckRayLength = Vector3.Distance(
                transform.position + Vector3.up * (standingCollider.height * 0.5f),
                transform.position + Vector3.down * groundCheckDistance
            );
            for (int i = 0; i < m_groundRayOrigins.Length; i++)
            {
                Gizmos.DrawRay(m_groundRayOrigins[i], Vector3.down * groundCheckRayLength);
            }
        }
        #endif
    }
}
