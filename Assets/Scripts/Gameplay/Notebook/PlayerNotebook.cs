using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RooseLabs.Gameplay.Notebook
{
    /// <summary>
    /// Data structure for transmitting borrowed rune information over the network.
    /// </summary>
    [Serializable]
    public struct BorrowedRuneData
    {
        public int runeIndex;
        public int ownerClientId;
    }

    /// <summary>
    /// Tracks a borrowed rune with its owner information.
    /// </summary>
    [Serializable]
    public class BorrowedRune
    {
        public int runeIndex;
        public string ownerName;

        public BorrowedRune(int runeIndex, string ownerName)
        {
            this.runeIndex = runeIndex;
            this.ownerName = ownerName;
        }
    }

    public enum RuneDetectionMode
    {
        OnDemand,      // Check only when runes page is opened
        Continuous     // Periodic automatic checks
    }

    /// <summary>
    /// Player-specific notebook component that exists on each player's character.
    /// Manages the player's spell loadout and rune collection for the current heist.
    /// Runes are shared automatically when players are within proximity range.
    /// </summary>
    public class PlayerNotebook : NetworkBehaviour
    {
        #region Serialized Fields
        [Header("Rune Proximity Settings")]
        [SerializeField] private RuneDetectionMode detectionMode = RuneDetectionMode.OnDemand;
        [SerializeField] private float proximityRange = 5f;
        [SerializeField] private float continuousCheckPeriod = 0.5f;
        #endregion

        #region Events
        /// <summary>Invoked when this player collects a new rune</summary>
        public event Action OnRuneCollected;

        /// <summary>Invoked when borrowed runes are updated</summary>
        public event Action OnBorrowedRunesChanged;

        /// <summary>
        /// Invoked when the set of toggled runes changes.
        /// Provides the complete list of currently toggled rune indices.
        /// </summary>
        public event Action<List<int>> OnToggledRunesChanged;

        /// <summary>
        /// Invoked when toggled runes change, providing the actual RuneSO objects.
        /// </summary>
        public event Action<List<RuneSO>> OnToggledRuneObjectsChanged;
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

        /// <summary>
        /// Runes borrowed from nearby players.
        /// </summary>
        private readonly List<BorrowedRune> m_borrowedRunes = new();

        /// <summary>
        /// Set of rune indices that are currently toggled/selected by the player.
        /// </summary>
        private readonly HashSet<int> m_toggledRunes = new();
        #endregion

        #region Coroutines
        private Coroutine m_continuousCheckCoroutine;
        #endregion

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

            // Start continuous checking if that mode is selected
            if (detectionMode == RuneDetectionMode.Continuous)
            {
                StartContinuousProximityCheck();
            }

            AddRandomUncollectedRune(true);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (m_continuousCheckCoroutine != null)
            {
                StopCoroutine(m_continuousCheckCoroutine);
                m_continuousCheckCoroutine = null;
            }
        }

        #endregion    

        #region Spell Loadout Management

        /// <summary>
        /// Initializes the spell loadout for this heist.
        /// Should be called before or at the start of the heist.
        /// </summary>
        public void InitializeSpellLoadout()
        {
            m_spellLoadout.equippedSpellIndices.Clear();

            if (GameManager.Instance != null && GameManager.Instance.LearnedSpellsIndices.Count > 0)
            {
                foreach (int spellIndex in GameManager.Instance.LearnedSpellsIndices)
                {
                    m_spellLoadout.equippedSpellIndices.Add(spellIndex);
                }

                this.LogInfo($"Initialized with {m_spellLoadout.equippedSpellIndices.Count} learned spells");
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

            this.LogInfo($"Spell loadout set: {spellIndices.Count} spells");
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
                if (spellIndex >= 0 && spellIndex < GameManager.Instance.SpellDatabase.Count)
                {
                    spells.Add(GameManager.Instance.SpellDatabase[spellIndex].SpellInfo);
                }
                else
                {
                    this.LogWarning($"Invalid spell index: {spellIndex}");
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

            int spellIndex = GameManager.Instance.SpellDatabase.IndexOf(spell);
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
            int runeIndex = GameManager.Instance.RuneDatabase.IndexOf(rune);

            if (runeIndex == -1)
            {
                this.LogInfo("Rune not found in RuneDatabase");
                return;
            }

            CollectRune(runeIndex);
        }

        /// <summary>
        /// Adds a random uncollected rune to this player's collection.
        /// Useful for testing or special game events.
        /// </summary>
        public void AddRandomUncollectedRune(bool all = false)
        {
            // Get list of uncollected rune indices
            List<int> uncollectedIndices = new List<int>();

            for (int i = 0; i < GameManager.Instance.RuneDatabase.Count; i++)
            {
                if (!m_runeCollection.collectedRuneIndices.Contains(i))
                {
                    uncollectedIndices.Add(i);
                }
            }

            // Check if there are any uncollected runes
            if (uncollectedIndices.Count == 0)
            {
                this.LogWarning("All runes have already been collected!");
                return;
            }

            // If 'all' is true, collect all uncollected runes
            if (all)
            {
                foreach (int runeIndex in uncollectedIndices)
                {
                    CollectRune(runeIndex);
                }
                this.LogInfo($"Added all uncollected runes: {uncollectedIndices.Count} runes added");
                return;
            } else
            {
                // Pick a random uncollected rune and add it
                int randomIndex = Random.Range(0, uncollectedIndices.Count);
                int runeIndexToAdd = uncollectedIndices[randomIndex];

                CollectRune(runeIndexToAdd);
                this.LogInfo($"Added random rune: {GameManager.Instance.RuneDatabase[runeIndexToAdd].name}");
            }
        }

        /// <summary>
        /// Adds a rune to this player's collection.
        /// Call this when the player picks up a rune in the world.
        /// </summary>
        public void CollectRune(int runeIndex)
        {
            if (m_runeCollection.collectedRuneIndices.Contains(runeIndex))
            {
                this.LogWarning($"Player already has rune {runeIndex}");
                return;
            }

            m_runeCollection.collectedRuneIndices.Add(runeIndex);

            // Notify server of the rune collection
            if (IsOwner)
            {
                ServerNotifyRuneCollected(runeIndex);
            }

            OnRuneCollected?.Invoke();

            this.LogInfo($"Rune {runeIndex} collected. Total runes: {m_runeCollection.collectedRuneIndices.Count}");
        }

        /// <summary>
        /// Notifies the server that this player collected a rune.
        /// </summary>
        [ServerRpc]
        private void ServerNotifyRuneCollected(int runeIndex)
        {
            // Add the rune to the server's copy of this player's collection
            if (!m_runeCollection.collectedRuneIndices.Contains(runeIndex))
            {
                m_runeCollection.collectedRuneIndices.Add(runeIndex);
                this.LogInfo($"[PlayerNotebook - SERVER] Player {Owner.ClientId} collected rune {runeIndex}");
            }
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
                if (runeIndex >= 0 && runeIndex < GameManager.Instance.RuneDatabase.Count)
                {
                    runes.Add(GameManager.Instance.RuneDatabase[runeIndex]);
                }
                else
                {
                    this.LogWarning($"Invalid rune index: {runeIndex}");
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

            int runeIndex = GameManager.Instance.RuneDatabase.IndexOf(rune);
            return HasRune(runeIndex);
        }

        #endregion

        #region Rune Proximity Detection

        /// <summary>
        /// Requests a one-time proximity check from the server.
        /// Used in OnDemand mode when the player opens the runes page.
        /// </summary>
        public void RequestProximityCheck()
        {
            if (!IsOwner)
                return;

            ServerCheckNearbyRunes(Owner);
        }

        /// <summary>
        /// Starts continuous proximity checking.
        /// Called automatically if detectionMode is Continuous.
        /// </summary>
        private void StartContinuousProximityCheck()
        {
            if (m_continuousCheckCoroutine != null)
                StopCoroutine(m_continuousCheckCoroutine);

            m_continuousCheckCoroutine = StartCoroutine(ContinuousProximityCheckCoroutine());
        }

        /// <summary>
        /// Coroutine that periodically requests proximity checks from the server.
        /// </summary>
        private IEnumerator ContinuousProximityCheckCoroutine()
        {
            WaitForSeconds wait = new WaitForSeconds(continuousCheckPeriod);

            while (true)
            {
                yield return wait;

                if (IsOwner)
                {
                    ServerCheckNearbyRunes(Owner);
                }
            }
        }

        /// <summary>
        /// Server-side method that checks for nearby players and their runes.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void ServerCheckNearbyRunes(NetworkConnection conn)
        {
            if (!IsServerInitialized)
                return;

            // Get all PlayerNotebook instances
            PlayerNotebook[] allNotebooks = FindObjectsByType<PlayerNotebook>(FindObjectsSortMode.None);

            List<BorrowedRuneData> borrowedRuneDataList = new List<BorrowedRuneData>();

            foreach (PlayerNotebook otherNotebook in allNotebooks)
            {
                // Skip self
                if (otherNotebook.Owner == conn)
                    continue;

                // Check distance
                float distance = Vector3.Distance(transform.position, otherNotebook.transform.position);
                if (distance <= proximityRange)
                {
                    // Get the other player's collected runes
                    List<int> otherRunes = otherNotebook.GetCollectedRunes();

                    foreach (int runeIndex in otherRunes)
                    {
                        // Only add runes that this player doesn't already own
                        if (!HasRune(runeIndex))
                        {
                            borrowedRuneDataList.Add(new BorrowedRuneData
                            {
                                runeIndex = runeIndex,
                                ownerClientId = otherNotebook.Owner.ClientId
                            });
                        }
                    }
                }
            }

            // Send the borrowed runes data to the client
            TargetUpdateBorrowedRunes(conn, borrowedRuneDataList.ToArray());
        }

        /// <summary>
        /// Target RPC that updates the client's borrowed runes list.
        /// </summary>
        [TargetRpc]
        private void TargetUpdateBorrowedRunes(NetworkConnection conn, BorrowedRuneData[] borrowedRuneData)
        {
            // Clear current borrowed runes
            m_borrowedRunes.Clear();

            // Convert borrowed rune data to BorrowedRune objects with player names
            foreach (BorrowedRuneData data in borrowedRuneData)
            {
                string ownerName = GetPlayerNameByClientId(data.ownerClientId);
                m_borrowedRunes.Add(new BorrowedRune(data.runeIndex, ownerName));
            }

            this.LogInfo($"Updated borrowed runes: {m_borrowedRunes.Count} borrowed");

            // Notify UI
            OnBorrowedRunesChanged?.Invoke();
        }

        /// <summary>
        /// Gets a player's name by their client ID.
        /// </summary>
        private string GetPlayerNameByClientId(int clientId)
        {
            // Try to find the PlayerConnection with this client ID
            var allConnections = FindObjectsByType<PlayerConnection>(FindObjectsSortMode.None);

            foreach (var connection in allConnections)
            {
                if (connection.Owner.ClientId == clientId)
                {
                    return connection.PlayerName;
                }
            }

            return $"Player {clientId}";
        }

        /// <summary>
        /// Gets all borrowed runes.
        /// </summary>
        public List<BorrowedRune> GetBorrowedRunes()
        {
            return new List<BorrowedRune>(m_borrowedRunes);
        }

        #endregion

        #region Rune Toggle Management

        /// <summary>
        /// Toggles a rune's selection state.
        /// </summary>
        /// <param name="runeIndex">The index of the rune to toggle</param>
        public void ToggleRune(int runeIndex)
        {
            if (m_toggledRunes.Contains(runeIndex))
            {
                m_toggledRunes.Remove(runeIndex);
                this.LogInfo($"Rune {runeIndex} deselected");
            }
            else
            {
                m_toggledRunes.Add(runeIndex);
                this.LogInfo($"Rune {runeIndex} selected");
            }

            // Broadcast the change (indices)
            OnToggledRunesChanged?.Invoke(new List<int>(m_toggledRunes));

            // Broadcast the change (RuneSO objects)
            List<RuneSO> toggledRunes = GetToggledRuneObjects();
            OnToggledRuneObjectsChanged?.Invoke(toggledRunes);

            this.LogInfo("Toggled Runes: " + string.Join(", ", toggledRunes.ConvertAll(r => r.Name)));
        }

        /// <summary>
        /// Gets all currently toggled runes.
        /// </summary>
        public List<int> GetToggledRunes()
        {
            return new List<int>(m_toggledRunes);
        }

        /// <summary>
        /// Checks if a specific rune is toggled.
        /// </summary>
        public bool IsRuneToggled(int runeIndex)
        {
            return m_toggledRunes.Contains(runeIndex);
        }

        /// <summary>
        /// Clears all toggled runes.
        /// </summary>
        public void ClearToggledRunes()
        {
            m_toggledRunes.Clear();
            OnToggledRunesChanged?.Invoke(new List<int>());
            this.LogInfo("All runes deselected");
        }

        /// <summary>
        /// Gets all currently toggled runes as RuneSO objects.
        /// </summary>
        public List<RuneSO> GetToggledRuneObjects()
        {
            var toggledRunes = new List<RuneSO>();

            if (GameManager.Instance == null)
            {
                this.LogWarning("GameManager.Instance is null, cannot get toggled runes");
                return toggledRunes;
            }

            foreach (int runeIndex in m_toggledRunes)
            {
                if (runeIndex >= 0 && runeIndex < GameManager.Instance.RuneDatabase.Count)
                {
                    toggledRunes.Add(GameManager.Instance.RuneDatabase[runeIndex]);
                }
                else
                {
                    this.LogWarning($"Invalid toggled rune index: {runeIndex}");
                }
            }

            return toggledRunes;
        }

        /// <summary>
        /// Gets a specific toggled rune by index as a RuneSO object.
        /// Returns null if the rune is not toggled or index is invalid.
        /// </summary>
        public RuneSO GetToggledRuneObject(int runeIndex)
        {
            if (!m_toggledRunes.Contains(runeIndex))
                return null;

            if (GameManager.Instance == null)
                return null;

            if (runeIndex >= 0 && runeIndex < GameManager.Instance.RuneDatabase.Count)
            {
                return GameManager.Instance.RuneDatabase[runeIndex];
            }

            return null;
        }

        /// <summary>
        /// Checks if a specific RuneSO is toggled.
        /// </summary>
        public bool IsRuneToggled(RuneSO rune)
        {
            if (GameManager.Instance == null)
                return false;

            int runeIndex = GameManager.Instance.RuneDatabase.IndexOf(rune);
            return IsRuneToggled(runeIndex);
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
            return null;
        }

        public void ResetNotebook()
        {
            m_runeCollection.collectedRuneIndices.Clear();
            m_borrowedRunes.Clear();
            m_toggledRunes.Clear();
            this.LogInfo("Player notebook reset");
        }

        #endregion
    }
}
