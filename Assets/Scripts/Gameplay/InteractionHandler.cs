using RooseLabs.Core;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.Player;
using RooseLabs.UI;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class InteractionHandler : MonoBehaviour
    {
        public static InteractionHandler Instance { get; private set; }

        private const float InteractMaxDistance = 2.5f;

        private IInteractable m_bestInteractable;
        private IInteractable m_currentInteractable;

        public IInteractable CurrentHovered
        {
            get => m_currentInteractable;
            private set
            {
                if (value != null)
                {
                    // Update interaction text
                    GUIManager.Instance.SetInteractionText(value.GetInteractionText());
                    if (value is Component c)
                    {
                        HelperFunctions.HighlightObject(c);
                    }
                }
                else
                {
                    GUIManager.Instance.SetInteractionText(string.Empty);
                    if (m_currentInteractable is Component c)
                    {
                        if (m_currentInteractable is Draggable { IsBeingDraggedByImpero: true })
                        {
                            // Don't unhighlight if being dragged
                            return;
                        }
                        HelperFunctions.UnhighlightObject(c);
                    }
                }
                m_currentInteractable = value;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            CurrentHovered = null;
            if (!PlayerCharacter.LocalCharacter) return;
            if (!InputHandler.GameplayActions.enabled)
            {
                GUIManager.Instance.SetInteractionText(string.Empty);
                return;
            }
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
            var ray = new Ray(character.Camera.transform.position, character.Camera.transform.forward);
            if (character.RaycastIgnoreSelf(ray, out var hitInfo1, InteractMaxDistance,
                    HelperFunctions.AllPhysicalLayerMask, QueryTriggerInteraction.Collide)
                && hitInfo1.collider.TryGetComponent(out IInteractable interactable1)
                && interactable1.IsInteractable(character)
            )
            {
                m_bestInteractable = interactable1;
            }
            else if (character.SphereCastIgnoreSelf(ray, 0.025f, out var hitInfo2, InteractMaxDistance,
                    HelperFunctions.AllPhysicalLayerMask, QueryTriggerInteraction.Collide)
                && hitInfo2.collider.TryGetComponent(out IInteractable interactable2)
                && interactable2.IsInteractable(character)
            )
            {
                m_bestInteractable = interactable2;
            }
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
