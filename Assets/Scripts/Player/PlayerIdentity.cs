using FishNet.Object.Synchronizing;
using FishNet.Object;
using TMPro;
using UnityEngine;
using RooseLabs.Network;

namespace RooseLabs.Player
{
    public class PlayerIdentity : NetworkBehaviour
    {
        private readonly SyncVar<string> m_playerName = new();

        [SerializeField] private TMP_Text nameText;

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to changes on this SyncVar
            m_playerName.OnChange += OnNameChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe when object is destroyed
            m_playerName.OnChange -= OnNameChanged;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                // Tell the server our chosen name
                SetPlayerName(NetworkConnector.Instance.PlayerName);
            }

            // Set immediately if already populated
            nameText.text = m_playerName.Value;
        }

        [ServerRpc]
        private void SetPlayerName(string newName)
        {
            m_playerName.Value = newName;
        }

        private void OnNameChanged(string prev, string next, bool asServer)
        {
            nameText.text = next;
        }
    }
}
