using System.Collections;
using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class Projectile : NetworkBehaviour
    {
        [Tooltip("Time in seconds before the projectile is automatically destroyed.")]
        [SerializeField] private float lifetime = 10f;

        private void Start()
        {
            if (IsServerInitialized)
            {
                StartCoroutine(DestroyAfterTime());
            }
        }

        private IEnumerator DestroyAfterTime()
        {
            yield return new WaitForSeconds(lifetime);
            Despawn(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsServerInitialized)
            {
                Despawn(gameObject);
            }
        }
    }
}
