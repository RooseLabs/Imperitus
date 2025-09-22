using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerData : MonoBehaviour
    {
        private float m_health = 1.0f;
        private float m_stamina = 1.0f;

        public float Health
        {
            get => m_health;
            set => m_health = Mathf.Clamp01(value);
        }

        public float Stamina
        {
            get => m_stamina;
            set => m_stamina = Mathf.Clamp01(value);
        }
    }
}
