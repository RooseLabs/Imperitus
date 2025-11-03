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
        [SerializeField] private GameObject notebookCanvasObject; // The Notebook GameObject itself
        [SerializeField] private NotebookUIController notebookUIController;
        [SerializeField] private TMP_Text runeCounterText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider staminaSlider;
        [SerializeField] private TMP_Text timerText;

        public Slider HealthSlider => healthSlider;
        public Slider StaminaSlider => staminaSlider;

        private HeistTimer m_heistTimer;
        private PlayerInput m_playerInput;
        private bool m_isNotebookOpen = false;
        private bool m_isNotebookEnabled = true;
        private Canvas m_notebookCanvas; // Cache the canvas component

        private void Awake()
        {
            Instance = this;
            m_heistTimer = GetComponentInChildren<HeistTimer>();

            // Cache the notebook canvas component
            if (notebookCanvasObject != null)
            {
                m_notebookCanvas = notebookCanvasObject.GetComponent<Canvas>();
                notebookCanvasObject.SetActive(false);
            }
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
            if (!m_isNotebookEnabled)
                return;

            if (m_isNotebookOpen)
                CloseNotebook();
            else
                OpenNotebook();
        }

        /// <summary>
        /// Opens the notebook UI.
        /// </summary>
        public void OpenNotebook()
        {
            if (!m_isNotebookEnabled)
                return;

            m_isNotebookOpen = true;

            // Enable the notebook canvas GameObject
            if (notebookCanvasObject != null)
                notebookCanvasObject.SetActive(true);

            // Set notebook canvas to render on top of other UI
            if (m_notebookCanvas != null)
            {
                m_notebookCanvas.sortingOrder = 100; // Higher than main GUI
                Debug.Log($"[GUIManager] Notebook canvas sort order set to: {m_notebookCanvas.sortingOrder}");
            }

            // Tell the notebook controller to refresh its content
            if (notebookUIController != null)
                notebookUIController.RefreshCurrentTab();

            // Show cursor and unlock it
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            // Block player movement and camera
            if (m_playerInput != null)
                m_playerInput.SetNotebookOpen(true);

            // Debug check for EventSystem
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogError("[GUIManager] No EventSystem found in scene! UI buttons won't work.");
            }
            else
            {
                Debug.Log($"[GUIManager] EventSystem found: {eventSystem.name}");
            }

            Debug.Log("[GUIManager] Notebook opened");
        }

        /// <summary>
        /// Closes the notebook UI.
        /// </summary>
        public void CloseNotebook()
        {
            m_isNotebookOpen = false;

            // Disable the notebook canvas GameObject
            if (notebookCanvasObject != null)
                notebookCanvasObject.SetActive(false);

            // Hide cursor and lock it back
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Re-enable player movement and camera
            if (m_playerInput != null)
                m_playerInput.SetNotebookOpen(false);

            Debug.Log("[GUIManager] Notebook closed");
        }

        /// <summary>
        /// Activates or deactivates the main GUI and notebook.
        /// Use this when transitioning between gameplay and menus.
        /// </summary>
        public void SetGUIActive(bool isActive)
        {
            guiRootCanvas.SetActive(isActive);

            // When disabling GUI, also close notebook if it's open
            if (!isActive && m_isNotebookOpen)
            {
                CloseNotebook();
            }
        }

        /// <summary>
        /// Enables or disables only the notebook without affecting other GUI elements.
        /// Useful if you want notebook available in some contexts but not others.
        /// </summary>
        public void SetNotebookEnabled(bool enabled)
        {
            m_isNotebookEnabled = enabled;

            // If disabling while open, close it
            if (!enabled && m_isNotebookOpen)
            {
                CloseNotebook();
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