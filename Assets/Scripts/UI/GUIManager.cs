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

        public Slider HealthSlider => healthSlider;
        public Slider StaminaSlider => staminaSlider;


        private void Awake()
        {
            Instance = this;
        }

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

    }
}
