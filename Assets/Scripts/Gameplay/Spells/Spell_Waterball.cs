using FishNet.Object;
using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Waterball : SpellBase
    {
        #region Serialized
        [Header("Waterball Spell Data")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 10f;
        #endregion

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            var character = PlayerCharacter.LocalCharacter;
            // Create a ray from the camera's position in the direction it is facing
            Ray ray = new Ray(character.Camera.transform.position, character.Camera.transform.forward);

            // Try to find the first object hit by the ray within 100 units
            Vector3 targetPoint = character.RaycastIgnoreSelf(ray, out RaycastHit hit, 100f, HelperFunctions.AllPhysicalLayerMask)
                ? hit.point // If something is hit, use that point as the target
                : ray.GetPoint(100f); // Otherwise, use a point 100 units ahead

            // Calculate the normalized direction vector from the cast point to the target point
            Vector3 direction = (targetPoint - transform.position).normalized;

            // Request the server to spawn and launch the projectile in the calculated direction
            LaunchProjectile_ServerRpc(direction);

            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        private void LaunchProjectile_ServerRpc(Vector3 direction)
        {
            NetworkObject nob = NetworkManager.GetPooledInstantiated(projectilePrefab, transform.position, Quaternion.identity, true);
            if (nob.TryGetComponent(out Rigidbody rb))
            {
                Spawn(nob);
                rb.AddForce(direction * projectileSpeed, ForceMode.VelocityChange);
            }
            else
            {
                Logger.Warning($"[Waterball] Projectile prefab {projectilePrefab.name} is missing a Rigidbody or NetworkObject component.");
                Destroy(nob);
            }
        }
    }
}
