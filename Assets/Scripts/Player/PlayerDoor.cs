using RooseLabs.Gameplay.Interactions;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerDoor : MonoBehaviour
    {
        [Header("Interaction Settings")]
        [SerializeField] private float raycastDistance = 3f;       // How far you can interact
        [SerializeField] private LayerMask doorLayer;         // Which layers count as "doors"

        private Camera m_camera;

        private PlayerCharacter m_character;

        private void Awake()
        {
            m_character = GetComponent<PlayerCharacter>();
        }

        private void Start()
        {
            // Assumes your player has a Camera tagged as MainCamera
            m_camera = m_character.Camera;

            if (m_character == null)
            {
                //Debug.LogWarning("[PlayerPickup] No Player component found on the GameObject.");
            }
        }

        private void Update()
        {
            if (m_character.Input.interactWasPressed)
            {
                // Debug.Log("[PlayerDoor] Interact input detected.");
                TryInteract();
            }
        }

        private void TryInteract()
        {
            Ray ray = new Ray(m_camera.transform.position, m_camera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance, doorLayer))
            {
                Door door = hit.collider.GetComponentInParent<Door>();
                if (door != null)
                {
                    door.TryToggleDoor();
                }
            }
        }
    }
}
