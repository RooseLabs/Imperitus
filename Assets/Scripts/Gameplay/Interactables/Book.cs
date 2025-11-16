using FishNet.Object;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class Book : Item, IRuneContainer
    {
        #region Serialized
        [Header("Book Data")]
        [SerializeField] private Animator animator;
        #endregion

        private RuneSO m_rune;

        public override void OnPickupStart()
        {
            if (!IsOwner) return;
            animator.SetBool("IsOpen", true);
        }

        public override void OnPickupEnd()
        {
            if (!IsOwner) return;
            if (m_rune)
            {
                HolderCharacter.Notebook.AddRune(m_rune);
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
            m_rune = null;
        }

        public override string GetInteractionText() => "Open";

        public void SetContainedRune(RuneSO rune)
        {
            m_rune = rune;
        }
    }
}
