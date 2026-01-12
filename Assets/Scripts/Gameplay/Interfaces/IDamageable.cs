namespace RooseLabs.Gameplay
{
    public interface IDamageable
    {
        /// <summary>
        /// Applies damage to this object.
        /// Returns true if damage was actually applied (e.g., target alive, in range, not invincible).
        /// </summary>
        bool ApplyDamage(DamageInfo damage);
    }
}
