using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class DormitoryDoor : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            var character = other.GetComponent<PlayerCharacter>();
            if (character != null && character.IsServerInitialized && character.IsOwner)
                GameManager.Instance.StartHeist();
        }
    }
}
