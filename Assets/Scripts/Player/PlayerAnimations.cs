using FishNet.Component.Animating;
using FishNet.Object;
using UnityEngine;

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

        [field: SerializeField] public Animator Animator { get; private set; }
        [field: SerializeField] public NetworkAnimator NetworkAnimator { get; private set; }
        [SerializeField] private Transform headLookTarget;

        private PlayerCharacter m_character;

        private void Start()
        {
            m_character = GetComponent<PlayerCharacter>();
        }

        public override void OnStartClient()
        {
            enabled = IsOwner;
        }

        private void LateUpdate()
        {
            UpdateAnimatorParameters();
            if (m_character.Data.speedChangedThisFrame)
                AdjustAnimationSpeed();
            RecalculateHeadLookTarget();
        }

        /// <summary>
        /// Updates the animator's parameters based on the player's current state.
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (!m_character.Data.stateChangedThisFrame) return;
            Animator.SetBool(B_IsRunning, m_character.Data.IsRunning);
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
            else if (m_character.Data.IsRunning) speed = m_character.Data.CurrentSpeed / AnimRunSpeed;
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

        public void Play(int stateHash)
        {
            NetworkAnimator.Play(stateHash);
        }
    }
}
