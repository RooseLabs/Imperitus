using FishNet.Object;
using FishNet.Object.Synchronizing;
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

        private readonly SyncVar<int> m_runeIndex = new(-1, new SyncTypeSettings( WritePermission.ClientUnsynchronized));

        public override void OnPickupStart()
        {
            if (!IsOwner) return;
            animator.SetBool("IsOpen", true);
        }

        public override void OnPickupEnd()
        {
            if (!IsOwner) return;
            if (m_runeIndex.Value > -1)
            {
                HolderCharacter.Notebook.CollectRune(m_runeIndex.Value);
                RuneCollected_ServerRPC();
            }
        }

        public override void OnDrop()
        {
            if (!IsOwner) return;
            animator.SetBool("IsOpen", false);
        }

        [ServerRpc(RunLocally = true)]
        private void RuneCollected_ServerRPC()
        {
            m_runeIndex.Value = -1;
        }

        public override string GetInteractionText() => "Open";

        public void SetContainedRune(RuneSO rune)
        {
            m_runeIndex.Value = GameManager.Instance.RuneDatabase.IndexOf(rune);
        }
    }
}
