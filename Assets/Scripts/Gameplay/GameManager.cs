using System;
using System.Collections.Generic;
using System.IO;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using GameKit.Dependencies.Utilities.Types;
using RooseLabs.Network;
using RooseLabs.ScriptableObjects;
using RooseLabs.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RooseLabs.Gameplay
{
    [DefaultExecutionOrder(-99)]
    public class GameManager : NetworkBehaviour
    {
        public static GameManager Instance { get; private set; }

        #region Serialized
        [SerializeField] GUIManager guiManager;
        [SerializeField][Scene] private string[] libraryScenes;
        [field: SerializeField] public RuneSO[] AllRunes { get; private set; }
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
            SceneLoadData sld = new(selectedSceneName)
            {
                ReplaceScenes = ReplaceOption.All,
                MovedNetworkObjects = PlayerHandler.CharacterNetworkObjects
            };
            SceneManager.LoadGlobalScenes(sld);

            m_heistTimer.StartTimer(m_heistTimer.defaultTime);
        }

        public void AddRune(RuneSO rune)
        {
            int runeIndex = Array.IndexOf(AllRunes, rune);
            if (m_collectedRunes.Contains(runeIndex)) return;
            m_collectedRunes.Add(runeIndex);
        }

        private void CollectedRunes_OnChange(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        {
            GUIManager.Instance.UpdateRuneCounter(m_collectedRunes.Count);
            Debug.Log($"Rune collection changed: Operation={op}, Index={index}, OldItem={oldItem}, NewItem={newItem}, AsServer={asServer}");
        }

        private static string GetSceneName(string fullPath)
        {
            return Path.GetFileNameWithoutExtension(fullPath);
        }
    }
}
