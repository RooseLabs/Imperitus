using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs
{
    public class Book : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        public void OnPickup(Player.Player player, PlayerPickup playerPickup)
        {
            Debug.Log("Book picked up by " + player.name);
        }

        public void OnInteract(Player.Player player, PlayerPickup playerPickup)
        {
            if (animator != null)
            {
                // Get the current state
                bool isOpen = animator.GetBool("Open");
                // Toggle the state
                bool newOpenState = !isOpen;
                animator.SetBool("Open", newOpenState);

                // Set position based on the new state
                if (newOpenState)
                {
                    // Book is opening
                    playerPickup.SetObjectPositionAndOrRotation(gameObject, new Vector3(0f,0.2f,-0.25f), Quaternion.Euler(-116f,-180f,90f));
                }
                else
                {
                    // Book is closing, set to a different position
                    playerPickup.SetObjectPositionAndOrRotation(gameObject, new Vector3(-0.11f, 0f, 0f));
                }
            }
        }
    }
}
