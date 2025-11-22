using RooseLabs.Network;
using RooseLabs.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class HUDManager : MonoBehaviour
    {
        #region Serialized
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text joinCodeText;
        [SerializeField] private TMP_Text interactionText;
        #endregion

        private void OnEnable()
        {
            if (NetworkConnector.Instance.CurrentSessionJoinCode != null)
            {
                joinCodeText.gameObject.SetActive(true);
                joinCodeText.text = NetworkConnector.Instance.CurrentSessionJoinCode;
            }
            else
            {
                joinCodeText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            var character = PlayerCharacter.LocalCharacter;
            healthSlider.value = character.Data.Health / character.Data.MaxHealth;
            float targetStaminaValue = character.Data.Stamina / character.Data.MaxStamina;
            staminaSlider.value = Mathf.MoveTowards(staminaSlider.value, targetStaminaValue, Time.deltaTime * 2f);
        }

        public void SetTimerActive(bool isActive)
        {
            timerText.gameObject.SetActive(isActive);
        }

        /// <summary>
        /// Updates the timer display, formatted as MM:SS.
        /// </summary>
        /// <param name="time">Time in seconds.</param>
        public void UpdateTimer(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Sets the interaction text displayed on the HUD.
        /// </summary>
        /// <param name="text">The text to display.</param>
        public void SetInteractionText(string text)
        {
            interactionText.text = text;
        }
    }
}
