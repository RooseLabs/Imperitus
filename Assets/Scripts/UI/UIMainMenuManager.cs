using System;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIMainMenuManager : MonoBehaviour
    {
        public event Action HostLocalGameButtonAction = delegate {};
        public event Action HostOnlineGameButtonAction = delegate {};
        public event Action JoinGameButtonAction = delegate {};
        public event Action SettingsButtonAction = delegate {};
        public event Action CreditsButtonAction = delegate {};
        public event Action QuitGameButtonAction = delegate {};

        public void HostLocalGameButton()
        {
            HostLocalGameButtonAction.Invoke();
        }

        public void HostOnlineGameButton()
        {
            HostOnlineGameButtonAction.Invoke();
        }

        public void JoinGameButton()
        {
            JoinGameButtonAction.Invoke();
        }

        public void SettingsButton()
        {
            SettingsButtonAction.Invoke();
        }

        public void CreditsButton()
        {
            CreditsButtonAction.Invoke();
        }

        public void QuitGameButton()
        {
            QuitGameButtonAction.Invoke();
        }
    }
}
