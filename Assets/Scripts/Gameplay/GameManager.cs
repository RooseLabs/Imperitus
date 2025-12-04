using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GameKit.Dependencies.Utilities.Types;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Gameplay.Spells;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        public SyncList<int> LearnedSpellsIndices { get; } = new() { 0 };

        private HeistTimer m_heistTimer;

        public Scene CurrentScene => SceneManagement.SceneManager.Instance.CurrentOnlineScene;
        public AssignmentData CurrentAssignment { get; private set; }

        private void Awake()
        {
            Instance = this;
            TryGetComponent(out m_heistTimer);
        }

        private void OnEnable()
        {
            SpellBase.OnSpellCast += OnSpellCast;
        }

        private void OnDisable()
        {
            SpellBase.OnSpellCast -= OnSpellCast;
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

            if (NotebookManager.Instance != null)
            {
                NotebookManager.Instance.UnlockSpellLoadout();
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

        private void OnSpellCast(SpellSO spell)
        {
            if (CurrentAssignment == null) return;
            if (IsServerInitialized)
            {
                OnSpellCast_Internal(spell);
            }
            else
            {
                OnSpellCast_ServerRpc(spell.Signature);
            }
        }

        private void OnSpellCast_Internal(SpellSO spell)
        {
            if (CurrentAssignment == null) return;
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

        [ServerRpc(RequireOwnership = false)]
        private void OnSpellCast_ServerRpc(int spellSignature)
        {
            var spell = SpellDatabase.GetSpellBySignature(spellSignature);
            if (!spell) return;
            OnSpellCast_Internal(spell.SpellInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
