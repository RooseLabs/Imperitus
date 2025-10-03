using System;
using System.Collections.Generic;
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

        private enum Region
        {
            Auto,
            NorthAmericaEast,
            NorthAmericaWest,
            NorthAmericaCentral,
            SouthAmericaEast,
            EuropeNorth,
            EuropeWest,
            EuropeCentral,
            AsiaSoutheast,
            AsiaNortheast,
            AsiaSouth,
            Australia
        }

        // From https://docs.unity.com/ugs/manual/relay/manual/locations-and-regions
        private static readonly Dictionary<Region, string> RegionToIdentifier = new()
        {
            { Region.Auto, null },
            { Region.NorthAmericaEast, "us-east1" },
            { Region.NorthAmericaWest, "us-west1" },
            { Region.NorthAmericaCentral, "us-central1" },
            { Region.SouthAmericaEast, "southamerica-east1" },
            { Region.EuropeNorth, "europe-north1" },
            { Region.EuropeWest, "europe-west4" },
            { Region.EuropeCentral, "europe-central2" },
            { Region.AsiaSoutheast, "asia-southeast1" },
            { Region.AsiaNortheast, "asia-northeast1" },
            { Region.AsiaSouth, "asia-south1" },
            { Region.Australia, "australia-southeast1" }
        };

        public static NetworkConnector Instance { get; private set; }

        [SerializeField] private NetworkManager networkManager;

        [Header("Unity Relay Settings")]
        [Tooltip("Connection type to use for the Unity Relay.")]
        [SerializeField] private ConnectionType connectionType = ConnectionType.UDP;
        [Tooltip("Maximum number of connections to allow to the Unity Relay.")]
        [SerializeField] private int maxConnections = 4;
        [Tooltip("Region to use for the Unity Relay. 'Auto' will select the best region based on the player's location.")]
        [SerializeField] private Region region = Region.Auto;

        private Multipass m_multipass;
        private Tugboat m_tugboat;
        private UnityTransport m_unityTransport;

        public string PlayerName { get; set; }
        public string CurrentSessionJoinCode { get; private set; }

        private void Awake()
        {
            m_multipass = networkManager.TransportManager.GetTransport<Multipass>();
            m_tugboat = m_multipass.GetTransport<Tugboat>();
            m_unityTransport = m_multipass.GetTransport<UnityTransport>();
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
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                // Request allocation and join code
                string regionIdentifier = RegionToIdentifier[region];
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections, regionIdentifier);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                // Configure transport
                m_unityTransport.SetRelayServerData(allocation.ToRelayServerData(connectionType.ToString().ToLower()));

                // Start host
                if (!m_unityTransport.StartConnection(true)) return null;
                CurrentSessionJoinCode = joinCode;
                m_multipass.SetClientTransport<UnityTransport>();
                m_unityTransport.StartConnection(false);
                return joinCode;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkConnector] Failed to start host with relay: {e.Message}");
                return null;
            }
        }

        public async Task<bool> StartClientWithRelay(string joinCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(joinCode))
                {
                    Debug.LogWarning("[NetworkConnector] Join code cannot be empty!");
                    return false;
                }
                joinCode = joinCode.ToUpper();

                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                m_unityTransport.SetRelayServerData(allocation.ToRelayServerData(connectionType.ToString().ToLower()));
                m_multipass.SetClientTransport<UnityTransport>();
                if (!m_unityTransport.StartConnection(false)) return false;
                CurrentSessionJoinCode = joinCode;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkConnector] Failed to start client with relay: {e.Message}");
                return false;
            }
        }
    }
}
