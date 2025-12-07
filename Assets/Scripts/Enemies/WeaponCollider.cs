using FishNet.Object;
using RooseLabs.Gameplay;
using UnityEngine;

namespace RooseLabs.Enemies
{
    /// <summary>
    /// Handles weapon collision detection for enemy attacks.
    /// Can be attached to the enemy parent, with a reference to the weapon tip GameObject.
    /// Server-authoritative - only processes collisions on the server.
    /// </summary>
    public class WeaponCollider : NetworkBehaviour
    {
        [Header("References")]
        public HanaduraAI ownerAI;
        public GameObject weaponTipObject;

        [Header("Damage Settings")]
        public float damageCooldown = 1f;

        [Header("Collision Detection")]
        public LayerMask playerLayer;

        // Internal state
        private Collider m_weaponCollider;
        private float m_damageTimer = 0f;
        private bool m_canDealDamage = false;

        private void Awake()
        {
            // Get collider from the weapon tip object
            if (weaponTipObject != null)
            {
                m_weaponCollider = weaponTipObject.GetComponent<Collider>();

                if (m_weaponCollider == null)
                {
                    //Debug.LogError("[WeaponCollider] No Collider found on weaponTipObject! Please add a collider to the weapon tip.");
                    return;
                }

                // Ensure it's a trigger
                if (!m_weaponCollider.isTrigger)
                {
                    //Debug.LogWarning("[WeaponCollider] Collider is not a trigger! Setting isTrigger to true.");
                    m_weaponCollider.isTrigger = true;
                }

                // Disable by default
                m_weaponCollider.enabled = false;
            }
            else
            {
                //Debug.LogError("[WeaponCollider] weaponTipObject reference is not set! Please assign it in the inspector.");
            }
        }

        private void Update()
        {
            if (!IsServerInitialized) return;

            // Countdown damage timer
            if (m_damageTimer > 0f)
            {
                m_damageTimer -= Time.deltaTime;
            }
        }

        /// <summary>
        /// Enable the weapon collider and reset damage cooldown.
        /// Called when the enemy starts an attack.
        /// </summary>
        public void EnableWeapon()
        {
            if (!IsServerInitialized) return;
            if (m_weaponCollider == null) return;

            m_weaponCollider.enabled = true;
            m_damageTimer = 0f; // Reset cooldown so first hit can register immediately
            m_canDealDamage = true;
            //Debug.Log("[WeaponCollider] Weapon enabled for attack");
        }

        /// <summary>
        /// Disable the weapon collider.
        /// Called when the attack ends or state changes.
        /// </summary>
        public void DisableWeapon()
        {
            if (!IsServerInitialized) return;
            if (m_weaponCollider == null) return;

            m_weaponCollider.enabled = false;
            m_canDealDamage = false;
            //Debug.Log("[WeaponCollider] Weapon disabled");
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only process on server
            if (!IsServerInitialized) return;

            // Can't deal damage if weapon is disabled or on cooldown
            if (!m_canDealDamage || m_damageTimer > 0f) return;

            // Check if we hit a player using layer mask
            if (((1 << other.gameObject.layer) & playerLayer) == 0) return;

            // Try to get IDamageable component
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                // Try getting it from parent (in case collider is on a child object)
                damageable = other.GetComponentInParent<IDamageable>();
            }

            if (damageable == null)
            {
                //Debug.LogWarning($"[WeaponCollider] Hit {other.name} but no IDamageable found!");
                return;
            }

            // Create damage info
            DamageInfo damage = new DamageInfo(
                ownerAI.attackDamage,
                other.ClosestPoint(weaponTipObject.transform.position),
                (other.transform.position - weaponTipObject.transform.position).normalized,
                ownerAI.transform
            );

            // Apply damage
            bool damageApplied = damageable.ApplyDamage(damage);

            if (damageApplied)
            {
                //Debug.Log($"[WeaponCollider] Hit {other.name} for {ownerAI.attackDamage} damage");

                // Start damage cooldown
                m_damageTimer = damageCooldown;
            }
            else
            {
                //Debug.Log($"[WeaponCollider] Hit {other.name} but damage was not applied (target may be dead or invincible)");
            }
        }
    }
}
