using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerAnimations : NetworkBehaviour
    {
        #region Float Parameters
        public static readonly int F_Movement = Animator.StringToHash("Movement");
        #endregion

        #region Bool Parameters
        public static readonly int B_IsRunning = Animator.StringToHash("IsRunning");
        public static readonly int B_IsCrouching = Animator.StringToHash("IsCrouching");
        public static readonly int B_IsCrawling = Animator.StringToHash("IsCrawling");
        #endregion

        [SerializeField] private Transform headLookTarget;

        private Player m_player;
        private Animator m_animator;

        private void Start()
        {
            m_player = GetComponent<Player>();
            m_animator = GetComponent<Animator>();
        }

        public override void OnStartClient()
        {
            enabled = IsOwner;
        }

        private void LateUpdate()
        {
            RecalculateHeadLookTarget();
        }

        private void RecalculateHeadLookTarget()
        {
            const float minAngleStanding = 60f;
            const float maxAngleStanding = 115f;
            const float minAngleCrouching = 110f;
            const float maxAngleCrouching = 150f;
            const float minAngleCrawling = 115f;
            const float maxAngleCrawling = 175f;

            float minAngle = m_player.Data.isCrawling ? minAngleCrawling : (m_player.Data.isCrouching ? minAngleCrouching : minAngleStanding);
            float maxAngle = m_player.Data.isCrawling ? maxAngleCrawling : (m_player.Data.isCrouching ? maxAngleCrouching : maxAngleStanding);

            Vector3 lookDirection = m_player.Data.lookDirection;
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

            headLookTarget.position = m_player.Camera.transform.position + lookDirection * 2.5f;
        }
    }
}
