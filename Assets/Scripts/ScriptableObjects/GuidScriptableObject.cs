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
            GenerateGuidFromAsset();
        }

        protected virtual void Reset()
        {
            GenerateGuidFromAsset();
        }

        private void GenerateGuidFromAsset()
        {
            UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(this, out string unityGuidStr, out long _);
            if (string.IsNullOrEmpty(unityGuidStr))
            {
                // Failed to retrieve asset GUID.
                return;
            }

            Guid assetGuid = new Guid(unityGuidStr);
            byte[] guidBytes = assetGuid.ToByteArray();
            ulong newLowBytes = BitConverter.ToUInt64(guidBytes, 0);
            ulong newHighBytes = BitConverter.ToUInt64(guidBytes, 8);

            if (lowBytes != newLowBytes || highBytes != newHighBytes)
            {
                lowBytes = newLowBytes;
                highBytes = newHighBytes;
                hashCode = HashCode.Combine(newLowBytes, newHighBytes);

                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        #endif
    }
}
