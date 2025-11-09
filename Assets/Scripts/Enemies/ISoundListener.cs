using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs
{
    public interface ISoundListener
    {
        /// <summary>
        /// Called server-side when a validated sound is heard by this listener.
        /// </summary>
        /// <param name="position">World position of the sound source</param>
        /// <param name="type">SoundType ScriptableObject for tuning data</param>
        /// <param name="intensity">A computed intensity (0..1) after falloff/occlusion</param>
        void OnSoundHeard(Vector3 position, SoundType type, float intensity);
    }
}
