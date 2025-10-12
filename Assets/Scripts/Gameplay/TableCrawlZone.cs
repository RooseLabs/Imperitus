using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs
{
    public class TableCrawlZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PlayerMovementCC>(out var player))
                player.SetNearTable(true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<PlayerMovementCC>(out var player))
                player.SetNearTable(false);
        }
    }
}
