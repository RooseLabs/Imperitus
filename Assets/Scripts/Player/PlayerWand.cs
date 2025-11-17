using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.Core;
using RooseLabs.Gameplay;
using RooseLabs.Gameplay.Spells;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerWand : NetworkBehaviour
    {
        #region Serialized
        [SerializeField] private PlayerCharacter character;
        [SerializeField] private SpellDatabase spellDatabase;

        [Tooltip("Point from which spells are cast. This should be at the tip of the wand.")]
        [SerializeField] private Transform spellCastPoint;
        [Tooltip("Container for the orbiting runes.")]
        [SerializeField] private Transform orbitingRunesContainer;
        #endregion

        public Transform AttachmentPoint => spellCastPoint.parent;

        public Vector3 SpellCastPointLocalPosition => spellCastPoint.localPosition;
        public Vector3 SpellCastPointPosition { get; private set; }

        private SpellBase m_currentSpellInstance;
        private bool m_currentSpellInstanceDirty = true; // Start dirty to ensure initial setup
        private bool m_isLastAvailableSpellTemporary = false;
        private RuneSO[] m_temporarySpellRunes;

        /// <summary>
        /// List of SpellBase prefabs that the player can currently use.
        /// </summary>
        private readonly List<SpellBase> m_availableSpells = new();

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
                    this.LogInfo($"Switched to spell index {m_currentSpellIndex} (Spell: {m_availableSpells[m_currentSpellIndex].SpellInfo.EnglishName})");
                }
            }
        }

        public override void OnStartNetwork()
        {
            if (!Owner.IsLocalClient) return;
            character.Notebook.OnToggledRuneObjectsChanged += OnRuneSelectionChanged;

            m_availableSpells.Clear();
            m_availableSpells.Add(GameManager.Instance.SpellDatabase[0]); // 0 = Impero (default spell)
        }

        public override void OnStopNetwork()
        {
            if (!IsOwner) return;
            character.Notebook.OnToggledRuneObjectsChanged -= OnRuneSelectionChanged;
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
            if (wasAiming != character.Data.isAiming)
                SetOrbitingRunesVisibility(character.Data.isAiming);
        }

        private void UpdateCurrentSpellInstance()
        {
            // Destroy previous spell instance (if it exists) and instantiate new one
            if (m_currentSpellInstance)
            {
                m_currentSpellInstance.Destroy();
                m_currentSpellInstance = null;
            }
            SpellBase spellPrefab = m_availableSpells[m_currentSpellIndex];
            if (spellPrefab)
            {
                m_currentSpellInstance = SpellBase.Instantiate(spellPrefab);
                this.LogInfo($"Instantiated spell '{spellPrefab.name}'");
            }
            else
            {
                this.LogError($"Spell at index {m_currentSpellIndex} is null!");
            }
            if (m_currentSpellInstance)
            {
                if (CurrentSpellIndex == m_availableSpells.Count - 1 && m_isLastAvailableSpellTemporary)
                {
                    // Current spell is temporary, set the orbiting runes to the temporary runes (selected by the player in the notebook)
                    SetOrbitingRunes(m_temporarySpellRunes);
                }
                else
                {
                    SetOrbitingRunes(m_currentSpellInstance.SpellInfo.Runes);
                }
            }
            else
            {
                ClearOrbitingRunes();
            }
            m_currentSpellInstanceDirty = false;
        }

        private void LateUpdate()
        {
            // We need to update this position in LateUpdate because its position is based on the wand attachment bone
            // which, in Update, would not have been evaluated by the Animator yet. This would be fine at normal FPS
            // but at low FPS it behaves unpredictably, reporting nonsensical positions. Unity shenanigans. ¯\_(ツ)_/¯
            SpellCastPointPosition = spellCastPoint.position;

            if (!PlayerCharacter.LocalCharacter) return;
            orbitingRunesContainer.rotation = Quaternion.LookRotation(
                orbitingRunesContainer.position - PlayerCharacter.LocalCharacter.Camera.transform.position);
        }

        private void OnRuneSelectionChanged(List<RuneSO> selectedRunes)
        {
            if (!IsOwner) return;
            if (selectedRunes.Count == 0 && m_isLastAvailableSpellTemporary)
            {
                // Remove the temporary spell if no runes are selected
                m_availableSpells.RemoveAt(m_availableSpells.Count - 1);
                m_isLastAvailableSpellTemporary = false;
                this.LogInfo("Removed temporary spell due to no runes being selected.");
                return;
            }
            int spellIndexToSwitchTo;
            var spell = GameManager.Instance.SpellDatabase.GetSpellWithMatchingRunes(selectedRunes);
            if (spell)
            {
                if (!m_availableSpells.Contains(spell))
                {
                    m_availableSpells.Add(spell);
                    spellIndexToSwitchTo = m_availableSpells.Count - 1;
                    m_isLastAvailableSpellTemporary = true;
                    m_temporarySpellRunes = selectedRunes.ToArray();
                    this.LogInfo($"Added temporary spell '{spell.SpellInfo.EnglishName}' to available spells.");
                }
                else
                {
                    // Found existing spell, switch to it and remove temporary spell if it exists
                    spellIndexToSwitchTo = m_availableSpells.IndexOf(spell);
                    if (m_isLastAvailableSpellTemporary)
                    {
                        m_availableSpells.RemoveAt(m_availableSpells.Count - 1);
                        this.LogInfo("Removed temporary spell from available spells due to selecting an existing spell.");
                    }
                    m_isLastAvailableSpellTemporary = false;
                    m_temporarySpellRunes = null;
                }
            }
            else
            {
                if (!m_isLastAvailableSpellTemporary)
                {
                    var failedSpell = GameManager.Instance.SpellDatabase[1]; // 1 = Failed Spell
                    m_availableSpells.Add(failedSpell);
                    m_isLastAvailableSpellTemporary = true;
                    this.LogInfo("Added 'Nothing' spell (Failed Spell) to available spells.");
                }
                m_temporarySpellRunes = selectedRunes.ToArray();
                spellIndexToSwitchTo = m_availableSpells.Count - 1;
            }
            CurrentSpellIndex = spellIndexToSwitchTo;
            if (m_currentSpellInstanceDirty)
            {
                UpdateCurrentSpellInstance();
            }
            else if (m_isLastAvailableSpellTemporary && CurrentSpellIndex == m_availableSpells.Count - 1)
            {
                // Update orbiting runes if the current spell is already the temporary spell
                SetOrbitingRunes(m_temporarySpellRunes);
            }
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

        #region Orbiting Runes
        private readonly List<OrbitingRune> m_orbitingRunes = new();
        private const float OrbitingRunesRadius = 3f;

        private void SetOrbitingRunes(RuneSO[] runes)
        {
            // Clear existing runes
            ClearOrbitingRunes();

            if (runes == null || runes.Length == 0) return;

            // Instantiate new runes
            foreach (var rune in runes)
            {
                if (!rune.Sprite) continue;
                GameObject runeObj = new GameObject($"OrbitingRune_{rune.name}");
                runeObj.transform.SetParent(orbitingRunesContainer);
                runeObj.transform.localPosition = Vector3.zero;
                runeObj.transform.localRotation = Quaternion.identity;
                runeObj.transform.localScale = Vector3.one * 2;
                var orbitingRuneComp = runeObj.AddComponent<OrbitingRune>();
                orbitingRuneComp.SetRune(rune);
                orbitingRuneComp.SetVisible(character.Data.isAiming);
                m_orbitingRunes.Add(orbitingRuneComp);
            }

            DistributeOrbitingRunes();
        }

        private void ClearOrbitingRunes()
        {
            foreach (Transform child in orbitingRunesContainer)
            {
                Destroy(child.gameObject);
            }
            m_orbitingRunes.Clear();
        }

        private void DistributeOrbitingRunes()
        {
            if (m_orbitingRunes.Count == 0) return;

            float angleStep = 360f / m_orbitingRunes.Count;
            float angle = 0f;

            foreach (var obj in m_orbitingRunes)
            {
                float x = OrbitingRunesRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                float y = OrbitingRunesRadius * Mathf.Sin(angle * Mathf.Deg2Rad);

                obj.SetPosition(new Vector3(x, y, obj.transform.localPosition.z));
                angle += angleStep;
            }
        }

        private void SetOrbitingRunesVisibility(bool isVisible)
        {
            foreach (var rune in m_orbitingRunes)
            {
                rune.SetVisible(isVisible);
            }
        }
        #endregion
    }
}
