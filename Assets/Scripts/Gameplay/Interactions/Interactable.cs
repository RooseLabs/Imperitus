using FishNet.Object;

namespace RooseLabs.Gameplay.Interactions
{
    public abstract class Interactable : NetworkBehaviour
    {
        /// <summary>
        /// Called when the player interacts with this object.
        /// </summary>
        /// <param name="player">The player who is interacting.</param>
        public abstract void Interact(Player.Player player);
    }
}
