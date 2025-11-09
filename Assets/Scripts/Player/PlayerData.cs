using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(1)]
    public class PlayerData : MonoBehaviour
    {
        private float m_maxHealth = 100f;
        private float m_health = 100f;
        private float m_maxStamina = 100f;
        private float m_stamina = 100f;

        public float MaxHealth
        {
            get => m_maxHealth;
            set => m_maxHealth = Mathf.Max(0f, value);
        }

        public float Health
        {
            get => m_health;
            set => m_health = Mathf.Clamp(value, 0f, m_maxHealth);
        }

        public float MaxStamina
        {
            get => m_maxStamina;
            set => m_maxStamina = Mathf.Max(0f, value);
        }

        public float Stamina
        {
            get => m_stamina;
            set => m_stamina = Mathf.Clamp(value, 0f, m_maxStamina);
        }

        private bool m_isSprinting = false;
        public bool IsSprinting
        {
            get => m_isSprinting;
            set
            {
                if (m_isSprinting == value) return;
                m_isSprinting = value;
                StateChangedThisFrame = true;
            }
        }

        private bool m_isCrouching = false;
        public bool IsCrouching
        {
            get => m_isCrouching;
            set
            {
                if (m_isCrouching == value) return;
                m_isCrouching = value;
                StateChangedThisFrame = true;
            }
        }

        private bool m_isCrawling = false;
        public bool IsCrawling
        {
            get => m_isCrawling;
            set
            {
                if (m_isCrawling == value) return;
                m_isCrawling = value;
                StateChangedThisFrame = true;
            }
        }

        private bool m_isRagdollActive = false;
        public bool IsRagdollActive
        {
            get => m_isRagdollActive;
            set
            {
                if (m_isRagdollActive == value) return;
                m_isRagdollActive = value;
                if (value)
                {
                    m_isCrawling = false;
                    m_isCrouching = false;
                    m_isSprinting = false;
                }
                StateChangedThisFrame = true;
            }
        }

        private float m_currentSpeed;
        public float CurrentSpeed
        {
            get => m_currentSpeed;
            set
            {
                m_currentSpeed = value;
                SpeedChangedThisFrame = !Mathf.Approximately(m_currentSpeed, value);
            }
        }

        public bool isAiming;
        public bool isCasting;

        public Vector2 lookValues;
        public Vector3 lookDirection;
        public Vector3 lookDirectionFlat;

        public float sinceUseStamina;

        public bool StateChangedThisFrame { get; private set; }
        public bool SpeedChangedThisFrame { get; private set; }

        private void LateUpdate()
        {
            StateChangedThisFrame = false;
            SpeedChangedThisFrame = false;
        }
    }
}
