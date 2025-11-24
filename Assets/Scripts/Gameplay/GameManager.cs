using System.Collections.Generic;
using System.IO;
using System.Linq;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GameKit.Dependencies.Utilities.Types;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

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

        public readonly SyncList<int> LearnedSpellsIndices = new();

        private HeistTimer m_heistTimer;

        public AssignmentData CurrentAssignment { get; private set; }

        private void Awake()
        {
            Instance = this;
            TryGetComponent(out m_heistTimer);
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

        private void Update()
        {
            UpdateHeist();
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

        private void HandleLobbyLoaded()
        {
            m_heistTimer.ToggleTimerVisibility(false);
            if (!IsServerInitialized) return;
            if (CurrentAssignment == null)
            {
                GenerateNewAssignment();
            }
            else
            {
                // If all tasks are complete, generate new assignment.
                bool allComplete = true;
                foreach (var taskId in CurrentAssignment.tasks)
                {
                    var task = TaskDatabase[taskId];
                    if (!task.IsCompleted)
                    {
                        allComplete = false;
                        break;
                    }
                }
                if (allComplete)
                {
                    GenerateNewAssignment();
                }
            }
        }

        private void GenerateNewAssignment()
        {
            CurrentAssignment = new AssignmentData
            {
                assignmentNumber = CurrentAssignment != null ? CurrentAssignment.assignmentNumber + 1 : 1,
                tasks = new List<int> { TaskDatabase.GetRandomIndex(t => !t.IsCompleted) }
            };
            NotebookManager.Instance.InitializeAssignment(CurrentAssignment);
        }

        [ServerRpc(RequireOwnership = false)]
        public void OnSpellCast(SpellSO spell)
        {
            foreach (var taskId in CurrentAssignment.tasks)
            {
                var task = TaskDatabase[taskId];
                if (task.IsCompleted) continue;
                if (task.CompletionCondition is CastSpellCondition csc)
                {
                    if (csc.Spell == spell)
                    {
                        task.IsCompleted = true;
                    }
                }
            }
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }

        // Method that gets the current heist timer value
        public float GetHeistTimerValue()
        {
            return m_heistTimer != null ? m_heistTimer.GetRemainingTime() : 0f;
        }
    }
}
