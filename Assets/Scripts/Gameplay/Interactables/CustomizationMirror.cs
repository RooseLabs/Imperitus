using RooseLabs.Player;
using RooseLabs.UI;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class CustomizationMirror : MonoBehaviour, IInteractable
    {
        public bool IsInteractable(PlayerCharacter interactor) => true;

        public void Interact(PlayerCharacter interactor)
        {
            GUIManager.Instance.OpenCustomizationMenu();
        }

        public string GetInteractionText() => "Customize Appearance";
    }
}
