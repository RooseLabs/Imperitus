using FishNet.Component.Ownership;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform), typeof(PredictedOwner))]
    public class Draggable : NetworkBehaviour
    {
        #region Serialized
        [Header("Draggable Settings")]
        [SerializeField] private float force = 600;
        [SerializeField] private float damping = 50;
        #endregion

        public virtual bool IsDoor => false;

        private readonly SyncVar<bool> m_canOwnershipBeTakenByCollision = new(true,
            new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner)
        );

        protected bool CanOwnershipBeTakenByCollision
        {
            get => m_canOwnershipBeTakenByCollision.Value;
            set {
                if (m_canOwnershipBeTakenByCollision.Value != value)
                    SetCanOwnershipBeTakenByCollision(value);
            }
        }

        public Collider Collider { get; private set; }
        protected Rigidbody rb;
        protected bool isBeingDraggedByImpero;

        public bool IsBeingDraggedByImpero => isBeingDraggedByImpero;

        protected PredictedOwner predictedOwner;
        private ConfigurableJoint m_joint;
        private Vector3 m_targetPosition;
        private float m_initialAngularDamping;
        private bool m_wasLastInteractionDrag;

        protected virtual void Awake()
        {
            TryGetComponent(out predictedOwner);
            TryGetComponent(out rb);
            Collider = GetComponent<Collider>();
            rb.Sleep();
        }

        public void HandleDragBegin(Vector3 hitPoint)
        {
            predictedOwner.TakeOwnership(true);
            CanOwnershipBeTakenByCollision = false;
            AttachJoint(hitPoint);
            isBeingDraggedByImpero = true;
            m_wasLastInteractionDrag = true;
            m_targetPosition = hitPoint;
            HandleDragBegin_Internal();
        }

        public void HandleDrag(Vector3 position)
        {
            if (!isBeingDraggedByImpero) return;
            m_targetPosition = position;
        }

        public void HandleDragEnd()
        {
            isBeingDraggedByImpero = false;
            HandleDragEnd_Internal();
            if (m_joint)
                Destroy(m_joint.gameObject);
            CanOwnershipBeTakenByCollision = true;
        }

        protected virtual void HandleDragBegin_Internal()
        {
            m_initialAngularDamping = rb.angularDamping;
            rb.angularDamping = 25f;
        }

        protected virtual void HandleDragEnd_Internal()
        {
            rb.angularDamping = m_initialAngularDamping;
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

            var jointRb = go.AddComponent<Rigidbody>();
            jointRb.isKinematic = true;

            m_joint = go.AddComponent<ConfigurableJoint>();
            m_joint.connectedBody = rb;
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

        protected virtual void FixedUpdate()
        {
            if (!IsOwner) return;

            if (isBeingDraggedByImpero && m_joint)
            {
                m_joint.targetPosition = m_joint.transform.InverseTransformPoint(m_targetPosition);
            }

            if (!m_wasLastInteractionDrag) return;
            bool moving = rb.linearVelocity.sqrMagnitude > 0.01f || rb.angularVelocity.sqrMagnitude > 0.01f;
            CanOwnershipBeTakenByCollision = !moving;
        }

        protected virtual void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                if (isBeingDraggedByImpero || !IsDraggable) return;
                if (!CanOwnershipBeTakenByCollision) return;
                m_wasLastInteractionDrag = false;
                if (!networkObject.IsOwner) return;
                predictedOwner.TakeOwnership(true);
            }
        }

        [ServerRpc(RunLocally = true)]
        private void SetCanOwnershipBeTakenByCollision(bool value) => m_canOwnershipBeTakenByCollision.Value = value;

        public virtual bool IsDraggable => true;
    }
}
