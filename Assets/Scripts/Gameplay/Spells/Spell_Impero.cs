using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Impero : SpellBase
    {
        #region Serialized
        [Tooltip("Maximum distance that the spell can reach.")]
        [SerializeField] private float maxDistance = 5f;
        #endregion

        private Draggable m_currentGrabbedObject;
        private Vector3 m_currentGrabbedLocalHitPoint;
        private float m_currentDragDistance;
        private float m_targetDragDistance;
        private float m_minSafeDragDistance;
        private const float MinDragDistanceBuffer = 0.5f;

        protected override bool OnCastFinished()
        {
            var character = PlayerCharacter.LocalCharacter;
            var cameraPosition = character.Camera.transform.position;
            if (!character.RaycastIgnoreSelf(cameraPosition, character.Data.lookDirection,
                    out RaycastHit hitInfo, maxDistance, HelperFunctions.AllPhysicalLayerMask))
            {
                Logger.Info("[Impero] No hit detected.");
                return false;
            }
            Logger.Info($"[Impero] Hit object: {hitInfo.collider.name}");
            if (!hitInfo.collider.TryGetComponent(out Draggable hitDraggable)) return false;

            if (!hitDraggable.IsDraggable) return false;
            if (hitDraggable.IsDoor)
            {
                m_minSafeDragDistance = 1.0f;
            }
            else
            {
                // Determine the minimum safe distance to avoid clipping with the object
                Vector3 closestPoint = hitInfo.collider.ClosestPoint(cameraPosition);
                float closestDistance = Vector3.Distance(cameraPosition, closestPoint);
                m_minSafeDragDistance = MinDragDistanceBuffer + (hitInfo.distance - closestDistance);
            }

            hitDraggable.HandleDragBegin(hitInfo.point);
            m_currentGrabbedLocalHitPoint = hitDraggable.transform.InverseTransformPoint(hitInfo.point);
            m_currentGrabbedObject = hitDraggable;
            m_currentDragDistance = hitInfo.distance;
            m_targetDragDistance = Mathf.Clamp(m_currentDragDistance, m_minSafeDragDistance, maxDistance);
            return true;
        }

        protected override void OnContinueCastSustained()
        {
            if (!m_currentGrabbedObject || !m_currentGrabbedObject.IsOwner || !m_currentGrabbedObject.IsDraggable)
            {
                CancelCast();
                return;
            }

            var character = PlayerCharacter.LocalCharacter;

            Vector3 cameraPosition = character.Camera.transform.position;
            Vector3 grabbedWorldHitPoint = m_currentGrabbedObject.transform.TransformPoint(m_currentGrabbedLocalHitPoint);
            float currentGrabDistance = Vector3.Distance(cameraPosition, grabbedWorldHitPoint);
            if (m_currentGrabbedObject.IsDoor)
            {
                if (currentGrabDistance > maxDistance)
                {
                    // Door is out of range, cancel the cast
                    CancelCast();
                    return;
                }
            }
            else
            {
                // Update the minimum safe drag distance to avoid clipping with the object
                Vector3 closestPoint = m_currentGrabbedObject.Collider.ClosestPoint(cameraPosition);
                float closestDistance = Vector3.Distance(closestPoint, cameraPosition);
                m_minSafeDragDistance = MinDragDistanceBuffer + Mathf.Max(0.0f, currentGrabDistance - closestDistance);
                m_targetDragDistance = Mathf.Clamp(m_targetDragDistance, m_minSafeDragDistance, maxDistance);
            }

            m_currentDragDistance = Mathf.Lerp(m_currentDragDistance, m_targetDragDistance, Time.deltaTime * 5f);
            Vector3 desiredPosition = character.Camera.transform.position + character.Data.lookDirection * m_currentDragDistance;
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

        protected override void ResetData()
        {
            base.ResetData();
            m_currentGrabbedObject = null;
            m_currentGrabbedLocalHitPoint = Vector3.zero;
            m_currentDragDistance = 0f;
            m_targetDragDistance = 0f;
            m_minSafeDragDistance = 0f;
        }
    }
}
