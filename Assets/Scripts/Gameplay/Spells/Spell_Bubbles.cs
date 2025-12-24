using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Bubbles : SpellBase
    {
        #region Serialized
        [Header("Bubbles Spell Data")]
        [SerializeField] private ParticleSystem particles;
        #endregion

        private float m_timeSinceLastEmit = 0f;

        protected override void OnStartCast()
        {
            particles.time = 0f;
            particles.Play();
            m_timeSinceLastEmit = 0.75f;
        }

        protected override void OnContinueCastSustained()
        {
            m_timeSinceLastEmit += Time.deltaTime;
            if (m_timeSinceLastEmit >= 0.85f)
            {
                particles.transform.rotation = Quaternion.LookRotation(CasterCharacter.Data.lookDirection);
                particles.Emit(1);
                m_timeSinceLastEmit = 0f;
            }
        }

        protected override void OnCancelCast()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        protected override void OnCancelCastSustained()
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
