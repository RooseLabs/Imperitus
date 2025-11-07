using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Manages assignment data synchronization across all players using FishNet.
    /// The server initializes the assignment and syncs it to all clients.
    /// </summary>
    public class NotebookManager : NetworkBehaviour
    {
        public static NotebookManager Instance { get; private set; }

        [SerializeField] private TaskImageDatabase taskImageDatabase;

        #region Events
        /// <summary>Invoked when assignment data is initialized or changed</summary>
        public event Action OnAssignmentDataChanged;
        #endregion

        #region Network Synced Data
        /// <summary>
        /// Network-synchronized assignment data. Only the server can modify this.
        /// Clients will automatically receive updates through FishNet's SyncVar system.
        /// </summary>
        private readonly SyncVar<NetworkAssignmentData> m_networkAssignment = new SyncVar<NetworkAssignmentData>();
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

            // Initialize the task image database
            if (taskImageDatabase != null)
            {
                taskImageDatabase.Initialize();
            }
            else
            {
                Debug.LogError("[NotebookManager] TaskImageDatabase is not assigned!", this);
            }
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

            // If we're a client connecting after the assignment was already set,
            // we need to process the current synced data
            if (!IsServerInitialized && m_networkAssignment.Value.tasks != null && m_networkAssignment.Value.tasks.Length > 0)
            {
                ConvertNetworkDataToLocal(m_networkAssignment.Value);
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
                Debug.LogError("[NotebookManager] InitializeAssignment called but server is not initialized!");
                return;
            }

            // Convert the AssignmentData to NetworkAssignmentData and set the SyncVar
            m_networkAssignment.Value = ConvertToNetworkData(assignmentData);

            // Also set it locally on the server
            m_assignmentData = assignmentData;

            Debug.Log($"[NotebookManager - SERVER] Assignment {assignmentData.assignmentNumber} initialized with {assignmentData.tasks.Count} tasks");

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

        #region Network Data Conversion
        /// <summary>
        /// Converts client-side AssignmentData to network-serializable format.
        /// </summary>
        private NetworkAssignmentData ConvertToNetworkData(AssignmentData localData)
        {
            NetworkAssignmentData networkData = new NetworkAssignmentData
            {
                assignmentNumber = localData.assignmentNumber,
                tasks = new NetworkAssignmentTask[localData.tasks.Count]
            };

            for (int i = 0; i < localData.tasks.Count; i++)
            {
                networkData.tasks[i] = new NetworkAssignmentTask
                {
                    description = localData.tasks[i].description,
                    imageId = localData.tasks[i].imageId
                };
            }

            return networkData;
        }

        /// <summary>
        /// Converts network data to client-side AssignmentData with sprite references.
        /// </summary>
        private void ConvertNetworkDataToLocal(NetworkAssignmentData networkData)
        {
            if (networkData.tasks == null || networkData.tasks.Length == 0)
            {
                Debug.LogWarning("[NotebookManager] Received empty network assignment data");
                return;
            }

            m_assignmentData = new AssignmentData
            {
                assignmentNumber = networkData.assignmentNumber,
                tasks = new System.Collections.Generic.List<AssignmentTask>()
            };

            foreach (var networkTask in networkData.tasks)
            {
                AssignmentTask task = new AssignmentTask
                {
                    description = networkTask.description,
                    imageId = networkTask.imageId,
                    taskImage = taskImageDatabase?.GetSprite(networkTask.imageId)
                };

                if (task.taskImage == null)
                {
                    Debug.LogWarning($"[NotebookManager] Could not find sprite for imageId: {networkTask.imageId}");
                }

                m_assignmentData.tasks.Add(task);
            }

            Debug.Log($"[NotebookManager - CLIENT] Assignment {m_assignmentData.assignmentNumber} received with {m_assignmentData.tasks.Count} tasks");
        }
        #endregion

        #region SyncVar Callbacks
        /// <summary>
        /// Called automatically by FishNet when m_networkAssignment changes.
        /// </summary>
        private void OnAssignmentSynced(NetworkAssignmentData prev, NetworkAssignmentData next, bool asServer)
        {
            if (asServer) return;

            Debug.Log($"[NotebookManager - CLIENT] Received assignment sync from server");
            ConvertNetworkDataToLocal(next);
            OnAssignmentDataChanged?.Invoke();
        }
        #endregion
    }
}