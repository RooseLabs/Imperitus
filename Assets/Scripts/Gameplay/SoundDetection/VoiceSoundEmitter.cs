using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// Attach this to each player GameObject that has a voice-reactive sphere collider.
    /// Periodically checks for listeners within the sphere and notifies them via SoundManager.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class VoiceSoundEmitter : MonoBehaviour
    {
        [Header("Voice Detection Settings")]
        [Tooltip("How often to check for listeners and emit voice detection (in seconds)")]
        public float emissionInterval = 0.15f;

        [Tooltip("The sphere collider that scales with voice input")]
        private SphereCollider voiceSphereCollider;

        private float nextEmissionTime;

        private void Awake()
        {
            voiceSphereCollider = GetComponent<SphereCollider>();

            if (voiceSphereCollider == null)
            {
                Debug.LogError($"[VoiceSoundEmitter] No SphereCollider found on {gameObject.name}");
                enabled = false;
                return;
            }

            if (!voiceSphereCollider.isTrigger)
            {
                Debug.LogWarning($"[VoiceSoundEmitter] SphereCollider on {gameObject.name} is not a trigger. Setting it to trigger.");
                voiceSphereCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (SoundManager.Instance == null) return;
            //if (Time.time < nextEmissionTime) return;

            nextEmissionTime = Time.time + emissionInterval;

            // Get the actual world-space radius of the sphere collider
            float currentRadius = voiceSphereCollider.bounds.extents.x;

            if (currentRadius <= 0.01f) return; // Skip if sphere is essentially collapsed

            // Notify the SoundManager to check for listeners
            SoundManager.Instance.EmitVoiceSound(transform.position, currentRadius, this);
        }
    }
}