using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class DormitoryDoor : MonoBehaviour, IInteractable
    {
        public bool IsInteractable(PlayerCharacter interactor)
        {
            return (bool)GameManager.Instance && interactor.IsServerInitialized;
        }

        public void Interact(PlayerCharacter interactor)
        {
            GameManager.Instance.OnDormitoryDoorInteracted();
        }

        public string GetInteractionText()
        {
            return GameManager.Instance.CurrentAssignment != null ? "Start Heist" : "Go to class";
        }
    }
}
