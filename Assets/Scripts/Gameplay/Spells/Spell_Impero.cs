using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Impero : SpellBase
    {
        #region Serialized
        [Tooltip("Maximum distance that the spell can reach.")]
        [SerializeField] private float maxDistance = 5f;
        #endregion

        private static int s_raycastMask;

        private Draggable m_currentGrabbedObject;
        private float m_currentDragDistance;
        private float m_targetDragDistance;
        private float m_minSafeDragDistance;
        private const float MinDragDistanceBuffer = 0.5f;

        private void Awake()
        {
            if (s_raycastMask == 0)
                s_raycastMask = LayerMask.GetMask("Default", "Ground", "PlayerHitbox", "Draggable");
        }

        protected override bool OnCastFinished()
        {
            var character = PlayerCharacter.LocalCharacter;
            if (!character.RaycastIgnoreSelf(character.Camera.transform.position, character.Data.lookDirection,
                    out RaycastHit hitInfo, maxDistance, s_raycastMask))
            {
                Logger.Info("[Impero] No hit detected.");
                return false;
            }
            Logger.Info($"[Impero] Hit object: {hitInfo.collider.name}");
            if (!hitInfo.collider.TryGetComponent(out Draggable hitDraggable)) return false;

            if (hitDraggable.IsDoor)
            {
                m_minSafeDragDistance = 1.0f;
            }
            else
            {
                // Determine the minimum safe distance to avoid clipping with the object
                Vector3 closestPoint = hitInfo.collider.ClosestPoint(character.Camera.transform.position);
                m_minSafeDragDistance = MinDragDistanceBuffer + Vector3.Distance(hitInfo.point, closestPoint);
            }

            hitDraggable.HandleDragBegin(hitInfo.point);
            m_currentGrabbedObject = hitDraggable;
            m_currentDragDistance = hitInfo.distance;
            m_targetDragDistance = Mathf.Clamp(m_currentDragDistance, m_minSafeDragDistance, maxDistance);
            return true;
        }

        protected override void OnContinueCastSustained()
        {
            if (!m_currentGrabbedObject || !m_currentGrabbedObject.IsOwner)
            {
                CancelCast();
                return;
            }

            var character = PlayerCharacter.LocalCharacter;
            Vector3 desiredPosition = character.Camera.transform.position + character.Data.lookDirection * m_currentDragDistance;

            if (!m_currentGrabbedObject.IsDoor)
            {
                // Update the minimum safe drag distance to avoid clipping with the object
                Vector3 closestPoint = m_currentGrabbedObject.Collider.ClosestPoint(character.Camera.transform.position);
                m_minSafeDragDistance = MinDragDistanceBuffer + Vector3.Distance(desiredPosition, closestPoint);
                m_targetDragDistance = Mathf.Clamp(m_targetDragDistance, m_minSafeDragDistance, maxDistance);
            }

            m_currentGrabbedObject.HandleDrag(desiredPosition);
        }

        protected override void OnCancelCastSustained()
        {
            // Release the dragged object
            m_currentGrabbedObject?.HandleDragEnd();
        }

        protected override void OnScrollBackwardHeld()
        {
            OnScroll(-0.1f);
        }

        protected override void OnScrollForwardHeld()
        {
            OnScroll(0.1f);
        }

        protected override void OnScroll(float value)
        {
            if (!m_currentGrabbedObject) return;
            m_targetDragDistance = Mathf.Clamp(m_targetDragDistance + value, m_minSafeDragDistance, maxDistance);
        }

        private void Update()
        {
            if (m_currentGrabbedObject)
            {
                m_currentDragDistance = Mathf.Lerp(m_currentDragDistance, m_targetDragDistance, Time.deltaTime * 5f);
            }
        }

        protected override void ResetData()
        {
            base.ResetData();
            m_currentGrabbedObject = null;
            m_currentDragDistance = 0f;
            m_targetDragDistance = 0f;
            m_minSafeDragDistance = 0f;
        }
    }
}
