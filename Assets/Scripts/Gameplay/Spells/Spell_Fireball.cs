using FishNet.Object;
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
            if (!vfxGameObject) return;
            vfxGameObject.transform.rotation = Quaternion.LookRotation(OwnerCharacter.Data.lookDirection);
            vfxGameObject.SetActive(true);
            if (IsServerInitialized)
                ToggleVFX_ServerRpc(true);
            else
                ToggleVFX_ObserversRpc(true);
        }

        protected override void OnContinueCast()
        {
            base.OnContinueCast();
            if (!vfxGameObject) return;
            vfxGameObject.transform.rotation = Quaternion.LookRotation(OwnerCharacter.Data.lookDirection);
        }

        protected override void OnCancelCast()
        {
            base.OnCancelCast();
            vfxGameObject?.SetActive(false);
            if (IsServerInitialized)
                ToggleVFX_ServerRpc(false);
            else
                ToggleVFX_ObserversRpc(false);
        }

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            Vector3 targetPoint = OwnerCharacter.Camera.transform.position + OwnerCharacter.Data.lookDirection * 100f;
            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - transform.position).normalized;

            LaunchProjectile_ServerRpc(direction);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void ToggleVFX_ServerRpc(bool enable)
        {
            ToggleVFX_ObserversRpc(enable);
        }

        [ObserversRpc(ExcludeOwner =  true)]
        private void ToggleVFX_ObserversRpc(bool enable)
        {
            vfxGameObject?.SetActive(enable);
        }

        [ServerRpc(RequireOwnership = true)]
        private void LaunchProjectile_ServerRpc(Vector3 direction)
        {
            if (!OwnerCharacter) return;
            NetworkObject nob = NetworkManager.GetPooledInstantiated(
                projectilePrefab, transform.position, Quaternion.LookRotation(direction), true);
            if (nob.TryGetComponent(out Projectile projectile))
            {
                Spawn(nob);
                DamageInfo damageInfo = new(damage, OwnerCharacter.gameObject.transform);
                projectile.Launch(direction * projectileSpeed, damageInfo);
            }
            else
            {
                Logger.Warning($"[Fireball] Projectile prefab {projectilePrefab.name} is missing a Projectile or NetworkObject component.");
                Destroy(nob);
            }
        }

        protected override void ResetData()
        {
            base.ResetData();
            vfxGameObject?.SetActive(false);
        }
    }
}
