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

            // Handle customization menu toggle input based on current state
            if (m_isCustomizationMenuOpen)
            {
                // When customization menu is open, check for close action from UI action map
                if (character.Input.CloseCustomizationMenu)
                {
                    ToggleCustomizationMenu();
                }
            }
            else
            {
                // When customization menu is closed, check for open action from gameplay action map
                if (character.Input.OpenCustomizationMenu)
                {
                    ToggleCustomizationMenu();
                }
            }
        }

        public void ToggleCustomizationMenu()
        {
            if (m_isCustomizationMenuOpen)
            {
                InputHandler.Instance.EnableGameplayInput();
                CloseCustomizationMenu();
            }
            else
            {
                InputHandler.Instance.EnableMenuInput();
                OpenCustomizationMenu();
            }
        }

        public void OpenCustomizationMenu()
        {
            m_isCustomizationMenuOpen = true;

            // Enable the customization menu canvas GameObject
            customizationMenuController.gameObject.SetActive(true);
            customizationMenuController.OnMenuOpened();
            Debug.Log("[GUIManager] Customization menu opened");
        }

        public void CloseCustomizationMenu()
        {
            m_isCustomizationMenuOpen = false;

            // Disable the customization menu canvas GameObject
            customizationMenuController.gameObject.SetActive(false);
            Debug.Log("[GUIManager] Customization menu closed");
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
