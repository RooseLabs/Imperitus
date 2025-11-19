using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class DormitoryDoor : MonoBehaviour, IInteractable
    {
        public bool IsInteractable(PlayerCharacter interactor) => true;

        public void Interact(PlayerCharacter interactor)
        {
            if (interactor.IsServerInitialized)
                GameManager.Instance.StartHeist();
        }

        public string GetInteractionText() => "Start Heist";
    }
}
