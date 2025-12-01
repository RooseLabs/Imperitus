using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewSoundType", menuName = "Imperitus/Sound Type")]
    public class SoundType : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Unique key (for debug/tuning). Used for lookups.")]
        public string key;

        [Header("Detection (Gameplay)")]
        [Tooltip("Max detection radius in meters for gameplay systems (AI, etc.)")]
        public float radius = 5f;

        [Tooltip("How long the sound exists for gameplay purposes (seconds).")]
        public float duration = 0.5f;

        [Tooltip("Priority for AI decision (higher = more important).")]
        public int priority = 0;

        [Header("Audio Playback")]
        [Tooltip("Audio clips to play (one chosen randomly if multiple)")]
        public AudioClip[] audioClips;

        [Tooltip("Base volume for this sound type (0-1)")]
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip("Pitch variation range (e.g., 0.1 = ±10% pitch randomization)")]
        [Range(0f, 0.5f)]
        public float pitchVariation = 0.05f;

        [Header("3D Audio Settings")]
        [Tooltip("Use 3D spatial audio? If false, plays as 2D (UI sounds, music, etc.)")]
        public bool is3D = true;

        [Tooltip("Minimum distance where volume is at maximum")]
        public float minDistance = 1f;

        [Tooltip("Maximum distance where sound is audible")]
        public float maxDistance = 50f;

        [Tooltip("How volume falls off with distance")]
        public AudioRolloffMode rolloffMode = AudioRolloffMode.Custom;

        [Tooltip("Custom rolloff curve (only used if rolloffMode is Custom)")]
        public AnimationCurve customRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Occlusion")]
        [Tooltip("Enable audio occlusion (muffling through walls)?")]
        public bool enableOcclusion = true;

        [Tooltip("Low-pass filter cutoff frequency when fully occluded (Hz). Lower = more muffled.")]
        [Range(500f, 22000f)]
        public float occludedCutoffFrequency = 800f;

        [Tooltip("Volume reduction when fully occluded (0 = silent, 1 = no reduction)")]
        [Range(0f, 1f)]
        public float occludedVolumeMultiplier = 0.3f;

        [Header("Advanced")]
        [Tooltip("Spatial blend override (0 = 2D, 1 = 3D). Leave at 1 if is3D is true.")]
        [Range(0f, 1f)]
        public float spatialBlend = 1f;

        [Tooltip("Doppler effect strength (0 = none, 1 = standard)")]
        [Range(0f, 5f)]
        public float dopplerLevel = 1f;

        [Tooltip("Audio mixer group (optional)")]
        public UnityEngine.Audio.AudioMixerGroup mixerGroup;

        /// <summary>
        /// Get a random audio clip from the array.
        /// </summary>
        public AudioClip GetRandomClip()
        {
            if (audioClips == null || audioClips.Length == 0)
                return null;
            return audioClips[Random.Range(0, audioClips.Length)];
        }

        /// <summary>
        /// Evaluate volume at a given distance.
        /// </summary>
        public float EvaluateVolumeAtDistance(float distance)
        {
            if (!is3D) return volume;

            if (distance <= minDistance)
                return volume;

            if (distance >= maxDistance)
                return 0f;

            float normalizedDistance = (distance - minDistance) / (maxDistance - minDistance);

            switch (rolloffMode)
            {
                case AudioRolloffMode.Logarithmic:  
                    return volume * (1f / (1f + normalizedDistance * 10f));

                case AudioRolloffMode.Linear:
                    return volume * (1f - normalizedDistance);

                case AudioRolloffMode.Custom:
                    return volume * customRolloff.Evaluate(normalizedDistance);

                default:
                    return volume;
            }
        }
    }
}