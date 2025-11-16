using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using GameKit.Dependencies.Utilities.Types;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RooseLabs.Gameplay
{
    [DefaultExecutionOrder(-99)]
    public partial class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Serialized
        [SerializeField][Scene] private string lobbyScene;
        [SerializeField][Scene] private string[] heistScenes;
        [field: SerializeField] public RuneDatabase RuneDatabase { get; private set; }
        [field: SerializeField] public SpellDatabase SpellDatabase { get; private set; }
        [field: SerializeField] public TaskDatabase TaskDatabase { get; private set; }
        #endregion

        private HeistTimer m_heistTimer;

        public AssignmentData CurrentAssignment { get; private set; }

        private void Awake()
        {
            Instance = this;
            m_heistTimer = GetComponent<HeistTimer>();
        }

        private void OnDestroy()
        {
            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoaded;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoaded;
        }

        private void HandleSceneLoaded(SceneLoadEndEventArgs args)
        {
            if (args.LoadedScenes.Length == 0) return;

            bool hasLoadedLobby = false;
            bool hasLoadedHeistScene = false;
            foreach (var scene in args.LoadedScenes)
            {
                this.LogInfo($"Loaded Scene: {scene.name}");
                if (scene.name == GetSceneName(lobbyScene))
                    hasLoadedLobby = true;
                else if (heistScenes.Any(heistScene => scene.name == GetSceneName(heistScene)))
                    hasLoadedHeistScene = true;
            }

            if (hasLoadedLobby)
            {
                HandleLobbyLoaded();
            }
            else if (hasLoadedHeistScene)
            {
                HandleHeistSceneLoaded();
            }
        }

        public void StartHeist()
        {
            if (!IsServerInitialized) return;
            int randomIndex = Random.Range(0, heistScenes.Length);
            string selectedSceneName = GetSceneName(heistScenes[randomIndex]);
            SceneManagement.SceneManager.Instance.LoadScene(selectedSceneName, PlayerHandler.CharacterNetworkObjects);
        }

        public void EndHeist()
        {
            if (!IsServerInitialized) return;
            SceneManagement.SceneManager.Instance.LoadScene(GetSceneName(lobbyScene), PlayerHandler.CharacterNetworkObjects);
        }

        private void HandleLobbyLoaded()
        {
            if (!IsServerInitialized) return;
            if (CurrentAssignment == null)
            {
                GenerateNewAssignment();
            }
            else
            {
                // TODO: Check if all tasks are completed
            }
        }

        private void HandleHeistSceneLoaded()
        {
            m_heistTimer.ShowTimer();
            if (IsServerInitialized)
            {
                SpawnHeistRuneContainerObjects();
                m_heistTimer.StartTimer(m_heistTimer.defaultTime);
            }
            else
            {
                DestroyRuneObjectSpawnPoints();
            }
        }

        private void GenerateNewAssignment()
        {
            CurrentAssignment = new AssignmentData
            {
                assignmentNumber = CurrentAssignment != null ? CurrentAssignment.assignmentNumber + 1 : 0,
                tasks = new List<int> { TaskDatabase.GetRandomIndex() }
            };
            NotebookManager.Instance.InitializeAssignment(CurrentAssignment);
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
