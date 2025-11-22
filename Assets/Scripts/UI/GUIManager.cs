using System.Runtime.CompilerServices;
using RooseLabs.Core;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Player;
using RooseLabs.Player.Customization;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.UI
{
    public class GUIManager : MonoBehaviour
    {
        private static Logger Logger => Logger.GetLogger("GUIManager");

        public static GUIManager Instance { get; private set; }

        [SerializeField] private HUDManager hudManager;
        [SerializeField] private NotebookUIController notebookUIController;
        [SerializeField] private CustomizationMenu customizationMenuController;

        private bool m_isNotebookOpen = false;
        private bool m_isCustomizationMenuOpen = false;
        private bool WindowIsOpen => m_isNotebookOpen || m_isCustomizationMenuOpen;

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

        private void LateUpdate()
        {
            if (WindowIsOpen)
            {
                // When any window is open, clear interaction text
                hudManager.SetInteractionText(string.Empty);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetHUDActive(bool isActive) => hudManager.gameObject.SetActive(isActive);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTimerActive(bool isActive) => hudManager.SetTimerActive(isActive);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTimer(float time) => hudManager.UpdateTimer(time);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInteractionText(string text)
        {
            if (WindowIsOpen) return;
            hudManager.SetInteractionText(text);
        }
    }
}
