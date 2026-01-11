using System;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    /// <summary>
    /// Pairs a VFX prefab with its associated sound effect and scale.
    /// </summary>
    [Serializable]
    public struct FailedSpellEffect
    {
        [Tooltip("The particle system VFX prefab to spawn.")]
        public ParticleSystem vfxPrefab;

        [Tooltip("Sound effect key from the SoundDatabase to play with this VFX.")]
        public string soundKey;

        [Tooltip("Scale multiplier for the VFX. If 0 or negative, defaults to 1.")]
        public float scale;
    }
}
