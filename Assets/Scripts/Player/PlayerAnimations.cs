using FishNet.Component.Animating;
using FishNet.Object;
using RooseLabs.Utils;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace RooseLabs.Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        #region Animation Clips
        public const string C_StandUpFaceUp   = "BaseRigPlayer|StandUpFront";
        public const string C_StandUpFaceDown = "BaseRigPlayer|StandUpBack";
        #endregion

        #region Animation States
        public static readonly int S_StandUpFaceUp   = Animator.StringToHash("FaceUpStandUp");
        public static readonly int S_StandUpFaceDown = Animator.StringToHash("FaceDownStandUp");
        #endregion

        #region Float Parameters
        public static readonly int F_Movement = Animator.StringToHash("Movement");
        #endregion

        #region Bool Parameters
        public static readonly int B_IsRunning   = Animator.StringToHash("IsRunning");
        public static readonly int B_IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int B_IsCrawling  = Animator.StringToHash("IsCrawling");
        #endregion

        #region Default Average Movement Speeds
        // These values represent the average movement speed of the player in each state with root motion enabled.
        // Using these values for each respective state gives the best results with minimal "sliding" of the feet.
        // We can use these as a reference to adjust the animation playback speed based on actual player movement speed.
        private const float AnimWalkSpeed   = 1.20f;
        private const float AnimRunSpeed    = 5.83f;
        private const float AnimCrouchSpeed = 0.67f;
        private const float AnimCrawlSpeed  = 0.25f;
        #endregion

        #region Serialized
        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public NetworkAnimator NetworkAnimator { get; private set; }

        [Header("Head IK")]
        [SerializeField] private Transform headLookTarget;

        [Header("Hand IK")]
        [SerializeField] private Rig armRig;
        [SerializeField] private Transform handTarget;
        [SerializeField] private Vector2 handViewportPos = new(0.8f, 0.2f);
        [SerializeField] private Quaternion handTargetOffsetRotation = Quaternion.identity;
        [SerializeField][Range(0.4f, 0.5f)] private float handDistance = 0.45f;
        #endregion

        private PlayerCharacter m_character;

        private Transform m_rightShoulder;
        private Transform m_rightLowerArm;
        private float m_armRigWeightVelocity = 0f;

        private void Start()
        {
            m_character = GetComponent<PlayerCharacter>();

            m_rightShoulder = Animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            m_rightLowerArm = Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        private void Update()
        {
            if (!IsOwner) return;
            UpdateAnimatorParameters();
            if (m_character.Data.SpeedChangedThisFrame)
                AdjustAnimationSpeed();
            RecalculateHeadLookTarget();
        }

        private void LateUpdate()
        {
            // Needs to be done in LateUpdate because it uses bone transforms,
            // which would be from the previous frame in Update.
            UpdateArmIK();
        }

        /// <summary>
        /// Updates the animator's parameters based on the player's current state.
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (!m_character.Data.StateChangedThisFrame) return;
            Animator.SetBool(B_IsRunning, m_character.Data.IsSprinting);
            Animator.SetBool(B_IsCrouching, m_character.Data.IsCrouching);
            Animator.SetBool(B_IsCrawling, m_character.Data.IsCrawling);
        }

        /// <summary>
        /// Adjusts the animation playback speed based on the player's current movement speed and state.
        /// This helps to synchronize the visual animation with the actual movement speed, reducing foot sliding.
        /// </summary>
        private void AdjustAnimationSpeed()
        {
            float speed;
            if (m_character.Data.IsRagdollActive) speed = 1.0f;
            else if (m_character.Data.IsCrawling) speed = m_character.Data.CurrentSpeed / AnimCrawlSpeed;
            else if (m_character.Data.IsCrouching) speed = m_character.Data.CurrentSpeed / AnimCrouchSpeed;
            else if (m_character.Data.IsSprinting) speed = m_character.Data.CurrentSpeed / AnimRunSpeed;
            else speed = m_character.Data.CurrentSpeed / AnimWalkSpeed;
            Animator.speed = Mathf.Clamp(speed, 1.0f, 2.0f);
        }

        /// <summary>
        /// Recalculates the head look target position based on the player's look direction and posture.
        /// </summary>
        private void RecalculateHeadLookTarget()
        {
            const float minAngleStanding  = 60f;
            const float maxAngleStanding  = 115f;
            const float minAngleCrouching = 110f;
            const float maxAngleCrouching = 150f;
            const float minAngleCrawling  = 115f;
            const float maxAngleCrawling  = 160f;

            float minAngle = m_character.Data.IsCrawling ? minAngleCrawling : (m_character.Data.IsCrouching ? minAngleCrouching : minAngleStanding);
            float maxAngle = m_character.Data.IsCrawling ? maxAngleCrawling : (m_character.Data.IsCrouching ? maxAngleCrouching : maxAngleStanding);

            Vector3 lookDirection = m_character.Data.lookDirection;
            Vector3 upRef = Vector3.up;

            float angle = Vector3.Angle(lookDirection, upRef);
            float clamped = Mathf.Clamp(angle, minAngle, maxAngle);
            float delta = clamped - angle;
            float absDelta = Mathf.Abs(delta);

            if (absDelta > 0.01f)
            {
                lookDirection = Vector3.RotateTowards(
                    lookDirection,
                    angle > maxAngle ? upRef : -upRef,
                    Mathf.Deg2Rad * absDelta,
                    0f
                );
            }

            headLookTarget.position = m_character.Camera.transform.position + lookDirection * 2.5f;
        }

        private void UpdateArmIK()
        {
            const float minAngleStandingHand  = 75f;
            const float maxAngleStandingHand  = 140f;
            const float minAngleCrouchingHand = 120f;
            const float maxAngleCrouchingHand = 140f;

            if (m_character.Data.isAiming)
            {
                armRig.weight = Mathf.SmoothDamp(armRig.weight, 1f, ref m_armRigWeightVelocity, 0.1f);
            }
            else
            {
                armRig.weight = Mathf.SmoothDamp(armRig.weight, 0f, ref m_armRigWeightVelocity, 0.1f);
                return;
            }

            if (!IsOwner) return;

            Vector2 viewAngles = CameraPlaneUtils.ViewportToViewAngles(m_character.Camera, handViewportPos);
            float yawOffset = viewAngles.x * Mathf.Rad2Deg;
            float pitchOffset = viewAngles.y * Mathf.Rad2Deg;

            Vector3 centerDir = m_character.Data.lookDirection;
            float centerY = centerDir.y;
            float centerHorizMag = Mathf.Sqrt(centerDir.x * centerDir.x + centerDir.z * centerDir.z);
            float centerPitch = Mathf.Atan2(centerY, centerHorizMag) * Mathf.Rad2Deg;
            float centerYaw = Mathf.Atan2(centerDir.x, centerDir.z) * Mathf.Rad2Deg;

            float desiredPitch = centerPitch + pitchOffset;
            float desiredYaw = centerYaw + yawOffset;

            float minAngle = m_character.Data.IsCrouching ? minAngleCrouchingHand : minAngleStandingHand;
            float maxAngle = m_character.Data.IsCrouching ? maxAngleCrouchingHand : maxAngleStandingHand;

            float minPitch = 90f - maxAngle;
            float maxPitch = 90f - minAngle;

            float clampedPitch = Mathf.Clamp(desiredPitch, minPitch, maxPitch);

            float pitchRad = clampedPitch * Mathf.Deg2Rad;
            float yawRad = desiredYaw * Mathf.Deg2Rad;

            Vector3 direction = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            );

            Vector3 shoulderPosition = m_rightShoulder.position;
            Vector3 viewTargetPosition = m_character.Camera.transform.position + direction * 0.5f;
            Vector3 directionToViewTarget = (viewTargetPosition - shoulderPosition).normalized;
            float distToViewTarget = Vector3.Distance(shoulderPosition, viewTargetPosition);

            handTarget.position = shoulderPosition + directionToViewTarget * Mathf.Min(handDistance, distToViewTarget);
            handTarget.rotation = m_rightLowerArm.rotation * handTargetOffsetRotation;
        }

        public void Play(int stateHash)
        {
            NetworkAnimator.Play(stateHash);
        }
    }
}
