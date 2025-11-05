using RooseLabs.ScriptableObjects;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    /// <summary>
    /// Controls the Notebook UI display.
    /// Subscribes to NotebookManager and PlayerNotebook events and updates UI elements accordingly.
    /// Opening/closing is handled by GUIManager.
    /// </summary>
    public class NotebookUIController : MonoBehaviour
    {
        [Header("Page Containers")]
        [SerializeField] private GameObject assignmentPage;
        [SerializeField] private GameObject runesPage;
        [SerializeField] private GameObject spellsPage;

        [Header("Assignment Page Elements")]
        [SerializeField] private TextMeshProUGUI assignmentNumberText;
        [SerializeField] private Transform assignmentTasksContainer;
        [SerializeField] private GameObject taskDescriptionPrefab;
        [SerializeField] private Transform assignmentTaskImagesContainer;

        [Header("Runes Page Elements")]
        [SerializeField] private Transform runesContainer;

        [Header("Spells Page Elements")]
        [SerializeField] private Transform spellsContainer;
        [SerializeField] private GameObject spellSlotPrefab;

        private enum NotebookTab
        {
            Assignment,
            Runes,
            Spells
        }

        private NotebookTab m_currentTab = NotebookTab.Assignment;
        private Gameplay.PlayerNotebook m_localPlayerNotebook;

        // Track which rune slots have been filled (indices of Image components in runesContainer)
        private List<int> m_availableRuneSlots = new List<int>();
        private Dictionary<int, int> m_runeIndexToSlotIndex = new Dictionary<int, int>(); // Maps rune index to slot index

        private void OnEnable()
        {
            Debug.Log("[NotebookUI] OnEnable called!");

            // Get reference to local player's notebook
            m_localPlayerNotebook = Gameplay.PlayerNotebook.GetLocalPlayerNotebook();
            if (m_localPlayerNotebook == null)
            {
                Debug.LogError("[NotebookUI] Could not find local player notebook!");
                return;
            }

            // Subscribe to data changes
            if (Gameplay.NotebookManager.Instance != null)
            {
                Gameplay.NotebookManager.Instance.OnAssignmentDataChanged += RefreshAssignmentPage;
            }

            if (m_localPlayerNotebook != null)
            {
                m_localPlayerNotebook.OnRuneCollected += OnRuneCollected;
            }

            // Initialize rune slots tracking
            InitializeRuneSlots();

            // Refresh current tab when enabled
            SwitchTab(m_currentTab);
        }

        private void OnDisable()
        {
            Debug.Log("[NotebookUI] OnDisable called!");

            // Unsubscribe from events
            if (Gameplay.NotebookManager.Instance != null)
            {
                Gameplay.NotebookManager.Instance.OnAssignmentDataChanged -= RefreshAssignmentPage;
            }

            if (m_localPlayerNotebook != null)
            {
                m_localPlayerNotebook.OnRuneCollected -= OnRuneCollected;
            }
        }

        #region Public Methods

        /// <summary>
        /// Switches to the Assignment tab. Call this from Unity Events.
        /// </summary>
        public void ShowAssignmentTab()
        {
            Debug.Log("[NotebookUI] ShowAssignmentTab called via Unity Event");
            SwitchTab(NotebookTab.Assignment);
        }

        /// <summary>
        /// Switches to the Runes tab. Call this from Unity Events.
        /// </summary>
        public void ShowRunesTab()
        {
            Debug.Log("[NotebookUI] ShowRunesTab called via Unity Event");
            SwitchTab(NotebookTab.Runes);
        }

        /// <summary>
        /// Switches to the Spells tab. Call this from Unity Events.
        /// </summary>
        public void ShowSpellsTab()
        {
            Debug.Log("[NotebookUI] ShowSpellsTab called via Unity Event");
            SwitchTab(NotebookTab.Spells);
        }

        /// <summary>
        /// Refreshes the currently displayed tab.
        /// Called by GUIManager when notebook is opened.
        /// </summary>
        public void RefreshCurrentTab()
        {
            switch (m_currentTab)
            {
                case NotebookTab.Assignment:
                    RefreshAssignmentPage();
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
            if (assignmentPage != null) assignmentPage.SetActive(false);
            if (runesPage != null) runesPage.SetActive(false);
            if (spellsPage != null) spellsPage.SetActive(false);

            // Show selected page and refresh its content
            switch (tab)
            {
                case NotebookTab.Assignment:
                    if (assignmentPage != null) assignmentPage.SetActive(true);
                    RefreshAssignmentPage();
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
        /// Note: This is less critical when using Unity Events set up in Inspector,
        /// but still good practice for consistency.
        /// </summary>

        #endregion

        #region Assignment Page

        private void RefreshAssignmentPage()
        {
            if (Gameplay.NotebookManager.Instance == null)
                return;

            var assignmentData = Gameplay.NotebookManager.Instance.GetCurrentAssignment();
            if (assignmentData == null)
            {
                Debug.LogWarning("[NotebookUI] No assignment data available");
                return;
            }

            // Update assignment number
            if (assignmentNumberText != null)
            {
                assignmentNumberText.text = $"Assignment {assignmentData.assignmentNumber}";
            }

            // Clear existing task descriptions
            if (assignmentTasksContainer != null)
            {
                foreach (Transform child in assignmentTasksContainer)
                {
                    Destroy(child.gameObject);
                }

                // Populate task descriptions
                foreach (var task in assignmentData.tasks)
                {
                    if (taskDescriptionPrefab != null)
                    {
                        GameObject taskObj = Instantiate(taskDescriptionPrefab, assignmentTasksContainer);
                        TextMeshProUGUI taskText = taskObj.GetComponentInChildren<TextMeshProUGUI>();
                        if (taskText != null)
                        {
                            taskText.text = task.description;
                        }
                    }
                }
            }

            // Clear existing task images
            if (assignmentTaskImagesContainer != null)
            {
                foreach (Transform child in assignmentTaskImagesContainer)
                {
                    Destroy(child.gameObject);
                }

                // Populate task images
                foreach (var task in assignmentData.tasks)
                {
                    if (task.taskImage != null)
                    {
                        GameObject imageObj = new GameObject("TaskImage");
                        imageObj.transform.SetParent(assignmentTaskImagesContainer, false);

                        Image img = imageObj.AddComponent<Image>();
                        img.sprite = task.taskImage;
                        img.preserveAspect = true;
                    }
                }
            }

            Debug.Log($"[NotebookUI] Assignment page refreshed with {assignmentData.tasks.Count} tasks");
        }

        #endregion

        #region Runes Page

        /// <summary>
        /// Initializes the list of available rune slots (indices 0-17 for the 18 slots).
        /// Called once when the notebook is opened.
        /// </summary>
        private void InitializeRuneSlots()
        {
            if (runesContainer == null)
                return;

            // Only initialize if not already initialized
            if (m_availableRuneSlots.Count > 0)
                return;

            m_runeIndexToSlotIndex.Clear();

            // Assume the runesContainer has exactly 18 Image components as children
            int slotCount = runesContainer.childCount;
            for (int i = 0; i < slotCount; i++)
            {
                m_availableRuneSlots.Add(i);

                // Disable the image initially (placeholder state)
                Image slotImage = runesContainer.GetChild(i).GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.enabled = false;
                }
            }

            Debug.Log($"[NotebookUI] Initialized {slotCount} rune slots");
        }

        /// <summary>
        /// Refreshes the runes page with all collected runes.
        /// </summary>
        private void RefreshRunesPage()
        {
            if (m_localPlayerNotebook == null || runesContainer == null)
                return;

            var collectedRunes = m_localPlayerNotebook.GetCollectedRuneObjects();
            var collectedIndices = m_localPlayerNotebook.GetCollectedRunes();

            // Reset available slots list but DON'T clear the mappings
            m_availableRuneSlots.Clear();

            // Start with all slots available
            int slotCount = runesContainer.childCount;
            for (int i = 0; i < slotCount; i++)
            {
                m_availableRuneSlots.Add(i);

                // Disable all images initially
                Image slotImage = runesContainer.GetChild(i).GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.enabled = false;
                }
            }

            // Remove slots that are already assigned from the available list
            foreach (var slotIndex in m_runeIndexToSlotIndex.Values)
            {
                m_availableRuneSlots.Remove(slotIndex);
            }

            // Place each collected rune
            for (int i = 0; i < collectedRunes.Count; i++)
            {
                RuneSO rune = collectedRunes[i];
                int runeIndex = collectedIndices[i];

                int slotIndex;

                // Check if this rune already has an assigned slot
                if (m_runeIndexToSlotIndex.ContainsKey(runeIndex))
                {
                    // Use the existing slot
                    slotIndex = m_runeIndexToSlotIndex[runeIndex];
                }
                else
                {
                    // Assign a new random slot
                    if (m_availableRuneSlots.Count == 0)
                    {
                        Debug.LogWarning("[NotebookUI] No more available rune slots!");
                        break;
                    }

                    int randomIndex = Random.Range(0, m_availableRuneSlots.Count);
                    slotIndex = m_availableRuneSlots[randomIndex];
                    m_availableRuneSlots.RemoveAt(randomIndex);

                    // Store the mapping for future use
                    m_runeIndexToSlotIndex[runeIndex] = slotIndex;
                }

                // Update the slot with the rune sprite
                Image slotImage = runesContainer.GetChild(slotIndex).GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = rune.Sprite;
                    slotImage.enabled = true;
                }
            }

            Debug.Log($"[NotebookUI] Runes page refreshed with {collectedRunes.Count} runes");
        }

        /// <summary>
        /// Called when the local player collects a new rune.
        /// Adds the rune to a random available slot without refreshing the entire page.
        /// </summary>
        private void OnRuneCollected()
        {
            if (m_localPlayerNotebook == null || runesContainer == null)
                return;

            // Get the most recently collected rune
            var collectedIndices = m_localPlayerNotebook.GetCollectedRunes();
            if (collectedIndices.Count == 0)
                return;

            int newRuneIndex = collectedIndices[collectedIndices.Count - 1];

            // Check if we already placed this rune
            if (m_runeIndexToSlotIndex.ContainsKey(newRuneIndex))
                return;

            // Check if there are available slots
            if (m_availableRuneSlots.Count == 0)
            {
                Debug.LogWarning("[NotebookUI] No more available rune slots!");
                return;
            }

            // Get the rune data
            if (Gameplay.GameManager.Instance == null)
                return;

            if (newRuneIndex < 0 || newRuneIndex >= Gameplay.GameManager.Instance.AllRunes.Length)
            {
                Debug.LogWarning($"[NotebookUI] Invalid rune index: {newRuneIndex}");
                return;
            }

            RuneSO rune = Gameplay.GameManager.Instance.AllRunes[newRuneIndex];

            // Pick a random available slot
            int randomIndex = Random.Range(0, m_availableRuneSlots.Count);
            int slotIndex = m_availableRuneSlots[randomIndex];
            m_availableRuneSlots.RemoveAt(randomIndex);

            // Store the mapping
            m_runeIndexToSlotIndex[newRuneIndex] = slotIndex;

            // Update the slot with the rune sprite
            Image slotImage = runesContainer.GetChild(slotIndex).GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.sprite = rune.Sprite;
                slotImage.enabled = true;
            }

            Debug.Log($"[NotebookUI] New rune collected and placed in slot {slotIndex}");
        }

        #endregion

        #region Spells Page

        private void RefreshSpellsPage()
        {
            if (m_localPlayerNotebook == null || spellsContainer == null)
                return;

            // Clear existing spell slots
            foreach (Transform child in spellsContainer)
            {
                Destroy(child.gameObject);
            }

            List<SpellSO> equippedSpells = m_localPlayerNotebook.GetEquippedSpells();

            // Populate equipped spells
            foreach (SpellSO spell in equippedSpells)
            {
                if (spellSlotPrefab == null)
                    continue;

                GameObject spellSlot = Instantiate(spellSlotPrefab, spellsContainer);

                // Find the runes container within the spell slot prefab
                Transform runesTransform = spellSlot.transform.Find("Runes");
                if (runesTransform != null && spell.Runes != null)
                {
                    // Create rune icons for this spell
                    foreach (RuneSO rune in spell.Runes)
                    {
                        GameObject runeIcon = new GameObject("RuneIcon");
                        runeIcon.transform.SetParent(runesTransform, false);

                        Image img = runeIcon.AddComponent<Image>();
                        img.sprite = rune.Sprite;
                        img.preserveAspect = true;
                    }
                }
            }

            Debug.Log($"[NotebookUI] Spells page refreshed with {equippedSpells.Count} spells");
        }

        #endregion
    }
}