using FishNet.Object;
using RooseLabs.Network;
using RooseLabs.Player;
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
        #endregion

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            var character = PlayerCharacter.LocalCharacter;
            // Create a ray from the camera's position in the direction it is facing
            // Ray ray = new Ray(character.Camera.transform.position, character.Camera.transform.forward);

            // Try to find the first object hit by the ray within 100 units
            // Vector3 targetPoint = character.RaycastIgnoreSelf(ray, out RaycastHit hit, 100f, HelperFunctions.AllPhysicalLayerMask)
            //     ? hit.point // If something is hit, use that point as the target
            //     : ray.GetPoint(100f); // Otherwise, use a point 100 units ahead

            Vector3 targetPoint = character.Camera.transform.position + character.Data.lookDirection * 100f;
            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - character.Wand.SpellCastPointPosition).normalized;

            // Request the server to spawn and launch the projectile in the calculated direction
            LaunchProjectile_ServerRpc(direction);

            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void LaunchProjectile_ServerRpc(Vector3 direction)
        {
            var character = PlayerHandler.GetCharacter(Owner);
            NetworkObject nob = NetworkManager.GetPooledInstantiated(
                projectilePrefab, character.Wand.SpellCastPointPosition, Quaternion.LookRotation(direction), true);
            if (nob.TryGetComponent(out Projectile projectile))
            {
                Spawn(nob);
                DamageInfo damageInfo = new(damage, character.gameObject.transform);
                projectile.Launch(direction * projectileSpeed, damageInfo);
            }
            else
            {
                Logger.Warning($"[Fireball] Projectile prefab {projectilePrefab.name} is missing a Projectile or NetworkObject component.");
                Destroy(nob);
            }
        }
    }
}
