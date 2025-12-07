using System;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [Serializable]
    public struct DamageInfo
    {
        public float amount;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
        public Transform source;

        public DamageInfo(float amount, Transform source = null) : this(amount, Vector3.zero, Vector3.zero, source) { }

        public DamageInfo(float amount, Vector3 hitPoint, Vector3 hitDirection, Transform source = null)
        {
            this.amount = amount;
            this.hitPoint = hitPoint;
            this.hitDirection = hitDirection;
            this.source = source;
        }
    }
}
