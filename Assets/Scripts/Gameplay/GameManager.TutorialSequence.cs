using RooseLabs.Gameplay.Notebook;
using RooseLabs.Gameplay.Spells;
using RooseLabs.ScriptableObjects;
using RooseLabs.Player;
using RooseLabs.UI;
using TMPro;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public partial class GameManager
    {
        #region Tutorial Inner Class
        private class TutorialSequence
        {
            #region Task Definition
            private class TutorialTask
            {
                public string Description { get; set; }
                public TaskType Type { get; set; }
                public bool Completed { get; private set; } = false;

                public TutorialTask(string description, TaskType type)
                {
                    Description = description;
                    Type = type;
                }

                public void MarkComplete()
                {
                    Completed = true;
                }
            }

            private enum TaskType
            {
                WalkAround,
                OpenNotebook,
                AimWithWand,
                CastImperoSpell
            }
            #endregion

            #region Tutorial State
            private TutorialTask[] m_tasks;
            private int m_currentTaskIndex = 0;
            private bool m_tutorialComplete = false;
            private TextMeshProUGUI m_taskDisplayText;
            private PlayerCharacter m_player;

            private const string TutorialCompleteKey = "TutorialComplete";
            private const string TutorialProgressKey = "TutorialProgress_";
            private const string TutorialPausedKey = "TutorialPaused";

            // Task completion delay system
            private float m_taskCompletionDelayBuffer = 2f; // Customizable delay in seconds
            private float m_taskCompletionTimer = 0f;
            private bool m_isTaskCompletionInProgress = false;

            // Pause/Resume system
            private bool m_isPaused = false;
            #endregion

            public TutorialSequence(TextMeshProUGUI taskDisplayText)
            {
                m_taskDisplayText = taskDisplayText;
                InitializeTasks();
            }

            private void InitializeTasks()
            {
                m_tasks = new TutorialTask[]
                {
                    new TutorialTask("Use [Movement Input buttons] to walk", TaskType.WalkAround),
                    new TutorialTask("Use [Notebook Input Button] to open your notebook", TaskType.OpenNotebook),
                    new TutorialTask("Use [Aim Input button] to aim with your wand", TaskType.AimWithWand),
                    new TutorialTask("Use the Impero spell to levitate an object", TaskType.CastImperoSpell)
                };
            }

            public void Initialize()
            {
                // DELETE LATER ITS FOR DEBUGGING
                //ResetTutorialProgress();

                // Check if tutorial was already completed
                if (PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1)
                {
                    m_tutorialComplete = true;
                    HideTutorial();
                    return;
                }

                // Load progress from PlayerPrefs
                m_currentTaskIndex = PlayerPrefs.GetInt(TutorialProgressKey + "Index", 0);

                // Mark any previously completed tasks
                for (int i = 0; i < m_currentTaskIndex; i++)
                {
                    m_tasks[i].MarkComplete();
                }

                // Load pause state from PlayerPrefs
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
            }

            public void Update()
            {
                if (m_tutorialComplete || m_currentTaskIndex >= m_tasks.Length)
                    return;

                // Don't update if paused
                if (m_isPaused)
                    return;

                m_player = PlayerCharacter.LocalCharacter;
                if (!m_player)
                {
                    return;
                }

                // Handle task completion delay
                if (m_isTaskCompletionInProgress)
                {
                    m_taskCompletionTimer -= Time.deltaTime;
                    if (m_taskCompletionTimer <= 0f)
                    {
                        // Delay complete, finalize task completion
                        CompleteCurrentTask();
                        m_isTaskCompletionInProgress = false;
                    }
                    return; // Don't check for new completions while delay is active
                }

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];

                // Check task completion based on type
                switch (currentTask.Type)
                {
                    case TaskType.WalkAround:
                        CheckWalkTask();
                        break;
                    case TaskType.OpenNotebook:
                        CheckNotebookTask();
                        break;
                    case TaskType.AimWithWand:
                        CheckAimTask();
                        break;
                    case TaskType.CastImperoSpell:
                        CheckSpellCastTask();
                        break;
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
                    // Save progress ONLY if tutorial is not complete
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

            #region Task Completion Checks

            private void CheckWalkTask()
            {
                if (m_tasks[m_currentTaskIndex].Completed)
                    return;

                // Check if player is moving (any movement input)
                if (m_player.Input.movementInput.sqrMagnitude > 0.01f)
                {
                    MarkTaskForCompletion();
                }
            }

            private void CheckNotebookTask()
            {
                if (m_tasks[m_currentTaskIndex].Completed)
                    return;

                // Check if notebook is open
                bool notebookIsOpen = GUIManager.Instance != null &&
                    GUIManager.ActiveWindows.Count > 0 &&
                    GUIManager.ActiveWindows[^1] is NotebookUIController;

                if (notebookIsOpen)
                {
                    MarkTaskForCompletion();
                }
            }

            private void CheckAimTask()
            {
                if (m_tasks[m_currentTaskIndex].Completed)
                    return;

                // Check if player is aiming
                if (m_player.Data.isAiming)
                {
                    MarkTaskForCompletion();
                }
            }

            private void CheckSpellCastTask()
            {
                if (m_tasks[m_currentTaskIndex].Completed)
                    return;

                // This will be set complete when OnSpellCast event fires with Impero spell
                // (handled via subscription in GameManager)
            }

            public void OnSpellCastCallback(SpellSO spell)
            {
                if (m_tutorialComplete || m_currentTaskIndex >= m_tasks.Length)
                    return;

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];
                if (currentTask.Type == TaskType.CastImperoSpell && !currentTask.Completed)
                {
                    // Check if it's the Impero spell (index 0)
                    if (GameManager.Instance.SpellDatabase[0].SpellInfo == spell)
                    {
                        MarkTaskForCompletion();
                    }
                }
            }

            #endregion

            #region Tutorial State Management

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

                m_taskDisplayText.text = m_tasks[m_currentTaskIndex].Description;
            }

            private void CompleteTutorial()
            {
                m_tutorialComplete = true;
                PlayerPrefs.SetInt(TutorialCompleteKey, 1);
                PlayerPrefs.DeleteKey(TutorialPausedKey);  // Clear pause state when tutorial is complete
                PlayerPrefs.Save();

                HideTutorial();
            }

            private void SaveProgress()
            {
                PlayerPrefs.SetInt(TutorialProgressKey + "Index", m_currentTaskIndex);
                PlayerPrefs.SetInt(TutorialPausedKey, m_isPaused ? 1 : 0);
                PlayerPrefs.Save();
            }

            public bool IsTutorialComplete() => m_tutorialComplete;

            #endregion

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

                // Save pause state to PlayerPrefs
                PlayerPrefs.SetInt(TutorialPausedKey, 1);
                PlayerPrefs.Save();
            }

            public void Resume()
            {
                // Don't resume if tutorial is already complete
                if (m_tutorialComplete)
                    return;

                m_isPaused = false;
                ShowTutorial();
                PlayerPrefs.SetInt(TutorialPausedKey, 0);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region Tutorial System Integration
        private TutorialSequence m_tutorialSequence;

        [SerializeField] private TextMeshProUGUI tutorialTaskDisplayText;
        [SerializeField] private float tutorialTaskCompletionDelay = 2f;

        public void InitializeTutorial()
        {
            if (m_tutorialSequence != null)
                return;

            if (tutorialTaskDisplayText == null)
            {
                Debug.LogWarning("[Tutorial] Task display text is not assigned!");
                return;
            }

            m_tutorialSequence = new TutorialSequence(tutorialTaskDisplayText);
            m_tutorialSequence.SetTaskCompletionDelay(tutorialTaskCompletionDelay);
            m_tutorialSequence.Initialize();
        }

        public void PauseTutorial()
        {
            if (m_tutorialSequence != null)
            {
                m_tutorialSequence.Pause();
            }
        }

        public void ResumeTutorial()
        {
            if (m_tutorialSequence != null)
            {
                m_tutorialSequence.Resume();
            }
        }

        private void UpdateTutorial()
        {
            if (m_tutorialSequence == null)
            {
                return;
            }

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
        #endregion
    }
}
