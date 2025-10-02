using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Player
{
    // TODO: Most of this is likely to be replaced by a more robust Item-Action system, with spells
    //       being considered items that can be "selected/held" and "used".
    public class PlayerCast : NetworkBehaviour
    {
        private Player m_player;

        [SerializeField] private Transform castPoint;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 10f;

        private static int s_layerMask;

        private void Awake()
        {
            if (s_layerMask == 0) s_layerMask = ~LayerMask.GetMask("Projectile");
        }

        private void Start()
        {
            if (projectilePrefab == null || castPoint == null)
            {
                Debug.LogError("[PlayerCast] Missing projectilePrefab or castPoint reference.");
                enabled = false;
                return;
            }
            m_player = GetComponent<Player>();
        }

        public override void OnStartClient()
        {
            enabled = IsOwner;
        }

        private void Update()
        {
            if (m_player.Input.aimIsPressed && m_player.Input.attackWasPressed)
            {
                // Create a ray from the camera's position in the direction it is facing
                Ray ray = new Ray(m_player.Camera.transform.position, m_player.Camera.transform.forward);

                // Try to find the first object hit by the ray within 100 units, ignoring the Projectile layer
                Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, 100f, s_layerMask)
                    ? hit.point // If something is hit, use that point as the target
                    : ray.GetPoint(100f); // Otherwise, use a point 100 units ahead

                // Calculate the normalized direction vector from the cast point to the target point
                Vector3 direction = (targetPoint - castPoint.position).normalized;

                // Request the server to spawn and launch the projectile in the calculated direction
                CastSpell_ServerRpc(direction);
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void CastSpell_ServerRpc(Vector3 direction)
        {
            GameObject projectileInstance = Instantiate(projectilePrefab, castPoint.position, castPoint.rotation);
            if (projectileInstance.TryGetComponent(out Rigidbody rb))
            {
                Spawn(projectileInstance);
                rb.AddForce(direction * projectileSpeed, ForceMode.VelocityChange);
            }
            else
            {
                Debug.LogWarning($"[PlayerCast] Projectile prefab {projectilePrefab.name} is missing a Rigidbody component.");
                Destroy(projectileInstance);
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (m_player == null || !m_player.Input.aimIsPressed) return;
            Gizmos.color = Color.red;
            Ray ray = new Ray(m_player.Camera.transform.position, m_player.Camera.transform.forward);
            Vector3 hitPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, s_layerMask))
            {
                Gizmos.DrawRay(ray.origin, ray.direction * hit.distance);
                Gizmos.DrawSphere(hit.point, 0.1f);
                hitPoint = hit.point;
            }
            else
            {
                Gizmos.DrawRay(ray.origin, ray.direction * 100f);
                hitPoint = ray.GetPoint(100f);
                Gizmos.DrawSphere(hitPoint, 0.1f);
            }
            Gizmos.color = Color.green;
            Gizmos.DrawLine(castPoint.position, hitPoint);
        }
        #endif
    }
}
