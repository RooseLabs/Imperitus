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

            m_heistTimer.StartTimer(m_heistTimer.defaultTime);
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
