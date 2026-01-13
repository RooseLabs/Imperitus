using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Utils;

namespace RooseLabs.Gameplay.Notebook
{
    public class NotebookManager : NetworkBehaviour
    {
        public static NotebookManager Instance { get; private set; }

        #region Events
        /// <summary>Invoked when spell loadout lock state changes</summary>
        public event Action<bool> OnSpellLoadoutLockChanged;
        #endregion

        #region Network Synced Data
        /// <summary>
        /// Network-synchronized spell loadout lock state.
        /// When true, players cannot modify their spell selections.
        /// </summary>
        private readonly SyncVar<bool> m_spellLoadoutLocked = new(new SyncTypeSettings(WritePermission.ServerOnly));
        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncVar changes
            m_spellLoadoutLocked.OnChange += OnSpellLoadoutLockSynced;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe from SyncVar changes
            m_spellLoadoutLocked.OnChange -= OnSpellLoadoutLockSynced;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsServerInitialized)
            {
                // Notify about initial lock state
                OnSpellLoadoutLockChanged?.Invoke(m_spellLoadoutLocked.Value);
            }
        }

        #region Spell Loadout Lock Management
        /// <summary>
        /// Gets whether the spell loadout is currently locked.
        /// </summary>
        public bool IsSpellLoadoutLocked => m_spellLoadoutLocked.Value;

        /// <summary>
        /// Locks the spell loadout for all players (SERVER ONLY).
        /// </summary>
        [Server]
        public void LockSpellLoadout()
        {
            if (!IsServerInitialized)
            {
                this.LogError("LockSpellLoadout called but server is not initialized!");
                return;
            }

            if (m_spellLoadoutLocked.Value)
            {
                this.LogWarning("Spell loadout is already locked");
                return;
            }

            m_spellLoadoutLocked.Value = true;
            this.LogInfo("Spell loadout locked for all players");

            // Invoke locally on server
            OnSpellLoadoutLockChanged?.Invoke(true);
        }

        /// <summary>
        /// Unlocks the spell loadout for all players (SERVER ONLY).
        /// </summary>
        [Server]
        public void UnlockSpellLoadout()
        {
            if (!IsServerInitialized)
            {
                this.LogError("UnlockSpellLoadout called but server is not initialized!");
                return;
            }

            if (!m_spellLoadoutLocked.Value)
            {
                this.LogWarning("Spell loadout is already unlocked");
                return;
            }

            m_spellLoadoutLocked.Value = false;
            this.LogInfo("Spell loadout unlocked for all players");

            // Invoke locally on server
            OnSpellLoadoutLockChanged?.Invoke(false);
        }
        #endregion

        #region SyncVar Callbacks
        private void OnSpellLoadoutLockSynced(bool prev, bool next, bool asServer)
        {
            if (asServer) return;

            this.LogInfo($"Received spell loadout lock sync from server: {next}");
            OnSpellLoadoutLockChanged?.Invoke(next);
        }
        #endregion
    }
}
