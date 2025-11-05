using System;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Manages static assignment data that is shared across all players.
    /// Since assignment data doesn't change during gameplay, no network synchronization is needed.
    /// This is a simple data holder that gets initialized at the start of the heist.
    /// </summary>
    public class NotebookManager : MonoBehaviour
    {
        public static NotebookManager Instance { get; private set; }

        #region Events

        /// <summary>Invoked when assignment data is initialized or changed</summary>
        public event Action OnAssignmentDataChanged;

        #endregion

        #region Assignment Data

        private AssignmentData m_assignmentData;

        #endregion

        #region Public Properties

        public AssignmentData CurrentAssignment => m_assignmentData;
        public int AssignmentNumber => m_assignmentData?.assignmentNumber ?? 0;

        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #region Assignment Management

        /// <summary>
        /// Initializes assignment data at the start of a heist.
        /// Call this when the heist/level starts to set up the notebook.
        /// </summary>
        public void InitializeAssignment(AssignmentData assignmentData)
        {
            m_assignmentData = assignmentData;
            OnAssignmentDataChanged?.Invoke();
            Debug.Log($"[NotebookManager] Assignment {assignmentData.assignmentNumber} initialized with {assignmentData.tasks.Count} tasks");
        }

        /// <summary>
        /// Gets the current assignment data.
        /// Returns null if no assignment has been initialized.
        /// </summary>
        public AssignmentData GetCurrentAssignment()
        {
            return m_assignmentData;
        }

        #endregion
    }
}