using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Notebook
{
    /// <summary>
    /// Manages assignment data synchronization across all players using FishNet.
    /// The server initializes the assignment and syncs it to all clients.
    /// </summary>
    public class NotebookManager : NetworkBehaviour
    {
        public static NotebookManager Instance { get; private set; }

        #region Events
        /// <summary>Invoked when assignment data is initialized or changed</summary>
        public event Action OnAssignmentDataChanged;
        #endregion

        #region Network Synced Data
        /// <summary>
        /// Network-synchronized assignment data. Only the server can modify this.
        /// Clients will automatically receive updates through FishNet's SyncVar system.
        /// </summary>
        private readonly SyncVar<AssignmentData> m_networkAssignment = new();
        #endregion

        #region Assignment Data
        private AssignmentData m_assignmentData;
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

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncVar changes
            m_networkAssignment.OnChange += OnAssignmentSynced;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe from SyncVar changes
            m_networkAssignment.OnChange -= OnAssignmentSynced;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServerInitialized)
            {
                // Get assignment data from the synced variable
                m_assignmentData = m_networkAssignment.Value;
            }
        }

        #region Assignment Management
        /// <summary>
        /// Initializes and syncs assignment data to all clients.
        /// </summary>
        [Server]
        public void InitializeAssignment(AssignmentData assignmentData)
        {
            if (!IsServerInitialized)
            {
                this.LogError("InitializeAssignment called but server is not initialized!");
                return;
            }

            m_networkAssignment.Value = assignmentData;

            // Also set it locally on the server
            m_assignmentData = assignmentData;

            this.LogInfo($"Assignment {assignmentData.assignmentNumber} initialized with {assignmentData.tasks.Count} tasks");

            OnAssignmentDataChanged?.Invoke();
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

        #region SyncVar Callbacks
        /// <summary>
        /// Called automatically by FishNet when m_networkAssignment changes.
        /// </summary>
        private void OnAssignmentSynced(AssignmentData prev, AssignmentData next, bool asServer)
        {
            if (asServer) return;

            this.LogInfo("Received assignment sync from server");
            // ConvertNetworkDataToLocal(next);
            OnAssignmentDataChanged?.Invoke();
        }
        #endregion
    }
}
