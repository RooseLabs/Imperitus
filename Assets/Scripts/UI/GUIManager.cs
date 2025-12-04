using System.Collections.Generic;
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

        [SerializeField] private HUDManager hudManager;
        [SerializeField] private UIPauseScreenManager pauseScreenManager;
        [SerializeField] private NotebookUIController notebookUIController;
        [SerializeField] private CustomizationMenu customizationMenuController;

        public static GUIManager Instance { get; private set; }
        public readonly static List<IWindow> ActiveWindows = new();

        private void Awake()
        {
            Instance = this;
            ActiveWindows.Clear();
        }

        #if !UNITY_EDITOR
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && ActiveWindows.Count == 0)
                OpenWindow(pauseScreenManager);
        }
        #endif

        private void LateUpdate()
        {
            var character = PlayerCharacter.LocalCharacter;
            if (!character) return;

            if (character.Input.resumeWasPressed)
            {
                // Close the topmost window
                if (ActiveWindows.Count > 0)
                {
                    CloseWindow(ActiveWindows[^1]);
                }
            }
            else if (character.Input.closeNotebookWasPressed)
            {
                // Close the notebook if it's open
                if (ActiveWindows.Contains(notebookUIController))
                {
                    CloseWindow(notebookUIController);
                }
            }
            else if (character.Input.openNotebookWasPressed)
            {
                OpenWindow(notebookUIController);
            }
            else if (character.Input.pauseWasPressed)
            {
                OpenWindow(pauseScreenManager);
            }
        }

        public static void OpenWindow(IWindow window)
        {
            if (ActiveWindows.Contains(window))
                return;
            window.Open();
            ActiveWindows.Add(window);
            InputHandler.Instance.EnableMenuInput();
        }

        public static void CloseWindow(IWindow window)
        {
            window.Close();
            ActiveWindows.Remove(window);
            if (ActiveWindows.Count == 0)
            {
                InputHandler.Instance.EnableGameplayInput();
            }
        }

        public void OpenCustomizationMenu()
        {
            OpenWindow(customizationMenuController);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetHUDActive(bool isActive) => hudManager.gameObject.SetActive(isActive);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTimerActive(bool isActive) => hudManager.SetTimerActive(isActive);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateTimer(float time) => hudManager.UpdateTimer(time);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetInteractionText(string text) => hudManager.SetInteractionText(text);
    }
}
