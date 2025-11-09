using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewSoundType", menuName = "Imperitus/Sound Type")]
    public class SoundType : ScriptableObject
    {
        [Tooltip("Unique key (for debug/tuning). Not network-serialized.")]
        public string key;

        [Tooltip("Max detection radius in meters.")]
        public float radius = 5f;

        [Tooltip("How long the sound exists (seconds).")]
        public float duration = 0.5f;

        [Tooltip("Priority for AI decision (higher = more important). (UNUSED FOR NOW)")]
        public int priority = 0;
    }
}
