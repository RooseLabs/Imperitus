using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(1)]
    public class PlayerData : MonoBehaviour, IDamageable
    {
        private float m_maxHealth = 100f;
        private float m_health = 100f;
        private float m_maxStamina = 100f;
        private float m_stamina = 100f;

        // References
        public Slider m_healthSlider;
        public Slider m_staminaSlider;

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

        public Vector2 lookValues;
        public Vector3 lookDirection;
        public Vector3 lookDirection_Flat;

        private bool m_isRunning = false;
        public bool IsRunning
        {
            get => m_isRunning;
            set
            {
                if (m_isRunning == value) return;
                m_isRunning = value;
                stateChangedThisFrame = true;
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
                stateChangedThisFrame = true;
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
                stateChangedThisFrame = true;
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
                    m_isRunning = false;
                }
                stateChangedThisFrame = true;
            }
        }

        private float m_currentSpeed;
        public float CurrentSpeed
        {
            get => m_currentSpeed;
            set
            {
                m_currentSpeed = value;
                speedChangedThisFrame = !Mathf.Approximately(m_currentSpeed, value);
            }
        }

        public bool stateChangedThisFrame = false;
        public bool speedChangedThisFrame = false;

        private void LateUpdate()
        {
            stateChangedThisFrame = false;
            speedChangedThisFrame = false;
        }

        public void UpdateHealth(float amount)
        {
            Health += amount;

            if (m_healthSlider != null)
                m_healthSlider.value = Health / MaxHealth;
        }

        public void UpdateStamina(float amount)
        {
            Stamina += amount;

            if (m_staminaSlider != null)
            {
                float targetValue = Stamina / MaxStamina;
                float speed = 10f;
                m_staminaSlider.value = Mathf.MoveTowards(
                    m_staminaSlider.value,
                    targetValue,
                    Time.deltaTime * speed
                );
            }
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (Health <= 0) return false;

            UpdateHealth(-damage.Amount);

            // TODO: trigger death if health <= 0
            if (Health <= 0)
            {
                Debug.Log("Player died!");
            }

            return true;
        }

        public void SetSliders(Slider health, Slider stamina)
        {
            m_healthSlider = health;
            m_staminaSlider = stamina;

            m_healthSlider.value = Health / MaxHealth;
            m_staminaSlider.value = Stamina / MaxStamina;
        }
    }
}
