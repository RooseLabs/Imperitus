using FishNet.Component.Transforming;
using RooseLabs.Gameplay.Interactables;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerItems : MonoBehaviour
    {
        [field: SerializeField] public Transform HeldItemPosition { get; private set; }

        private PlayerCharacter m_character;
        private Vector3 m_itemHeldPositionInCameraSpace;
        private NetworkTransform m_heldItemPositionNetworkTransform;

        public Item CurrentHeldItem { get; private set; }

        private void Awake()
        {
            TryGetComponent(out m_character);
            HeldItemPosition.TryGetComponent(out m_heldItemPositionNetworkTransform);
            m_itemHeldPositionInCameraSpace = m_character.Camera.transform.InverseTransformPoint(HeldItemPosition.position);
            SetNetworkTransformSync(m_heldItemPositionNetworkTransform, false);
        }

        private void Update()
        {
            if (!m_character.IsOwner) return;
            if (!CurrentHeldItem) return;
            if (m_character.Input.dropWasPressed)
            {
                DropCurrentItem();
            }
        }

        private void LateUpdate()
        {
            if (!m_character.IsOwner) return;
            UpdateHeldItemPosition();
        }

        private void UpdateHeldItemPosition()
        {
            const float maxPitchAngle = 15f;
            Transform cam = m_character.Camera.transform;
            Vector3 lookEuler = Quaternion.LookRotation(m_character.Data.lookDirection).eulerAngles;

            // Normalize pitch to -180 to 180 range
            float pitch = lookEuler.x;
            pitch = (pitch > 180f) ? pitch - 360f : pitch;
            // Clamp pitch within threshold
            pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);
            // Apply clamped pitch to Euler angles
            lookEuler.x = pitch;
            Quaternion clampedRotation = Quaternion.Euler(lookEuler);

            Vector3 targetPosition = cam.position + clampedRotation * m_itemHeldPositionInCameraSpace;
            Quaternion targetRotation = Quaternion.LookRotation(cam.position - targetPosition);

            HeldItemPosition.position = targetPosition;
            HeldItemPosition.rotation = targetRotation;
        }

        public void PickupItem(Item item)
        {
            CurrentHeldItem = item;
            item.transform.SetParent(HeldItemPosition);
            SetNetworkTransformSync(m_heldItemPositionNetworkTransform, true);
        }

        private void DropCurrentItem()
        {
            if (!CurrentHeldItem) return;
            CurrentHeldItem.RequestDrop();
            CurrentHeldItem = null;
            SetNetworkTransformSync(m_heldItemPositionNetworkTransform, false);
        }

        private void SetNetworkTransformSync(NetworkTransform nm, bool enable)
        {
            if (!nm) return;
            SynchronizedProperty props = enable ? SynchronizedProperty.Position | SynchronizedProperty.Rotation
                : SynchronizedProperty.None;
            nm.SetSynchronizedProperties(props);
        }
    }
}
