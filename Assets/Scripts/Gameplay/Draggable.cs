using FishNet.Component.Ownership;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(PredictedOwner), typeof(NetworkTransform))]
    public class Draggable : NetworkBehaviour
    {
        #region Serialized
        [SerializeField] private float force = 600;
        [SerializeField] private float damping = 50;
        #endregion

        private readonly SyncVar<bool> canOwnershipBeTakenByCollision = new(
            new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner)
        );

        private bool CanOwnershipBeTakenByCollision
        {
            get => canOwnershipBeTakenByCollision.Value;
            set {
                if (canOwnershipBeTakenByCollision.Value != value)
                    SetCanOwnershipBeTakenByCollision(value);
            }
        }

        private PredictedOwner m_predictedOwner;
        private Rigidbody m_rigidbody;
        public Collider Collider { get; private set; }

        private ConfigurableJoint m_joint;
        private Vector3 m_targetPosition;
        private float m_initialAngularDamping;
        private bool m_isDragging;
        private bool m_wasLastInteractionDrag;

        private void Awake()
        {
            TryGetComponent(out m_predictedOwner);
            TryGetComponent(out m_rigidbody);
            Collider = GetComponent<Collider>();
        }

        public void HandleDragBegin(Vector3 hitPoint)
        {
            CanOwnershipBeTakenByCollision = false;
            AttachJoint(hitPoint);
            m_isDragging = true;
            m_wasLastInteractionDrag = true;
            m_targetPosition = hitPoint;
            m_initialAngularDamping = m_rigidbody.angularDamping;
            m_rigidbody.angularDamping = 25f;
            m_predictedOwner.TakeOwnership(true);
        }

        public void HandleDrag(Vector3 position)
        {
            if (!m_isDragging) return;
            m_targetPosition = position;
        }

        public void HandleDragEnd()
        {
            m_isDragging = false;
            m_rigidbody.angularDamping = m_initialAngularDamping;
            if (m_joint)
                Destroy(m_joint.gameObject);
            CanOwnershipBeTakenByCollision = true;
        }

        private void AttachJoint(Vector3 attachmentPosition)
        {
            if (m_joint)
                Destroy(m_joint.gameObject);

            GameObject go = new GameObject("Attachment Point")
            {
                hideFlags = HideFlags.HideInHierarchy,
                transform =
                {
                    position = attachmentPosition
                }
            };

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            m_joint = go.AddComponent<ConfigurableJoint>();
            m_joint.connectedBody = m_rigidbody;
            m_joint.configuredInWorldSpace = true;

            // Set the joint to XYZ movement mode
            m_joint.xMotion = ConfigurableJointMotion.Free;
            m_joint.yMotion = ConfigurableJointMotion.Free;
            m_joint.zMotion = ConfigurableJointMotion.Free;

            // Lock rotation
            // m_joint.angularXMotion = ConfigurableJointMotion.Locked;
            // m_joint.angularYMotion = ConfigurableJointMotion.Locked;
            // m_joint.angularZMotion = ConfigurableJointMotion.Locked;

            var drive = NewJointDrive(force, damping);
            m_joint.xDrive = drive;
            m_joint.yDrive = drive;
            m_joint.zDrive = drive;
        }

        private static JointDrive NewJointDrive(float force, float damping)
        {
            return new JointDrive
            {
                positionSpring = force,
                positionDamper = damping,
                maximumForce = Mathf.Infinity
            };
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;

            if (m_isDragging && m_joint)
            {
                m_joint.targetPosition = m_joint.transform.InverseTransformPoint(m_targetPosition);
            }

            if (!m_wasLastInteractionDrag) return;
            bool moving = m_rigidbody.linearVelocity.sqrMagnitude > 0.01f || m_rigidbody.angularVelocity.sqrMagnitude > 0.01f;
            if (moving && CanOwnershipBeTakenByCollision)
            {
                SetCanOwnershipBeTakenByCollision(false);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out PlayerCharacter playerCharacter))
            {
                if (m_isDragging) return;
                if (!CanOwnershipBeTakenByCollision) return;
                m_wasLastInteractionDrag = false;
                if (playerCharacter != PlayerCharacter.LocalCharacter) return;
                m_predictedOwner.TakeOwnership(true);
            }
        }

        [ServerRpc(RunLocally = true)]
        private void SetCanOwnershipBeTakenByCollision(bool value) => canOwnershipBeTakenByCollision.Value = value;
    }
}
