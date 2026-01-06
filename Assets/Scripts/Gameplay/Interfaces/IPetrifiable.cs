namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Interface for entities that can be petrified (frozen in place with paused animation).
    /// Entities immune to status effects (like Kiwi) should not implement this interface.
    /// </summary>
    public interface IPetrifiable
    {
        /// <summary>
        /// Returns true if currently petrified.
        /// </summary>
        bool IsPetrified { get; }

        /// <summary>
        /// Apply petrify effect: freeze movement and pause animation at current frame.
        /// </summary>
        /// <param name="duration">How long the petrify lasts in seconds.</param>
        void Petrify(float duration);

        /// <summary>
        /// Remove petrify effect and resume normal behavior.
        /// </summary>
        void Unpetrify();
    }
}
