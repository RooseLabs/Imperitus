using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class ExplosiveProjectile : Projectile
    {
        #region Serialized
        [SerializeField, Tooltip("Inner radius of the explosion after projectile impact. Entities within this radius will take full damage.")]
        private float innerRadius = 1f;
        [SerializeField, Tooltip("Outer radius of the explosion after projectile impact. Entities within this radius will damage with falloff.")]
        private float outerRadius = 2f;

        [SerializeField, Tooltip("Time in seconds before the explosion is destroyed")]
        private float explosionLifetime = 3f;

        [SerializeField, Tooltip("The explosion VFX that should be enabled on impact. Can be a prefab or a scene object.")]
        private GameObject explosionVFX;
        #endregion

        private GameObject m_explosionInstance;

        protected override void OnProjectileCollision(Collider col)
        {
            // Activate explosion effect at the projectile's position
            ActivateExplosion(projectileRigidbody.Rigidbody.position);
        }

        protected override void OnProjectileLifetimeExpired()
        {
            // Activate explosion effect at the projectile's position
            ActivateExplosion(projectileRigidbody.Rigidbody.position);
        }

        private void ActivateExplosion(Vector3 position)
        {
            // Disable projectile visuals and collider
            projectileRigidbody.gameObject.SetActive(false);

            // Activate explosion VFX
            if (explosionVFX)
            {
                if (string.IsNullOrEmpty(explosionVFX.scene.name))
                {
                    // This is a prefab, instantiate it
                    m_explosionInstance = Instantiate(explosionVFX, position, Quaternion.identity, gameObject.transform);
                }
                else
                {
                    m_explosionInstance = explosionVFX;
                    m_explosionInstance.transform.position = position;
                    m_explosionInstance.transform.localRotation = Quaternion.identity;
                    m_explosionInstance.SetActive(true);
                }
            }

            if (!isServer)
            {
                // Schedule destruction of the explosion effect
                Invoke(nameof(ExplosionEnd), explosionLifetime);
                return;
            }

            // Get all IDamageable objects within the explosion radius
            var hitColliders = Physics.OverlapSphere(position, outerRadius, HelperFunctions.AllPhysicalLayerMask, QueryTriggerInteraction.Collide);
            // Do a sphere cast from the explosion center to each object to check if there's any obstruction in between
            foreach (var col in hitColliders)
            {
                if (!col.gameObject.TryGetComponent(out IDamageable damageable))
                    continue;

                if (!ExplosionUtils.IsColliderHitByExplosion(position, col, innerRadius, outerRadius, HelperFunctions.AllPhysicalLayerMask))
                    continue;

                // Calculate distance-based damage falloff, clamped to a minimum of 5% of the full damage
                Vector3 closestPoint = col.ClosestPoint(position);
                float distance = Vector3.Distance(position, closestPoint);
                float damageMultiplier = 1f;
                if (distance > innerRadius)
                {
                    damageMultiplier = 1f - ((distance - innerRadius) / (outerRadius - innerRadius));
                    damageMultiplier = Mathf.Clamp(damageMultiplier, 0.05f, 1f);
                }
                DamageInfo adjustedDamageInfo = damageInfo;
                adjustedDamageInfo.amount = damageInfo.amount * damageMultiplier;
                adjustedDamageInfo.position = closestPoint;
                damageable.ApplyDamage(adjustedDamageInfo);
            }

            Invoke(nameof(ExplosionEnd), explosionLifetime);
        }

        private void ExplosionEnd()
        {
            m_explosionInstance?.SetActive(false);
            Destroy(gameObject);
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Only draw gizmos if this object or one of its children is selected
            GameObject selectedObject = UnityEditor.Selection.activeGameObject;
            if (!selectedObject || (selectedObject != gameObject && (!selectedObject.transform.parent || selectedObject.transform.parent.gameObject != gameObject)))
                return;
            var position = explosionVFX?.transform.position
                ?? projectileRigidbody?.transform.position
                ?? transform.position;
            // Draw explosion inner radius
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(position, innerRadius);
            // Draw explosion outer radius
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(position, outerRadius);
        }
        #endif
    }
}
