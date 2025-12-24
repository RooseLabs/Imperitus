using System.Collections;
using UnityEngine;
using FishNet.Object;
using RooseLabs.Player;
using RooseLabs.Utils;

namespace RooseLabs.Gameplay.Spells
{
    public class Lux : SpellBase
    {
        #region Serialized
        [Header("Lux Spell Data")]
        [SerializeField]
        private GameObject glowingOrb;
        [SerializeField, Tooltip("Local-space offset from the caster's position; rotated by look direction or model rotation.")]
        private Vector3 offsetPosition;
        [SerializeField, Tooltip("Time in seconds for the orb to fully materialize (scale and fade in) after being spawned.")]
        private float orbMaterializeDuration = 1f;
        [SerializeField, Tooltip("Total time in seconds the orb remains alive.")]
        private float orbLifeDuration = 120f;
        [SerializeField, Tooltip("Smoothing time for the orb to follow the caster's position.")]
        private float orbFollowSmoothTime = 0.5f;
        [SerializeField, Tooltip("Minimum distance to keep from obstacles when following the caster.")]
        private float orbObstacleAvoidanceRadius = 0.5f;
        [SerializeField, Tooltip("Number of seconds before the orb dies out at which fading out begins.")]
        private float orbFadeOutDuration = 10f;
        #endregion

        private Material m_orbMaterial;
        private Vector3 m_orbOriginalScale;
        private Light m_orbLight;
        private float m_originalLightIntensity;
        private Vector3 m_orbVelocity;
        private bool m_isFollowingCaster = false;

        private static readonly int MainOpacityID = Shader.PropertyToID("_MainOpacity");

        private void Awake()
        {
            if (!glowingOrb) return;
            // Unparent the orb so it's at the root of the scene
            glowingOrb.transform.SetParent(null, true);
            // Create a material instance for the orb to avoid modifying the shared material
            if (glowingOrb.TryGetComponent(out Renderer orbRenderer))
            {
                m_orbMaterial = Instantiate(orbRenderer.sharedMaterial);
                orbRenderer.material = m_orbMaterial;
            }
            m_orbOriginalScale = glowingOrb.transform.localScale;
            glowingOrb.transform.localScale = Vector3.zero;
            glowingOrb.transform.rotation = Quaternion.identity;
            // Obtain the light component and cache its original intensity
            if (glowingOrb.TryGetComponent(out m_orbLight))
            {
                m_originalLightIntensity = m_orbLight.intensity;
                m_orbLight.intensity = 0f;
            }
            glowingOrb.SetActive(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            glowingOrb.gameObject.name = $"LuxGlowingOrb ({CasterCharacter.Player.PlayerName})";
        }

        protected override void OnStartCast()
        {
            base.OnStartCast();
            if (IsServerInitialized)
            {
                ActivateOrb_ObserversRpc();
            }
            else
            {
                ActivateOrb_ServerRpc();
                ActivateOrb();
            }
        }

        private void ActivateOrb()
        {
            if (!glowingOrb) return;
            m_isFollowingCaster = false;
            glowingOrb.SetActive(true);
            glowingOrb.transform.position = transform.position;
            glowingOrb.transform.localScale = Vector3.zero;
            m_orbMaterial.SetFloat(MainOpacityID, 0f);
            StartCoroutine(AnimateOrb());
        }

        private IEnumerator AnimateOrb()
        {
            // Materialize the orb (scale up and fade in)
            float elapsedTime = 0f;
            while (elapsedTime < orbMaterializeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / orbMaterializeDuration;
                glowingOrb.transform.localScale = Vector3.Lerp(Vector3.zero, m_orbOriginalScale, t);
                m_orbMaterial.SetFloat(MainOpacityID, t);
                m_orbLight.intensity = Mathf.Lerp(0f, m_originalLightIntensity, t);
                yield return null;
            }

            m_isFollowingCaster = true;

            // Fade out the orb over its life duration, starting at the last N seconds
            elapsedTime = 0f;
            float fadeOutDuration = Mathf.Clamp(orbFadeOutDuration, 0f, orbLifeDuration);
            float fadeStartTime = orbLifeDuration - fadeOutDuration;
            while (elapsedTime < orbLifeDuration)
            {
                elapsedTime += Time.deltaTime;
                float opacity = 1f;
                float intensity = m_originalLightIntensity;
                if (elapsedTime >= fadeStartTime)
                {
                    float fadeT = Mathf.InverseLerp(fadeStartTime, orbLifeDuration, elapsedTime);
                    opacity = Mathf.Lerp(1f, 0f, fadeT);
                    intensity = Mathf.Lerp(m_originalLightIntensity, 0f, fadeT);
                }
                m_orbMaterial.SetFloat(MainOpacityID, opacity);
                m_orbLight.intensity = intensity;
                yield return null;
            }

            glowingOrb.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!glowingOrb || !CasterCharacter || !m_isFollowingCaster) return;

            Quaternion offsetRotation = CasterCharacter == PlayerCharacter.LocalCharacter
                ? Quaternion.LookRotation(CasterCharacter.Data.lookDirectionFlat)
                : CasterCharacter.ModelTransform.rotation;

            Vector3 rotatedOffset = offsetRotation * offsetPosition;
            Vector3 casterPosition = CasterCharacter.GetBodypart(HumanBodyBones.Hips).Transform.position;
            Vector3 desiredTargetPosition = casterPosition + rotatedOffset;

            // Obstacle avoidance
            Vector3 orbCurrentPosition = glowingOrb.transform.position;
            Vector3 adjustedPosition = desiredTargetPosition;
            const int rayCount = 12;
            float angleStep = 360f / rayCount;
            float avoidDistance = orbObstacleAvoidanceRadius;
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
            glowingOrb.transform.position = Vector3.SmoothDamp(
                orbCurrentPosition, adjustedPosition, ref m_orbVelocity, orbFollowSmoothTime);
        }

        [ServerRpc(RequireOwnership = true)]
        private void ActivateOrb_ServerRpc()
        {
            ActivateOrb_ObserversRpc();
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void ActivateOrb_ObserversRpc()
        {
            ActivateOrb();
        }
    }
}
