using System;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [Serializable]
    public struct DamageInfo
    {
        public float amount;
        public Vector3 position;
        public Transform source;

        public DamageInfo(float amount, Transform source = null) : this(amount, Vector3.zero, source) { }

        public DamageInfo(float amount, Vector3 position, Transform source = null)
        {
            this.amount = amount;
            this.position = position;
            this.source = source;
        }
    }
}
