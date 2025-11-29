using System;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class ProjectileRigidbody : MonoBehaviour
    {
        public Rigidbody Rigidbody { get; private set; }

        #region Events
        public event Action<Collision> CollisionEnter;
        public event Action<Collision> CollisionStay;
        public event Action<Collision> CollisionExit;
        public event Action<Collider> TriggerEnter;
        public event Action<Collider> TriggerStay;
        public event Action<Collider> TriggerExit;
        #endregion

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
        }

        public void ResetState()
        {
            Rigidbody.linearVelocity = Vector3.zero;
            Rigidbody.angularVelocity = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.position = Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision) => CollisionEnter?.Invoke(collision);
        private void OnCollisionStay(Collision collision) => CollisionStay?.Invoke(collision);
        private void OnCollisionExit(Collision collision) => CollisionExit?.Invoke(collision);
        private void OnTriggerEnter(Collider other) => TriggerEnter?.Invoke(other);
        private void OnTriggerStay(Collider other) => TriggerStay?.Invoke(other);
        private void OnTriggerExit(Collider other) => TriggerExit?.Invoke(other);
    }
}
