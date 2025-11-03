using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.Core;
using RooseLabs.Gameplay.Spells;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerWand : NetworkBehaviour
    {
        #region Serialized
        [SerializeField] private PlayerCharacter character;
        [SerializeField] private SpellDatabaseSO spellDatabase;

        [Tooltip("Point from which spells are cast. This should be at the tip of the wand.")]
        [field: SerializeField] public Transform SpellCastPoint { get; private set; }
        #endregion

        private InputHandler m_inputHandler;

        private SpellBase m_currentSpellInstance;
        private bool m_currentSpellInstanceDirty = true; // Start dirty to ensure initial setup

        // TODO: Either add public methods to add/remove spells from this list or make it public.
        private readonly List<int> m_availableSpells = new() { 0, 1 };

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
                    Debug.Log($"[PlayerWand] Switched to spell index {m_currentSpellIndex} (Spell ID: {m_availableSpells[m_currentSpellIndex]})");
                }
            }
        }

        private void Start()
        {
            m_inputHandler = InputHandler.Instance;
        }

        public override void OnStartClient()
        {
            enabled = IsOwner;
        }

        private void Update()
        {
            bool wasAiming = character.Data.IsAiming;
            character.Data.IsAiming = CanUseWand && character.Input.aimIsPressed;
            if (character.Data.IsAiming)
            {
                if (m_currentSpellInstance)
                {
                    if (m_currentSpellInstance.CanAimToSustain && m_currentSpellInstance.IsBeingSustained)
                    {
                        m_currentSpellInstance.ContinueCast();
                        character.Data.IsCasting = true;
                    }
                    else if (character.Input.castWasPressed)
                    {
                        m_currentSpellInstance.StartCast();
                        character.Data.IsCasting = true;
                    }
                    else if (character.Input.castIsPressed)
                    {
                        m_currentSpellInstance.ContinueCast();
                        character.Data.IsCasting = true;
                    }
                    else if (character.Input.castWasReleased)
                    {
                        m_currentSpellInstance.CancelCast();
                        character.Data.IsCasting = false;
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
                }
                if (character.Data.IsCasting) return;
                // TODO: Switching spells needs a small cooldown (<= 1 second).
                if (character.Input.scrollButtonWasPressed || (m_inputHandler.IsCurrentDeviceGamepad() && character.Input.nextIsPressed && character.Input.previousIsPressed))
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
                    UpdateCurrentSpellInstance();
            }
            else if (character.Data.IsCasting)
            {
                m_currentSpellInstance?.CancelCast();
                character.Data.IsCasting = false;
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
            GameObject spellPrefab = spellDatabase[spellID];
            if (spellPrefab)
            {
                m_currentSpellInstance = SpellBase.Instantiate(spellPrefab, SpellCastPoint.position);
                Debug.Log($"[PlayerWand] Instantiated spell ID {spellID} ({spellPrefab.name})");
            }
            else
            {
                Debug.LogWarning($"[PlayerWand] Spell ID {spellID} not found in database.");
            }
            m_currentSpellInstanceDirty = false;
        }

        // TODO: We probably also want to prevent using the wand when there's no active heist.
        public bool CanUseWand =>
            !character.Data.IsCrawling &&
            !character.Data.IsRunning &&
            !character.Data.IsRagdollActive;
    }
}
