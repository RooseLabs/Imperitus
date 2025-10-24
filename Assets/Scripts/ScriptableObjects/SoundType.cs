using UnityEngine;

namespace RooseLabs
{
    [CreateAssetMenu(menuName = "Imperitus/Sound Type", fileName = "NewSoundType")]
    public class SoundType : ScriptableObject
    {
        [Tooltip("Unique key (for debug/tuning). Not network-serialized.")]
        public string key;

        [Tooltip("Max detection radius in meters.")]
        public float radius = 5f;

        [Tooltip("How long the sound exists (seconds).")]
        public float duration = 0.5f;

        [Tooltip("Volume multiplier; can be used to scale radius if desired.")]
        public float volume = 1f;

        [Tooltip("Priority for AI decision (higher = more important).")]
        public int priority = 0;

        // If you later want to add AudioClip or falloff curves, add them here
        // but be aware they shouldn't be passed via RPC.
    }
}
