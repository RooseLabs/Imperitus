using System.Collections.Generic;
using System.Linq;
using FishNet.Object;
using RooseLabs.Gameplay;
using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs.Enemies
{
    public class EnemyDraggableCollision : NetworkBehaviour
    {
        [Header("Damage Settings")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private float damageForceThreshold = 50f;
        [SerializeField] private float forceToDamageMultiplier = 2f;
        [SerializeField] private float minimumDamageVelocity = 2.5f; 

        [Header("Cooldown Settings")]
        [SerializeField] private float damageCooldownDuration = 1f;

        // Track cooldowns per player for this specific enemy
        private readonly Dictionary<int, float> m_playerCooldowns = new();

        [Header("References")]
        [SerializeField] private BaseEnemy baseEnemy;

        private void Awake()
        {
            if (!baseEnemy)
                TryGetComponent(out baseEnemy);
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out Draggable draggable))
            {
                if (draggable.IsDoor)
                {
                    if (!draggable.IsController)
                    {
                        // My door now.
                        draggable.RemoveOwnership();
                    }
                    return;
                }

                if (!draggable.IsBeingDraggedByImpero)
                {
                    //Debug.Log($"Draggable {other.gameObject.name} is not being controlled by a player, no damage applied");
                    return;
                }

                Rigidbody draggableRb = other.collider.attachedRigidbody;
                if (draggableRb != null && draggableRb.linearVelocity.magnitude < minimumDamageVelocity)
                {
                    //Debug.Log($"Draggable velocity too low ({draggableRb.linearVelocity.magnitude:F2}), no damage applied");
                    return;
                }

                int playerID = draggable.Owner.ClientId;
                string playerName = PlayerHandler.GetPlayer(draggable.Owner).PlayerName;

                // Check if this player is on cooldown for this enemy
                if (m_playerCooldowns.ContainsKey(playerID) && Time.time < m_playerCooldowns[playerID])
                {
                    //Debug.Log($"Player {playerID} is on cooldown for this enemy. Time remaining: {playerCooldowns[playerID] - Time.time:F2}s");
                    return;
                }

                int damage = CalculateDamageFromCollision(other);
                if (damage > 0)
                {
                    DamageInfo damageInfo = new DamageInfo(damage, transform);
                    baseEnemy.ApplyDamage(damageInfo);

                    // Set cooldown for this player
                    m_playerCooldowns[playerID] = Time.time + damageCooldownDuration;

                    Debug.Log($"{playerName} applied {damage} damage from impact force! Cooldown active for {damageCooldownDuration}s");
                }
            }
        }

        private int CalculateDamageFromCollision(Collision collision)
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            float impactForce = relativeVelocity.magnitude * collision.collider.attachedRigidbody.mass;

            if (impactForce < damageForceThreshold)
                return 0;

            float forceDamage = ((impactForce / 3) - damageForceThreshold) * forceToDamageMultiplier;
            int totalDamage = baseDamage + Mathf.RoundToInt(forceDamage);

            return Mathf.Clamp(totalDamage, 0, int.MaxValue);
        }

        private void Update()
        {
            if (Time.frameCount % 120 == 0) // Check every 120 frames (~2 seconds at 60fps)
            {
                CleanupExpiredCooldowns();
            }
        }

        private void CleanupExpiredCooldowns()
        {
            var expiredPlayers = m_playerCooldowns
                .Where(kvp => Time.time > kvp.Value)
                .Select(kvp => kvp.Key).ToList();

            foreach (var playerID in expiredPlayers)
            {
                m_playerCooldowns.Remove(playerID);
            }
        }
    }
}
