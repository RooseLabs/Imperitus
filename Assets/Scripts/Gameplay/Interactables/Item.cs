using System.Collections;
using FishNet.Object;
using RooseLabs.Player;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay.Interactables
{
    public enum ItemState
    {
        Ground,
        Held
    }

    public class Item : Draggable, IInteractable
    {
        private static Logger Logger => Logger.GetLogger("Interactions");

        public ItemState State { get; private set; } = ItemState.Ground;

        public PlayerCharacter HolderCharacter { get; private set; }

        private void SetState(ItemState newState, PlayerCharacter character = null)
        {
            State = newState;
            switch (newState)
            {
                case ItemState.Ground:
                    Collider.enabled = true;
                    rb.isKinematic = false;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    break;
                case ItemState.Held:
                    HolderCharacter = character;
                    Collider.enabled = false;
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    break;
            }
        }

        // private bool m_hasDropped = false;
        // private void OnCollisionEnter(Collision collision)
        // {
        //     if (m_hasDropped) return; // Only emit once per drop
        //
        //     // Emit "ItemDropped" sound
        //     var soundEmitter = GetComponent<SoundEmitter>();
        //     if (soundEmitter != null)
        //     {
        //         Logger.Info("[Book] Emitting ItemDropped sound.");
        //         soundEmitter.RequestEmitFromClient("ItemDropped");
        //     }
        //
        //     m_hasDropped = true;
        // }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPickup(PlayerCharacter character)
        {
            if (State == ItemState.Held) return;
            GiveOwnership(character.Owner);
            PickupAccepted_ObserversRPC(character);
        }

        [ObserversRpc]
        private void PickupAccepted_ObserversRPC(PlayerCharacter character)
        {
            SetState(ItemState.Held, character);
            transform.SetParent(character.Items.HeldItemPosition);
            if (IsOwner)
            {
                StartCoroutine(DoPickupSequence());
            }
        }

        private IEnumerator DoPickupSequence()
        {
            OnPickupStart();
            Vector3 startPosition = transform.localPosition;
            Quaternion startRotation = transform.localRotation;
            Vector3 targetPosition = Vector3.zero;
            Quaternion targetRotation = Quaternion.identity;
            float elapsedTime = 0f;
            const float duration = 1f;
            while (elapsedTime < duration)
            {
                float t = elapsedTime / duration;
                transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;
            OnPickupEnd();
        }

        public virtual void OnPickupStart() {}
        public virtual void OnPickupEnd() {}

        public override bool IsDraggable => State == ItemState.Ground;

        public virtual bool IsInteractable(PlayerCharacter interactor) => State == ItemState.Ground;

        public virtual void Interact(PlayerCharacter interactor)
        {
            RequestPickup(interactor);
        }

        public virtual string GetInteractionText() => string.Empty;
    }
}
