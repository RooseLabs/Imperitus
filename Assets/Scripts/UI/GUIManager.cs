using RooseLabs.Core;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Player;
using RooseLabs.Player.Customization;
using UnityEngine;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        public static GUIManager Instance { get; private set; }

        [SerializeField] private HUDManager hudManager;
        [SerializeField] private NotebookUIController notebookUIController;
        [SerializeField] private CustomizationMenu customizationMenuController;

        private bool m_isNotebookOpen = false;
        private bool m_isCustomizationMenuOpen = false;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            var character = PlayerCharacter.LocalCharacter;
            if (!character) return;

            if (character.Input.resumeWasPressed)
            {
                if (m_isCustomizationMenuOpen)
                {
                    CloseCustomizationMenu();
                }
                else if (m_isNotebookOpen)
                {
                    ToggleNotebook();
                }
                return;
            }

            // Handle notebook toggle input based on current state
            if (m_isNotebookOpen)
            {
                // When notebook is open, check for close action from UI action map
                if (character.Input.closeNotebookWasPressed)
                {
                    ToggleNotebook();
                }
            }
            else
            {
                // When notebook is closed, check for open action from gameplay action map
                if (character.Input.openNotebookWasPressed)
                {
                    ToggleNotebook();
                }
            }
        }

        public void OpenCustomizationMenu()
        {
            if (m_isCustomizationMenuOpen) return;
            m_isCustomizationMenuOpen = true;

            InputHandler.Instance.EnableMenuInput();
            // Enable the customization menu canvas GameObject
            customizationMenuController.gameObject.SetActive(true);
            customizationMenuController.OnMenuOpened();
        }

        public void CloseCustomizationMenu()
        {
            m_isCustomizationMenuOpen = false;

            InputHandler.Instance.EnableGameplayInput();
            // Disable the customization menu canvas GameObject
            customizationMenuController.gameObject.SetActive(false);
        }

        public void ToggleNotebook()
        {
            if (m_isNotebookOpen)
            {
                InputHandler.Instance.EnableGameplayInput();
                CloseNotebook();
            }
            else
            {
                InputHandler.Instance.EnableMenuInput();
                OpenNotebook();
            }
        }

        public void OpenNotebook()
        {
            m_isNotebookOpen = true;

            // Enable the notebook canvas GameObject
            notebookUIController.gameObject.SetActive(true);
            // Refresh the content of the notebook's current tab
            notebookUIController.RefreshCurrentTab();
        }

        public void CloseNotebook()
        {
            m_isNotebookOpen = false;

            // Disable the notebook canvas GameObject
            notebookUIController.gameObject.SetActive(false);
        }

        public void SetHUDActive(bool isActive) => hudManager.gameObject.SetActive(isActive);

        public void SetTimerActive(bool isActive) => hudManager.SetTimerActive(isActive);
        public void UpdateTimer(float time) => hudManager.UpdateTimer(time);
    }
}
