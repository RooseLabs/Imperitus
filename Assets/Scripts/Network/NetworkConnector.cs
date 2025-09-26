using System.Threading.Tasks;
using FishNet.Managing;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using FishNet.Transporting.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace RooseLabs.Network
{
    public class NetworkConnector : MonoBehaviour
    {
        private enum ConnectionType
        {
            UDP,
            DTLS,
            WS,
            WSS
        }

        public static NetworkConnector Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;

        [Header("Unity Relay Settings")]
        [Tooltip("Connection type to use for the Unity Relay.")]
        [SerializeField] private ConnectionType connectionType = ConnectionType.UDP;
        [Tooltip("Maximum number of connections to allow to the Unity Relay.")]
        [SerializeField] private int maxConnections = 4;

        private Multipass m_multipass;
        private Tugboat m_tugboat;
        private UnityTransport m_unityTransport;

        public string CurrentSessionJoinCode { get; private set; }

        private void Awake()
        {
            m_multipass = networkManager.TransportManager.GetTransport<Multipass>();
            m_tugboat = networkManager.TransportManager.GetTransport<Tugboat>();
            m_unityTransport = networkManager.TransportManager.GetTransport<UnityTransport>();
            Instance = this;
        }

        public bool StartHostLocally()
        {
            return m_tugboat.StartConnection(true) && StartClientLocally();
        }

        public bool StartClientLocally()
        {
            m_multipass.SetClientTransport<Tugboat>();
            return m_tugboat.StartConnection(false);
        }

        public async Task<string> StartHostWithRelay()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Request allocation and join code
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            // Configure transport
            m_unityTransport.SetRelayServerData(allocation.ToRelayServerData(nameof(connectionType).ToLower()));

            // Start host
            if (m_unityTransport.StartConnection(true))
            {
                CurrentSessionJoinCode = joinCode;
                m_multipass.SetClientTransport<UnityTransport>();
                m_unityTransport.StartConnection(false);
                return joinCode;
            }
            return null;
        }

        public async Task<bool> StartClientWithRelay(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                Debug.LogWarning("[NetworkConnector] Join code cannot be empty!");
                return false;
            }
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            m_unityTransport.SetRelayServerData(allocation.ToRelayServerData(nameof(connectionType).ToLower()));
            m_multipass.SetClientTransport<UnityTransport>();
            if (!m_unityTransport.StartConnection(false)) return false;
            CurrentSessionJoinCode = joinCode;
            return true;
        }
    }
}
