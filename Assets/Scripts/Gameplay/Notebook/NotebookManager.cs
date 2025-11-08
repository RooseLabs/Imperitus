using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Server-authoritative manager for notebook data.
    /// Handles synchronization of shared data (quests, runes) and provides
    /// a clean interface for UI and gameplay systems to query notebook state.
    /// </summary>
    public class NotebookManager : NetworkBehaviour
    {
        public static NotebookManager Instance { get; private set; }

        #region Events
        /// <summary>Invoked when quest data changes (title, description, or objectives)</summary>
        public event Action OnQuestDataChanged;

        /// <summary>Invoked when rune collection changes</summary>
        public event Action OnRuneCollectionChanged;

        /// <summary>Invoked when an objective is completed</summary>
        public event Action<int> OnObjectiveCompleted;
        #endregion

        #region Synchronized Data
        // Quest information - synchronized across all clients
        private readonly SyncVar<string> m_questTitle = new();
        private readonly SyncVar<string> m_questDescription = new();
        private readonly SyncList<ObjectiveData> m_objectives = new();

        // Runes are already tracked in GameManager.CollectedRunes
        // We'll reference that directly instead of duplicating
        #endregion

        #region Public Properties
        public string QuestTitle => m_questTitle.Value;
        public string QuestDescription => m_questDescription.Value;
        public IReadOnlyList<ObjectiveData> Objectives => m_objectives.Collection;

        /// <summary>
        /// Returns indices of collected runes from GameManager.
        /// </summary>
        public List<int> CollectedRuneIndices => GameManager.Instance.CollectedRunes;
        #endregion

        private void Awake()
        {
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncVar/SyncList changes
            m_questTitle.OnChange += OnQuestTitleChanged;
            m_questDescription.OnChange += OnQuestDescriptionChanged;
            m_objectives.OnChange += OnObjectivesChanged;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            m_questTitle.OnChange -= OnQuestTitleChanged;
            m_questDescription.OnChange -= OnQuestDescriptionChanged;
            m_objectives.OnChange -= OnObjectivesChanged;
        }

        #region Quest Management (Server Authority)

        /// <summary>
        /// Initializes quest data at the start of a heist. Server only.
        /// </summary>
        [Server]
        public void InitializeQuest(QuestData questData)
        {
            m_questTitle.Value = questData.questTitle;
            m_questDescription.Value = questData.questDescription;

            m_objectives.Clear();
            foreach (var objective in questData.objectives)
            {
                m_objectives.Add(objective);
            }

            Debug.Log($"[NotebookManager] Quest initialized: {questData.questTitle}");
        }

        /// <summary>
        /// Marks an objective as completed. Server only.
        /// </summary>
        //[Server]
        //public void CompleteObjective(int objectiveIndex)
        //{
        //    if (objectiveIndex < 0 || objectiveIndex >= m_objectives.Count)
        //    {
        //        Debug.LogWarning($"[NotebookManager] Invalid objective index: {objectiveIndex}");
        //        return;
        //    }

        //    if (m_objectives[objectiveIndex].isCompleted)
        //    {
        //        return; // Already completed
        //    }

        //    var objective = m_objectives[objectiveIndex];
        //    objective.isCompleted = true;
        //    m_objectives[objectiveIndex] = objective;

        //    NotifyObjectiveCompleted(objectiveIndex);

        //    Debug.Log($"[NotebookManager] Objective {objectiveIndex} completed: {objective.description}");
        //}

        /// <summary>
        /// Updates an objective's description. Server only.
        /// Useful for dynamic objectives that change during gameplay.
        /// </summary>
        //[Server]
        //public void UpdateObjectiveDescription(int objectiveIndex, string newDescription)
        //{
        //    if (objectiveIndex < 0 || objectiveIndex >= m_objectives.Count)
        //    {
        //        Debug.LogWarning($"[NotebookManager] Invalid objective index: {objectiveIndex}");
        //        return;
        //    }

        //    var objective = m_objectives[objectiveIndex];
        //    objective.description = newDescription;
        //    m_objectives[objectiveIndex] = objective;
        //}

        #endregion

        #region RPC Notifications

        /// <summary>
        /// Notifies the notebook that rune collection has changed.
        /// Call this from GameManager when runes are collected.
        /// </summary>
        public void NotifyRuneCollectionChanged()
        {
            OnRuneCollectionChanged?.Invoke();
        }

        /// <summary>
        /// Notifies all clients that an objective was completed.
        /// </summary>
        //[ObserversRpc]
        //private void NotifyObjectiveCompleted(int objectiveIndex)
        //{
        //    OnObjectiveCompleted?.Invoke(objectiveIndex);
        //}

        #endregion

        #region SyncVar/SyncList Change Handlers

        private void OnQuestTitleChanged(string prev, string next, bool asServer)
        {
            OnQuestDataChanged?.Invoke();
        }

        private void OnQuestDescriptionChanged(string prev, string next, bool asServer)
        {
            OnQuestDataChanged?.Invoke();
        }

        private void OnObjectivesChanged(SyncListOperation op, int index, ObjectiveData oldItem, ObjectiveData newItem, bool asServer)
        {
            OnQuestDataChanged?.Invoke();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets the current quest data as a snapshot.
        /// Useful for UI population.
        /// </summary>
        public QuestData GetCurrentQuestData()
        {
            var questData = new QuestData
            {
                questTitle = m_questTitle.Value,
                questDescription = m_questDescription.Value,
                objectives = new List<ObjectiveData>(m_objectives.Collection)
            };
            return questData;
        }

        /// <summary>
        /// Checks if all objectives are completed.
        /// </summary>
        //public bool AreAllObjectivesCompleted()
        //{
        //    foreach (var objective in m_objectives.Collection)
        //    {
        //        if (!objective.isCompleted)
        //            return false;
        //    }
        //    return m_objectives.Count > 0;
        //}

        #endregion
    }
}