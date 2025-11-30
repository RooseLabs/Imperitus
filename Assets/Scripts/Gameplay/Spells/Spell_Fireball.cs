using FishNet.Object;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Fireball : SpellBase
    {
        #region Serialized
        [Header("Fireball Spell Data")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private GameObject vfxGameObject;
        #endregion

        protected override void OnStartCast()
        {
            base.OnStartCast();
            ToggleCastVFX(true);
        }

        protected override void OnCancelCast()
        {
            base.OnCancelCast();
            ToggleCastVFX(false);
        }

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            Vector3 targetPoint = CasterCharacter.Camera.transform.position + CasterCharacter.Data.lookDirection * 100f;
            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - transform.position).normalized;

            if (IsServerInitialized)
            {
                LaunchProjectile_ObserversRpc(direction);
            }
            else
            {
                LaunchProjectile_ServerRpc(direction);
                LaunchProjectile(direction);
            }
            return true;
        }

        private void Update()
        {
            if (!vfxGameObject) return;
            if (!CasterCharacter) return;
            if (CasterCharacter != PlayerCharacter.LocalCharacter)
                vfxGameObject.transform.rotation = Quaternion.LookRotation(CasterCharacter.ModelTransform.forward);
            else
                vfxGameObject.transform.rotation = Quaternion.LookRotation(CasterCharacter.Data.lookDirection);
        }

        private void ToggleCastVFX(bool enable)
        {
            if (!vfxGameObject) return;
            if (IsServerInitialized)
            {
                ToggleCastVFX_ObserversRpc(enable);
            }
            else
            {
                ToggleCastVFX_ServerRpc(enable);
                ToggleCastVFX_Internal(enable);
            }
        }

        private void ToggleCastVFX_Internal(bool enable)
        {
            // If we're enabling but it's already active, disable it first to restart the effect
            if (enable && vfxGameObject.activeSelf)
                vfxGameObject.SetActive(false);
            vfxGameObject.SetActive(enable);
        }

        private void LaunchProjectile(Vector3 direction)
        {
            if (!CasterCharacter) return;
            var pGo = Instantiate(projectilePrefab, transform.position, Quaternion.LookRotation(direction));
            if (pGo.TryGetComponent(out Projectile projectile))
            {
                DamageInfo damageInfo = new(damage, CasterCharacter.gameObject.transform);
                projectile.Launch(direction * projectileSpeed, damageInfo);
            }
            else
            {
                Logger.Warning($"[Fireball] Projectile prefab {projectilePrefab.name} is missing a Projectile component.");
                Destroy(pGo);
            }
        }

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void ToggleCastVFX_ServerRpc(bool enable)
        {
            ToggleCastVFX_ObserversRpc(enable);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void ToggleCastVFX_ObserversRpc(bool enable)
        {
            ToggleCastVFX_Internal(enable);
        }

        [ServerRpc(RequireOwnership = true)]
        private void LaunchProjectile_ServerRpc(Vector3 direction)
        {
            LaunchProjectile_ObserversRpc(direction);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void LaunchProjectile_ObserversRpc(Vector3 direction)
        {
            LaunchProjectile(direction);
        }
        #endregion

        protected override void ResetData()
        {
            base.ResetData();
            vfxGameObject?.SetActive(false);
        }
    }
}
