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
        public Transform Source;
        public Vector3 Position;
        public string SourceName;

        public DamageInfo(int amount, DamageType type, Transform source, Vector3 position, string sourceName = null)
        {
            Amount = amount;
            Type = type;
            Source = source;
            Position = position;
            SourceName = sourceName;
        }
    }
}
