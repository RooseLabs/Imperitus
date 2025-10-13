using RooseLabs.Core;
using RooseLabs.Network;
using RooseLabs.Player;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UITitleScreenManager : MonoBehaviour
    {
        [SerializeField] private UIMainMenuManager mainMenuPanel;
        [SerializeField] private GameObject usernamePanel;
        [SerializeField] private TextMeshProUGUI currentUsernameGO;
        // [SerializeField] private UISettingsManager settingsPanel;
        // [SerializeField] private UICreditsManager creditsPanel;

        // TODO: This should be moved to a JoinGamePanel script
        [SerializeField] private TMP_InputField joinCodeInputField;

        private const string PrefsUsernameKey = "PlayerUsername";

        private void Start()
        {
            InputHandler.Instance.EnableMenuInput();
            mainMenuPanel.HostLocalGameButtonAction += HostLocalGameButtonClicked;
            mainMenuPanel.HostOnlineGameButtonAction += HostOnlineGameButtonClicked;
            mainMenuPanel.JoinGameButtonAction += OpenJoinGameScreen;
            mainMenuPanel.SettingsButtonAction += OpenSettingsScreen;
            mainMenuPanel.CreditsButtonAction += OpenCreditsScreen;
            mainMenuPanel.QuitGameButtonAction += QuitGame;
            mainMenuPanel.UsernameButtonAction += OpenUsernameScreen;
            mainMenuPanel.CloseUsernameAction += CloseUsernameScreen;
            mainMenuPanel.SaveUsernameAction += SaveUsername;

            SetPlayerName();
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
            currentUsernameGO.text = PlayerConnection.Nickname;
            PlayerPrefs.SetString(PrefsUsernameKey, PlayerConnection.Nickname);
            PlayerPrefs.Save();
        }

        private void HostLocalGameButtonClicked()
        {
            NetworkConnector.Instance.StartHostLocally();
        }

        private void HostOnlineGameButtonClicked()
        {
            _ = NetworkConnector.Instance.StartHostWithRelay();
        }

        private void OpenJoinGameScreen()
        {
            if (string.IsNullOrWhiteSpace(joinCodeInputField.text))
            {
                NetworkConnector.Instance.StartClientLocally();
                return;
            }
            _ = NetworkConnector.Instance.StartClientWithRelay(joinCodeInputField.text);
        }

        private void OpenUsernameScreen()
        {
            if (mainMenuPanel != null && mainMenuPanel.gameObject != null)
            {
                Transform parentTransform = mainMenuPanel.gameObject.transform;
                for (int i = 0; i < parentTransform.childCount; i++)
                {
                    parentTransform.GetChild(i).gameObject.SetActive(false);
                }
            }

            if (usernamePanel != null)
            {
                usernamePanel.SetActive(true);
                var input = usernamePanel.GetComponentInChildren<TMP_InputField>();
                if (input != null)
                    input.text = PlayerConnection.Nickname;
            }
        }

        private void CloseUsernameScreen()
        {
            mainMenuPanel.gameObject.SetActive(true);

            if (mainMenuPanel != null && mainMenuPanel.gameObject != null)
            {
                Transform parentTransform = mainMenuPanel.gameObject.transform;
                for (int i = 0; i < parentTransform.childCount; i++)
                {
                    parentTransform.GetChild(i).gameObject.SetActive(true);
                }
            }

            if (usernamePanel != null)
            {
                usernamePanel.SetActive(false);
            }
        }

        private void SaveUsername()
        {
            var input = usernamePanel.GetComponentInChildren<TMP_InputField>();
            if (input == null) return;

            string newUsername = input.text.Trim();

            if (string.IsNullOrWhiteSpace(newUsername))
                return;

            PlayerConnection.Nickname = newUsername;
            currentUsernameGO.text = newUsername;

            PlayerPrefs.SetString(PrefsUsernameKey, newUsername);
            PlayerPrefs.Save();

            CloseUsernameScreen();
        }

        private void OpenSettingsScreen()
        {
            // mainMenuPanel.gameObject.SetActive(false);
            // settingsPanel.gameObject.SetActive(true);
        }

        private void OpenCreditsScreen()
        {
            //mainMenuPanel.gameObject.SetActive(false);
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
