using System;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    public class GuidScriptableObject : ScriptableObject, IEquatable<GuidScriptableObject>
    {
        [SerializeField, HideInInspector] private ulong lowBytes;
        [SerializeField, HideInInspector] private ulong highBytes;
        [SerializeField, HideInInspector] private int hashCode;

        public bool Equals(GuidScriptableObject other)
        {
            return other && lowBytes == other.lowBytes && highBytes == other.highBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is GuidScriptableObject other && Equals(other);
        }

        public override int GetHashCode() => hashCode;

        #if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (lowBytes == 0 && highBytes == 0)
            {
                GenerateNewGuid();
            }
        }

        private void Reset()
        {
            GenerateNewGuid();
        }

        private void GenerateNewGuid()
        {
            Guid newGuid = Guid.NewGuid();
            byte[] guidBytes = newGuid.ToByteArray();
            lowBytes = BitConverter.ToUInt64(guidBytes, 0);
            highBytes = BitConverter.ToUInt64(guidBytes, 8);
            hashCode = HashCode.Combine(lowBytes, highBytes);

            UnityEditor.EditorUtility.SetDirty(this);
        }
        #endif
    }
}
