using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BubbleParticleCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string popSoundKey = "Bubble_Pop";

        private ParticleSystem m_particleSystem;
        private readonly List<ParticleCollisionEvent> m_collisionEvents = new();

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (string.IsNullOrEmpty(popSoundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            // Get collision events to find the hit positions
            int numCollisionEvents = m_particleSystem.GetCollisionEvents(other, m_collisionEvents);
            
            var soundType = SoundManager.Instance.soundDatabase.GetByKey(popSoundKey);
            if (soundType == null) return;

            // Play sound locally for each collision - no need to network since particles simulate independently on each client
            for (int i = 0; i < numCollisionEvents; i++)
            {
                Vector3 hitPosition = m_collisionEvents[i].intersection;
                SoundManager.Instance.PlaySoundLocal(soundType, hitPosition);
            }
        }
    }
}
