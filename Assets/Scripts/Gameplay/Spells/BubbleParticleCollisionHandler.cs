using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    [RequireComponent(typeof(ParticleSystem))]
    public class BubbleParticleCollisionHandler : MonoBehaviour
    {
        [SerializeField] private string popSoundKey = "Bubble_Pop";

        private ParticleSystem m_particleSystem;
        private Bubbles m_bubblesSpell;
        private readonly List<ParticleCollisionEvent> m_collisionEvents = new();

        private void Awake()
        {
            m_particleSystem = GetComponent<ParticleSystem>();
            // Find the Bubbles spell in parent hierarchy
            m_bubblesSpell = GetComponentInParent<Bubbles>();
        }

        private void OnParticleCollision(GameObject other)
        {
            if (string.IsNullOrEmpty(popSoundKey)) return;

            // Get collision events to find the hit positions
            int numCollisionEvents = m_particleSystem.GetCollisionEvents(other, m_collisionEvents);
            
            for (int i = 0; i < numCollisionEvents; i++)
            {
                Vector3 hitPosition = m_collisionEvents[i].intersection;
                
                // Use the spell to broadcast the sound to all players
                if (m_bubblesSpell != null)
                {
                    m_bubblesSpell.PlayPopSound(hitPosition);
                }
                else
                {
                    // Fallback to local-only if spell reference not found
                    if (SoundManager.Instance != null && SoundManager.Instance.soundDatabase != null)
                    {
                        var soundType = SoundManager.Instance.soundDatabase.GetByKey(popSoundKey);
                        if (soundType != null)
                        {
                            SoundManager.Instance.PlaySoundLocal(soundType, hitPosition);
                        }
                    }
                }
            }
        }
    }
}
