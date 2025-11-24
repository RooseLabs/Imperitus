using System.IO;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Name of the scene pending unload after the next scene load completes.
        /// </summary>
        private string m_pendingUnloadAfterLoadSceneName = string.Empty;

        private string m_currentlyLoadedOnlineSceneName = string.Empty;
        /// <summary>
        /// Name of the currently loaded online scene.
        /// </summary>
        private string CurrentlyLoadedOnlineSceneName
        {
            get => m_currentlyLoadedOnlineSceneName;
            set
            {
                if (m_currentlyLoadedOnlineSceneName == value) return;
                m_currentlyLoadedOnlineSceneName = value;
                m_currentOnlineScene = null;
            }
        }

        private Scene? m_currentOnlineScene;
        /// <summary>
        /// The currently loaded online scene.
        /// </summary>
        public Scene CurrentOnlineScene
        {
            get
            {
                m_currentOnlineScene ??= UnitySceneManager.GetSceneByName(CurrentlyLoadedOnlineSceneName);
                return m_currentOnlineScene.Value;
            }
        }

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
            bool startingOnlineSceneLoaded = false;
            bool gameplayManagersSceneLoaded = false;
            foreach (Scene s in obj.LoadedScenes)
            {
                Logger.Info($"Scene loaded: {s.name}");
                if (s.name == GetSceneName(gameplayManagersScene))
                {
                    gameplayManagersSceneLoaded = true;
                    continue;
                }
                if (s.name == GetSceneName(startingOnlineScene))
                    startingOnlineSceneLoaded = true;
                CurrentlyLoadedOnlineSceneName = s.name;
            }

            if (startingOnlineSceneLoaded)
            {
                UnloadOfflineScene();
            }
            else if (gameplayManagersSceneLoaded)
            {
                LoadStartingOnlineScene();
            }

            if (!string.IsNullOrEmpty(m_pendingUnloadAfterLoadSceneName))
            {
                Logger.Info($"Unloading pending scene '{m_pendingUnloadAfterLoadSceneName}'.");
                Scene pendingScene = UnitySceneManager.GetSceneByName(m_pendingUnloadAfterLoadSceneName);
                if (pendingScene.IsValid())
                {
                    Logger.Info("Previous scene is valid, unloading.");
                    SceneUnloadData sud = new(pendingScene);
                    m_networkManager.SceneManager.UnloadGlobalScenes(sud);
                }
                else
                {
                    Logger.Warning("Previous scene is invalid.");
                }
                m_pendingUnloadAfterLoadSceneName = string.Empty;
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
            Logger.Info("Loading Gameplay Managers scene.");
            SceneLoadData sld = new(GetSceneName(gameplayManagersScene))
            {
                ReplaceScenes = ReplaceOption.All
            };
            m_networkManager.SceneManager.LoadGlobalScenes(sld);
        }

        private void LoadStartingOnlineScene()
        {
            string startingOnlineSceneName = GetSceneName(startingOnlineScene);
            Logger.Info($"Loading starting online scene '{startingOnlineSceneName}'.");
            SceneLoadData sld = new(startingOnlineSceneName)
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

            Logger.Info($"Loading scene '{sceneName}'.");

            Scene previousScene = CurrentOnlineScene;
            if (previousScene.IsValid())
            {
                Logger.Info($"Scheduling previous scene '{CurrentlyLoadedOnlineSceneName}' for unload after new scene is loaded.");
                m_pendingUnloadAfterLoadSceneName = CurrentlyLoadedOnlineSceneName;
            }

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
            Logger.Info($"Loaded offline scene '{GetSceneName(offlineScene)}'.");
        }

        private void UnloadOfflineScene()
        {
            Scene s = UnitySceneManager.GetSceneByName(GetSceneName(offlineScene));
            if (string.IsNullOrEmpty(s.name))
                return;

            UnitySceneManager.UnloadSceneAsync(s);
            Logger.Info($"Unloaded offline scene '{s.name}'.");
        }

        /// <summary>
        /// Moves a GameObject to the currently loaded online scene.
        /// </summary>
        /// <param name="go">The GameObject to move.</param>
        public void MoveGameObjectToOnlineScene(GameObject go)
        {
            Scene onlineScene = CurrentOnlineScene;
            if (!onlineScene.IsValid())
            {
                Logger.Warning("Cannot move GameObject to online scene because no online scene is loaded.");
                return;
            }
            UnitySceneManager.MoveGameObjectToScene(go, onlineScene);
            Logger.Info($"Moved GameObject '{go.name}' to online scene '{onlineScene.name}'.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
