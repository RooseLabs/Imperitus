using FishNet;
using RooseLabs.Utils;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay
{
    public struct ProjectileCollision
    {
        public Collider collider;
        public Vector3 hitPoint;
        public Vector3 hitNormal;
    }

    public class Projectile : MonoBehaviour
    {
        protected static Logger Logger => Logger.GetLogger("Projectile");

        #region Serialized
        [SerializeField]
        protected ProjectileRigidbody projectileRigidbody;

        [SerializeField, Tooltip("Time in seconds before the projectile is destroyed")]
        private float projectileLifetime = 10f;

        [SerializeField, Tooltip("The launch VFX that should be enabled on launch. Can be a prefab or a scene object.")]
        private GameObject launchVFX;
        #endregion

        public Rigidbody Rigidbody => projectileRigidbody.Rigidbody;

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

            // Activate launch VFX
            if (launchVFX)
            {
                if (string.IsNullOrEmpty(launchVFX.scene.name))
                {
                    // This is a prefab, instantiate it
                    Instantiate(launchVFX, projectileRigidbody.transform.position, projectileRigidbody.transform.rotation, gameObject.transform);
                }
                else
                {
                    launchVFX.transform.position = projectileRigidbody.transform.position;
                    launchVFX.transform.rotation = projectileRigidbody.transform.rotation;
                    launchVFX.SetActive(true);
                }
            }

            m_timeSinceLaunch = 0f;
            m_hasCollided = false;
            this.damageInfo = damageInfo;
            projectileRigidbody.Rigidbody.isKinematic = false;
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

            ProjectileCollision collision;
            if (col.contacts.Length > 0)
            {
                collision = new ProjectileCollision
                {
                    collider = col.collider,
                    hitPoint = col.contacts[0].point,
                    hitNormal = col.contacts[0].normal
                };
            }
            else
            {
                Vector3 position = projectileRigidbody.Rigidbody.position;
                Vector3 closestPoint = col.collider.ClosestPoint(position);
                Vector3 normal = (position - closestPoint).normalized;
                if (normal.sqrMagnitude < 0.01f)
                {
                    normal = -projectileRigidbody.transform.forward;
                }
                collision = new ProjectileCollision
                {
                    collider = col.collider,
                    hitPoint = closestPoint,
                    hitNormal = normal
                };
            }

            OnProjectileCollision(collision);
        }

        private void OnProjectileTriggerEnter(Collider other)
        {
            // Trigger colliders don't respect the physics layer collision matrix, so we have to check manually
            if (Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
                return;
            if (!CanCollideWith(other))
                return;
            Logger.Info($"Projectile collided with {other.gameObject.name} (trigger, {LayerMask.LayerToName(other.gameObject.layer)})");

            Vector3 position = projectileRigidbody.Rigidbody.position;
            Vector3 closestPoint = other.ClosestPoint(position);
            Vector3 normal = (position - closestPoint).normalized;

            // If normal is zero (inside collider), use projectile's forward direction
            if (normal.sqrMagnitude < 0.01f)
            {
                normal = -projectileRigidbody.transform.forward;
            }

            ProjectileCollision collision = new ProjectileCollision
            {
                collider = other,
                hitPoint = closestPoint,
                hitNormal = normal
            };

            OnProjectileCollision(collision);
        }

        protected virtual void OnProjectileCollision(ProjectileCollision collision)
        {
            m_hasCollided = true;
            if (isServer && collision.collider.TryGetComponentInParent(out IDamageable damageable))
            {
                damageInfo.hitPoint = collision.hitPoint;
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
