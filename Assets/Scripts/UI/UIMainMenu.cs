using System;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIMainMenu : MonoBehaviour
    {
        public event Action OnHostGameButtonPressed = delegate {};
        public event Action OnJoinGameButtonPressed = delegate {};
        public event Action OnPlayOfflineButtonPressed = delegate {};
        public event Action OnSettingsButtonPressed = delegate {};
        public event Action OnCreditsButtonPressed = delegate {};
        public event Action OnQuitGameButtonPressed = delegate {};
        public event Action OnUsernameButtonPressed = delegate {};
        public event Action OnCloseUsernameButtonPressed = delegate {};
        public event Action OnSaveUsernameButtonPressed = delegate {};

        public void HostGameButton()
        {
            OnHostGameButtonPressed.Invoke();
        }

        public void JoinGameButton()
        {
            OnJoinGameButtonPressed.Invoke();
        }

        public void PlayOfflineButton()
        {
            OnPlayOfflineButtonPressed.Invoke();
        }

        public void SettingsButton()
        {
            OnSettingsButtonPressed.Invoke();
        }

        public void CreditsButton()
        {
            OnCreditsButtonPressed.Invoke();
        }

        public void QuitGameButton()
        {
            OnQuitGameButtonPressed.Invoke();
        }

        public void UsernameButton()
        {
            OnUsernameButtonPressed.Invoke();
        }

        public void CloseUsername()
        {
            OnCloseUsernameButtonPressed.Invoke();
        }

        public void SaveUsername()
        {
            OnSaveUsernameButtonPressed.Invoke();
        }
    }
}
