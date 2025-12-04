using RooseLabs.Gameplay;
using RooseLabs.Network;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIPauseScreenManager : MonoBehaviour, IWindow
    {
        [SerializeField] private UIPauseMenu pauseMenuPanel;

        private void OnEnable()
        {
            pauseMenuPanel.OnResumeButtonPressed += ResumeGame;
            pauseMenuPanel.OnSettingsButtonPressed += OpenSettingsMenu;
            pauseMenuPanel.OnMainMenuButtonPressed += ReturnToMainMenu;
            pauseMenuPanel.OnQuitGameButtonPressed += QuitGame;

            if (GameManager.IsSinglePlayer)
                Time.timeScale = 0f;
        }

        private void OnDisable()
        {
            pauseMenuPanel.OnResumeButtonPressed -= ResumeGame;
            pauseMenuPanel.OnSettingsButtonPressed -= OpenSettingsMenu;
            pauseMenuPanel.OnMainMenuButtonPressed -= ReturnToMainMenu;
            pauseMenuPanel.OnQuitGameButtonPressed -= QuitGame;

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

        private void ResumeGame()
        {
            GUIManager.CloseWindow(this);
        }

        private void OpenSettingsMenu()
        {
            // TODO: Add in-game settings menu
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
