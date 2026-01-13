using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class DormitoryDoor : MonoBehaviour, IInteractable
    {
        [SerializeField] private string doorOpenSoundKey = "DoorOpen";

        public bool IsInteractable(PlayerCharacter interactor)
        {
            return (bool)GameManager.Instance && interactor.IsServerInitialized;
        }

        public void Interact(PlayerCharacter interactor)
        {
            // Play door open sound
            PlayDoorSound();

            GameManager.Instance.OnDormitoryDoorInteracted();
        }

        public string GetInteractionText()
        {
            return GameManager.Instance.CurrentAssignment.tasks != null ? "Start Heist" : "Go to class";
        }

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
