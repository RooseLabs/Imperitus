using RooseLabs.Core;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerMovement : MonoBehaviour
    {
        private Player m_player;
        private Rigidbody m_rb;
        private Animator m_animator;
        private InputHandler m_inputHandler;

        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed   = 1.50f; // Average speed from animation: 1.20f;
        [SerializeField] private float runSpeed    = 5.00f; // Average speed from animation: 5.83f;
        [SerializeField] private float crouchSpeed = 0.67f; // Average speed from animation: 0.67f;
        [SerializeField] private float crawlSpeed  = 0.25f; // Average speed from animation: 0.25f;
        [SerializeField] private float jumpHeight  = 0.50f;
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Colliders")]
        [SerializeField] private CapsuleCollider standingCollider;
        [SerializeField] private BoxCollider runningCollider;
        [SerializeField] private BoxCollider crouchingCollider;
        [SerializeField] private BoxCollider crawlingCollider;

        public Collider CurrentCollider
        {
            get
            {
                return standingCollider;
                if (m_player.Data.isCrawling) return crawlingCollider;
                if (m_player.Data.isCrouching) return crouchingCollider;
                if (m_player.Data.isRunning) return runningCollider;
                return standingCollider;
            }
        }

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

        private int m_playerLayerMask;
        private readonly Vector3[] m_groundRayOrigins = new Vector3[5];
        private bool m_isGrounded = true;

        private float m_movementValue; // -1 to 1, where negative is backwards, positive is forwards, and 0 is idle
        private Vector3 m_movementVector;
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
            HandleLookInput();
            HandleMovementInput();
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

            Vector3 deltaMovement = m_rb.rotation * m_movementVector * (CurrentStateSpeed * Time.fixedDeltaTime);
            m_rb.MovePosition(m_rb.position + deltaMovement);

            if (m_jumpIsQueued)
            {
                float jumpVelocity = Mathf.Sqrt(2f * jumpHeight * Physics.gravity.magnitude);
                m_rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
                m_jumpIsQueued = false;
            }
        }

        private void HandleMovementInput()
        {
            Vector2 moveInput = m_player.Input.movementInput;

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
            m_movementVector.z = m_movementValue;

            if (moveInput == Vector2.zero && Mathf.Abs(m_animator.GetFloat(PlayerAnimations.F_Movement)) <= 0.01f)
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, 0.0f);
            }
            else
            {
                m_animator.SetFloat(PlayerAnimations.F_Movement, m_movementValue, 0.1f, Time.deltaTime);
            }

            if (m_player.Input.crouchWasPressed)
            {
                m_player.Data.isCrouching = !m_player.Data.isCrouching;
                m_animator.SetBool(PlayerAnimations.B_IsCrouching, m_player.Data.isCrouching);
            }

            bool canRun = m_movementValue > 0.0f && !m_player.Data.isCrouching && !m_player.Data.isCrawling;
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

        private void HandleLookInput()
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
            Collider col = CurrentCollider;
            float height = col is CapsuleCollider cap1 ? cap1.height : (col as BoxCollider).size.y;
            float halfHeight = height * 0.5f;
            float radius = col is CapsuleCollider cap2 ? cap2.radius : Mathf.Min((col as BoxCollider).size.x, (col as BoxCollider).size.z) / 2f;
            Vector3 colliderCenter = transform.position + Vector3.up * halfHeight;
            m_groundRayOrigins[0] = colliderCenter;
            m_groundRayOrigins[1] = colliderCenter + transform.right * radius;
            m_groundRayOrigins[2] = colliderCenter - transform.right * radius;
            m_groundRayOrigins[3] = colliderCenter + transform.forward * radius;
            m_groundRayOrigins[4] = colliderCenter - transform.forward * radius;
            float rayLength = halfHeight + groundCheckDistance;
            m_isGrounded = false;
            foreach (Vector3 origin in m_groundRayOrigins)
            {
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayLength, ~m_playerLayerMask))
                    continue;
                float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                if (slopeAngle < maxSlopeAngle)
                {
                    m_isGrounded = true;
                }
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = Color.red;
            Collider col = CurrentCollider;
            float height = col is CapsuleCollider cap1 ? cap1.height : (col as BoxCollider).size.y;
            float groundCheckRayLength = height * 0.5f + groundCheckDistance;
            for (int i = 0; i < m_groundRayOrigins.Length; i++)
            {
                Gizmos.DrawRay(m_groundRayOrigins[i], Vector3.down * groundCheckRayLength);
            }
        }
        #endif
    }
}
