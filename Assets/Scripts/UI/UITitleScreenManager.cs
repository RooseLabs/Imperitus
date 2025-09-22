using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using RooseLabs.Core;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UITitleScreenManager : MonoBehaviour
    {
        [SerializeField] private UIMainMenuManager mainMenuPanel;
        // [SerializeField] private UISettingsManager settingsPanel;
        // [SerializeField] private UICreditsManager creditsPanel;

        // TODO: These should be moved to a JoinGamePanel script
        [SerializeField] private TMP_InputField ipAddressInputField;
        [SerializeField] private TMP_InputField portInputField;

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
            // SceneLoadData sld = new SceneLoadData("DevelopmentScene1")
            // {
            //     ReplaceScenes = ReplaceOption.All
            // };
            // InstanceFinder.SceneManager.LoadGlobalScenes(sld);
        }

        private void Start()
        {
            InputHandler.Instance.EnableMenuInput();
            mainMenuPanel.HostGameButtonAction += HostGameButtonClicked;
            mainMenuPanel.JoinGameButtonAction += OpenJoinGameScreen;
            mainMenuPanel.SettingsButtonAction += OpenSettingsScreen;
            mainMenuPanel.CreditsButtonAction += OpenCreditsScreen;
            mainMenuPanel.QuitGameButtonAction += QuitGame;
        }

        private void HostGameButtonClicked()
        {
            m_networkManager.ServerManager.StartConnection();
            m_networkManager.ClientManager.StartConnection();
        }

        private void OpenJoinGameScreen()
        {
            string ipAddress = string.IsNullOrWhiteSpace(ipAddressInputField.text) ? "localhost" : ipAddressInputField.text;
            m_networkManager.TransportManager.Transport.SetClientAddress(ipAddress);
            if (ushort.TryParse(portInputField.text, out var port))
                m_networkManager.TransportManager.Transport.SetPort(port);
            m_networkManager.ClientManager.StartConnection();
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
