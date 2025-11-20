using RooseLabs.Gameplay.Interactables;
using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class InteractionHandler : MonoBehaviour
    {
        public static InteractionHandler Instance { get; private set; }

        private const float InteractMaxDistance = 2.5f;

        public IInteractable CurrentHovered { get; private set; }

        private IInteractable m_bestInteractable;

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            CurrentHovered = null;
            if (!PlayerCharacter.LocalCharacter) return;
            if (CanInteract)
            {
                FindBestInteractable();
                DoInteraction(m_bestInteractable);
            }
            else
            {
                m_bestInteractable = null;
            }
            CurrentHovered = m_bestInteractable;
        }

        private void FindBestInteractable()
        {
            m_bestInteractable = null;
            var character = PlayerCharacter.LocalCharacter;
            if (!character.RaycastIgnoreSelf(
                character.Camera.transform.position, character.Camera.transform.forward,
                out var hitInfo, InteractMaxDistance, HelperFunctions.AllPhysicalLayerMask,
                queryTriggerInteraction: QueryTriggerInteraction.Collide
            )) return;
            hitInfo.collider.TryGetComponent(out IInteractable interactable);
            if (interactable == null || !interactable.IsInteractable(character)) return;
            m_bestInteractable = interactable;
        }

        private void DoInteraction(IInteractable interactable)
        {
            if (interactable == null) return;
            var character = PlayerCharacter.LocalCharacter;
            if (character.Input.interactWasPressed)
                interactable.Interact(PlayerCharacter.LocalCharacter);
        }

        private bool CanInteract => !PlayerCharacter.LocalCharacter.Data.IsRagdollActive &&
                                    !PlayerCharacter.LocalCharacter.Data.isDead &&
                                    !PlayerCharacter.LocalCharacter.Items.CurrentHeldItem;
    }
}
