using RooseLabs.Gameplay.Notebook;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.ScriptableObjects;
using RooseLabs.Player;
using RooseLabs.UI;
using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;
using FishNet.Object;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        #region Tutorial Sequence Flow
        private class TutorialSequence
        {
            private TutorialTask[] m_tasks;
            private int m_currentTaskIndex = 0;
            private bool m_tutorialComplete = false;
            private TextMeshProUGUI m_taskDisplayText;
            private PlayerCharacter m_player;
            private bool m_isSubscribedToInputEvents = false;

            private const string TutorialCompleteKey = "TutorialComplete";
            private const string TutorialProgressKey = "TutorialProgress_";
            private const string TutorialPausedKey = "TutorialPaused";

            private float m_taskCompletionDelayBuffer = 3f;
            private float m_taskCompletionTimer = 0f;
            private bool m_isTaskCompletionInProgress = false;

            private bool m_isPaused = false;

            private SearchForRunesManager m_searchForRunesManager;

            private static readonly Dictionary<TaskType, (string description, string[] actionNames)> TaskConfig = new()
            {
                { TaskType.WalkAround, ("Use {Move} to walk around.", new[] { "Move" }) },
                { TaskType.SearchForRunes, ("Find the 3 Rune Books in this room. Use {Interact} to interact and extract each rune to your own notebook.", new[] { "Interact" }) },
                { TaskType.OpenNotebook, ("Press {OpenNotebook} to open your notebook.", new[] { "OpenNotebook" }) },
                { TaskType.CombineRunes, ("Open the Runes tab and toggle ({Cast}) the 3 runes to combine them into the Impero spell in your wand.", new[] { "Cast" }) },
                { TaskType.AimWithWand, ("The Impero spell has been learned and added to your wand! Hold {Aim} to aim with your wand.", new[] { "Aim" }) },
                { TaskType.CastImperoSpell, ("Hold {Cast} while aiming ({Aim}) with your wand to use the Impero spell and levitate an object. Try using it on the pillows!", new[] { "Cast", "Aim" }) },
                { TaskType.TutorialComplete, ("Tutorial Complete!", System.Array.Empty<string>()) }
            };

            private Dictionary<TaskType, System.Func<bool>> m_taskCompletionChecks;

            public TutorialSequence(TextMeshProUGUI taskDisplayText)
            {
                m_taskDisplayText = taskDisplayText;
                InitializeTaskCompletionChecks();
                InitializeTasks();
            }

            private void InitializeTaskCompletionChecks()
            {
                m_taskCompletionChecks = new Dictionary<TaskType, System.Func<bool>>
                {
                    { TaskType.WalkAround, () => m_player.Input.movementInput.sqrMagnitude > 0.01f },
                    { TaskType.SearchForRunes, () => m_searchForRunesManager != null && m_searchForRunesManager.AreAllRunesCollected() },
                    { TaskType.OpenNotebook, () =>
                        GUIManager.Instance != null &&
                        GUIManager.ActiveWindows.Count > 0 &&
                        GUIManager.ActiveWindows[^1] is NotebookUIController },
                    { TaskType.CombineRunes, () => CheckIfImperoRunesToggled() },
                    { TaskType.AimWithWand, () => m_player.Data.isAiming },
                    { TaskType.CastImperoSpell, () => false }, // Handled by OnSpellCastCallback
                    { TaskType.TutorialComplete, () => true } // Completes immediately after delay
                };
            }

            private bool CheckIfImperoRunesToggled()
            {
                PlayerNotebook notebook = m_player?.Notebook;
                if (notebook == null)
                    return false;

                SpellSO imperoSpell = Instance.m_imperoSpell;
                if (imperoSpell == null || imperoSpell.Runes == null)
                    return false;

                // Get the currently toggled runes
                var toggledRunes = notebook.GetToggledRuneObjects();
                if (toggledRunes.Count != imperoSpell.Runes.Length)
                    return false;

                // Check if all Impero runes are toggled
                foreach (var rune in imperoSpell.Runes)
                {
                    if (!toggledRunes.Contains(rune))
                        return false;
                }

                return true;
            }

            private void InitializeTasks()
            {
                m_tasks = TaskConfig.Select(kvp =>
            new TutorialTask(kvp.Value.description, kvp.Key, kvp.Value.actionNames)).ToArray();
            }

            private string GetSpriteMarkupForAction(string actionName)
            {
                var action = InputHandler.GameplayActions?.FindAction(actionName);
                if (action == null)
                    return $"{{{actionName}}}"; // Return placeholder if action not found

                var tags = InputSpriteData.GetAllSpriteTags(action, InputHandler.CurrentInputScheme).ToList();

                // Fallback to other scheme if no tags found
                if (tags.Count == 0)
                {
                    var fallbackScheme = InputHandler.CurrentInputScheme == InputScheme.KeyboardMouse
                    ? InputScheme.Gamepad
                      : InputScheme.KeyboardMouse;
                    tags = InputSpriteData.GetAllSpriteTags(action, fallbackScheme).ToList();
                }

                if (tags.Count == 0)
                    return $"{{{actionName}}}"; // Return placeholder if no tags found

                // Combine all sprite tags into a single string
                return string.Join(" ", tags.Select(tag => $"<sprite name=\"{tag}\">"));
            }

            public void Initialize()
            {
                // Check if tutorial was previously completed
                bool tutorialWasCompleted = PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1;

                if (tutorialWasCompleted)
                {
                    m_tutorialComplete = true;
                    HideTutorial();
                    return;
                }

                // Load progress from PlayerPrefs
                m_currentTaskIndex = PlayerPrefs.GetInt(TutorialProgressKey + "Index", 0);

                // If we're past SearchForRunes but before completion,
                // we need to reset back to SearchForRunes since runes are cleared on heist start
                // This ensures the player can re-collect runes and complete the tutorial
                int searchForRunesIndex = GetTaskIndex(TaskType.SearchForRunes);

                if (m_currentTaskIndex > searchForRunesIndex && m_currentTaskIndex <= m_tasks.Length - 1)
                {
                    // Check if player has all the required runes - if not, reset to SearchForRunes
                    bool hasAllRunes = CheckIfPlayerHasAllRequiredRunes();
                    if (!hasAllRunes)
                    {
                       
                        m_currentTaskIndex = searchForRunesIndex;
                        SaveProgress();
                    }
                }

                for (int i = 0; i < m_currentTaskIndex; i++)
                {
                    m_tasks[i].MarkComplete();
                }

                // Check if tutorial was paused
                bool wasPaused = PlayerPrefs.GetInt(TutorialPausedKey, 0) == 1;

                if (wasPaused)
                {
                    m_isPaused = true;
                    HideTutorial();
                }
                else
                {
                    ShowTutorial();
                }

                SubscribeToInputEvents();
            }

            private int GetTaskIndex(TaskType taskType)
            {
                for (int i = 0; i < m_tasks.Length; i++)
                {
                    if (m_tasks[i].Type == taskType)
                        return i;
                }
                return -1;
            }

            private bool CheckIfPlayerHasAllRequiredRunes()
            {
                PlayerNotebook notebook = PlayerCharacter.LocalCharacter?.Notebook;
                if (notebook == null)
                    return false;

                SpellSO imperoSpell = Instance.m_imperoSpell;
                if (imperoSpell == null || imperoSpell.Runes == null)
                    return false;

                foreach (var rune in imperoSpell.Runes)
                {
                    if (!notebook.HasRune(rune))
                        return false;
                }

                return true;
            }

            private void SubscribeToInputEvents()
            {
                if (!m_isSubscribedToInputEvents && InputHandler.Instance != null)
                {
                    InputHandler.Instance.InputSchemeChanged += OnInputSchemeChanged;
                    InputHandler.Instance.InputDeviceChanged += OnInputDeviceChanged;
                    m_isSubscribedToInputEvents = true;
                }
            }

            private void UnsubscribeFromInputEvents()
            {
                if (m_isSubscribedToInputEvents && InputHandler.Instance != null)
                {
                    InputHandler.Instance.InputSchemeChanged -= OnInputSchemeChanged;
                    InputHandler.Instance.InputDeviceChanged -= OnInputDeviceChanged;
                    m_isSubscribedToInputEvents = false;
                }
            }

            public void Update()
            {
                if (m_tutorialComplete || m_currentTaskIndex >= m_tasks.Length)
                    return;
                if (m_isPaused)
                    return;
                m_player = PlayerCharacter.LocalCharacter;
                if (!m_player)
                    return;

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];

                // Handle SearchForRunes task initialization
                // Request book spawning from server (any client can trigger this)
                if (currentTask.Type == TaskType.SearchForRunes && !currentTask.Completed && m_searchForRunesManager == null)
                {
                    // Create manager on all clients to track state
                    m_searchForRunesManager = new SearchForRunesManager();

                    // Request the server to spawn books (only happens once due to server-side check)
                    Instance.RequestSpawnTutorialBooks_ServerRpc();
                }

                if (m_isTaskCompletionInProgress)
                {
                    m_taskCompletionTimer -= Time.deltaTime;
                    if (m_taskCompletionTimer <= 0f)
                    {
                        CompleteCurrentTask();
                        m_isTaskCompletionInProgress = false;
                    }
                    return;
                }

                // CastImperoSpell is handled by OnSpellCastCallback, not by continuous check
                if (currentTask.Type != TaskType.CastImperoSpell && !currentTask.Completed &&
               m_taskCompletionChecks[currentTask.Type].Invoke())
                {
                    MarkTaskForCompletion();
                }
            }

            private void CompleteCurrentTask()
            {
                m_tasks[m_currentTaskIndex].MarkComplete();

                m_currentTaskIndex++;
                if (m_currentTaskIndex >= m_tasks.Length)
                {
                    CompleteTutorial();
                }
                else
                {
                    SaveProgress();
                    UpdateTaskDisplay();
                }
            }

            private void MarkTaskForCompletion()
            {
                if (!m_isTaskCompletionInProgress)
                {
                    m_isTaskCompletionInProgress = true;
                    m_taskCompletionTimer = m_taskCompletionDelayBuffer;
                }
            }

            public void OnSpellCastCallback(SpellSO spell)
            {
                if (m_tutorialComplete || m_currentTaskIndex >= m_tasks.Length)
                    return;

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];
                if (currentTask.Type == TaskType.CastImperoSpell && !currentTask.Completed)
                {
                    if (Instance.SpellDatabase[0].SpellInfo == spell)
                    {
                        MarkTaskForCompletion();
                    }
                }
            }

            private void ShowTutorial()
            {
                if (m_taskDisplayText != null)
                {
                    m_taskDisplayText.gameObject.SetActive(true);
                    UpdateTaskDisplay();
                }
            }

            private void HideTutorial()
            {
                if (m_taskDisplayText != null)
                {
                    m_taskDisplayText.gameObject.SetActive(false);
                    m_taskDisplayText.text = string.Empty;
                }
            }

            private void UpdateTaskDisplay()
            {
                if (m_taskDisplayText == null || m_currentTaskIndex >= m_tasks.Length)
                    return;

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];

                // Start with the base description
                string formattedText = currentTask.Description;

                // Replace all {ActionName} placeholders with corresponding sprite markup
                foreach (string actionName in currentTask.ActionNames)
                {
                    string placeholder = $"{{{actionName}}}";
                    string spriteMarkup = GetSpriteMarkupForAction(actionName);
                    formattedText = formattedText.Replace(placeholder, spriteMarkup);
                }

                m_taskDisplayText.text = formattedText;
                m_taskDisplayText.spriteAsset = InputSpriteData.GetSpriteAssetForInputDevice(InputHandler.CurrentInputDevice);
            }

            private void CompleteTutorial()
            {
                m_tutorialComplete = true;
                PlayerPrefs.SetInt(TutorialCompleteKey, 1);
                PlayerPrefs.DeleteKey(TutorialPausedKey);
                PlayerPrefs.Save();
                HideTutorial();
                UnsubscribeFromInputEvents();

                // Reinitialize the local player's loadout so Impero becomes a permanent spell
                var localCharacter = PlayerCharacter.LocalCharacter;
                if (localCharacter != null)
                {
                    // Clear toggled runes since Impero is now permanent
                    localCharacter.Notebook?.ClearToggledRunes();

                    // Reinitialize notebook loadout
                    localCharacter.Notebook?.InitializeSpellLoadout();

                    // Reinitialize wand loadout to make Impero permanent
                    localCharacter.Wand?.ReinitializeSpellLoadout();
                }
            }

            private void SaveProgress()
            {
                PlayerPrefs.SetInt(TutorialProgressKey + "Index", m_currentTaskIndex);
                PlayerPrefs.SetInt(TutorialPausedKey, m_isPaused ? 1 : 0);
                PlayerPrefs.Save();
            }

            public bool IsTutorialComplete() => m_tutorialComplete;

            public static void ResetTutorial()
            {
                PlayerPrefs.DeleteKey(TutorialCompleteKey);
                for (int i = 0; i < 4; i++)
                {
                    PlayerPrefs.DeleteKey(TutorialProgressKey + "Index");
                }
                PlayerPrefs.DeleteKey(TutorialPausedKey);
                PlayerPrefs.Save();
            }

            public void SetTaskCompletionDelay(float delaySeconds)
            {
                m_taskCompletionDelayBuffer = Mathf.Max(0f, delaySeconds);
            }

            public void Pause()
            {
                m_isPaused = true;
                HideTutorial();
                PlayerPrefs.SetInt(TutorialPausedKey, 1);
                PlayerPrefs.Save();
            }

            public void Resume()
            {
                if (m_tutorialComplete)
                    return;
                m_isPaused = false;
                ShowTutorial();
                PlayerPrefs.SetInt(TutorialPausedKey, 0);
                PlayerPrefs.Save();
            }

            private void OnInputSchemeChanged(InputScheme newScheme)
            {
                if (!m_tutorialComplete && !m_isPaused && m_currentTaskIndex < m_tasks.Length)
                {
                    UpdateTaskDisplay();
                }
            }

            private void OnInputDeviceChanged(InputDevice newDevice)
            {
                if (!m_tutorialComplete && !m_isPaused && m_currentTaskIndex < m_tasks.Length)
                    UpdateTaskDisplay();
            }

            private class SearchForRunesManager
            {
                public SearchForRunesManager() {}

                public bool AreAllRunesCollected()
                {
                    // Check if the player's notebook contains all the runes from the Impero spell
                    PlayerNotebook notebook = PlayerCharacter.LocalCharacter?.Notebook;
                    if (notebook == null)
                        return false;

                    // Get the Impero spell's runes to check against
                    SpellSO imperoSpell = Instance.m_imperoSpell;
                    if (imperoSpell == null || imperoSpell.Runes == null)
                        return false;

                    // Check if all runes from the Impero spell are in the player's notebook
                    foreach (var rune in imperoSpell.Runes)
                    {
                        if (!notebook.HasRune(rune))
                            return false;
                    }

                    return true;
                }
            }
        }
        #endregion

        #region Tutorial Sequence Integration
        private TutorialSequence m_tutorialSequence;
        [SerializeField] private TextMeshProUGUI tutorialTaskDisplayText;
        [SerializeField] private float tutorialTaskCompletionDelay = 2f;
        [SerializeField] private SpellSO m_imperoSpell;
        [SerializeField] private GameObject m_runeBookPrefab;
        [SerializeField] private Vector3[] m_tutorialRuneBookSpawnPositions;

        // Track if tutorial books have been spawned (server-side)
        private bool m_tutorialBooksSpawned = false;
        private List<RuneBook> m_spawnedTutorialBooks = new();

        public void InitializeTutorial()
        {
            // Reset tutorial state for fresh initialization
            // This handles the case of returning from a heist
            if (m_tutorialSequence != null)
            {
                m_tutorialSequence = null;
            }

            // Reset tutorial books spawned flag so books can spawn again
            // The books were destroyed when leaving the lobby scene
            if (IsServerInitialized)
            {
                m_tutorialBooksSpawned = false;
                m_spawnedTutorialBooks.Clear();
            }

            if (tutorialTaskDisplayText == null)
            {
                Debug.LogWarning("[Tutorial] Task display text is not assigned in serialized field!");
                return;
            }

            m_tutorialSequence = new TutorialSequence(tutorialTaskDisplayText);
            m_tutorialSequence.SetTaskCompletionDelay(tutorialTaskCompletionDelay);
            m_tutorialSequence.Initialize();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSpawnTutorialBooks_ServerRpc()
        {
            // Only spawn once
            if (m_tutorialBooksSpawned)
            {
                return;
            }

            SpawnTutorialBooks();
        }

        private void SpawnTutorialBooks()
        {
            if (m_imperoSpell == null)
            {
                Debug.LogError("[Tutorial] Impero spell reference is not assigned!");
                return;
            }

            if (m_runeBookPrefab == null)
            {
                Debug.LogError("[Tutorial] Rune book prefab is not assigned!");
                return;
            }

            if (m_tutorialRuneBookSpawnPositions == null || m_tutorialRuneBookSpawnPositions.Length == 0)
            {
                Debug.LogError("[Tutorial] No rune book spawn positions assigned!");
                return;
            }

            RuneSO[] imperoRunes = m_imperoSpell.Runes;
            if (imperoRunes == null || imperoRunes.Length == 0)
            {
                Debug.LogError("[Tutorial] Impero spell has no runes!");
                return;
            }

            // Spawn a book for each rune in the Impero spell (up to the number of spawn positions)
            int bookCount = Mathf.Min(imperoRunes.Length, m_tutorialRuneBookSpawnPositions.Length);
            for (int i = 0; i < bookCount; i++)
            {
                SpawnRuneBook(m_tutorialRuneBookSpawnPositions[i], imperoRunes[i]);
            }

            m_tutorialBooksSpawned = true;
        }

        private void SpawnRuneBook(Vector3 spawnPosition, RuneSO rune)
        {
            // Instantiate the networked prefab
            GameObject bookInstance = Object.Instantiate(m_runeBookPrefab, spawnPosition, Quaternion.identity);

            if (bookInstance.TryGetComponent(out RuneBook runeBook))
            {
                runeBook.SetContainedRune(rune);

                // Enable rune reuse for tutorial books so multiple players can collect the same rune
                runeBook.AllowRuneReuse = true;

                m_spawnedTutorialBooks.Add(runeBook);

                // Spawn the networked object so all clients see it
                if (bookInstance.TryGetComponent(out NetworkObject netObj))
                {
                    ServerManager.Spawn(netObj);
                }
            }
            else
            {
                Debug.LogError("[Tutorial] Spawned rune book prefab does not have RuneBook component!");
                Object.Destroy(bookInstance);
            }
        }

        public void PauseTutorial()
        {
            if (m_tutorialSequence != null)
                m_tutorialSequence.Pause();
        }

        public void ResumeTutorial()
        {
            if (m_tutorialSequence != null)
                m_tutorialSequence.Resume();
        }

        private void UpdateTutorial()
        {
            if (m_tutorialSequence == null)
                return;
            m_tutorialSequence.Update();
        }

        private void OnTutorialSpellCast(SpellSO spell)
        {
            if (m_tutorialSequence == null)
                return;
            m_tutorialSequence.OnSpellCastCallback(spell);
        }

        public static void ResetTutorialProgress()
        {
            TutorialSequence.ResetTutorial();
        }

        public bool IsTutorialComplete()
        {
            if (m_tutorialSequence == null)
            {
                // Tutorial not initialized - check PlayerPrefs directly
                return PlayerPrefs.GetInt("TutorialComplete", 0) == 1;
            }
            return m_tutorialSequence.IsTutorialComplete();
        }
        #endregion
    }
}
