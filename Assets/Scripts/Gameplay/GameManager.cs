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

        private readonly SyncList<int> m_collectedRunes = new();
        public List<int> CollectedRunes => m_collectedRunes.Collection;

        private HeistTimer m_heistTimer;

        private void Awake()
        {
            Instance = this;
            m_collectedRunes.OnChange += CollectedRunes_OnChange;

            m_heistTimer = GetComponent<HeistTimer>();
        }

        public void StartHeist()
        {
            int randomIndex = Random.Range(0, libraryScenes.Length);
            string selectedSceneName = GetSceneName(libraryScenes[randomIndex]);
            SceneManagement.SceneManager.Instance.LoadScene(selectedSceneName, PlayerHandler.CharacterNetworkObjects);

            m_heistTimer.StartTimer(m_heistTimer.defaultTime);

            var questData = new QuestData
            {
                questTitle = "ODEIO UNITY",
                questDescription = "Apanhar 3 runas magicas e ir embora por favor eu vou-me matar",
            };

            NotebookManager.Instance.InitializeQuest(questData);
        }

        public void PreparePlayerForHeist(PlayerNotebook playerNotebook)
        {
            // This would typically be called from a heist preparation menu
            // or loaded from player's save data

            var spellIndices = new System.Collections.Generic.List<int>
            {
                0, // First spell from GameManager.AllSpells
                2, // Third spell
                5  // Sixth spell
            };

            playerNotebook.SetSpellLoadout(spellIndices);
        }

        public void AddRune(RuneSO rune)
        {
            int runeIndex = Array.IndexOf(AllRunes, rune);
            if (m_collectedRunes.Contains(runeIndex)) return;
            m_collectedRunes.Add(runeIndex);
        }

        //private void CollectedRunes_OnChange(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        //{
        //    GUIManager.Instance.UpdateRuneCounter(m_collectedRunes.Count);
        //    Debug.Log($"Rune collection changed: Operation={op}, Index={index}, OldItem={oldItem}, NewItem={newItem}, AsServer={asServer}");
        //}

        private void CollectedRunes_OnChange(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        {
            GUIManager.Instance.UpdateRuneCounter(m_collectedRunes.Count);

            // NEW: Notify notebook of rune collection
            if (NotebookManager.Instance != null)
            {
                NotebookManager.Instance.NotifyRuneCollectionChanged();
            }

            Debug.Log($"Rune collection changed: Operation={op}, Index={index}, OldItem={oldItem}, NewItem={newItem}, AsServer={asServer}");
        }


        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
