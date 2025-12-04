using System;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIPauseMenu : MonoBehaviour
    {
        public event Action OnResumeButtonPressed = delegate {};
        public event Action OnSettingsButtonPressed = delegate {};
        public event Action OnMainMenuButtonPressed = delegate {};
        public event Action OnQuitGameButtonPressed = delegate {};

        public void ResumeButton()
        {
            OnResumeButtonPressed.Invoke();
        }

        public void SettingsButton()
        {
            OnSettingsButtonPressed.Invoke();
        }

        public void MainMenuButton()
        {
            OnMainMenuButtonPressed.Invoke();
        }

        public void QuitGameButton()
        {
            OnQuitGameButtonPressed.Invoke();
        }
    }
}
