using RooseLabs.Core;
using RooseLabs.Network;
using RooseLabs.Player;
using RooseLabs.UI.Elements;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UITitleScreenManager : MonoBehaviour
    {
        [SerializeField] private UIMainMenu mainMenuPanel;
        [SerializeField] private UISettingsScreenManager settingsPanel;
        // [SerializeField] private UICreditsManager creditsPanel;

        [Header("Join Game Panel")]
        [SerializeField] private UIConfirmPanel joinGamePanel;
        [SerializeField] private TMP_InputField joinGameInputField;

        [Header("Username Panel")]
        [SerializeField] private UIConfirmPanel usernamePanel;
        [SerializeField] private TMP_InputField usernameInputField;
        [SerializeField] private TMP_Text usernameButtonText;

        private const string PrefsUsernameKey = "PlayerUsername";

        private void Start()
        {
            InputHandler.Instance.EnableMenuInput();
            SubscribeToEvents();

            SetPlayerName();
        }

        private void SubscribeToEvents()
        {
            mainMenuPanel.OnHostGameButtonPressed += OnHostGameButtonClicked;
            mainMenuPanel.OnJoinGameButtonPressed += OpenJoinGameScreen;
            mainMenuPanel.OnPlayOfflineButtonPressed += OnPlayOfflineButtonClicked;
            mainMenuPanel.OnSettingsButtonPressed += OpenSettingsScreen;
            mainMenuPanel.OnCreditsButtonPressed += OpenCreditsScreen;
            mainMenuPanel.OnQuitGameButtonPressed += QuitGame;
            mainMenuPanel.OnUsernameButtonPressed += OpenUsernameScreen;
        }

        private void UnsubscribeFromEvents()
        {
            mainMenuPanel.OnHostGameButtonPressed -= OnHostGameButtonClicked;
            mainMenuPanel.OnJoinGameButtonPressed -= OpenJoinGameScreen;
            mainMenuPanel.OnPlayOfflineButtonPressed -= OnPlayOfflineButtonClicked;
            mainMenuPanel.OnSettingsButtonPressed -= OpenSettingsScreen;
            mainMenuPanel.OnCreditsButtonPressed -= OpenCreditsScreen;
            mainMenuPanel.OnQuitGameButtonPressed -= QuitGame;
            mainMenuPanel.OnUsernameButtonPressed -= OpenUsernameScreen;
        }

        private void SetPlayerName()
        {
            if (PlayerPrefs.HasKey(PrefsUsernameKey))
            {
                PlayerConnection.Nickname = PlayerPrefs.GetString(PrefsUsernameKey);
            }
            if (string.IsNullOrWhiteSpace(PlayerConnection.Nickname))
            {
                PlayerConnection.Nickname = "Player" + Random.Range(1000, 9999);
            }
            usernameButtonText.text = PlayerConnection.Nickname;
            PlayerPrefs.SetString(PrefsUsernameKey, PlayerConnection.Nickname);
            PlayerPrefs.Save();
        }

        private void OnPlayOfflineButtonClicked()
        {
            UnsubscribeFromEvents();
            NetworkConnector.Instance.StartHostLocally();
        }

        private async void OnHostGameButtonClicked()
        {
            UnsubscribeFromEvents();
            var result = await NetworkConnector.Instance.StartHostWithRelay();
            if (result == null) SubscribeToEvents();
        }

        private void OpenJoinGameScreen()
        {
            mainMenuPanel.gameObject.SetActive(false);
            joinGamePanel.gameObject.SetActive(true);
            joinGamePanel.OnConfirmButtonPressed += JoinGame;
            joinGamePanel.OnCancelButtonPressed += CloseJoinGameScreen;
        }

        private void CloseJoinGameScreen()
        {
            joinGamePanel.OnConfirmButtonPressed -= JoinGame;
            joinGamePanel.OnCancelButtonPressed -= CloseJoinGameScreen;
            joinGamePanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(true);
        }

        private async void JoinGame()
        {
            if (string.IsNullOrWhiteSpace(joinGameInputField.text))
            {
                NetworkConnector.Instance.StartClientLocally();
                return;
            }
            UnsubscribeFromEvents();
            var result = await NetworkConnector.Instance.StartClientWithRelay(joinGameInputField.text);
            if (!result) SubscribeToEvents();
        }

        private void OpenUsernameScreen()
        {
            mainMenuPanel.gameObject.SetActive(false);
            usernamePanel.gameObject.SetActive(true);
            usernameInputField.text = PlayerConnection.Nickname;
            usernamePanel.OnConfirmButtonPressed += SaveUsername;
            usernamePanel.OnCancelButtonPressed += CloseUsernameScreen;
        }

        private void CloseUsernameScreen()
        {
            usernamePanel.OnConfirmButtonPressed -= SaveUsername;
            usernamePanel.OnCancelButtonPressed -= CloseUsernameScreen;
            usernamePanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(true);
        }

        private void SaveUsername()
        {
            string newUsername = usernameInputField.text.Trim();

            if (string.IsNullOrWhiteSpace(newUsername))
                return;

            PlayerConnection.Nickname = newUsername;
            usernameButtonText.text = newUsername;

            PlayerPrefs.SetString(PrefsUsernameKey, newUsername);
            PlayerPrefs.Save();

            CloseUsernameScreen();
        }

        private void OpenSettingsScreen()
        {
            mainMenuPanel.gameObject.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
            settingsPanel.OnCloseButtonPressed += CloseSettingsScreen;
        }

        private void CloseSettingsScreen()
        {
            settingsPanel.OnCloseButtonPressed -= CloseSettingsScreen;
            settingsPanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(true);
        }

        private void OpenCreditsScreen()
        {
            // mainMenuPanel.gameObject.SetActive(false);
            // creditsPanel.gameObject.SetActive(true);
            // creditsPanel.BackButtonAction += CloseCreditsScreen;
        }

        private void CloseCreditsScreen()
        {
            // creditsPanel.BackButtonAction -= CloseCreditsScreen;
            // creditsPanel.gameObject.SetActive(false);
            mainMenuPanel.gameObject.SetActive(true);
        }

        private void QuitGame()
        {
            Application.Quit();
        }
    }
}
