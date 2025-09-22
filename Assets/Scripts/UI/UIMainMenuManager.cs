using System;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIMainMenuManager : MonoBehaviour
    {
        public Action HostGameButtonAction;
        public Action JoinGameButtonAction;
        public Action SettingsButtonAction;
        public Action CreditsButtonAction;
        public Action QuitGameButtonAction;

        public void HostGameButton()
        {
            HostGameButtonAction.Invoke();
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
