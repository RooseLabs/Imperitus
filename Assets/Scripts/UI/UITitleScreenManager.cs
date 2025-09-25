using System.Threading.Tasks;
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.UTP;
using RooseLabs.Core;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UITitleScreenManager : MonoBehaviour
    {
        [SerializeField] private UIMainMenuManager mainMenuPanel;
        // [SerializeField] private UISettingsManager settingsPanel;
        // [SerializeField] private UICreditsManager creditsPanel;

        // TODO: These should be moved to a JoinGamePanel script
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
            mainMenuPanel.HostGameButtonAction += HostGameButtonClicked;
            mainMenuPanel.JoinGameButtonAction += OpenJoinGameScreen;
            mainMenuPanel.SettingsButtonAction += OpenSettingsScreen;
            mainMenuPanel.CreditsButtonAction += OpenCreditsScreen;
            mainMenuPanel.QuitGameButtonAction += QuitGame;
        }

        private void HostGameButtonClicked()
        {
            StartHostWithRelay(4, "udp").ContinueWith(task =>
            {
                Debug.Log("Join Code: " + task.Result);
            });
        }

        private void OpenJoinGameScreen()
        {
            StartClientWithRelay(joinCodeInputField.text, "udp").ContinueWith(task =>
            {
                if (!task.Result)
                    Debug.LogError("Failed to connect to the server");
            });
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
        
        private async Task<string> StartHostWithRelay(int maxConnections, string connectionType)
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Request allocation and join code
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            // Configure transport
            var unityTransport = m_networkManager.TransportManager.GetTransport<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));

            // Start host
            if (m_networkManager.ServerManager.StartConnection()) // Server is successfully started.
            {
                m_networkManager.ClientManager.StartConnection(); // You can choose not to call this method. Then only the server will start.
                return joinCode;
            }
            return null;
        }

        private async Task<bool> StartClientWithRelay(string joinCode, string connectionType)
        {
            if (string.IsNullOrEmpty(joinCode)) return false;
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            var allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: joinCode);
            var unityTransport = m_networkManager.TransportManager.GetTransport<UnityTransport>();
            unityTransport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, connectionType));
            return m_networkManager.ClientManager.StartConnection();
        }
    }
}
