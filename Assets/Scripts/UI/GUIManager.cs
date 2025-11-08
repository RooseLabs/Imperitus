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
        [SerializeField] private NotebookUIController notebookUIController;

        [SerializeField] private TMP_Text runeCounterText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private TMP_Text timerText;

        public Slider HealthSlider => healthSlider;
        public Slider StaminaSlider => staminaSlider;

        private HeistTimer m_heistTimer;
        private PlayerInput m_playerInput;

        private void Awake()
        {
            Instance = this;
            m_heistTimer = GetComponentInChildren<HeistTimer>();
        }

        private void Update()
        {
            // Handle notebook toggle input
            if (m_playerInput != null && m_playerInput.openNotebookWasPressed)
            {
                ToggleNotebook();
            }
        }

        /// <summary>
        /// Sets the player input reference for this GUI manager.
        /// Call this when the local player spawns.
        /// </summary>
        public void SetPlayerInput(PlayerInput playerInput)
        {
            m_playerInput = playerInput;
        }

        /// <summary>
        /// Toggles the notebook open/closed.
        /// </summary>
        public void ToggleNotebook()
        {
            if (notebookUIController != null)
            {
                notebookUIController.ToggleNotebook();
            }
        }

        /// <summary>
        /// Activates or deactivates the main GUI and notebook.
        /// Use this when transitioning between gameplay and menus.
        /// </summary>
        public void SetGUIActive(bool isActive)
        {
            guiRootCanvas.SetActive(isActive);
        }

        /// <summary>
        /// Enables or disables only the notebook without affecting other GUI elements.
        /// Useful if you want notebook available in some contexts but not others.
        /// </summary>
        public void SetNotebookEnabled(bool enabled)
        {
            if (notebookUIController != null)
            {
                notebookUIController.SetNotebookEnabled(enabled);
            }
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