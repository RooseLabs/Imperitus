using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactions
{
    public class Door : NetworkBehaviour
    {
        private readonly SyncVar<bool> isOpen = new();

        private Animator m_animator;

        private void Awake()
        {
            m_animator = GetComponent<Animator>();

            // Subscribe to changes (this fires on all clients when the server updates isOpen)
            isOpen.OnChange += OnDoorStateChanged;
        }

        // Called when player tries to interact
        public void TryToggleDoor()
        {
            // Clients send a request to server
            if (IsClientInitialized)
            {
                CmdToggleDoor();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void CmdToggleDoor()
        {
            isOpen.Value = !isOpen.Value; // Server flips state
        }

        // Runs automatically when isOpen changes (on all clients and server)
        private void OnDoorStateChanged(bool oldValue, bool newValue, bool asServer)
        {
            m_animator.SetBool("IsOpen", newValue);
        }
    }
}
