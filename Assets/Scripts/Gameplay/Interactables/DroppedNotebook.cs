using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class DroppedNotebook : Draggable, IInteractable
    {
        private readonly List<RuneSO> m_availableRunes = new();
        private readonly SyncList<int> m_syncedRuneIndices = new();

        /// The character who dropped this notebook.
        private PlayerCharacter m_character;

        protected override void Awake()
        {
            base.Awake();
            m_syncedRuneIndices.OnChange += OnSyncedRuneIndicesChanged;
        }

        private void OnDestroy()
        {
            m_syncedRuneIndices.OnChange -= OnSyncedRuneIndicesChanged;
        }

        public string GetInteractionText() => $"Recover Runes of {m_character.Player.PlayerName}";

        public bool IsInteractable(PlayerCharacter interactor)
        {
            // The dropped notebook is interactable if there is at least one rune available
            return m_availableRunes.Count > 0;
        }

        public void Interact(PlayerCharacter interactor)
        {
            // Check if notebook contains any rune the player is missing
            // If a valid rune is found, add it to the player's notebook collection
            // After updating the player's notebook, remove the rune(s) from the dropped notebook rune list

            if (!IsInteractable(interactor))
                return;

            for (int i = m_availableRunes.Count - 1; i >= 0; i--)
            {
                var rune = m_availableRunes[i];
                if (!interactor.Notebook.HasRune(rune))
                {
                    interactor.Notebook.AddRune(rune);
                    m_availableRunes.RemoveAt(i);
                    Debug.Log($"{interactor.Player.PlayerName} picked up rune {rune.Name} from dropped notebook.");
                }
            }
        }

        private void OnSyncedRuneIndicesChanged(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        {
            if (asServer) return;
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
        public void Initialize(PlayerCharacter character)
        {
            m_character = character;

            var runeIndices = character.Notebook.GetCollectedRunes();
            m_syncedRuneIndices.Clear();
            m_syncedRuneIndices.AddRange(runeIndices);

            RebuildAvailableRunesFromSyncedIndices();

            m_character.Notebook.RemoveAllRunes();
        }

        private void RebuildAvailableRunesFromSyncedIndices()
        {
            m_availableRunes.Clear();

            if (GameManager.Instance == null)
            {
                this.LogError("GameManager.Instance is null!");
                return;
            }

            foreach (int runeIndex in m_syncedRuneIndices)
            {
                if (runeIndex >= 0 && runeIndex < GameManager.Instance.RuneDatabase.Count)
                {
                    m_availableRunes.Add(GameManager.Instance.RuneDatabase[runeIndex]);
                }
                else
                {
                    this.LogWarning($"Invalid rune index: {runeIndex}");
                }
            }
        }
    }
}
