using System.Collections.Generic;
using System.IO;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using GameKit.Dependencies.Utilities.Types;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RooseLabs.Gameplay
{
    [DefaultExecutionOrder(-99)]
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Serialized
        [SerializeField][Scene] private string[] libraryScenes;
        [field: SerializeField] public RuneDatabase RuneDatabase { get; private set; }
        [field: SerializeField] public SpellDatabase SpellDatabase { get; private set; }
        [SerializeField] private TaskImageDatabase taskImageDatabase;
        #endregion

        private HeistTimer m_heistTimer;

        private void Awake()
        {
            Instance = this;
            m_heistTimer = GetComponent<HeistTimer>();

            // Initialize the task image database
            if (taskImageDatabase != null)
            {
                taskImageDatabase.Initialize();
            }
            else
            {
                Debug.LogError("[GameManager] TaskImageDatabase is not assigned!", this);
            }
        }

        private void OnDestroy()
        {
            if (InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnLoadEnd -= HandleSceneLoaded;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            InstanceFinder.SceneManager.OnLoadEnd += HandleSceneLoaded;
        }

        private void HandleSceneLoaded(SceneLoadEndEventArgs args)
        {
            if (!IsServerInitialized) return;

            AssignmentData assignment = CreateAssignmentData();

            //Debug.Log($"[GameManager - SERVER] About to initialize assignment. NotebookManager.Instance is null? {NotebookManager.Instance == null}");
            //Debug.Log($"[GameManager - SERVER] Assignment object created with {assignment.tasks.Count} tasks");

            NotebookManager.Instance.InitializeAssignment(assignment);

            //Debug.Log($"[GameManager - SERVER] Assignment initialized. Can retrieve from NotebookManager? {NotebookManager.Instance.GetCurrentAssignment() != null}");
        }


        [Server]
        public void StartHeist()
        {
            int randomIndex = Random.Range(0, libraryScenes.Length);
            string selectedSceneName = GetSceneName(libraryScenes[randomIndex]);
            SceneManagement.SceneManager.Instance.LoadScene(selectedSceneName, PlayerHandler.CharacterNetworkObjects);

            m_heistTimer.ShowTimer();
            m_heistTimer.StartTimer(m_heistTimer.defaultTime);
        }

        /// <summary>
        /// Creates assignment data with proper image IDs from the TaskImageDatabase.
        /// </summary>
        private AssignmentData CreateAssignmentData()
        {
            if (taskImageDatabase == null)
            {
                Debug.LogError("[GameManager] Cannot create assignment - TaskImageDatabase is null!");
                return null;
            }

            // Get available image IDs from the database
            List<string> availableImageIds = taskImageDatabase.GetAllImageIds();

            AssignmentData assignment = new AssignmentData
            {
                assignmentNumber = 1,
                tasks = new List<AssignmentTask>()
            };

            string imageId1 = GetRandomImageId(availableImageIds);
            assignment.tasks.Add(new AssignmentTask
            {
                description = "Collect all the ancient runes scattered around the library.",
                imageId = imageId1,
                taskImage = taskImageDatabase.GetSprite(imageId1)
            });

            string imageId2 = GetRandomImageId(availableImageIds);
            assignment.tasks.Add(new AssignmentTask
            {
                description = "Avoid the library guardians while collecting the runes.",
                imageId = imageId2,
                taskImage = taskImageDatabase.GetSprite(imageId2)
            });

            string imageId3 = GetRandomImageId(availableImageIds);
            assignment.tasks.Add(new AssignmentTask
            {
                description = "Return to the entrance once all runes are collected.",
                imageId = imageId3,
                taskImage = taskImageDatabase.GetSprite(imageId3)
            });

            return assignment;
        }

        /// <summary>
        /// Gets a random image ID from the available list.
        /// </summary>
        private string GetRandomImageId(List<string> availableIds)
        {
            if (availableIds.Count == 0)
            {
                Debug.LogError("[GameManager] No available image IDs!");
                return "default";
            }

            return availableIds[Random.Range(0, availableIds.Count)];
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}