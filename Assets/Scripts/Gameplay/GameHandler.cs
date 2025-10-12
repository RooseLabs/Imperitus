using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.ScriptableObjects;
using RooseLabs.UI;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [DefaultExecutionOrder(-99)]
    public class GameHandler : NetworkBehaviour
    {
        public static GameHandler Instance { get; private set; }

        [field: SerializeField] public RuneSO[] AllRunes { get; private set; }

        private readonly SyncList<int> m_collectedRunes = new();
        public List<int> CollectedRunes => m_collectedRunes.Collection;

        private void Awake()
        {
            Instance = this;
            m_collectedRunes.OnChange += CollectedRunes_OnChange;
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
    }
}
