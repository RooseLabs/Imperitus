using RooseLabs.Network;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class ReturnToLobbyInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string doorOpenSoundKey = "DoorOpen";

        private bool AreAllAliveCharactersNearby(float radius = 20f)
        {
            foreach (var character in PlayerHandler.AllConnectedCharacters)
            {
                if (character.Data.isDead) continue;
                float distance = Vector3.Distance(transform.position, character.transform.position);
                if (distance > radius) return false;
            }
            return true;
        }

        public bool IsInteractable(PlayerCharacter interactor) => true;

        public void Interact(PlayerCharacter interactor)
        {
            if (AreAllAliveCharactersNearby())
            {
                // Play door open sound
                PlayDoorSound();

                GameManager.Instance.ReturnToLobby();
            }
        }

        public string GetInteractionText() => AreAllAliveCharactersNearby() ? "Return to Dormitory" : "All players must be nearby to return";

        private void PlayDoorSound()
        {
            if (string.IsNullOrEmpty(doorOpenSoundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(doorOpenSoundKey);
            if (soundType != null)
            {
                SoundManager.Instance.PlaySoundLocal(soundType, transform.position);
            }
        }
    }
}
