using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Bubbles : SpellBase
    {
        #region Serialized
        [Header("Bubbles Spell Data")]
        [SerializeField] private ParticleSystem particles;

        [Header("Sound Effects")]
        [SerializeField] private string castSoundKey = "Bubble_Cast";
        [SerializeField] private string popSoundKey = "Bubble_Pop";
        #endregion

        private float m_timeSinceLastEmit = 0f;
        private SoundEmitter m_soundEmitter;

        protected override void OnStartCast()
        {
            particles.time = 0f;
            particles.Play();
            m_timeSinceLastEmit = 0.75f;

            // Play cast sound for the initial bubble
            PlayCastSound();
        }

        protected override void OnContinueCastSustained()
        {
            m_timeSinceLastEmit += Time.deltaTime;
            if (m_timeSinceLastEmit >= 0.85f)
            {
                particles.Emit(1);
                m_timeSinceLastEmit = 0f;

                // Play cast sound for each new bubble
                PlayCastSound();
            }
        }

        private void Update()
        {
            if (!CasterCharacter) return;
            particles.transform.rotation = Quaternion.LookRotation(CasterCharacter.Data.lookDirection);
        }

        protected override void OnCancelCast()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        protected override void OnCancelCastSustained()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void PlayCastSound()
        {
            if (CasterCharacter == null) return;

            if (m_soundEmitter == null)
            {
                m_soundEmitter = CasterCharacter.GetComponent<SoundEmitter>();
            }

            if (!string.IsNullOrEmpty(castSoundKey))
            {
                m_soundEmitter?.RequestEmitFromClient(castSoundKey);
            }
        }

        /// <summary>
        /// Called by BubbleParticleCollisionHandler when a bubble pops.
        /// Broadcasts the sound to all players.
        /// </summary>
        public void PlayPopSound(Vector3 position)
        {
            if (string.IsNullOrEmpty(popSoundKey)) return;

            if (IsServerInitialized)
            {
                PlayPopSound_ObserversRpc(position);
            }
            else
            {
                PlayPopSound_ServerRpc(position);
                PlayPopSoundLocal(position);
            }
        }

        private void PlayPopSoundLocal(Vector3 position)
        {
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(popSoundKey);
            if (soundType != null)
            {
                SoundManager.Instance.PlaySoundLocal(soundType, position);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void PlayPopSound_ServerRpc(Vector3 position)
        {
            PlayPopSound_ObserversRpc(position);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void PlayPopSound_ObserversRpc(Vector3 position)
        {
            PlayPopSoundLocal(position);
        }
    }
}
