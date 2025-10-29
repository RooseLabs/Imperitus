using FishNet.Component.Ownership;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class Draggable : NetworkBehaviour
    {
        private readonly SyncVar<bool> isMoving = new(
            new SyncTypeSettings(WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner)
        );

        private PredictedOwner m_predictedOwner;
        private Rigidbody m_rigidbody;

        private void Awake()
        {
            TryGetComponent(out m_predictedOwner);
            TryGetComponent(out m_rigidbody);
        }

        private void FixedUpdate()
        {
            if (!IsOwner) return;
            bool moving = m_rigidbody.linearVelocity.sqrMagnitude > 0.01f || m_rigidbody.angularVelocity.sqrMagnitude > 0.01f;
            if (moving != isMoving.Value)
            {
                SetIsMoving(moving);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out PlayerCharacter playerCharacter))
            {
                if (isMoving.Value) return;
                if (playerCharacter != PlayerCharacter.LocalCharacter) return;
                m_predictedOwner.TakeOwnership(true);
            }
        }

        [ServerRpc(RunLocally = true)]
        private void SetIsMoving(bool value) => isMoving.Value = value;
    }
}
