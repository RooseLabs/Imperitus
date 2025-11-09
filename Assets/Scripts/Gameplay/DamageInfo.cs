using UnityEngine;

namespace RooseLabs.Gameplay
{
    public enum DamageType
    {
        Melee
    }

    public struct DamageInfo
    {
        public int Amount;
        public DamageType Type;
        public Transform Source; // The attacker
        public Vector3 Position; // Optional: point of impact

        public DamageInfo(int amount, DamageType type, Transform source, Vector3 position)
        {
            Amount = amount;
            Type = type;
            Source = source;
            Position = position;
        }
    }
}
