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
        private class SpellSlot
        {
            public SpellBase SpellPrefab { get; private set; }
            public SpellBase SpellInstance { get; set; }
            public bool IsTemporary { get; }
            public ICollection<RuneSO> Runes => m_customRunes ?? SpellPrefab?.SpellInfo.Runes;

            private readonly ICollection<RuneSO> m_customRunes;

            public SpellSlot(SpellBase spellPrefab, bool isTemporary = false, ICollection<RuneSO> customRunes = null)
            {
                SpellPrefab = spellPrefab;
                IsTemporary = isTemporary;
                m_customRunes = customRunes;
            }

            public SpellBase Instantiate()
            {
                if ((bool)SpellPrefab && !SpellInstance)
                {
                    SpellInstance = SpellBase.Instantiate(SpellPrefab);
                }
                return SpellInstance;
            }

            public void DestroyInstance()
            {
                if (!SpellInstance) return;
                SpellInstance.Destroy();
                SpellInstance = null;
            }
        }

        #region Serialized
        [SerializeField] private PlayerCharacter character;
        [SerializeField] private SpellDatabase spellDatabase;

        [Tooltip("Point from which spells are cast. This should be at the tip of the wand.")]
        [SerializeField] private Transform spellCastPoint;

        [Header("Orbiting Runes")]
        [Tooltip("Container for the orbiting runes.")]
        [SerializeField] private Transform orbitingRunesContainer;
        [Tooltip("Local scale of the orbiting runes.")]
        [SerializeField] private Vector3 orbitingRunesLocalScale = Vector3.one * 0.35f;
        [Tooltip("Radius at which the runes orbit around the wand.")]
        [SerializeField] private float orbitingRunesRadius = 3.5f;
        #endregion

        #region Public Properties
        public Transform AttachmentPoint => spellCastPoint.parent;
        public Vector3 SpellCastPointLocalPosition => spellCastPoint.localPosition;
        public Vector3 SpellCastPointPosition { get; private set; }
        public bool CanUseWand =>
            !character.Data.IsCrawling &&
            !character.Data.IsSprinting &&
            !character.Data.IsRagdollActive &&
            !character.Data.isDead;
        #endregion

        #region Private Fields
        /// <summary>
        /// List of spell slots that the player can currently use.
        /// Permanent spells come first, temporary spell is always last (if present).
        /// </summary>
        private readonly List<SpellSlot> m_spellSlots = new();

        private const float SpellSwitchCooldownDuration = 0.25f;
        private float m_spellSwitchCooldownTimer = 0f;

        private int m_currentSpellIndex = 0;
        private SpellSlot m_currentSpellSlot;
        private SpellBase m_currentSpellInstance;

        private int CurrentSpellIndex
        {
            get => m_currentSpellIndex;
            set
            {
                if (m_spellSlots.Count == 0) return;
                int previousIndex = m_currentSpellIndex;
                SpellSlot previousSpellSlot = m_currentSpellSlot;
                SpellBase previousSpellInstance = m_currentSpellInstance;
                m_currentSpellIndex = (value % m_spellSlots.Count + m_spellSlots.Count) % m_spellSlots.Count;
                if (previousIndex != m_currentSpellIndex)
                {
                    m_spellSwitchCooldownTimer = SpellSwitchCooldownDuration;
                    previousSpellInstance?.StopAim();
                    m_currentSpellInstance = null;
                }
                m_currentSpellSlot = m_spellSlots[m_currentSpellIndex];
                if (previousSpellSlot == null ||
                    previousSpellSlot.SpellPrefab.SpellInfo != m_currentSpellSlot.SpellPrefab.SpellInfo)
                {
                    SetOrbitingRunes(m_currentSpellSlot.Runes);
                }
            }
        }
        #endregion

        public override void OnStartNetwork()
        {
            if (!Owner.IsLocalClient) return;
            character.Notebook.OnToggledRuneObjectsChanged += OnRuneSelectionChanged;
            character.Notebook.OnToggledSpellsChanged += OnSpellSelectionChanged;
        }

        public override void OnStopNetwork()
        {
            if (!IsOwner) return;
            character.Notebook.OnToggledRuneObjectsChanged -= OnRuneSelectionChanged;
            character.Notebook.OnToggledSpellsChanged -= OnSpellSelectionChanged;
        }

        public override void OnStartClient()
        {
            if (!IsOwner) return;
            InitializeSpellLoadout();
            SetOrbitingRunes(m_currentSpellSlot?.Runes);
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Update spell switch cooldown timer
            if (m_spellSwitchCooldownTimer > 0f)
            {
                m_spellSwitchCooldownTimer -= Time.deltaTime;
            }

            UpdateAimingState();
            if (character.Data.isAiming)
            {
                m_currentSpellSlot ??= m_spellSlots[m_currentSpellIndex];
                if (!m_currentSpellInstance && m_currentSpellSlot != null)
                {
                    m_currentSpellInstance = m_currentSpellSlot.Instantiate();
                }

                m_currentSpellInstance?.Aim();
                HandleSpellCasting();

                if (!character.Data.isCasting)
                {
                    HandleSpellSwitching();
                }
            }
            else
            {
                if (character.Data.isCasting)
                {
                    m_currentSpellInstance?.CancelCast();
                    character.Data.isCasting = false;
                }
                m_currentSpellInstance?.StopAim();
            }
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

        #region Event Handlers
        private void OnRuneSelectionChanged(ICollection<RuneSO> selectedRunes)
        {
            // Handle empty selection
            if (selectedRunes.Count == 0)
            {
                RemoveTemporarySpell();
                return;
            }

            // Find spell matching the selected runes
            SpellBase matchingSpell = spellDatabase.GetSpellWithMatchingRunes(selectedRunes);

            if (matchingSpell)
            {
                HandleValidSpellSelection(matchingSpell, selectedRunes);
            }
            else
            {
                HandleInvalidSpellSelection(selectedRunes);
            }
        }

        private void OnSpellSelectionChanged(ICollection<int> selectedSpellIndices)
        {
            // Rebuild the loadout
            InitializeSpellLoadout();

            foreach (int spellIndex in selectedSpellIndices)
            {
                var spell = spellDatabase[spellIndex];
                SpellSlot permanentSlot = new SpellSlot(spell, isTemporary: false);
                m_spellSlots.Add(permanentSlot);
            }

            // Ensure current spell index is valid
            if (m_currentSpellIndex >= m_spellSlots.Count)
            {
                CurrentSpellIndex = 0;
            }
        }
        #endregion

        #region Spell Management
        private void InitializeSpellLoadout()
        {
            m_spellSlots.Clear();
            m_spellSlots.Add(new SpellSlot(GameManager.Instance.SpellDatabase[0])); // 0 = Impero (default spell)
            m_currentSpellIndex = 0;
            m_currentSpellSlot = m_spellSlots[m_currentSpellIndex];
        }

        private void HandleValidSpellSelection(SpellBase spell, ICollection<RuneSO> selectedRunes)
        {
            // Check if the spell is already present in the spell slots
            int existingIndex = m_spellSlots.FindIndex(slot => slot.SpellPrefab == spell);

            if (existingIndex >= 0)
            {
                // Spell is already present, switch to it
                CurrentSpellIndex = existingIndex;
                if (!m_spellSlots[existingIndex].IsTemporary)
                {
                    // The present spell is a permanent one, remove temporary spell if present
                    RemoveTemporarySpell();
                }
            }
            else
            {
                // The spell is not present, add it as a temporary
                AddOrUpdateTemporarySpell(spell, selectedRunes);
                CurrentSpellIndex = m_spellSlots.Count - 1;
                this.LogInfo($"Set temporary spell to '{spell.SpellInfo.EnglishName}'.");
            }
        }

        private void HandleInvalidSpellSelection(ICollection<RuneSO> selectedRunes)
        {
            // Add failed spell as temporary
            SpellBase failedSpell = spellDatabase[1]; // 1 = Failed Spell
            AddOrUpdateTemporarySpell(failedSpell, selectedRunes);
            CurrentSpellIndex = m_spellSlots.Count - 1;
            this.LogInfo("Set temporary spell to 'Failed Spell' due to invalid rune combination.");
        }

        private void AddOrUpdateTemporarySpell(SpellBase spell, ICollection<RuneSO> runes)
        {
            SpellSlot newTemporarySpellSlot = new SpellSlot(spell, isTemporary: true, customRunes: runes);
            if (m_spellSlots[^1].IsTemporary)
            {
                // There is already a temporary spell, destroy its instance and replace it with the new one
                m_spellSlots[^1].DestroyInstance();
                m_spellSlots[^1] = newTemporarySpellSlot;

                // Instantiate the new temporary spell
                if (newTemporarySpellSlot.Instantiate())
                {
                    this.LogInfo($"Instantiated temporary spell '{newTemporarySpellSlot.SpellPrefab.SpellInfo.EnglishName}'");
                }

                if (m_currentSpellIndex == m_spellSlots.Count - 1)
                {
                    // If the temporary spell was selected, update the orbiting runes to the new ones
                    SetOrbitingRunes(newTemporarySpellSlot.Runes);
                }
            }
            else
            {
                // No temporary spell present, add it
                m_spellSlots.Add(newTemporarySpellSlot);

                // Instantiate the new temporary spell
                if (newTemporarySpellSlot.Instantiate())
                {
                    this.LogInfo($"Instantiated temporary spell '{newTemporarySpellSlot.SpellPrefab.SpellInfo.EnglishName}'");
                }
            }
        }

        // Note: AddPermanentSpell and RemovePermanentSpell are currently unused.
        // They are implemented in a way that is better suited to add/remove individual spells during a heist,
        // as they handle the existence of temporary spells, removing them in favor of permanent ones when necessary.
        // However, such a feature is not currently planned, as the spell loadout must be set before starting a heist.
        // Nonetheless, for the sake of completeness and uncertainty about future design changes, they are kept here.
        private void AddPermanentSpell(SpellBase spell)
        {
            // Check if the spell is already present in the spell slots
            int existingIndex = m_spellSlots.FindIndex(slot => slot.SpellPrefab == spell);
            SpellBase existingInstance = null;

            if (existingIndex >= 0)
            {
                if (m_spellSlots[existingIndex].IsTemporary)
                {
                    // The spell exists as temporary, destroy it and replace with permanent version
                    existingInstance = m_spellSlots[existingIndex].SpellInstance;
                    m_spellSlots.RemoveAt(existingIndex);
                }
                else
                {
                    // Spell is already present as permanent, do nothing
                    return;
                }
            }

            // Add new permanent spell
            SpellSlot permanentSlot = new SpellSlot(spell, isTemporary: false);
            permanentSlot.SpellInstance = existingInstance;
            m_spellSlots.Add(permanentSlot);

            if (existingIndex >= 0 && m_currentSpellIndex == existingIndex)
            {
                // If the added spell was previously selected as temporary, switch back to it
                m_currentSpellIndex = m_spellSlots.Count - 1;
            }
        }

        private void RemovePermanentSpell(SpellBase spell)
        {
            int existingIndex = m_spellSlots.FindIndex(slot => slot.SpellPrefab == spell);
            if (existingIndex <= 0)
            {
                // Spell not present or is the default spell at index 0 (that cannot be removed), do nothing
                return;
            }

            if (m_spellSlots[existingIndex].IsTemporary)
            {
                // Spell is temporary, do nothing.
                // Note that we can't have the same spell as both permanent and temporary, so if we found it as
                // a temporary, we know there isn't a permanent copy of it that we can remove.
                return;
            }

            bool wasSelected = m_currentSpellIndex == existingIndex;
            m_spellSlots[existingIndex].DestroyInstance();
            m_spellSlots.RemoveAt(existingIndex);

            if (wasSelected && m_spellSlots.Count > 0)
            {
                // If we removed the selected spell, switch to first spell
                CurrentSpellIndex = 0;
            }
        }

        public void RemoveTemporarySpell()
        {
            // We can only have one temporary spell, and it's always the last one in the list and never the first
            if (m_spellSlots.Count > 1 && m_spellSlots[^1].IsTemporary)
            {
                bool wasSelected = m_currentSpellIndex == m_spellSlots.Count - 1;

                // Destroy the temporary spell instance
                m_spellSlots[^1].DestroyInstance();
                m_spellSlots.RemoveAt(m_spellSlots.Count - 1);

                if (wasSelected && m_spellSlots.Count > 0)
                {
                    // If we removed the selected spell, switch to first spell
                    CurrentSpellIndex = 0;
                }

                this.LogInfo("Removed temporary spell.");
            }
        }
        #endregion

        private void UpdateAimingState()
        {
            bool wasAiming = character.Data.isAiming;
            character.Data.isAiming = CanUseWand && character.Input.aimIsPressed;

            if (wasAiming != character.Data.isAiming)
            {
                SyncAimingState(character.Data.isAiming);
                SetOrbitingRunesVisibility(character.Data.isAiming);
            }
        }

        private void HandleSpellCasting()
        {
            if (!m_currentSpellInstance) return;

            // Handle sustained spells
            if (m_currentSpellInstance.CanAimToSustain && m_currentSpellInstance.IsBeingSustained)
            {
                m_currentSpellInstance.ContinueCast();
            }
            // Handle spell input
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

            // Handle scroll input during sustained spells
            if (m_currentSpellInstance.IsBeingSustained)
            {
                HandleSustainedSpellScrollInput();
            }

            character.Data.isCasting = m_currentSpellInstance.IsCasting;
        }

        private void HandleSustainedSpellScrollInput()
        {
            if (character.Input.scrollBackwardIsPressed && character.Input.scrollForwardIsPressed)
            {
                // Both pressed, do nothing
                return;
            }

            if (character.Input.scrollForwardWasPressed)
                m_currentSpellInstance.ScrollForwardPressed();
            else if (character.Input.scrollForwardIsPressed)
                m_currentSpellInstance.ScrollForwardHeld();
            else if (character.Input.scrollBackwardWasPressed)
                m_currentSpellInstance.ScrollBackwardPressed();
            else if (character.Input.scrollBackwardIsPressed)
                m_currentSpellInstance.ScrollBackwardHeld();
            else if (character.Input.scrollInput != 0f)
                m_currentSpellInstance.Scroll(character.Input.scrollInput);
        }

        private void HandleSpellSwitching()
        {
            if (m_spellSwitchCooldownTimer > 0f)
                return;

            bool resetToFirst = character.Input.scrollButtonWasPressed ||
                                (InputHandler.CurrentInputScheme == InputScheme.Gamepad &&
                                 character.Input.nextIsPressed && character.Input.previousIsPressed);

            if (resetToFirst)
                CurrentSpellIndex = 0;
            else if (character.Input.nextWasPressed || character.Input.scrollInput >= 1f)
                CurrentSpellIndex--;
            else if (character.Input.previousWasPressed || character.Input.scrollInput <= -1f)
                CurrentSpellIndex++;
        }

        #region Network Sync
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
        #endregion

        #region Orbiting Runes
        private readonly List<OrbitingRune> m_orbitingRunes = new();

        private void SetOrbitingRunes(ICollection<RuneSO> runes)
        {
            // Clear existing runes
            ClearOrbitingRunes();

            if (runes == null || runes.Count == 0) return;

            // Instantiate new runes
            foreach (var rune in runes)
            {
                if (!rune.Sprite) continue;
                GameObject runeObj = new GameObject($"OrbitingRune_{rune.name}");
                runeObj.transform.SetParent(orbitingRunesContainer);
                runeObj.transform.localPosition = Vector3.zero;
                runeObj.transform.localRotation = Quaternion.identity;
                runeObj.transform.localScale = orbitingRunesLocalScale;
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
                float x = orbitingRunesRadius * Mathf.Cos(angle * Mathf.Deg2Rad);
                float y = orbitingRunesRadius * Mathf.Sin(angle * Mathf.Deg2Rad);

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
