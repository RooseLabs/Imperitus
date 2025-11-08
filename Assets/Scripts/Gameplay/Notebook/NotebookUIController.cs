using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    /// <summary>
    /// Controls the Notebook UI display.
    /// Subscribes to NotebookManager events and updates UI elements accordingly.
    /// Separated from data logic for clean architecture.
    /// Input handling should be done externally (e.g., via GUIManager).
    /// </summary>
    public class NotebookUIController : MonoBehaviour
    {
        [Header("Notebook Canvas")]
        [SerializeField] private Canvas notebookCanvas;

        [Header("Notebook UI Root")]
        [SerializeField] private GameObject notebookPanel;

        [Header("Tab Buttons")]
        [SerializeField] private Button questTabButton;
        [SerializeField] private Button runesTabButton;
        [SerializeField] private Button spellsTabButton;

        [Header("Page Containers")]
        [SerializeField] private GameObject questPage;
        [SerializeField] private GameObject runesPage;
        [SerializeField] private GameObject spellsPage;

        [Header("Quest Page Elements")]
        [SerializeField] private TextMeshProUGUI questTitleText;
        [SerializeField] private TextMeshProUGUI questDescriptionText;
        [SerializeField] private Transform objectivesContainer;
        [SerializeField] private GameObject objectivePrefab;

        [Header("Runes Page Elements")]
        [SerializeField] private Transform runesContainer;
        [SerializeField] private GameObject runeSlotPrefab;

        [Header("Spells Page Elements")]
        [SerializeField] private Transform spellsContainer;
        [SerializeField] private GameObject spellSlotPrefab;

        private enum NotebookTab
        {
            Quest,
            Runes,
            Spells
        }

        private NotebookTab m_currentTab = NotebookTab.Quest;
        private bool m_isOpen = false;
        private bool m_isEnabled = true; // Tracks if notebook is allowed to be opened

        private void Start()
        {
            // Initial setup
            if (notebookCanvas != null)
                notebookCanvas.enabled = false;
            notebookPanel.SetActive(false);

            // Subscribe to tab button clicks
            questTabButton?.onClick.AddListener(() => SwitchTab(NotebookTab.Quest));
            runesTabButton?.onClick.AddListener(() => SwitchTab(NotebookTab.Runes));
            spellsTabButton?.onClick.AddListener(() => SwitchTab(NotebookTab.Spells));

            // Subscribe to data changes
            if (Gameplay.NotebookManager.Instance != null)
            {
                Gameplay.NotebookManager.Instance.OnQuestDataChanged += RefreshQuestPage;
                Gameplay.NotebookManager.Instance.OnRuneCollectionChanged += RefreshRunesPage;
                Gameplay.NotebookManager.Instance.OnObjectiveCompleted += OnObjectiveCompleted;
            }

            // Initial page display
            SwitchTab(NotebookTab.Quest);
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (Gameplay.NotebookManager.Instance != null)
            {
                Gameplay.NotebookManager.Instance.OnQuestDataChanged -= RefreshQuestPage;
                Gameplay.NotebookManager.Instance.OnRuneCollectionChanged -= RefreshRunesPage;
                Gameplay.NotebookManager.Instance.OnObjectiveCompleted -= OnObjectiveCompleted;
            }

            questTabButton?.onClick.RemoveAllListeners();
            runesTabButton?.onClick.RemoveAllListeners();
            spellsTabButton?.onClick.RemoveAllListeners();
        }

        #region Notebook Control

        /// <summary>
        /// Enables or disables the notebook canvas.
        /// Use this to control whether the notebook can be opened (e.g., disable in menus).
        /// </summary>
        public void SetNotebookEnabled(bool enabled)
        {
            m_isEnabled = enabled;

            if (notebookCanvas != null)
                notebookCanvas.enabled = enabled;

            // If disabling while open, close it
            if (!enabled && m_isOpen)
            {
                CloseNotebook();
            }
        }

        public void ToggleNotebook()
        {
            if (!m_isEnabled)
                return;

            if (m_isOpen)
                CloseNotebook();
            else
                OpenNotebook();
        }

        public void OpenNotebook()
        {
            if (!m_isEnabled)
                return;

            m_isOpen = true;

            if (notebookCanvas != null)
                notebookCanvas.enabled = true;
            notebookPanel.SetActive(true);

            // Refresh the current tab's content
            RefreshCurrentTab();
        }

        public void CloseNotebook()
        {
            m_isOpen = false;
            //notebookPanel.SetActive(false);

            // Keep canvas enabled but hide panel, or disable canvas entirely
            // Disabling canvas prevents any interaction
            if (notebookCanvas != null && !m_isEnabled)
                notebookCanvas.enabled = false;
        }

        private void SwitchTab(NotebookTab tab)
        {
            m_currentTab = tab;

            // Hide all pages
            questPage.SetActive(false);
            runesPage.SetActive(false);
            spellsPage.SetActive(false);

            // Show selected page and refresh its content
            switch (tab)
            {
                case NotebookTab.Quest:
                    questPage.SetActive(true);
                    RefreshQuestPage();
                    break;
                case NotebookTab.Runes:
                    runesPage.SetActive(true);
                    RefreshRunesPage();
                    break;
                case NotebookTab.Spells:
                    spellsPage.SetActive(true);
                    RefreshSpellsPage();
                    break;
            }
        }

        private void RefreshCurrentTab()
        {
            switch (m_currentTab)
            {
                case NotebookTab.Quest:
                    RefreshQuestPage();
                    break;
                case NotebookTab.Runes:
                    RefreshRunesPage();
                    break;
                case NotebookTab.Spells:
                    RefreshSpellsPage();
                    break;
            }
        }

        #endregion

        #region Quest Page

        private void RefreshQuestPage()
        {
            if (Gameplay.NotebookManager.Instance == null)
                return;

            // Update quest title and description
            questTitleText.text = Gameplay.NotebookManager.Instance.QuestTitle;
            questDescriptionText.text = Gameplay.NotebookManager.Instance.QuestDescription;

            // Clear existing objectives
            //foreach (Transform child in objectivesContainer)
            //{
            //    Destroy(child.gameObject);
            //}

            // Populate objectives
            //var objectives = Gameplay.NotebookManager.Instance.Objectives;
            //for (int i = 0; i < objectives.Count; i++)
            //{
            //    var objectiveData = objectives[i];
            //    GameObject objectiveObj = Instantiate(objectivePrefab, objectivesContainer);

            //    // Assuming the prefab has a Text component for description
            //    // and a Toggle or Image for completion status
            //    Text descText = objectiveObj.GetComponentInChildren<Text>();
            //    if (descText != null)
            //    {
            //        descText.text = objectiveData.description;

            //        // Visual feedback for completed objectives
            //        if (objectiveData.isCompleted)
            //        {
            //            descText.fontStyle = FontStyle.Italic;
            //            descText.color = Color.gray;
            //        }
            //    }

            //    // You can add a checkmark image or toggle here
            //    Toggle toggle = objectiveObj.GetComponentInChildren<Toggle>();
            //    if (toggle != null)
            //    {
            //        toggle.isOn = objectiveData.isCompleted;
            //        toggle.interactable = false; // Read-only
            //    }
            //}
        }

        private void OnObjectiveCompleted(int objectiveIndex)
        {
            // Optionally add visual/audio feedback here
            Debug.Log($"[NotebookUI] Objective {objectiveIndex} completed!");

            // Refresh the quest page to show updated status
            if (m_currentTab == NotebookTab.Quest && m_isOpen)
            {
                RefreshQuestPage();
            }
        }

        #endregion

        #region Runes Page

        private void RefreshRunesPage()
        {
            if (Gameplay.GameManager.Instance == null)
                return;

            // Clear existing rune slots
            foreach (Transform child in runesContainer)
            {
                Destroy(child.gameObject);
            }

            // Get collected rune indices
            var collectedIndices = Gameplay.GameManager.Instance.CollectedRunes;
            var allRunes = Gameplay.GameManager.Instance.AllRunes;

            // Populate collected runes
            foreach (int runeIndex in collectedIndices)
            {
                if (runeIndex >= 0 && runeIndex < allRunes.Length)
                {
                    RuneSO rune = allRunes[runeIndex];
                    GameObject runeSlot = Instantiate(runeSlotPrefab, runesContainer);

                    // Assuming prefab has Image and Text components
                    Image icon = runeSlot.GetComponentInChildren<Image>();
                    if (icon != null)
                        icon.sprite = rune.Sprite;

                    Text nameText = runeSlot.GetComponentInChildren<Text>();
                    if (nameText != null)
                        nameText.text = rune.Name;
                }
            }
        }

        #endregion

        #region Spells Page

        private void RefreshSpellsPage()
        {
            // Clear existing spell slots
            foreach (Transform child in spellsContainer)
            {
                Destroy(child.gameObject);
            }

            // Get local player's spell loadout
            var playerNotebook = Gameplay.PlayerNotebook.GetLocalPlayerNotebook();
            if (playerNotebook == null)
            {
                Debug.LogWarning("[NotebookUI] Could not find local player notebook");
                return;
            }

            List<SpellSO> equippedSpells = playerNotebook.GetEquippedSpells();

            // Populate equipped spells
            foreach (SpellSO spell in equippedSpells)
            {
                GameObject spellSlot = Instantiate(spellSlotPrefab, spellsContainer);

                // Assuming prefab has Text components for name
                Text latinNameText = spellSlot.transform.Find("LatinName")?.GetComponent<Text>();
                if (latinNameText != null)
                    latinNameText.text = spell.Name;

                Text englishNameText = spellSlot.transform.Find("EnglishName")?.GetComponent<Text>();
                if (englishNameText != null)
                    englishNameText.text = spell.EnglishName;

                // Optionally display the runes required for this spell
                Transform runesTransform = spellSlot.transform.Find("Runes");
                if (runesTransform != null)
                {
                    foreach (RuneSO rune in spell.Runes)
                    {
                        // Create small rune icons showing spell composition
                        GameObject runeIcon = new GameObject("RuneIcon");
                        runeIcon.transform.SetParent(runesTransform);
                        Image img = runeIcon.AddComponent<Image>();
                        img.sprite = rune.Sprite;
                    }
                }
            }
        }

        #endregion
    }
}