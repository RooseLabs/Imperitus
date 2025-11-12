using FishNet.Object;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class Book : Item
    {
        #region Serialized
        [Header("Book Data")]
        [SerializeField] private RuneSO rune;
        [SerializeField] private Animator animator;
        #endregion

        public override void OnPickupStart()
        {
            if (!IsOwner) return;
            animator.SetBool("IsOpen", true);
        }

        public override void OnPickupEnd()
        {
            if (!IsOwner) return;
            if (rune)
            {
                HolderCharacter.Notebook.AddRune(rune);
                if (IsServerInitialized) RuneCollected_ObserversRPC();
                else RuneCollected_ServerRPC();
            }
        }

        public override void OnDrop()
        {
            if (!IsOwner) return;
            animator.SetBool("IsOpen", false);
        }

        [ServerRpc]
        private void RuneCollected_ServerRPC()
        {
            RuneCollected_ObserversRPC();
        }

        [ObserversRpc]
        private void RuneCollected_ObserversRPC()
        {
            rune = null;
        }

        public override string GetInteractionText() => "Open";
    }
}
