using FishNet.Object;
using RooseLabs.Player;
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
        [SerializeField] private float spawnOffset = 1f;
        [SerializeField] private GameObject vfxGameObject;
        #endregion

        #region Private Fields
        private Projectile m_spawnedProjectile;
        private Vector3 m_initialProjectileScale;
        #endregion

        protected override void OnStartCast()
        {
            base.OnStartCast();
            ManageCastingVisuals(true);
        }

        protected override void OnCancelCast()
        {
            base.OnCancelCast();
            ManageCastingVisuals(false);
        }

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            if (!m_spawnedProjectile) return false;

            Vector3 targetPoint = CasterCharacter.Camera.transform.position + CasterCharacter.Data.lookDirection * 100f;
            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - m_spawnedProjectile.transform.position).normalized;

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
            if (!CasterCharacter) return;

            // Calculate look direction once for both VFX and projectile
            Vector3 lookDirection = (CasterCharacter == PlayerCharacter.LocalCharacter)
                ? CasterCharacter.Data.lookDirection
                : CasterCharacter.ModelTransform.forward;

            Vector3 offsetPosition = transform.position + lookDirection * spawnOffset;
            Quaternion lookRotation = Quaternion.LookRotation(lookDirection);

            // Update VFX
            if (vfxGameObject && vfxGameObject.activeSelf)
            {
                vfxGameObject.transform.position = offsetPosition;
                vfxGameObject.transform.rotation = lookRotation;
            }

            // Update projectile casting visual
            if (m_spawnedProjectile)
            {
                m_spawnedProjectile.Rigidbody.position = offsetPosition;
                m_spawnedProjectile.Rigidbody.rotation = lookRotation;
                m_spawnedProjectile.transform.localScale = Vector3.Lerp(Vector3.zero, m_initialProjectileScale, CastProgressNormalized);
            }
        }

        private void ManageCastingVisuals(bool enable)
        {
            if (IsServerInitialized)
            {
                ManageCastingVisuals_ObserversRpc(enable);
            }
            else
            {
                ManageCastingVisuals_ServerRpc(enable);
                ManageCastingVisuals_Internal(enable);
            }
        }

        private void ManageCastingVisuals_Internal(bool enable)
        {
            // Handle VFX
            if (vfxGameObject)
            {
                // If we're enabling but it's already active, disable it first to restart the effect
                if (enable && vfxGameObject.activeSelf)
                    vfxGameObject.SetActive(false);
                vfxGameObject.SetActive(enable);

                // Position VFX with offset when enabling
                if (enable && CasterCharacter)
                {
                    Vector3 lookDirection = (CasterCharacter == PlayerCharacter.LocalCharacter)
                        ? CasterCharacter.Data.lookDirection
                        : CasterCharacter.ModelTransform.forward;
                    vfxGameObject.transform.position = transform.position + lookDirection * spawnOffset;
                }
            }

            // Handle projectile
            if (enable)
            {
                SpawnProjectile();
            }
            else
            {
                CleanupProjectile();
            }
        }

        private void SpawnProjectile()
        {
            if (!CasterCharacter) return;
            if (!projectilePrefab) return;

            // Clean up any existing projectile first
            CleanupProjectile();

            // Calculate spawn position with offset in front of the player
            Vector3 lookDirection = (CasterCharacter == PlayerCharacter.LocalCharacter)
                ? CasterCharacter.Data.lookDirection
                : CasterCharacter.ModelTransform.forward;
            Vector3 spawnPosition = transform.position + lookDirection * spawnOffset;

            // Spawn the projectile at the offset position
            m_spawnedProjectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(lookDirection))
                .GetComponent<Projectile>();

            // Store and reset the scale to zero for interpolation
            if (m_spawnedProjectile)
            {
                m_initialProjectileScale = m_spawnedProjectile.transform.localScale;
                m_spawnedProjectile.transform.localScale = Vector3.zero;
            }
        }

        private void CleanupProjectile()
        {
            if (m_spawnedProjectile)
            {
                Destroy(m_spawnedProjectile.gameObject);
                m_spawnedProjectile = null;
            }
        }

        private void LaunchProjectile(Vector3 direction)
        {
            if (!CasterCharacter) return;
            if (!m_spawnedProjectile) return;

            // Ensure projectile is at full scale before launching
            m_spawnedProjectile.transform.localScale = m_initialProjectileScale;
            m_spawnedProjectile.transform.rotation = Quaternion.LookRotation(direction);

            DamageInfo damageInfo = new(damage, CasterCharacter.gameObject.transform);
            damageInfo.hitDirection = direction;
            m_spawnedProjectile.Launch(direction * projectileSpeed, damageInfo);

            // Clear the reference since the projectile is now launched and managing itself
            m_spawnedProjectile = null;
        }

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void ManageCastingVisuals_ServerRpc(bool enable)
        {
            ManageCastingVisuals_ObserversRpc(enable);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void ManageCastingVisuals_ObserversRpc(bool enable)
        {
            ManageCastingVisuals_Internal(enable);
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
    }
}
