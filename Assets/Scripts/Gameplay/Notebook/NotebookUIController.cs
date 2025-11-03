using RooseLabs.ScriptableObjects;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    /// <summary>
    /// Controls the Notebook UI display.
    /// Subscribes to NotebookManager events and updates UI elements accordingly.
    /// Separated from data logic for clean architecture.
    /// Opening/closing is handled by GUIManager.
    /// </summary>
    public class NotebookUIController : MonoBehaviour
    {
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

        private void OnEnable()
        {
            Debug.Log("[NotebookUI] OnEnable called!");

            try
            {
                // Fix raycast blocking on button texts
                FixButtonRaycastTargets();

                // Subscribe to tab button clicks with debug logging
                if (questTabButton != null)
                {
                    Debug.Log($"[NotebookUI] Quest button found: {questTabButton.name}, Interactable: {questTabButton.interactable}");
                    Debug.Log($"[NotebookUI] Quest button enabled: {questTabButton.enabled}, gameObject active: {questTabButton.gameObject.activeInHierarchy}");
                    Debug.Log($"[NotebookUI] Quest button onClick listener count before: {questTabButton.onClick.GetPersistentEventCount()}");

                    questTabButton.onClick.AddListener(() => {
                        Debug.Log("[NotebookUI] Quest tab button clicked");
                        SwitchTab(NotebookTab.Quest);
                    });

                    Debug.Log($"[NotebookUI] Quest button onClick listener count after: {questTabButton.onClick.GetPersistentEventCount()}");
                }
                else
                {
                    Debug.LogWarning("[NotebookUI] Quest tab button is null!");
                }

                if (runesTabButton != null)
                {
                    Debug.Log($"[NotebookUI] Runes button found: {runesTabButton.name}, Interactable: {runesTabButton.interactable}");
                    runesTabButton.onClick.AddListener(() => {
                        Debug.Log("[NotebookUI] Runes tab button clicked");
                        SwitchTab(NotebookTab.Runes);
                    });
                }
                else
                {
                    Debug.LogWarning("[NotebookUI] Runes tab button is null!");
                }

                if (spellsTabButton != null)
                {
                    Debug.Log($"[NotebookUI] Spells button found: {spellsTabButton.name}, Interactable: {spellsTabButton.interactable}");
                    spellsTabButton.onClick.AddListener(() => {
                        Debug.Log("[NotebookUI] Spells tab button clicked");
                        SwitchTab(NotebookTab.Spells);
                    });
                }
                else
                {
                    Debug.LogWarning("[NotebookUI] Spells tab button is null!");
                }

                Debug.Log("[NotebookUI] About to subscribe to data changes...");

                // Subscribe to data changes
                if (Gameplay.NotebookManager.Instance != null)
                {
                    Debug.Log("[NotebookUI] NotebookManager found, subscribing to events...");
                    Gameplay.NotebookManager.Instance.OnQuestDataChanged += RefreshQuestPage;
                    Gameplay.NotebookManager.Instance.OnRuneCollectionChanged += RefreshRunesPage;
                    Gameplay.NotebookManager.Instance.OnObjectiveCompleted += OnObjectiveCompleted;
                }
                else
                {
                    Debug.LogWarning("[NotebookUI] NotebookManager.Instance is null!");
                }

                Debug.Log("[NotebookUI] About to call SwitchTab...");
                // Refresh current tab when enabled
                SwitchTab(m_currentTab);
                Debug.Log("[NotebookUI] OnEnable completed successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NotebookUI] Exception in OnEnable: {e.Message}\n{e.StackTrace}");
            }
        }

        private void OnDisable()
        {
            Debug.Log("[NotebookUI] OnDisable called!");

            // Unsubscribe from events
            if (Gameplay.NotebookManager.Instance != null)
            {
                Gameplay.NotebookManager.Instance.OnQuestDataChanged -= RefreshQuestPage;
                Gameplay.NotebookManager.Instance.OnRuneCollectionChanged -= RefreshRunesPage;
                Gameplay.NotebookManager.Instance.OnObjectiveCompleted -= OnObjectiveCompleted;
            }

            if (questTabButton != null)
                questTabButton.onClick.RemoveAllListeners();
            if (runesTabButton != null)
                runesTabButton.onClick.RemoveAllListeners();
            if (spellsTabButton != null)
                spellsTabButton.onClick.RemoveAllListeners();
        }

        #region Public Methods

        /// <summary>
        /// Refreshes the currently displayed tab.
        /// Called by GUIManager when notebook is opened.
        /// </summary>
        public void RefreshCurrentTab()
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

        #region Tab Control

        private void SwitchTab(NotebookTab tab)
        {
            Debug.Log($"[NotebookUI] Switching to tab: {tab}");
            m_currentTab = tab;

            // Hide all pages
            if (questPage != null) questPage.SetActive(false);
            if (runesPage != null) runesPage.SetActive(false);
            if (spellsPage != null) spellsPage.SetActive(false);

            // Show selected page and refresh its content
            switch (tab)
            {
                case NotebookTab.Quest:
                    if (questPage != null) questPage.SetActive(true);
                    RefreshQuestPage();
                    break;
                case NotebookTab.Runes:
                    if (runesPage != null) runesPage.SetActive(true);
                    RefreshRunesPage();
                    break;
                case NotebookTab.Spells:
                    if (spellsPage != null) spellsPage.SetActive(true);
                    RefreshSpellsPage();
                    break;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Disables raycast target on all text elements inside buttons.
        /// This prevents text from blocking button clicks.
        /// </summary>
        private void FixButtonRaycastTargets()
        {
            Button[] allButtons = new Button[] { questTabButton, runesTabButton, spellsTabButton };

            foreach (Button button in allButtons)
            {
                if (button == null) continue;

                // Get all TextMeshProUGUI components in the button and its children
                TextMeshProUGUI[] texts = button.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (TextMeshProUGUI text in texts)
                {
                    if (text.raycastTarget)
                    {
                        text.raycastTarget = false;
                        Debug.Log($"[NotebookUI] Disabled raycast target on text in button: {button.name}");
                    }
                }
            }
        }

        // Temporary debug method - remove after fixing
        private void Update()
        {
            // Test if pressing 1/2/3 keys manually triggers the tabs
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Debug.Log("[NotebookUI] Manual test: Invoking Quest button");
                questTabButton?.onClick.Invoke();
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                Debug.Log("[NotebookUI] Manual test: Invoking Runes button");
                runesTabButton?.onClick.Invoke();
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                Debug.Log("[NotebookUI] Manual test: Invoking Spells button");
                spellsTabButton?.onClick.Invoke();
            }
        }

        #endregion

        #region Quest Page

        private void RefreshQuestPage()
        {
            if (Gameplay.NotebookManager.Instance == null)
                return;

            // Update quest title and description
            if (questTitleText != null)
                questTitleText.text = Gameplay.NotebookManager.Instance.QuestTitle;
            if (questDescriptionText != null)
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
            if (m_currentTab == NotebookTab.Quest && gameObject.activeInHierarchy)
            {
                RefreshQuestPage();
            }
        }

        #endregion

        #region Runes Page

        private void RefreshRunesPage()
        {
            if (Gameplay.GameManager.Instance == null || runesContainer == null)
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
            if (spellsContainer == null)
                return;

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