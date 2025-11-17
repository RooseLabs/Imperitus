using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class ReturnToLobbyTrigger : MonoBehaviour
    {
        private Collider m_collider;

        private void Awake()
        {
            m_collider = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // If all alive players are in the trigger, end the heist successfully
            bool allPlayersInTrigger = true;
            foreach (var player in PlayerHandler.AllCharacters)
            {
                if (player.Data.isDead) continue;
                if (player.TryGetComponent(out Collider playerCollider))
                {
                    if (!m_collider.bounds.Intersects(playerCollider.bounds))
                    {
                        allPlayersInTrigger = false;
                        break;
                    }
                }
            }
            if (allPlayersInTrigger)
            {
                GameManager.Instance.NotifyReturnToLobby();
            }
        }
    }
}
