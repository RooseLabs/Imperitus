using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.Player
{
    public class PlayerData : MonoBehaviour, IDamageable
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

        public Vector2 lookValues;
        public Vector3 lookDirection;
        public Vector3 lookDirection_Flat;

        public bool isRunning = false;
        public bool isCrouching = false;
        public bool isCrawling = false;

        public float currentSpeed;

        // References
        public Slider m_healthSlider;
        public Slider m_staminaSlider;

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
