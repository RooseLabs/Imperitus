using System.Collections.Generic;
using System.IO;
using FishNet.Object;
using GameKit.Dependencies.Utilities.Types;
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
        #endregion

        private HeistTimer m_heistTimer;

        private void Awake()
        {
            Instance = this;
            m_heistTimer = GetComponent<HeistTimer>();
        }

        public void StartHeist()
        {
            int randomIndex = Random.Range(0, libraryScenes.Length);
            string selectedSceneName = GetSceneName(libraryScenes[randomIndex]);
            SceneManagement.SceneManager.Instance.LoadScene(selectedSceneName, PlayerHandler.CharacterNetworkObjects);

            m_heistTimer.ShowTimer();
            m_heistTimer.StartTimer(m_heistTimer.defaultTime);

            AssignmentData assignment = new AssignmentData
            {
                assignmentNumber = 1,
                tasks = new List<AssignmentTask> {
                    new AssignmentTask
                    {
                        description = "Collect all the ancient runes scattered around the library.",
                           taskImage = CreateRandomSquareSprite()
                    },
                    new AssignmentTask
                    {
                        description = "Avoid the library guardians while collecting the runes.",
                        taskImage = CreateRandomSquareSprite()
                    },
                    new AssignmentTask
                    {
                        description = "Return to the entrance once all runes are collected.",
                        taskImage = CreateRandomSquareSprite()
                    }
                }
            };

            Debug.Log($"[GameManager] About to initialize assignment. NotebookManager.Instance is null? {NotebookManager.Instance == null}");
            Debug.Log($"[GameManager] Assignment object created with {assignment.tasks.Count} tasks");

            NotebookManager.Instance.InitializeAssignment(assignment);

            Debug.Log($"[GameManager] Assignment initialized. Can retrieve from NotebookManager? {NotebookManager.Instance.GetCurrentAssignment() != null}");
        }

        Sprite CreateRandomSquareSprite() => Sprite.Create(MakeColorTex(Random.ColorHSV()), new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        Texture2D MakeColorTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
