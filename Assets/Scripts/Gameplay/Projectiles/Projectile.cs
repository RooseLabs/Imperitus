using FishNet.Object;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay
{
    public class Projectile : NetworkBehaviour
    {
        protected static Logger Logger => Logger.GetLogger("Projectile");

        #region Serialized
        [SerializeField]
        protected ProjectileRigidbody projectileRigidbody;

        [SerializeField, Tooltip("Time in seconds before the projectile is destroyed")]
        private float projectileLifetime = 10f;
        #endregion

        protected DamageInfo m_damageInfo;
        private float m_timeSinceLaunch;
        private bool m_hasCollided;

        /// <summary>
        /// Launches the projectile with the specified force and damage info.
        /// </summary>
        /// <param name="force">The force vector to apply to the projectile.</param>
        /// <param name="damageInfo">The damage information to apply on impact.</param>
        /// <param name="mode">The force mode to use when applying the force. Default is ForceMode.VelocityChange.</param>
        public void Launch(Vector3 force, DamageInfo damageInfo, ForceMode mode = ForceMode.VelocityChange)
        {
            if (!IsServerInitialized) return;
            if (!projectileRigidbody) return;

            m_timeSinceLaunch = 0f;
            m_hasCollided = false;
            m_damageInfo = damageInfo;
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
            if (!IsServerInitialized) return;
            if (m_hasCollided) return;

            m_timeSinceLaunch += Time.deltaTime;
            if (m_timeSinceLaunch >= projectileLifetime)
            {
                OnProjectileLifetimeExpired();
            }
        }

        private bool CanCollideWith(Collider col)
        {
            if (m_timeSinceLaunch < 0.1f && col.transform.IsChildOf(m_damageInfo.source))
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
            if (IsServerInitialized && col.gameObject.TryGetComponent(out IDamageable damageable))
            {
                m_damageInfo.position = col.ClosestPointOnBounds(projectileRigidbody.Rigidbody.position);
                damageable.ApplyDamage(m_damageInfo);
            }

            projectileRigidbody.gameObject.SetActive(false);

            // Default behavior is to despawn the projectile on collision
            if (IsServerInitialized)
                Despawn(gameObject);
        }

        /// <summary>
        /// Called when the projectile's lifetime has expired.
        /// Default behavior is to despawn the projectile.
        /// </summary>
        protected virtual void OnProjectileLifetimeExpired()
        {
            if (IsServerInitialized)
                Despawn(gameObject);
        }

        public override void ResetState(bool asServer)
        {
            m_damageInfo = default;
            m_timeSinceLaunch = 0f;
            m_hasCollided = false;
            projectileRigidbody.ResetState();
            projectileRigidbody.gameObject.SetActive(true);
            base.ResetState(asServer);
        }
    }
}
