using RooseLabs.Gameplay.Notebook;
using RooseLabs.ScriptableObjects;
using RooseLabs.Player;
using RooseLabs.UI;
using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;

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

            private static readonly Dictionary<TaskType, (string description, string actionName)> TaskConfig = new()
            {
                { TaskType.WalkAround, ("Use to walk", "Move") },
                { TaskType.OpenNotebook, ("Press to open your notebook", "OpenNotebook") },
                { TaskType.AimWithWand, ("Hold to aim with your wand", "Aim") },
                { TaskType.CastImperoSpell, ("Hold while aiming with your wand to use the Impero spell and levitate an object", "Cast") }
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
                        { TaskType.OpenNotebook, () => 
                            GUIManager.Instance != null &&
                            GUIManager.ActiveWindows.Count > 0 &&
                            GUIManager.ActiveWindows[^1] is NotebookUIController },
                        { TaskType.AimWithWand, () => m_player.Data.isAiming },
                        { TaskType.CastImperoSpell, () => false }
                  };
            }

            private void InitializeTasks()
            {
                m_tasks = TaskConfig.Select(kvp =>
                        new TutorialTask(kvp.Value.description, kvp.Key, GetSpriteTagsForAction(kvp.Value.actionName))).ToArray();
            }

            private string[] GetSpriteTagsForAction(string actionName)
            {
                var action = InputHandler.GameplayActions?.FindAction(actionName);
                if (action == null)
                    return System.Array.Empty<string>();

                var kbmTags = InputSpriteData.GetAllSpriteTags(action, InputScheme.KeyboardMouse).ToList();
                var gamepadTags = InputSpriteData.GetAllSpriteTags(action, InputScheme.Gamepad).ToList();

                // Use keyboard tags if available and current scheme is keyboard/mouse, otherwise use gamepad tags, fallback to keyboard if no gamepad
                var tagsToUse = (InputHandler.CurrentInputScheme == InputScheme.KeyboardMouse && kbmTags.Count > 0)
                                ? kbmTags : (gamepadTags.Count > 0 ? gamepadTags : kbmTags);

                return tagsToUse.ToArray();
            }

            public void Initialize()
            {
                // DELETE LATER ITS FOR DEBUGGING
                //ResetTutorialProgress();

                if (PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1)
                {
                    m_tutorialComplete = true;
                    HideTutorial();
                    return;
                }

                m_currentTaskIndex = PlayerPrefs.GetInt(TutorialProgressKey + "Index", 0);
                for (int i = 0; i < m_currentTaskIndex; i++)
                {
                    m_tasks[i].MarkComplete();
                }

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

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];
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
                    RebuildCurrentTaskSpriteTags();
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

                object[] spriteMarkups = new object[currentTask.SpriteTags.Length];

                InputDevice currentDevice = InputHandler.CurrentInputDevice;

                for (int i = 0; i < currentTask.SpriteTags.Length; i++)
                {
                    string spriteTag = currentTask.SpriteTags[i];
                    spriteMarkups[i] = !string.IsNullOrEmpty(spriteTag) ? $"<sprite name=\"{spriteTag}\">" : $"{{{i}}}";
                }

                string formattedDescription = BuildFormattedDescription(currentTask.Description, spriteMarkups.Length);
                m_taskDisplayText.text = string.Format(formattedDescription, spriteMarkups);
                m_taskDisplayText.spriteAsset = InputSpriteData.GetSpriteAssetForInputDevice(currentDevice);
            }

            private string BuildFormattedDescription(string baseDescription, int bindingCount)
            {
                string result = baseDescription;
                System.Text.RegularExpressions.Regex placeholderRegex = new System.Text.RegularExpressions.Regex(@"\{\d+\}(\{\d+\})*");
                result = placeholderRegex.Replace(result, "");
                string placeholderSequence = string.Join(" ", Enumerable.Range(0, bindingCount).Select(i => $"{{{i}}}"));

                if (result.StartsWith("Use "))
                    result = "Use " + placeholderSequence + result.Substring(4);
                else if (result.StartsWith("Press "))
                    result = "Press " + placeholderSequence + result.Substring(6);
                else if (result.StartsWith("Hold "))
                    result = "Hold " + placeholderSequence + result.Substring(5);
                return result;
            }

            private void CompleteTutorial()
            {
                m_tutorialComplete = true;
                PlayerPrefs.SetInt(TutorialCompleteKey, 1);
                PlayerPrefs.DeleteKey(TutorialPausedKey);
                PlayerPrefs.Save();
                HideTutorial();
                UnsubscribeFromInputEvents();
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
                    RebuildCurrentTaskSpriteTags();
                    UpdateTaskDisplay();
                }
            }

            private void OnInputDeviceChanged(InputDevice newDevice)
            {
                if (!m_tutorialComplete && !m_isPaused && m_currentTaskIndex < m_tasks.Length)
                    UpdateTaskDisplay();
            }

            private void RebuildCurrentTaskSpriteTags()
            {
                if (m_currentTaskIndex >= m_tasks.Length)
                    return;

                TutorialTask currentTask = m_tasks[m_currentTaskIndex];
                var config = TaskConfig[currentTask.Type];
                currentTask.SpriteTags = GetSpriteTagsForAction(config.actionName);
            }
        }
        #endregion

        #region Tutorial Sequence Integration
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
        #endregion
    }
}
