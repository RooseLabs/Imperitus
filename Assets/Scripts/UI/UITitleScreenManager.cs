using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using RooseLabs.Core;
using RooseLabs.Network;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UITitleScreenManager : MonoBehaviour
    {
        [SerializeField] private UIMainMenuManager mainMenuPanel;
        // [SerializeField] private UISettingsManager settingsPanel;
        // [SerializeField] private UICreditsManager creditsPanel;

        // TODO: This should be moved to a JoinGamePanel script
        [SerializeField] private TMP_InputField joinCodeInputField;

        private NetworkManager m_networkManager;

        private void Awake()
        {
            m_networkManager = InstanceFinder.NetworkManager;
        }

        private void OnEnable()
        {
            if (!m_networkManager) return;
            m_networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
        }

        private void OnDisable()
        {
            if (!m_networkManager) return;
            m_networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
        }

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            if (obj.ConnectionState != LocalConnectionState.Started) return;
            SceneLoadData sld = new SceneLoadData("DevelopmentScene1")
            {
                ReplaceScenes = ReplaceOption.All
            };
            InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }

        private void Start()
        {
            InputHandler.Instance.EnableMenuInput();
            mainMenuPanel.HostLocalGameButtonAction += HostLocalGameButtonClicked;
            mainMenuPanel.HostOnlineGameButtonAction += HostOnlineGameButtonClicked;
            mainMenuPanel.JoinGameButtonAction += OpenJoinGameScreen;
            mainMenuPanel.SettingsButtonAction += OpenSettingsScreen;
            mainMenuPanel.CreditsButtonAction += OpenCreditsScreen;
            mainMenuPanel.QuitGameButtonAction += QuitGame;
        }

        private void HostLocalGameButtonClicked()
        {
            NetworkConnector.Instance.StartHostLocally();
        }

        private async void HostOnlineGameButtonClicked()
        {
            string code = await NetworkConnector.Instance.StartHostWithRelay();
            Debug.Log($"Lobby Code: {code}");
        }

        private async void OpenJoinGameScreen()
        {
            if (string.IsNullOrWhiteSpace(joinCodeInputField.text))
            {
                NetworkConnector.Instance.StartClientLocally();
                return;
            }
            await NetworkConnector.Instance.StartClientWithRelay(joinCodeInputField.text);
        }

        private void OpenSettingsScreen()
        {
            // mainMenuPanel.gameObject.SetActive(false);
            // settingsPanel.gameObject.SetActive(true);
        }

        private void OpenCreditsScreen()
        {
            mainMenuPanel.gameObject.SetActive(false);
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
