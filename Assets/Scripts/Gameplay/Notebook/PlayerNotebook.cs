using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Player-specific notebook component that exists on each player's character.
    /// Manages the player's spell loadout for the current heist.
    /// This data is NOT synchronized - each player has their own local spell list.
    /// </summary>
    public class PlayerNotebook : NetworkBehaviour
    {
        /// <summary>
        /// The spell loadout this player brought into the current heist.
        /// Set before the heist starts and remains constant during gameplay.
        /// </summary>
        private PlayerSpellLoadout m_spellLoadout;

        #region Initialization

        private void Awake()
        {
            m_spellLoadout = new PlayerSpellLoadout();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Only initialize for the local player
            if (!base.IsOwner)
                return;

            // Load the player's spell loadout for this heist
            // In a real implementation, this would come from a persistence system
            // or a heist preparation menu. For now, we'll use a placeholder.
            InitializeSpellLoadout();
        }

        #endregion

        #region Spell Loadout Management

        /// <summary>
        /// Initializes the spell loadout for this heist.
        /// Should be called before or at the start of the heist.
        /// In production, this would receive data from your persistence/progression system.
        /// </summary>
        public void InitializeSpellLoadout()
        {
            // TODO: Replace this with actual data from your save system or heist prep menu
            // For now, this is a placeholder that gives the player the first 3 spells
            m_spellLoadout.equippedSpellIndices.Clear();

            // Example: Player brings the first 3 learned spells
            // You'll replace this with actual saved data
            if (GameManager.Instance != null && GameManager.Instance.AllSpells.Length > 0)
            {
                int spellCount = Mathf.Min(3, GameManager.Instance.AllSpells.Length);
                for (int i = 0; i < spellCount; i++)
                {
                    m_spellLoadout.equippedSpellIndices.Add(i);
                }

                Debug.Log($"[PlayerNotebook] Initialized with {spellCount} spells for local player");
            }
        }

        /// <summary>
        /// Sets the spell loadout directly. Use this when loading from a save system
        /// or when the player selects spells in a heist preparation menu.
        /// </summary>
        /// <param name="spellIndices">Indices of spells from GameManager.AllSpells</param>
        public void SetSpellLoadout(List<int> spellIndices)
        {
            m_spellLoadout.equippedSpellIndices.Clear();
            m_spellLoadout.equippedSpellIndices.AddRange(spellIndices);

            Debug.Log($"[PlayerNotebook] Spell loadout set: {spellIndices.Count} spells");
        }

        /// <summary>
        /// Gets the spell loadout for this player.
        /// Returns indices into GameManager.AllSpells array.
        /// </summary>
        public List<int> GetSpellLoadout()
        {
            return new List<int>(m_spellLoadout.equippedSpellIndices);
        }

        /// <summary>
        /// Gets the actual SpellSO objects for the spells this player brought.
        /// </summary>
        public List<SpellSO> GetEquippedSpells()
        {
            var spells = new List<SpellSO>();

            if (GameManager.Instance == null)
                return spells;

            foreach (int spellIndex in m_spellLoadout.equippedSpellIndices)
            {
                if (spellIndex >= 0 && spellIndex < GameManager.Instance.AllSpells.Length)
                {
                    spells.Add(GameManager.Instance.AllSpells[spellIndex]);
                }
                else
                {
                    Debug.LogWarning($"[PlayerNotebook] Invalid spell index: {spellIndex}");
                }
            }

            return spells;
        }

        /// <summary>
        /// Checks if the player has a specific spell equipped for this heist.
        /// </summary>
        public bool HasSpellEquipped(SpellSO spell)
        {
            if (GameManager.Instance == null)
                return false;

            int spellIndex = System.Array.IndexOf(GameManager.Instance.AllSpells, spell);
            return m_spellLoadout.equippedSpellIndices.Contains(spellIndex);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets a reference to the local player's notebook.
        /// Useful for UI systems that need to display the local player's spell loadout.
        /// </summary>
        public static PlayerNotebook GetLocalPlayerNotebook()
        {
            // Find the local player's character and get their notebook component
            // You may need to adjust this based on how you identify the local player
            var allNotebooks = FindObjectsByType<PlayerNotebook>(FindObjectsSortMode.None);
            foreach (var notebook in allNotebooks)
            {
                if (notebook.IsOwner)
                    return notebook;
            }

            Debug.LogWarning("[PlayerNotebook] Could not find local player's notebook");
            return null;
        }

        #endregion
    }
}