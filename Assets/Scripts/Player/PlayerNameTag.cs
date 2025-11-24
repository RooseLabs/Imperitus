using System.Collections;
using FishNet.Object;
using RooseLabs.Network;
using RooseLabs.Utils;
using TMPro;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerNameTag : NetworkBehaviour
    {
        [SerializeField] private TMP_Text nameTagText;

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                gameObject.SetActive(false);
                return;
            }
            PlayerConnection player = PlayerHandler.GetPlayer(Owner);
            if (player == null)
            {
                StartCoroutine(WaitForPlayerConnection());
                return;
            }
            this.LogInfo("PlayerNameTag found PlayerConnection immediately.");
            if (!string.IsNullOrEmpty(player.PlayerName))
            {
                nameTagText.text = player.PlayerName;
                this.LogInfo($"PlayerNameTag set name to {player.PlayerName}");
            }
            SubscribeToNameChanges(player);
        }

        private IEnumerator WaitForPlayerConnection()
        {
            yield return new WaitUntil(() => (bool)PlayerHandler.GetPlayer(Owner));
            SubscribeToNameChanges(PlayerHandler.GetPlayer(Owner));
        }

        private void SubscribeToNameChanges(PlayerConnection player)
        {
            player.OnNameChanged += OnNameChanged;
        }

        public override void OnStopClient()
        {
            if (IsOwner) return;
            PlayerConnection player = PlayerHandler.GetPlayer(Owner);
            if (player != null)
            {
                player.OnNameChanged -= OnNameChanged;
            }
        }

        private void LateUpdate()
        {
            if (PlayerCharacter.LocalCharacter)
                transform.forward = PlayerCharacter.LocalCharacter.Camera.transform.forward;
        }

        private void OnNameChanged(string newName)
        {
            this.LogInfo($"PlayerNameTag changed to {newName}");
            nameTagText.text = newName;
        }
    }
}
