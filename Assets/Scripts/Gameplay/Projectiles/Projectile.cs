using FishNet;
using RooseLabs.Utils;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay
{
    public class Projectile : MonoBehaviour
    {
        protected static Logger Logger => Logger.GetLogger("Projectile");

        #region Serialized
        [SerializeField]
        protected ProjectileRigidbody projectileRigidbody;

        [SerializeField, Tooltip("Time in seconds before the projectile is destroyed")]
        private float projectileLifetime = 10f;
        #endregion

        private float m_timeSinceLaunch;
        private bool m_hasCollided;

        protected bool isServer;
        protected DamageInfo damageInfo;

        private void Awake()
        {
            isServer = InstanceFinder.IsServerStarted;
        }

        /// <summary>
        /// Launches the projectile with the specified force and damage info.
        /// </summary>
        /// <param name="force">The force vector to apply to the projectile.</param>
        /// <param name="damageInfo">The damage information to apply on impact.</param>
        /// <param name="mode">The force mode to use when applying the force. Default is ForceMode.VelocityChange.</param>
        public void Launch(Vector3 force, DamageInfo damageInfo, ForceMode mode = ForceMode.VelocityChange)
        {
            if (!projectileRigidbody) return;

            m_timeSinceLaunch = 0f;
            m_hasCollided = false;
            this.damageInfo = damageInfo;
            projectileRigidbody.Rigidbody.AddForce(force, mode);
        }

        private void OnEnable()
        {
            projectileRigidbody.CollisionEnter += OnProjectileCollisionEnter;
            projectileRigidbody.TriggerEnter += OnProjectileTriggerEnter;
        }

        private void OnDisable()
        {
            projectileRigidbody.CollisionEnter -= OnProjectileCollisionEnter;
            projectileRigidbody.TriggerEnter -= OnProjectileTriggerEnter;
        }

        private void Update()
        {
            if (m_hasCollided) return;

            m_timeSinceLaunch += Time.deltaTime;
            if (m_timeSinceLaunch >= projectileLifetime)
            {
                OnProjectileLifetimeExpired();
            }
        }

        private bool CanCollideWith(Collider col)
        {
            if (!damageInfo.source) return true;
            if (m_timeSinceLaunch < 0.1f && col.transform.IsChildOf(damageInfo.source))
            {
                // Ignore collision with the source for a brief moment after launch
                return false;
            }
            return true;
        }

        private void OnProjectileCollisionEnter(Collision col)
        {
            if (!CanCollideWith(col.collider))
                return;
            Logger.Info($"Projectile collided with {col.gameObject.name} ({LayerMask.LayerToName(col.gameObject.layer)})");
            OnProjectileCollision(col.collider);
        }

        private void OnProjectileTriggerEnter(Collider other)
        {
            // Trigger colliders don't respect the physics layer collision matrix, so we have to check manually
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
                return;
            if (!CanCollideWith(other))
                return;
            Logger.Info($"Projectile collided with {other.gameObject.name} (trigger, {LayerMask.LayerToName(other.gameObject.layer)})");
            OnProjectileCollision(other);
        }

        protected virtual void OnProjectileCollision(Collider col)
        {
            m_hasCollided = true;
            if (isServer && col.TryGetComponentInParent(out IDamageable damageable))
            {
                damageInfo.hitPoint = col.ClosestPointOnBounds(projectileRigidbody.Rigidbody.position);
                damageable.ApplyDamage(damageInfo);
            }

            projectileRigidbody.gameObject.SetActive(false);

            // Default behavior is to despawn the projectile on collision
            Destroy(gameObject);
        }

        /// <summary>
        /// Called when the projectile's lifetime has expired.
        /// Default behavior is to despawn the projectile.
        /// </summary>
        protected virtual void OnProjectileLifetimeExpired()
        {
            Destroy(gameObject);
        }
    }
}
