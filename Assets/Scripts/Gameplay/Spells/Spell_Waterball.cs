using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Waterball : SpellBase
    {
        #region Serialized
        [Header("Waterball Spell Data")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 10f;
        [SerializeField] private float damage = 10f;
        #endregion

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            Vector3 targetPoint = CasterCharacter.Camera.transform.position + CasterCharacter.Data.lookDirection * 100f;
            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - transform.position).normalized;

            if (IsServerInitialized)
                LaunchProjectile_ObserversRpc(direction);
            else
                LaunchProjectile_ServerRpc(direction);
            LaunchProjectile(direction);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void LaunchProjectile_ServerRpc(Vector3 direction)
        {
            LaunchProjectile_ObserversRpc(direction);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true)]
        private void LaunchProjectile_ObserversRpc(Vector3 direction)
        {
            LaunchProjectile(direction);
        }

        private void LaunchProjectile(Vector3 direction)
        {
            if (!CasterCharacter) return;
            var pGo = Instantiate(projectilePrefab, transform.position, Quaternion.LookRotation(direction));
            if (pGo.TryGetComponent(out Projectile projectile))
            {
                DamageInfo damageInfo = new(damage, CasterCharacter.gameObject.transform);
                damageInfo.hitDirection = direction;
                projectile.Launch(direction * projectileSpeed, damageInfo);
            }
            else
            {
                Logger.Warning($"[Waterball] Projectile prefab {projectilePrefab.name} is missing a Projectile component.");
                Destroy(pGo);
            }
        }
    }
}
