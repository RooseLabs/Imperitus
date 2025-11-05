using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Connection;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Player-specific notebook component that exists on each player's character.
    /// Manages the player's spell loadout and rune collection for the current heist.
    /// Runes are shared automatically when players are within proximity range.
    /// </summary>
    public class PlayerNotebook : NetworkBehaviour
    {
        #region Configuration
        [Header("Rune Sharing Settings")]
        [SerializeField] private float runeShareRadius = 5f;
        [SerializeField] private float runeShareCheckInterval = 0.5f;
        #endregion

        #region Events
        /// <summary>Invoked when this player collects a new rune</summary>
        public event Action OnRuneCollected;
        #endregion

        #region Player-Specific Data
        /// <summary>
        /// The spell loadout this player brought into the current heist.
        /// Set before the heist starts and remains constant during gameplay.
        /// </summary>
        private PlayerSpellLoadout m_spellLoadout;

        /// <summary>
        /// The runes this specific player has collected during the heist.
        /// Each player tracks their own runes independently.
        /// </summary>
        private PlayerRuneCollection m_runeCollection;
        #endregion

        private Coroutine m_sharingCoroutine;

        #region Initialization

        private void Awake()
        {
            m_spellLoadout = new PlayerSpellLoadout();
            m_runeCollection = new PlayerRuneCollection();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Only initialize for the local player
            if (!base.IsOwner)
                return;

            // Load the player's spell loadout for this heist
            InitializeSpellLoadout();

            // Start the proximity sharing system
            m_sharingCoroutine = StartCoroutine(RuneSharingLoop());
        }

        private void OnDestroy()
        {
            if (m_sharingCoroutine != null)
            {
                StopCoroutine(m_sharingCoroutine);
            }
        }

        #endregion

        #region Proximity Rune Sharing

        private IEnumerator RuneSharingLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(runeShareCheckInterval);
                CheckAndShareRunesWithNearbyPlayers();
            }
        }

        private void CheckAndShareRunesWithNearbyPlayers()
        {
            if (!base.IsOwner)
                return;

            // Find all other player notebooks
            var allNotebooks = FindObjectsByType<PlayerNotebook>(FindObjectsSortMode.None);

            foreach (var otherNotebook in allNotebooks)
            {
                // Skip self
                if (otherNotebook == this)
                    continue;

                // Check distance
                float distance = Vector3.Distance(transform.position, otherNotebook.transform.position);

                if (distance <= runeShareRadius)
                {
                    RequestRuneShare(otherNotebook);
                }
            }
        }

        private void RequestRuneShare(PlayerNotebook otherNotebook)
        {
            RequestRuneShareServerRpc(otherNotebook, base.LocalConnection);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRuneShareServerRpc(PlayerNotebook targetNotebook, NetworkConnection requester = null)
        {
            // Server forwards the request to the target player
            TargetReceiveRuneShareRequest(targetNotebook.Owner, requester);
        }

        [TargetRpc]
        private void TargetReceiveRuneShareRequest(NetworkConnection target, NetworkConnection requester)
        {
            // The target player sends their runes back to the requester
            var myRunes = GetCollectedRunes();
            TargetReceiveSharedRunes(requester, myRunes.ToArray());
        }

        [TargetRpc]
        private void TargetReceiveSharedRunes(NetworkConnection target, int[] sharedRuneIndices)
        {
            // Add any runes we don't already have
            bool anyNewRunes = false;

            foreach (int runeIndex in sharedRuneIndices)
            {
                if (!m_runeCollection.collectedRuneIndices.Contains(runeIndex))
                {
                    m_runeCollection.collectedRuneIndices.Add(runeIndex);
                    anyNewRunes = true;
                    Debug.Log($"[PlayerNotebook] Received shared rune {runeIndex} from nearby player");
                }
            }

            if (anyNewRunes)
            {
                OnRuneCollected?.Invoke();
            }
        }

        /// <summary>
        /// Sets the rune sharing radius. Useful for testing or dynamic difficulty.
        /// </summary>
        public void SetRuneShareRadius(float radius)
        {
            runeShareRadius = Mathf.Max(0f, radius);
        }

        /// <summary>
        /// Gets the current rune sharing radius.
        /// </summary>
        public float GetRuneShareRadius()
        {
            return runeShareRadius;
        }

        #endregion

        #region Spell Loadout Management

        /// <summary>
        /// Initializes the spell loadout for this heist.
        /// Should be called before or at the start of the heist.
        /// </summary>
        public void InitializeSpellLoadout()
        {
            // TODO: Replace this with actual data later... for now we just give the player some spells
            m_spellLoadout.equippedSpellIndices.Clear();

            // Example: Player brings the first 3 learned spells
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

        #region Rune Collection Management

        /// <summary>
        /// Adds a rune to this player's collection by RuneSO reference.
        /// Call this when the player picks up a rune in the world.
        /// </summary>
        public void AddRune(RuneSO rune)
        {
            if (GameManager.Instance == null)
                return;

            int runeIndex = Array.IndexOf(GameManager.Instance.AllRunes, rune);

            if (runeIndex == -1)
            {
                Debug.LogWarning($"[PlayerNotebook] Rune not found in GameManager.AllRunes");
                return;
            }

            CollectRune(runeIndex);
        }

        /// <summary>
        /// Adds a random uncollected rune to this player's collection.
        /// Useful for testing or special game events.
        /// </summary>
        public void AddRandomUncollectedRune()
        {
            if (GameManager.Instance == null)
                return;

            // Get list of uncollected rune indices
            List<int> uncollectedIndices = new List<int>();

            for (int i = 0; i < GameManager.Instance.AllRunes.Length; i++)
            {
                if (!m_runeCollection.collectedRuneIndices.Contains(i))
                {
                    uncollectedIndices.Add(i);
                }
            }

            // Check if there are any uncollected runes
            if (uncollectedIndices.Count == 0)
            {
                Debug.LogWarning("[PlayerNotebook] All runes have already been collected!");
                return;
            }

            // Pick a random uncollected rune and add it
            int randomIndex = UnityEngine.Random.Range(0, uncollectedIndices.Count);
            int runeIndexToAdd = uncollectedIndices[randomIndex];

            CollectRune(runeIndexToAdd);

            Debug.Log($"[PlayerNotebook] Added random rune: {GameManager.Instance.AllRunes[runeIndexToAdd].name}");
        }

        /// <summary>
        /// Adds a rune to this player's collection.
        /// Call this when the player picks up a rune in the world.
        /// </summary>
        /// <param name="runeIndex">Index of the rune in GameManager.AllRunes</param>
        public void CollectRune(int runeIndex)
        {
            if (m_runeCollection.collectedRuneIndices.Contains(runeIndex))
            {
                Debug.LogWarning($"[PlayerNotebook] Player already has rune {runeIndex}");
                return;
            }

            m_runeCollection.collectedRuneIndices.Add(runeIndex);
            OnRuneCollected?.Invoke();

            Debug.Log($"[PlayerNotebook] Rune {runeIndex} collected. Total runes: {m_runeCollection.collectedRuneIndices.Count}");
        }

        /// <summary>
        /// Gets the list of rune indices this player has collected.
        /// </summary>
        public List<int> GetCollectedRunes()
        {
            return new List<int>(m_runeCollection.collectedRuneIndices);
        }

        /// <summary>
        /// Gets the actual RuneSO objects for the runes this player has collected.
        /// </summary>
        public List<RuneSO> GetCollectedRuneObjects()
        {
            var runes = new List<RuneSO>();

            if (GameManager.Instance == null)
                return runes;

            foreach (int runeIndex in m_runeCollection.collectedRuneIndices)
            {
                if (runeIndex >= 0 && runeIndex < GameManager.Instance.AllRunes.Length)
                {
                    runes.Add(GameManager.Instance.AllRunes[runeIndex]);
                }
                else
                {
                    Debug.LogWarning($"[PlayerNotebook] Invalid rune index: {runeIndex}");
                }
            }

            return runes;
        }

        /// <summary>
        /// Checks if this player has collected a specific rune.
        /// </summary>
        public bool HasRune(int runeIndex)
        {
            return m_runeCollection.collectedRuneIndices.Contains(runeIndex);
        }

        /// <summary>
        /// Checks if this player has collected a specific rune by RuneSO reference.
        /// </summary>
        public bool HasRune(RuneSO rune)
        {
            if (GameManager.Instance == null)
                return false;

            int runeIndex = System.Array.IndexOf(GameManager.Instance.AllRunes, rune);
            return HasRune(runeIndex);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Gets a reference to the local player's notebook.
        /// Useful for UI systems that need to display the local player's data.
        /// </summary>
        public static PlayerNotebook GetLocalPlayerNotebook()
        {
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