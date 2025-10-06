using FishNet.Component.Animating;
using FishNet.Object;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs
{
    public class Book : NetworkBehaviour
    {
        [SerializeField] private Animator animator;

        public void OnPickup(Player.Player player, PlayerPickup playerPickup)
        {
            Debug.Log("Book picked up by " + player.name);
        }

        public void OnInteract(Player.Player player, PlayerPickup playerPickup)
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
            }
            else
            {
                // Closing
                playerPickup.SetObjectPositionAndOrRotation(gameObject, new Vector3(-0.11f, 0f, 0f));
            }
        }
    }
}
