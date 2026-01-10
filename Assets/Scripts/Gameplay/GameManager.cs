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
using RooseLabs.UI;
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

        public static bool IsSinglePlayer => NetworkConnector.Instance.CurrentSessionJoinCode == null;

        public const int MaxAttemptsPerAssignment = 3;
        public int CurrentAttemptNumber { get; private set; } = 1;

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
            SpellBase.OnSpellCast += OnTutorialSpellCast;
        }

        private void OnDisable()
        {
            SpellBase.OnSpellCast -= OnSpellCast;
            SpellBase.OnSpellCast -= OnTutorialSpellCast;
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
            UpdateTutorial();
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
            if (CurrentAssignment == null) return;
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
            else
            {
                if (CurrentAttemptNumber >= MaxAttemptsPerAssignment)
                {
                    // Failed assignment
                    GUIManager.Instance.PlayCutscene(
                        "You have failed to complete your assignment in time.",
                        "Your Magic Theory Professor is disappointed.",
                        "You have been given a new assignment."
                    );
                    GenerateNewAssignment();
                }
                else
                {
                    GUIManager.Instance.PlayCutscene(
                        "You have returned to the dormitory.",
                        "Your current assignment is still pending.",
                        $"You have <color=#FF0000>{MaxAttemptsPerAssignment - CurrentAttemptNumber} attempts</color> " +
                        $"remaining to complete this assignment."
                    );
                    CurrentAttemptNumber++;
                }
            }

            if (NotebookManager.Instance != null)
            {
                NotebookManager.Instance.UnlockSpellLoadout();
            }

            // Initialize tutorial when lobby is loaded
            InitializeTutorial();

            // Resume tutorial if it was paused
            ResumeTutorial();
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

        public void OnDormitoryDoorInteracted()
        {
            if (!IsServerInitialized) return;
            if (CurrentAssignment == null)
            {
                // If assignment is null, we are likely in a new game session.
                // Instead of starting a heist, we should show a cutscene and generate the first assignment.
                GenerateNewAssignment();
                GUIManager.Instance.PlayCutscene(
                    "Your Magic Theory Professor has given you a new assignment:",
                    $"<i>{TaskDatabase[CurrentAssignment!.tasks[0]].Description}</i>",
                    "You have <color=#FF0000>3 days</color> until the deadline to complete this assignment."
                );
            }
            else
            {
                StartHeist();
            }
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
