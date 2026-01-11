using System;
using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class FailedSpell : SpellBase
    {
        [Serializable]
        private struct FailedSpellEffect
        {
            [Tooltip("The particle system VFX prefab to spawn.")]
            public ParticleSystem vfxPrefab;

            [Tooltip("Sound effect key from the SoundDatabase to play with this VFX.")]
            public string soundKey;

            [Tooltip("Scale multiplier for the VFX. If 0 or negative, defaults to 1.")]
            public float scale;
        }

        #region Serialized
        [Header("Failed Spell Effects")]
        [SerializeField, Tooltip("List of VFX/SFX effects to randomly choose from when the failed spell is cast.")]
        private FailedSpellEffect[] effects;

        [SerializeField, Tooltip("Local-space offset from the wand's cast point for VFX spawn position.")]
        private Vector3 spawnOffset = Vector3.zero;
        #endregion

        protected override bool OnCastFinished()
        {
            if (effects == null || effects.Length == 0)
            {
                return base.OnCastFinished();
            }

            int randomIndex = UnityEngine.Random.Range(0, effects.Length);

            if (IsServerInitialized)
            {
                PlayRandomEffect_ObserversRpc(randomIndex);
            }
            else
            {
                PlayRandomEffect_ServerRpc(randomIndex);
                PlayEffect(randomIndex);
            }

            return base.OnCastFinished();
        }

        private void PlayEffect(int effectIndex)
        {
            if (effects == null || effectIndex < 0 || effectIndex >= effects.Length) return;

            FailedSpellEffect effect = effects[effectIndex];

            Vector3 spawnPosition = transform.position + transform.TransformDirection(spawnOffset);

            // Play VFX
            if (effect.vfxPrefab != null)
            {
                ParticleSystem vfxInstance = Instantiate(effect.vfxPrefab, spawnPosition, transform.rotation);
                float effectScale = effect.scale <= 0f ? 1f : effect.scale;
                vfxInstance.transform.localScale = Vector3.one * effectScale;
                vfxInstance.Play();

                // Destroy the VFX after it finishes playing
                float duration = vfxInstance.main.duration + vfxInstance.main.startLifetime.constantMax;
                Destroy(vfxInstance.gameObject, duration);
            }

            // Play SFX
            PlaySound(effect.soundKey, spawnPosition);
        }

        private void PlaySound(string soundKey, Vector3 position)
        {
            if (string.IsNullOrEmpty(soundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(soundKey);
            if (soundType == null) return;

            SoundManager.Instance.PlaySoundLocal(soundType, position);
        }

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void PlayRandomEffect_ServerRpc(int effectIndex)
        {
            PlayRandomEffect_ObserversRpc(effectIndex);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void PlayRandomEffect_ObserversRpc(int effectIndex)
        {
            PlayEffect(effectIndex);
        }
        #endregion
    }
}
