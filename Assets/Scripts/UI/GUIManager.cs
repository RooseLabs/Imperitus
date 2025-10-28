using RooseLabs.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        public static GUIManager Instance { get; private set; }

        [SerializeField] private GameObject guiRootCanvas;
        [SerializeField] private TMP_Text runeCounterText;

        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;

        [SerializeField] private TMP_Text timerText;

        public Slider HealthSlider => healthSlider;
        public Slider StaminaSlider => staminaSlider;

        private HeistTimer m_heistTimer;

        private void Awake()
        {
            Instance = this;

            m_heistTimer = GetComponentInChildren<HeistTimer>();
        }

        //private void Start()
        //{
        //    m_heistTimer = GetComponentInChildren<HeistTimer>();
        //}

        public void SetGUIActive(bool isActive)
        {
            guiRootCanvas.SetActive(isActive);
        }

        public void UpdateRuneCounter(int count)
        {
            runeCounterText.text = $"{count} Runes Discovered";
        }

        public void UpdateSliders(PlayerData data)
        {
            if (data == null) return;

            data.SetSliders(healthSlider, staminaSlider);
        }

        internal void ToggleTimerText(bool isActive)
        {
            if (timerText != null)
                timerText.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// Formats the time as MM:SS and updates the timer TMP_Text.
        /// </summary>
        internal void UpdateTimerText(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }
}
