using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class DraggableDoor : Draggable
    {
        #region Serialized
        [Tooltip("Colliders that should be ignored (e.g. neighboring door parts, potentially walls and ground).")]
        [SerializeField] private Collider[] ignoredColliders;
        [Tooltip("Strength of the spring force moving the door back to its rest position.")]
        [SerializeField] private float positionSpringStrength = 5f;
        [Tooltip("Strength of the spring torque rotating the door back to its rest rotation.")]
        [SerializeField] private float rotationSpringStrength = 5f;
        [Tooltip("Damping factor applied when returning to rest position.")]
        [SerializeField] private float dampingFactor = 0.8f;
        #endregion

        private static int s_blockingObjectsLayerMask;

        public override bool IsDoor => true;

        private Vector3 m_initialPosition;
        private Quaternion m_initialRotation;
        private Coroutine m_returnToRestPosition;
        private float m_notMovingTimer;

        protected override void Awake()
        {
            base.Awake();
            if (s_blockingObjectsLayerMask == 0)
                s_blockingObjectsLayerMask = LayerMask.GetMask("PlayerObjectCollision", "Draggable");
            m_initialPosition = transform.position;
            m_initialRotation = transform.rotation;
            foreach (var col in ignoredColliders)
            {
                Physics.IgnoreCollision(Collider, col);
            }
        }

        protected override void HandleDragBegin_Internal()
        {
            if (m_returnToRestPosition != null)
            {
                StopCoroutine(m_returnToRestPosition);
                m_returnToRestPosition = null;
            }
        }

        protected override void HandleDragEnd_Internal() { }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!IsController) return;

            if (isBeingDraggedByImpero) return;
            if (m_returnToRestPosition != null) return;

            bool notMoving = Mathf.Approximately(rb.linearVelocity.sqrMagnitude, 0f) &&
                             Mathf.Approximately(rb.angularVelocity.sqrMagnitude, 0f);
            if (notMoving)
            {
                m_notMovingTimer += Time.deltaTime;
                if (IsServerInitialized && !Owner.IsValid)
                {
                    // This should only run on the server when there's no owner (the clientHost isn't the owner either)
                    // This makes the clientHost also wait for 2 seconds of no movement before removing ownership,
                    // making a total of 3 seconds of wait before starting the coroutine to return to rest position.
                    if (m_notMovingTimer < 1f) return;
                    bool isAtRestPosition = Vector3.Distance(rb.position, m_initialPosition) < 0.01f &&
                                            Quaternion.Angle(rb.rotation, m_initialRotation) < 0.1f;
                    if (!isAtRestPosition)
                        m_returnToRestPosition = StartCoroutine(TryReturnToRestPosition());
                }
                else
                {
                    // For normal Draggable objects ownership is only removed when the object is either dragged by
                    // another player or collided with an object owned by another player. However, for doors we want
                    // the server to be able to return them to their rest position after some time of inactivity,
                    // so we need to automatically remove ownership after some time.
                    if (m_notMovingTimer < 2f) return;
                    RemoveOwnership_ServerRpc();
                    m_notMovingTimer = 0f;
                }
                return;
            }
            m_notMovingTimer = 0f;
        }

        /// <summary>
        /// Attempts to smoothly return the door to its initial rest position and rotation.
        /// </summary>
        private IEnumerator TryReturnToRestPosition()
        {
            while (true)
            {
                // Calculate position movement
                Vector3 toTarget = m_initialPosition - rb.position;
                Vector3 targetVelocity = toTarget * positionSpringStrength;
                rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * dampingFactor);

                // Calculate rotation movement
                Quaternion rotationDifference = m_initialRotation * Quaternion.Inverse(rb.rotation);
                rotationDifference.ToAngleAxis(out float angle, out Vector3 axis);

                if (angle > 180f)
                    angle -= 360f;

                Vector3 targetAngularVelocity = axis * (angle * Mathf.Deg2Rad * rotationSpringStrength);
                rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, targetAngularVelocity, Time.fixedDeltaTime * dampingFactor);

                // Stop if we're very close to the target
                if (toTarget.magnitude < 0.01f && Mathf.Abs(angle) < 0.1f)
                {
                    // Ensure the object comes to a complete stop
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.Sleep();
                    m_returnToRestPosition = null;
                    yield break;
                }
                yield return new WaitForFixedUpdate();
            }
        }

        public override void OnOwnershipServer(NetworkConnection prevOwner)
        {
            if (!IsController && m_returnToRestPosition != null)
            {
                StopCoroutine(m_returnToRestPosition);
                m_returnToRestPosition = null;
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void RemoveOwnership_ServerRpc()
        {
            RemoveOwnership();
        }

        protected override void OnCollisionEnter(Collision other)
        {
            base.OnCollisionEnter(other);
            if (isBeingDraggedByImpero) return;
            if (m_returnToRestPosition != null)
            {
                StopCoroutine(m_returnToRestPosition);
                m_returnToRestPosition = null;
            }
        }
    }
}
