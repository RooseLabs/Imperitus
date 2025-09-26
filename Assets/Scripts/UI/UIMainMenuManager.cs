using System;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIMainMenuManager : MonoBehaviour
    {
        public Action HostLocalGameButtonAction;
        public Action HostOnlineGameButtonAction;
        public Action JoinGameButtonAction;
        public Action SettingsButtonAction;
        public Action CreditsButtonAction;
        public Action QuitGameButtonAction;

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
