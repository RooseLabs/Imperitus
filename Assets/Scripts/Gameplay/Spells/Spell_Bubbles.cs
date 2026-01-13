using FishNet.Object;
using RooseLabs.Player;
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
        private bool m_isEmitting = false;

        protected override void OnStartCast()
        {
            if (IsServerInitialized)
            {
                StartParticles_ObserversRpc();
            }
            else
            {
                StartParticles_ServerRpc();
                StartParticles_Internal();
            }
        }

        private void Update()
        {
            if (!CasterCharacter) return;

            // Calculate look direction - use camera look direction for local player, model forward for remote players
            Vector3 lookDirection = (CasterCharacter == PlayerCharacter.LocalCharacter)
                ? CasterCharacter.Data.lookDirection
                : CasterCharacter.ModelTransform.forward;

            // Update particle rotation to follow look direction
            particles.transform.rotation = Quaternion.LookRotation(lookDirection);

            // Emit particles periodically while spell is being sustained (runs on all clients)
            if (m_isEmitting)
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
        }

        protected override void OnCancelCast()
        {
            if (IsServerInitialized)
            {
                StopParticles_ObserversRpc(true);
            }
            else
            {
                StopParticles_ServerRpc(true);
                StopParticles_Internal(true);
            }
        }

        protected override void OnCancelCastSustained()
        {
            if (IsServerInitialized)
            {
                StopParticles_ObserversRpc(false);
            }
            else
            {
                StopParticles_ServerRpc(false);
                StopParticles_Internal(false);
            }
        }

        private void PlayCastSound()
        {
            if (string.IsNullOrEmpty(castSoundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(castSoundKey);
            if (soundType != null)
            {
                SoundManager.Instance.PlaySoundLocal(soundType, transform.position);
            }
        }

        private void StartParticles_Internal()
        {
            particles.time = 0f;
            particles.Play();
            m_timeSinceLastEmit = 0.75f;
            m_isEmitting = true;

            // Play cast sound for the initial bubble
            PlayCastSound();
        }

        private void StopParticles_Internal(bool clearParticles)
        {
            m_isEmitting = false;
            particles.Stop(true, clearParticles ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void StartParticles_ServerRpc()
        {
            StartParticles_ObserversRpc();
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void StartParticles_ObserversRpc()
        {
            StartParticles_Internal();
        }

        [ServerRpc(RequireOwnership = true)]
        private void StopParticles_ServerRpc(bool clearParticles)
        {
            StopParticles_ObserversRpc(clearParticles);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void StopParticles_ObserversRpc(bool clearParticles)
        {
            StopParticles_Internal(clearParticles);
        }
        #endregion
    }
}
