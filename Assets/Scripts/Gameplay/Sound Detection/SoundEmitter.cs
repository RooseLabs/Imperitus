using FishNet.Object;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

namespace RooseLabs
{
    [RequireComponent(typeof(NetworkObject))]
    public class SoundEmitter : NetworkBehaviour
    {
        [System.Serializable]
        public struct NamedSound
        {
            [Tooltip("The SoundType ScriptableObject defining radius, duration, etc.")]
            public SoundType type;

            public string Key => type != null ? type.key : string.Empty;
        }


        [Tooltip("List of sounds this emitter can produce.")]
        public List<NamedSound> availableSounds = new List<NamedSound>();

        [Tooltip("Optional offset for sound origin (e.g., player feet offset).")]
        public Vector3 soundOriginOffset = Vector3.zero;

        // === PUBLIC INTERFACE ===

        /// <summary>
        /// Server-only method to emit a specific sound by key.
        /// </summary>
        public void EmitServer(string soundKey)
        {
            if (!IsServerInitialized)
                return;

            var soundType = GetSoundType(soundKey);
            if (soundType == null)
            {
                Debug.LogWarning($"{nameof(SoundEmitter)}: No SoundType found for key '{soundKey}' on {gameObject.name}");
                return;
            }

            Vector3 pos = transform.position + soundOriginOffset;
            SoundManager.Instance.EmitSound(soundType, pos, this);
        }

        /// <summary>
        /// Server-only method to emit a specific SoundType directly.
        /// </summary>
        public void EmitServer(SoundType type)
        {
            if (!IsServerInitialized)
                return;

            if (type == null)
            {
                Debug.LogWarning($"{nameof(SoundEmitter)}: Null SoundType on {gameObject.name}");
                return;
            }

            Vector3 pos = transform.position + soundOriginOffset;
            SoundManager.Instance.EmitSound(type, pos, this);
        }

        /// <summary>
        /// Client -> Server request to emit a sound by key.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestEmitServer(string soundKey)
        {
            EmitServer(soundKey);
        }

        /// <summary>
        /// Client -> Server request to emit a sound by index
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestEmitServerIndex(int soundIndex)
        {
            if (soundIndex < 0 || soundIndex >= availableSounds.Count)
            {
                Debug.LogWarning($"{nameof(SoundEmitter)}: Invalid sound index {soundIndex} on {gameObject.name}");
                return;
            }

            var soundType = availableSounds[soundIndex].type;
            EmitServer(soundType);
        }

        /// <summary>
        /// Helper for client-side call.
        /// </summary>
        public void RequestEmitFromClient(string soundKey)
        {
            if (IsClientInitialized)
                RequestEmitServer(soundKey);
            else
                EmitServer(soundKey);
        }

        public void RequestEmitFromClient(int soundIndex)
        {
            if (IsClientInitialized)
                RequestEmitServerIndex(soundIndex);
            else if (soundIndex >= 0 && soundIndex < availableSounds.Count)
                EmitServer(availableSounds[soundIndex].type);
        }

        // === PRIVATE HELPERS ===

        private SoundType GetSoundType(string key)
        {
            for (int i = 0; i < availableSounds.Count; i++)
            {
                if (availableSounds[i].type != null &&
                    availableSounds[i].type.key.Equals(key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return availableSounds[i].type;
                }
            }
            return null;
        }
    }
}
