using FishNet.Object;
using RooseLabs.Network;
using TMPro;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerNameTag : NetworkBehaviour
    {
        [SerializeField] private TMP_Text nameTagText;

        private Transform localCamera;

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
                Debug.LogError("PlayerIdentity could not find PlayerConnection for owner.");
                return;
            }
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
            if (localCamera != null)
            {
                transform.forward = localCamera.forward;
            }
            else if (PlayerCharacter.LocalCharacter != null)
            {
                localCamera = PlayerCharacter.LocalCharacter.Camera.transform;
            }
        }

        private void OnNameChanged(string newName)
        {
            nameTagText.text = newName;
        }
    }
}
