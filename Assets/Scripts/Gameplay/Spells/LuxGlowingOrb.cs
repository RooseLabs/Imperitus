using System.Collections;
using UnityEngine;
using RooseLabs.Player;
using RooseLabs.Utils;

namespace RooseLabs.Gameplay.Spells
{
    public class LuxGlowingOrb : MonoBehaviour
    {
        private Material m_orbMaterial;
        private Vector3 m_orbOriginalScale;
        private Light m_orbLight;
        private float m_originalLightIntensity;

        private float m_orbLifetimeElapsed = 0f;
        private bool m_shouldDestroyOrb = false;

        // Configuration for following behavior
        private PlayerCharacter m_targetCharacter;
        private Vector3 m_offsetPosition;
        private Vector3 m_orbVelocity;
        private float m_orbFollowSmoothTime;
        private float m_orbObstacleAvoidanceRadius;
        private bool m_isFollowingCaster = false;

        private static readonly int MainOpacityID = Shader.PropertyToID("_MainOpacity");

        private void Awake()
        {
            // Create a material instance to avoid modifying the shared material
            if (TryGetComponent(out Renderer orbRenderer))
            {
                m_orbMaterial = orbRenderer.material;
            }

            // Cache original scale and reset to zero
            m_orbOriginalScale = transform.localScale;
            transform.localScale = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // Cache light component and its original intensity
            if (TryGetComponent(out m_orbLight))
            {
                m_originalLightIntensity = m_orbLight.intensity;
                m_orbLight.intensity = 0f;
            }

            gameObject.SetActive(false);
        }

        public void Initialize(PlayerCharacter targetCharacter, Vector3 offsetPosition, float orbFollowSmoothTime, float orbObstacleAvoidanceRadius)
        {
            m_targetCharacter = targetCharacter;
            m_offsetPosition = offsetPosition;
            m_orbFollowSmoothTime = orbFollowSmoothTime;
            m_orbObstacleAvoidanceRadius = orbObstacleAvoidanceRadius;
        }

        public void StartAnimation(Vector3 castPosition, float materializeDuration, float lifeDuration, float fadeOutDuration)
        {
            m_isFollowingCaster = false;
            gameObject.SetActive(true);
            m_orbMaterial.SetFloat(MainOpacityID, 0f);
            transform.position = castPosition;
            transform.localScale = Vector3.zero;
            StartCoroutine(AnimateOrb(materializeDuration, lifeDuration, fadeOutDuration));
        }

        public void ScheduleDestruction()
        {
            // If orb is not active but exists, destroy it immediately
            if (!gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            m_shouldDestroyOrb = true;
        }

        private void LateUpdate()
        {
            if (!m_targetCharacter || !m_isFollowingCaster) return;

            Quaternion offsetRotation = m_targetCharacter == PlayerCharacter.LocalCharacter
                ? Quaternion.LookRotation(m_targetCharacter.Data.lookDirectionFlat)
                : m_targetCharacter.ModelTransform.rotation;

            Vector3 rotatedOffset = offsetRotation * m_offsetPosition;
            Vector3 casterPosition = m_targetCharacter.GetBodypart(HumanBodyBones.Hips).Transform.position;
            Vector3 desiredTargetPosition = casterPosition + rotatedOffset;

            // Obstacle avoidance
            Vector3 orbCurrentPosition = transform.position;
            Vector3 adjustedPosition = desiredTargetPosition;
            const int rayCount = 12;
            float angleStep = 360f / rayCount;
            float avoidDistance = m_orbObstacleAvoidanceRadius;
            for (int i = 0; i < rayCount; ++i)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 worldDir = offsetRotation * dir;
                Ray ray = new Ray(orbCurrentPosition, worldDir);
                if (Physics.Raycast(ray, out RaycastHit hit, avoidDistance, HelperFunctions.AllPhysicalLayerMask, QueryTriggerInteraction.Collide))
                {
                    // Push orb away from the hit point
                    Vector3 pushDir = (orbCurrentPosition - hit.point).normalized;
                    adjustedPosition += pushDir * (avoidDistance - hit.distance);
                }
            }

            // Smoothly move the orb to the adjusted position
            transform.position = Vector3.SmoothDamp(orbCurrentPosition, adjustedPosition, ref m_orbVelocity, m_orbFollowSmoothTime);
        }

        private IEnumerator AnimateOrb(float materializeDuration, float lifeDuration, float fadeOutDuration)
        {
            // Materialize the orb (scale up and fade in)
            float elapsedTime = 0f;
            while (elapsedTime < materializeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / materializeDuration;
                transform.localScale = Vector3.Lerp(Vector3.zero, m_orbOriginalScale, t);
                m_orbMaterial.SetFloat(MainOpacityID, t);
                m_orbLight.intensity = Mathf.Lerp(0f, m_originalLightIntensity, t);
                yield return null;
            }
            m_orbMaterial.SetFloat(MainOpacityID, 1f);
            m_orbLight.intensity = m_originalLightIntensity;

            m_isFollowingCaster = true;
            // Fade out the orb over its life duration, starting at the last N seconds
            m_orbLifetimeElapsed = 0f;
            float clampedFadeOutDuration = Mathf.Clamp(fadeOutDuration, 0f, lifeDuration);
            float fadeStartTime = lifeDuration - clampedFadeOutDuration;
            while (m_orbLifetimeElapsed < lifeDuration)
            {
                m_orbLifetimeElapsed += Time.deltaTime;
                if (m_orbLifetimeElapsed >= fadeStartTime)
                {
                    float fadeT = Mathf.InverseLerp(fadeStartTime, lifeDuration, m_orbLifetimeElapsed);
                    m_orbMaterial.SetFloat(MainOpacityID, Mathf.Lerp(1f, 0f, fadeT));
                    m_orbLight.intensity = Mathf.Lerp(m_originalLightIntensity, 0f, fadeT);
                }
                yield return null;
            }

            if (m_shouldDestroyOrb)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
