using FishNet.Object;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay.Interactions
{
    public class Book : NetworkBehaviour
    {
        private Logger Logger => Logger.GetLogger("Interactions");

        [SerializeField] private RuneSO rune;
        [SerializeField] private Animator animator;
        private bool hasDropped = false;

        public void OnPickup(PlayerCharacter character, PlayerPickup playerPickup)
        {
            Logger.Info("Book picked up by " + character.name);
            hasDropped = false;
        }

        public void OnInteract(Player.PlayerCharacter character, PlayerPickup playerPickup)
        {
            ToggleBook_ServerRPC(playerPickup);
        }

        [ServerRpc(RequireOwnership = false)]
        private void ToggleBook_ServerRPC(PlayerPickup playerPickup)
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            bool isOpen = animator.GetBool("Open");
            bool newOpenState = !isOpen;

            animator.SetBool("Open", newOpenState);

            if (newOpenState)
            {
                // Opening
                playerPickup.SetObjectPositionAndOrRotation(gameObject, new Vector3(0f, 0.2f, -0.25f), Quaternion.Euler(-116f, -180f, 90f));
                if (rune != null)
                {
                    GameManager.Instance.AddRune(rune);
                    rune = null; // Ensure the rune can only be collected once
                }
            }
            else
            {
                // Closing
                playerPickup.SetObjectPositionAndOrRotation(gameObject, new Vector3(-0.11f, 0f, 0f));
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasDropped) return; // Only emit once per drop

            // Emit "ItemDropped" sound
            var soundEmitter = GetComponent<SoundEmitter>();
            if (soundEmitter != null)
            {
                Logger.Info("[Book] Emitting ItemDropped sound.");
                soundEmitter.RequestEmitFromClient("ItemDropped");
            }

            hasDropped = true;
        }
    }
}
