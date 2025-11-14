using FishNet;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using NUnit.Framework;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class DroppedNotebook : Item
    {
        [SerializeField] GameObject notebookVisual;
        private PlayerNotebook deadPlayerNotebook;
        private PlayerConnection deadPlayerConnection;

        private readonly SyncList<int> syncedRuneIndices = new SyncList<int>();
        private List<RuneSO> availableRunes = new List<RuneSO>();

        private void Awake()
        {
            base.Awake();
            syncedRuneIndices.OnChange += OnSyncedRuneIndicesChanged;
        }

        private void OnDestroy()
        {
            syncedRuneIndices.OnChange -= OnSyncedRuneIndicesChanged;
        }

        private void OnSyncedRuneIndicesChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        {
            if (asServer)
                return;

            RebuildAvailableRunesFromSyncedIndices();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServerInitialized)
            {
                RebuildAvailableRunesFromSyncedIndices();
            }
        }

        [Server]
        public void Initialize(PlayerNotebook notebook)
        {
            deadPlayerNotebook = notebook;

            var runeIndices = notebook.GetCollectedRunes();
            syncedRuneIndices.Clear();

            foreach (int index in runeIndices)
            {
                syncedRuneIndices.Add(index);
            }

            RebuildAvailableRunesFromSyncedIndices();

            deadPlayerNotebook.RemoveAllRunes();
        }

        private void RebuildAvailableRunesFromSyncedIndices()
        {
            availableRunes.Clear();

            if (GameManager.Instance == null)
            {
                Debug.LogError("[DroppedNotebook] GameManager.Instance is null!");
                return;
            }

            foreach (int runeIndex in syncedRuneIndices)
            {
                if (runeIndex >= 0 && runeIndex < GameManager.Instance.RuneDatabase.Count)
                {
                    availableRunes.Add(GameManager.Instance.RuneDatabase[runeIndex]);
                }
                else
                {
                    Debug.LogWarning($"[DroppedNotebook] Invalid rune index: {runeIndex}");
                }
            }
        }

        public override void OnPickupEnd()
        {
            // Check if notebook contains any rune the player is missing
            // If a valid rune is found, add it to the player's notebook collection
            // After updating the player's notebook, remove the rune(s) from the dropped notebook rune list

            for (int i = availableRunes.Count - 1; i >= 0; i--)
            {
                var rune = availableRunes[i];
                if (!HolderCharacter.Notebook.HasRune(rune))
                {
                    HolderCharacter.Notebook.AddRune(rune);
                    availableRunes.RemoveAt(i);
                    Debug.Log($"{HolderCharacter.name} picked up rune {rune.Name} from dropped notebook.");
                }
            }
        }

        public override string GetInteractionText() => "Get Runes";
    }
}
