using RooseLabs.Gameplay;
using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIPauseScreenManager : MonoBehaviour, IWindow
    {
        [SerializeField] private UIPauseMenu pauseMenuPanel;
        [SerializeField] private UISettingsScreenManager settingsScreenManager;

        private void OnEnable()
        {
            OpenMainPanel();
            if (GameManager.IsSinglePlayer)
                Time.timeScale = 0f;
        }

        private void OnDisable()
        {
            CloseMainPanel();
            if (GameManager.IsSinglePlayer)
                Time.timeScale = 1f;
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OpenMainPanel()
        {
            pauseMenuPanel.gameObject.SetActive(true);
            pauseMenuPanel.OnResumeButtonPressed += ResumeGame;
            pauseMenuPanel.OnSettingsButtonPressed += OpenSettingsMenu;
            pauseMenuPanel.OnMainMenuButtonPressed += ReturnToMainMenu;
            pauseMenuPanel.OnQuitGameButtonPressed += QuitGame;
        }

        private void CloseMainPanel()
        {
            pauseMenuPanel.OnResumeButtonPressed -= ResumeGame;
            pauseMenuPanel.OnSettingsButtonPressed -= OpenSettingsMenu;
            pauseMenuPanel.OnMainMenuButtonPressed -= ReturnToMainMenu;
            pauseMenuPanel.OnQuitGameButtonPressed -= QuitGame;
            pauseMenuPanel.gameObject.SetActive(false);
        }

        private void ResumeGame()
        {
            GUIManager.CloseWindow(this);
        }

        private void OpenSettingsMenu()
        {
            CloseMainPanel();
            GUIManager.OpenWindow(settingsScreenManager);
            settingsScreenManager.OnCloseButtonPressed += () =>
            {
                GUIManager.CloseWindow(settingsScreenManager);
            };
            settingsScreenManager.OnClose += OpenMainPanel;
        }

        private void ReturnToMainMenu()
        {
            var networkConnector = NetworkConnector.Instance;
            if (networkConnector)
            {
                networkConnector.Disconnect();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
            }
        }

        private void QuitGame()
        {
            var networkConnector = NetworkConnector.Instance;
            if (networkConnector)
            {
                networkConnector.Disconnect();
            }
            Application.Quit();
        }
    }
}
