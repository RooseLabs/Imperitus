using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.Core;
using RooseLabs.Gameplay.Spells;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Player
{
    public class PlayerWand : NetworkBehaviour
    {
        private static Logger Logger => Logger.GetLogger("PlayerWand");

        #region Serialized
        [SerializeField] private PlayerCharacter character;
        [SerializeField] private SpellDatabase spellDatabase;

        [Tooltip("Point from which spells are cast. This should be at the tip of the wand.")]
        [SerializeField] private Transform spellCastPoint;
        #endregion

        public Vector3 SpellCastPointLocalPosition => spellCastPoint.localPosition;
        public Vector3 SpellCastPointPosition { get; private set; }

        private SpellBase m_currentSpellInstance;
        private bool m_currentSpellInstanceDirty = true; // Start dirty to ensure initial setup

        private readonly List<int> m_availableSpells = new() { 0, 2, 1 };

        private int m_currentSpellIndex = 0;
        private int CurrentSpellIndex
        {
            get => m_currentSpellIndex;
            set
            {
                if (m_availableSpells.Count == 0) return;
                int previousValue = m_currentSpellIndex;
                m_currentSpellIndex = (value % m_availableSpells.Count + m_availableSpells.Count) % m_availableSpells.Count;
                if (previousValue != m_currentSpellIndex)
                {
                    m_currentSpellInstanceDirty = true;
                    Logger.Info($"[PlayerWand] Switched to spell index {m_currentSpellIndex} (Spell ID: {m_availableSpells[m_currentSpellIndex]})");
                }
            }
        }

        private void Update()
        {
            if (!IsOwner) return;
            bool wasAiming = character.Data.isAiming;
            character.Data.isAiming = CanUseWand && character.Input.aimIsPressed;
            if (wasAiming != character.Data.isAiming)
            {
                SyncAimingState(character.Data.isAiming);
            }
            if (character.Data.isAiming)
            {
                if (m_currentSpellInstance)
                {
                    if (m_currentSpellInstance.CanAimToSustain && m_currentSpellInstance.IsBeingSustained)
                    {
                        m_currentSpellInstance.ContinueCast();
                    }
                    else if (character.Input.castWasPressed)
                    {
                        m_currentSpellInstance.StartCast();
                    }
                    else if (character.Input.castIsPressed)
                    {
                        m_currentSpellInstance.ContinueCast();
                    }
                    else if (character.Input.castWasReleased)
                    {
                        m_currentSpellInstance.CancelCast();
                    }
                    if (m_currentSpellInstance.IsBeingSustained)
                    {
                        if (character.Input.scrollBackwardIsPressed && character.Input.scrollForwardIsPressed) { /* noop */ }
                        else if (character.Input.scrollForwardWasPressed) m_currentSpellInstance.ScrollForwardPressed();
                        else if (character.Input.scrollForwardIsPressed) m_currentSpellInstance.ScrollForwardHeld();
                        else if (character.Input.scrollBackwardWasPressed) m_currentSpellInstance.ScrollBackwardPressed();
                        else if (character.Input.scrollBackwardIsPressed) m_currentSpellInstance.ScrollBackwardHeld();
                        else if (character.Input.scrollInput != 0f) m_currentSpellInstance.Scroll(character.Input.scrollInput);
                    }
                    character.Data.isCasting = m_currentSpellInstance.IsCasting;
                }
                if (character.Data.isCasting) return;
                // TODO: Switching spells needs a small cooldown (<= 1 second).
                if (character.Input.scrollButtonWasPressed || (InputHandler.Instance.IsCurrentDeviceGamepad() && character.Input.nextIsPressed && character.Input.previousIsPressed))
                {
                    CurrentSpellIndex = 0;
                }
                else if (character.Input.nextWasPressed || character.Input.scrollInput >= 1f)
                {
                    CurrentSpellIndex++;
                }
                else if (character.Input.previousWasPressed || character.Input.scrollInput <= -1f)
                {
                    CurrentSpellIndex--;
                }
                if (m_currentSpellInstanceDirty)
                {
                    UpdateCurrentSpellInstance();
                }
            }
            else if (character.Data.isCasting)
            {
                m_currentSpellInstance?.CancelCast();
                character.Data.isCasting = false;
            }
        }

        private void UpdateCurrentSpellInstance()
        {
            // Destroy previous spell instance (if it exists) and instantiate new one
            if (m_currentSpellInstance)
            {
                m_currentSpellInstance.Destroy();
                m_currentSpellInstance = null;
            }
            int spellID = m_availableSpells[m_currentSpellIndex];
            SpellBase spellPrefab = spellDatabase[spellID];
            if (spellPrefab)
            {
                m_currentSpellInstance = SpellBase.Instantiate(spellPrefab);
                Logger.Info($"[PlayerWand] Instantiated spell ID {spellID} ({spellPrefab.name})");
            }
            else
            {
                Logger.Warning($"[PlayerWand] Spell ID {spellID} not found in database.");
            }
            m_currentSpellInstanceDirty = false;
        }

        private void LateUpdate()
        {
            // We need to update this position in LateUpdate because its position is based on the wand attachment bone
            // which, in Update, would not have been evaluated by the Animator yet. This would be fine at normal FPS
            // but at low FPS it behaves unpredictably, reporting nonsensical positions. Unity shenanigans. ¯\_(ツ)_/¯
            SpellCastPointPosition = spellCastPoint.position;
        }

        // TODO: We probably also want to prevent using the wand when there's no active heist.
        public bool CanUseWand =>
            !character.Data.IsCrawling &&
            !character.Data.IsSprinting &&
            !character.Data.IsRagdollActive &&
            !character.Data.isDead;

        private void SyncAimingState(bool isAiming)
        {
            if (IsServerInitialized)
                SyncAimingState_ObserversRPC(isAiming);
            else
                SyncAimingState_ServerRPC(isAiming);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SyncAimingState_ServerRPC(bool isAiming)
        {
            SyncAimingState_ObserversRPC(isAiming);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void SyncAimingState_ObserversRPC(bool isAiming)
        {
            character.Data.isAiming = isAiming;
        }
    }
}
