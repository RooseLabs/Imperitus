using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class EnteredLibraryTrigger : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            GameManager.Instance.NotifyEnteredLibrary();
            Destroy(gameObject);
        }
    }
}
