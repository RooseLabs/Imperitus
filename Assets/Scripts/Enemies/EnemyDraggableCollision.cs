using System.Collections.Generic;
using System.Linq;
using FishNet.Component.Ownership;
using FishNet.Object;
using RooseLabs.Enemies;
using RooseLabs.Gameplay;
using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs
{
    public class EnemyDraggableCollision : NetworkBehaviour
    {
        [SerializeField] private EnemyData enemyData;

        [Header("Damage Settings")]
        [SerializeField] private int baseDamage = 10;
        [SerializeField] private float damageForceThreshold = 50f;
        [SerializeField] private float forceToTamageMultiplier = 2f;
        [SerializeField] private float minimumDamageVelocity = 2.5f; 

        [Header("Cooldown Settings")]
        [SerializeField] private float damageCooldownDuration = 1f;

        // Track cooldowns per player for this specific enemy
        private Dictionary<int, float> playerCooldowns = new Dictionary<int, float>();

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                if (other.gameObject.TryGetComponent(out Draggable draggable))
                {
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

                    // Get the player who owns this draggable object
                    if (!draggable.TryGetComponent(out PredictedOwner predictedOwner))
                    {
                        Debug.LogWarning("Draggable object has no PredictedOwner component!");
                        return;
                    }

                    int playerID = predictedOwner.Owner.ClientId;
                    string playerName = PlayerHandler.GetPlayer(predictedOwner.Owner).PlayerName;

                    // Check if this player is on cooldown for this enemy
                    if (playerCooldowns.ContainsKey(playerID) && Time.time < playerCooldowns[playerID])
                    {
                        //Debug.Log($"Player {playerID} is on cooldown for this enemy. Time remaining: {playerCooldowns[playerID] - Time.time:F2}s");
                        return;
                    }

                    int damage = CalculateDamageFromCollision(other);
                    if (damage > 0)
                    {
                        DamageInfo damageInfo = new DamageInfo(
                            damage,
                            DamageType.Melee,
                            transform,
                            other.contacts[0].point,
                            playerName
                        );
                        enemyData.ApplyDamage(damageInfo);

                        // Set cooldown for this player
                        playerCooldowns[playerID] = Time.time + damageCooldownDuration;

                        Debug.Log($"{playerName} applied {damage} damage from impact force! Cooldown active for {damageCooldownDuration}s");
                    }
                }
            }
        }

        private int CalculateDamageFromCollision(Collision collision)
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            float impactForce = relativeVelocity.magnitude * collision.collider.attachedRigidbody.mass;

            if (impactForce < damageForceThreshold)
                return 0;

            float forceDamage = ((impactForce / 3) - damageForceThreshold) * forceToTamageMultiplier;
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
            var expiredPlayers = playerCooldowns
                .Where(kvp => Time.time > kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var playerID in expiredPlayers)
            {
                playerCooldowns.Remove(playerID);
            }
        }
    }
}