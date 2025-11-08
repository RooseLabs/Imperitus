using System.IO;
using FishNet.Managing;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using GameKit.Dependencies.Utilities.Types;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.SceneManagement
{
    public class SceneManager : MonoBehaviour
    {
        private static Logger Logger => Logger.GetLogger("SceneManager");

        #region Serialized
        /// <summary>
        /// True to replace all scenes with the offline scene immediately.
        /// </summary>
        [Tooltip("True to replace all scenes with the offline scene immediately.")]
        [SerializeField]
        private bool startInOffline = true;
        /// <summary>
        /// </summary>
        [Tooltip("Scene to load when disconnected. Server and client will load this scene.")]
        [SerializeField]
        [Scene]
        private string offlineScene;

        /// <summary>
        /// </summary>
        [Tooltip("Scene containing gameplay managers, loaded before the online scene.")]
        [SerializeField]
        [Scene]
        private string gameplayManagersScene;

        /// <summary>
        /// </summary>
        [Tooltip("Scene to load when connected. Server and client will load this scene.")]
        [SerializeField]
        [Scene]
        private string startingOnlineScene;
        #endregion

        public static SceneManager Instance { get; private set; }

        private NetworkManager m_networkManager;
        private string m_currentlyLoadedOnlineScene = string.Empty;
        private string m_pendingUnloadAfterLoad = string.Empty;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Deinitialize();
        }

        private void Initialize()
        {
            m_networkManager = GetComponentInParent<NetworkManager>();
            if (m_networkManager == null)
            {
                Logger.Error($"NetworkManager not found on {gameObject.name} or any parent objects. SceneManager will not work.");
                return;
            }
            // A NetworkManager won't be initialized if it's being destroyed.
            if (!m_networkManager.Initialized)
                return;
            if (startingOnlineScene == string.Empty || offlineScene == string.Empty || gameplayManagersScene == string.Empty)
            {
                Logger.Warning("Online, offline, or gameplay managers scene is not specified. Scenes will not load properly.");
                return;
            }

            m_networkManager.ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
            m_networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            m_networkManager.SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
            if (startInOffline)
                LoadOfflineScene();
        }

        private void Deinitialize()
        {
            if (!ApplicationState.IsQuitting() && m_networkManager != null && m_networkManager.Initialized)
            {
                m_networkManager.ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
                m_networkManager.ServerManager.OnServerConnectionState -= ServerManager_OnServerConnectionState;
                m_networkManager.SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
            }
        }

        private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs obj)
        {
            bool currentOnlineSceneLoaded = false;
            bool startingOnlineSceneLoaded = false;
            bool gameplayManagersSceneLoaded = false;
            foreach (Scene s in obj.LoadedScenes)
            {
                if (s.name == m_currentlyLoadedOnlineScene)
                    currentOnlineSceneLoaded = true;
                if (s.name == GetSceneName(startingOnlineScene))
                    startingOnlineSceneLoaded = true;
                if (s.name == GetSceneName(gameplayManagersScene))
                    gameplayManagersSceneLoaded = true;
            }

            if (startingOnlineSceneLoaded)
            {
                m_currentlyLoadedOnlineScene = GetSceneName(startingOnlineScene);
                UnloadOfflineScene();
            }
            else if (gameplayManagersSceneLoaded)
            {
                LoadStartingOnlineScene();
            }
            else if (currentOnlineSceneLoaded && !string.IsNullOrEmpty(m_pendingUnloadAfterLoad))
            {
                Scene pendingScene = UnitySceneManager.GetSceneByName(m_pendingUnloadAfterLoad);
                if (pendingScene.IsValid())
                {
                    SceneUnloadData sud = new(pendingScene);
                    m_networkManager.SceneManager.UnloadGlobalScenes(sud);
                }
                else
                {
                    m_pendingUnloadAfterLoad = string.Empty;
                }
            }
        }

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs obj)
        {
            /* When server starts load scenes. */
            if (obj.ConnectionState == LocalConnectionState.Started)
            {
                /* If not exactly one server is started then
                 * that means either none are started, which isn't true because
                 * we just got a started callback, or two+ are started.
                 * When a server has already started there's no reason to load
                 * scenes again. */
                if (!m_networkManager.ServerManager.IsOnlyOneServerStarted())
                    return;

                // If here can load the gameplay managers scene.
                LoadGameplayManagersScene();
            }
            // When server stops load offline scene.
            else if (obj.ConnectionState == LocalConnectionState.Stopped && !m_networkManager.ServerManager.IsAnyServerStarted())
            {
                LoadOfflineScene();
            }
        }

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs obj)
        {
            if (obj.ConnectionState == LocalConnectionState.Stopped)
            {
                // Only load offline scene if not also server.
                if (!m_networkManager.IsServerStarted)
                    LoadOfflineScene();
            }
        }

        private void LoadGameplayManagersScene()
        {
            SceneLoadData sld = new(GetSceneName(gameplayManagersScene))
            {
                ReplaceScenes = ReplaceOption.All
            };
            m_networkManager.SceneManager.LoadGlobalScenes(sld);
        }

        private void LoadStartingOnlineScene()
        {
            SceneLoadData sld = new(GetSceneName(startingOnlineScene))
            {
                ReplaceScenes = ReplaceOption.None
            };
            sld.PreferredActiveScene = new PreferredScene(sld.SceneLookupDatas[0]);
            m_networkManager.SceneManager.LoadGlobalScenes(sld);
        }

        /// <summary>
        /// Loads a new online scene, replacing the previous one.
        /// This must be called on the server.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        /// <param name="movedNetworkObjects">NetworkObjects to move to the new scene.</param>
        public void LoadScene(string sceneName, NetworkObject[] movedNetworkObjects = null)
        {
            if (!m_networkManager.IsServerStarted)
            {
                Logger.Warning("LoadScene can only be called on an active server.");
                return;
            }

            Scene previousScene = UnitySceneManager.GetSceneByName(m_currentlyLoadedOnlineScene);
            if (previousScene.IsValid())
            {
                m_pendingUnloadAfterLoad = m_currentlyLoadedOnlineScene;
            }
            m_currentlyLoadedOnlineScene = sceneName;

            SceneLoadData sld = new(sceneName);
            sld.ReplaceScenes = ReplaceOption.None;
            sld.PreferredActiveScene = new PreferredScene(sld.SceneLookupDatas[0]);
            if (movedNetworkObjects != null)
                sld.MovedNetworkObjects = movedNetworkObjects;

            m_networkManager.SceneManager.LoadGlobalScenes(sld);
        }

        private void LoadOfflineScene()
        {
            // Already in offline scene.
            if (UnitySceneManager.GetActiveScene().name == GetSceneName(offlineScene))
                return;
            UnitySceneManager.LoadScene(offlineScene);
        }

        private void UnloadOfflineScene()
        {
            Scene s = UnitySceneManager.GetSceneByName(GetSceneName(offlineScene));
            if (string.IsNullOrEmpty(s.name))
                return;

            UnitySceneManager.UnloadSceneAsync(s);
        }

        private string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
