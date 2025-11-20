using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerConnection : NetworkBehaviour
    {
        public static PlayerConnection LocalPlayer;

        public PlayerCharacter Character => PlayerHandler.GetCharacter(Owner);

        [SerializeField] private NetworkObject playerPrefab;

        public static string Nickname;
        private readonly SyncVar<string> m_playerName = new(new SyncTypeSettings(
            WritePermission.ClientUnsynchronized, ReadPermission.ExcludeOwner)
        );

        public string PlayerName => m_playerName.Value;

        public event Action<string> OnNameChanged = delegate { };

        private void OnEnable()
        {
            m_playerName.OnChange += PlayerName_OnChange;
        }

        private void OnDisable()
        {
            m_playerName.OnChange -= PlayerName_OnChange;
        }

        public override void OnStartNetwork()
        {
            PlayerHandler.RegisterPlayer(Owner, this);
        }

        public override void OnStopNetwork()
        {
            PlayerHandler.UnregisterPlayer(Owner);
            PlayerHandler.UnregisterCharacter(Owner);
        }

        public override void OnStartServer()
        {
            // Spawn this player's character at this object's position and rotation
            NetworkObject playerObject = NetworkManager.GetPooledInstantiated(playerPrefab, transform.position, transform.rotation, asServer: true);
            Spawn(playerObject, Owner, gameObject.scene);
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                LocalPlayer = this;
                SetPlayerName(Nickname);
            }
        }

        [ServerRpc(RunLocally = true)]
        private void SetPlayerName(string newName)
        {
            m_playerName.Value = newName;
            name = $"Player ({newName})";
        }

        private void PlayerName_OnChange(string prev, string next, bool asServer)
        {
            OnNameChanged.Invoke(next);
        }
    }
}
