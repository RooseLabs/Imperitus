using UnityEngine;
using FishNet.Object;

namespace RooseLabs.Gameplay.Spells
{
    public class Lux : SpellBase
    {
        #region Serialized
        [Header("Lux Spell Data")]
        [SerializeField]
        private LuxGlowingOrb glowingOrb;
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

        [Header("Sound Effects")]
        [SerializeField] private string castSoundKey = "Light";
        #endregion

        private void Awake()
        {
            if (!glowingOrb) return;

            // Unparent the orb so it's at the root of the scene
            glowingOrb.transform.SetParent(null, true);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            glowingOrb.Initialize(CasterCharacter, offsetPosition, orbFollowSmoothTime, orbObstacleAvoidanceRadius);
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
            glowingOrb.StartAnimation(transform.position, orbMaterializeDuration, orbLifeDuration, orbFadeOutDuration);
        }

        private void PlayCastSound()
        {
            if (string.IsNullOrEmpty(castSoundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(castSoundKey);
            if (soundType == null) return;

            SoundManager.Instance.PlaySoundLocal(soundType, transform.position);
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
            PlayCastSound();
        }

        private void OnDestroy()
        {
            if (!glowingOrb) return;
            glowingOrb.ScheduleDestruction();
        }
    }
}