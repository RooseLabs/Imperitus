using RooseLabs.Player;

namespace RooseLabs.Gameplay.Interactables
{
    public interface IInteractable
    {
        bool IsInteractable(PlayerCharacter interactor);
        void Interact(PlayerCharacter interactor);
        string GetInteractionText();
    }
}
