using RooseLabs.Gameplay.Interactables;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerItems : MonoBehaviour
    {
        [field: SerializeField] public Transform HeldItemPosition { get; private set; }

        private PlayerCharacter m_character;
        private Vector3 m_itemHeldPositionInCameraSpace;
        public Item CurrentHeldItem { get; private set; }

        // Perlin noise parameters
        private float m_noiseTime = 0f;
        private const float NoiseFrequency = 1f;    // Speed of noise changes
        private const float NoiseAmplitude = 0.03f; // Magnitude of noise

        private void Awake()
        {
            TryGetComponent(out m_character);
            m_itemHeldPositionInCameraSpace = m_character.Camera.transform.InverseTransformPoint(HeldItemPosition.position);
        }

        private void LateUpdate()
        {
            if (!m_character.IsOwner) return;
            UpdateHeldItemPosition();
        }

        private void UpdateHeldItemPosition()
        {
            // if (!CurrentHeldItem) return;
            const float maxPitchAngle = 15f;
            Transform cam = m_character.Camera.transform;
            Vector3 camEuler = cam.eulerAngles;

            // Update noise time for smooth Perlin animation
            m_noiseTime += Time.deltaTime * NoiseFrequency;

            // Generate minor Perlin noise offsets in local camera space
            float noiseX = (Mathf.PerlinNoise(m_noiseTime, 0f) - 0.5f) * 2f * NoiseAmplitude;
            float noiseY = (Mathf.PerlinNoise(m_noiseTime + 10f, 0f) - 0.5f) * 2f * NoiseAmplitude;
            float noiseZ = (Mathf.PerlinNoise(m_noiseTime + 20f, 0f) - 0.5f) * 2f * NoiseAmplitude;

            Vector3 noisyLocalPos = m_itemHeldPositionInCameraSpace + new Vector3(noiseX, noiseY, noiseZ);

            // Normalize pitch to -180 to 180 range
            float pitch = camEuler.x;
            pitch = (pitch > 180f) ? pitch - 360f : pitch;

            // Clamp pitch within threshold
            float clampedPitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

            // Apply clamped pitch to Euler angles
            Vector3 clampedEuler = camEuler;
            clampedEuler.x = clampedPitch;
            Quaternion clampedRotation = Quaternion.Euler(clampedEuler);

            Vector3 targetPosition = cam.position + clampedRotation * noisyLocalPos;
            Quaternion targetRotation = Quaternion.LookRotation(cam.position - targetPosition);
            float t = 20f * Time.deltaTime;

            HeldItemPosition.position = Vector3.Lerp(
                HeldItemPosition.position,
                targetPosition,
                t
            );
            HeldItemPosition.rotation = Quaternion.Slerp(
                HeldItemPosition.rotation,
                targetRotation,
                t
            );
        }
    }
}
