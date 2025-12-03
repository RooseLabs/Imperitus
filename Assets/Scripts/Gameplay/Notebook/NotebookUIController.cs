using RooseLabs.Enemies;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using RooseLabs.Utils;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.Gameplay.Notebook
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
        [SerializeField] private TMP_Text assignmentNumberText;
        [SerializeField] private Transform assignmentTasksContainer;
        [SerializeField] private GameObject taskDescriptionPrefab;
        [SerializeField] private Transform assignmentTaskImagesContainer;

        [Header("Runes Page Elements")]
        [SerializeField] private Transform runesContainer;

        [Header("Spells Page Elements")]
        [SerializeField] private Transform spellsContainer;
        [SerializeField] private GameObject spellSlotPrefab;

        [Header("Spell Toggle Visual Settings")]
        [SerializeField] private Color spellToggledColor = new Color(1f, 1f, 1f, 1f); // White/full opacity when toggled
        [SerializeField] private Color spellUntoggledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Gray/semi-transparent when not toggled

        [Header("Spell Loadout Lock")]
        [SerializeField] private Color spellLockedOverlayColor = new Color(0.3f, 0.3f, 0.3f, 0.3f); // Dark overlay when locked

        private enum NotebookTab
        {
            Assignment,
            Runes,
            Spells
        }

        private NotebookTab m_currentTab = NotebookTab.Assignment;
        private PlayerNotebook m_localPlayerNotebook;

        // Track which rune slots have been filled (indices of Image components in runesContainer)
        private List<int> m_availableRuneSlots = new List<int>();
        private Dictionary<int, int> m_runeIndexToSlotIndex = new Dictionary<int, int>(); // Maps rune index to slot index
        private Dictionary<int, GameObject> m_borrowedRuneSlots = new Dictionary<int, GameObject>(); // Maps slot index to the Image GameObject for borrowed runes

        private void OnEnable()
        {
            //Debug.Log("[NotebookUI] OnEnable called!");

            // Get reference to local player's notebook
            m_localPlayerNotebook = PlayerNotebook.GetLocalPlayerNotebook();
            if (m_localPlayerNotebook == null)
            {
                //Debug.LogError("[NotebookUI] Could not find local player notebook!");
                return;
            }

            // Subscribe to data changes
            if (NotebookManager.Instance != null)
            {
                NotebookManager.Instance.OnAssignmentDataChanged += RefreshAssignmentPage;
                NotebookManager.Instance.OnSpellLoadoutLockChanged += OnSpellLoadoutLockChanged; // Add this
            }

            if (m_localPlayerNotebook != null)
            {
                m_localPlayerNotebook.OnRuneCollected += OnRuneCollected;
                m_localPlayerNotebook.OnBorrowedRunesChanged += OnBorrowedRunesChanged;
            }

            // Initialize rune slots tracking
            InitializeRuneSlots();

            // Refresh current tab when enabled
            SwitchTab(m_currentTab);
        }

        private void OnDisable()
        {
            //Debug.Log("[NotebookUI] OnDisable called!");

            // Unsubscribe from events
            if (NotebookManager.Instance != null)
            {
                NotebookManager.Instance.OnAssignmentDataChanged -= RefreshAssignmentPage;
                NotebookManager.Instance.OnSpellLoadoutLockChanged -= OnSpellLoadoutLockChanged; // Add this
            }

            if (m_localPlayerNotebook != null)
            {
                m_localPlayerNotebook.OnRuneCollected -= OnRuneCollected;
                m_localPlayerNotebook.OnBorrowedRunesChanged -= OnBorrowedRunesChanged;
            }
        }

        #region Public Methods

        /// <summary>
        /// Switches to the Assignment tab. Call this from Unity Events.
        /// </summary>
        public void ShowAssignmentTab()
        {
            //Debug.Log("[NotebookUI] ShowAssignmentTab called via Unity Event");
            SwitchTab(NotebookTab.Assignment);
        }

        /// <summary>
        /// Switches to the Runes tab. Call this from Unity Events.
        /// </summary>
        public void ShowRunesTab()
        {
            //Debug.Log("[NotebookUI] ShowRunesTab called via Unity Event");

            // Request proximity check when opening runes page (for OnDemand mode)
            if (m_localPlayerNotebook != null)
            {
                m_localPlayerNotebook.RequestProximityCheck();
            }

            SwitchTab(NotebookTab.Runes);
        }

        /// <summary>
        /// Switches to the Spells tab. Call this from Unity Events.
        /// </summary>
        public void ShowSpellsTab()
        {
            //Debug.Log("[NotebookUI] ShowSpellsTab called via Unity Event");
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
            //Debug.Log($"[NotebookUI] Switching to tab: {tab}");
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

        #region Assignment Page

        private void RefreshAssignmentPage()
        {
            if (NotebookManager.Instance == null)
                return;

            var assignmentData = NotebookManager.Instance.GetCurrentAssignment();
            if (assignmentData == null)
            {
                //Debug.LogWarning("[NotebookUI] No assignment data available");
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
                foreach (var taskId in assignmentData.tasks)
                {
                    var task = GameManager.Instance.TaskDatabase[taskId];
                    if (taskDescriptionPrefab != null)
                    {
                        GameObject taskObj = Instantiate(taskDescriptionPrefab, assignmentTasksContainer);
                        TMP_Text taskText = taskObj.GetComponentInChildren<TMP_Text>();
                        if (taskText != null)
                        {
                            taskText.text = task.Description;
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
                foreach (var taskId in assignmentData.tasks)
                {
                    var task = GameManager.Instance.TaskDatabase[taskId];
                    if (task.Image != null)
                    {
                        GameObject imageObj = new GameObject("TaskImage");
                        imageObj.transform.SetParent(assignmentTaskImagesContainer, false);

                        Image img = imageObj.AddComponent<Image>();
                        img.sprite = task.Image;
                        img.preserveAspect = true;
                    }
                }
            }

            //Debug.Log($"[NotebookUI] Assignment page refreshed with {assignmentData.tasks.Count} tasks");
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
            m_borrowedRuneSlots.Clear();

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

            //Debug.Log($"[NotebookUI] Initialized {slotCount} rune slots");
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

                    // Only remove labels for slots that aren't borrowed runes
                    if (!m_borrowedRuneSlots.ContainsKey(i))
                    {
                        TMP_Text existingLabel = slotImage.GetComponentInChildren<TMP_Text>();
                        if (existingLabel != null)
                        {
                            Destroy(existingLabel.gameObject);
                        }
                    }
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

                if (m_runeIndexToSlotIndex.ContainsKey(runeIndex))
                {
                    slotIndex = m_runeIndexToSlotIndex[runeIndex];
                }
                else
                {
                    if (m_availableRuneSlots.Count == 0)
                    {
                        Debug.LogWarning("[NotebookUI] No more available rune slots!");
                        break;
                    }

                    int randomIndex = Random.Range(0, m_availableRuneSlots.Count);
                    slotIndex = m_availableRuneSlots[randomIndex];
                    m_availableRuneSlots.RemoveAt(randomIndex);

                    m_runeIndexToSlotIndex[runeIndex] = slotIndex;
                }

                Image slotImage = runesContainer.GetChild(slotIndex).GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = rune.Sprite;
                    slotImage.enabled = true;

                    // Add Button component if it doesn't exist
                    Button runeButton = slotImage.gameObject.GetComponent<Button>();
                    if (runeButton == null)
                    {
                        runeButton = slotImage.gameObject.AddComponent<Button>();
                    }

                    // Store the rune index for the click handler
                    int capturedRuneIndex = runeIndex; // Capture for closure

                    // Remove old listeners and add new one
                    runeButton.onClick.RemoveAllListeners();
                    runeButton.onClick.AddListener(() => OnRuneClicked(capturedRuneIndex));

                    // Update visual state based on toggle status
                    UpdateRuneToggleVisual(slotImage.gameObject, m_localPlayerNotebook.IsRuneToggled(runeIndex));

                    // If this was a borrowed rune that we now own, remove the label
                    if (m_borrowedRuneSlots.ContainsKey(slotIndex))
                    {
                        TMP_Text label = slotImage.GetComponentInChildren<TMP_Text>();
                        if (label != null)
                        {
                            Destroy(label.gameObject);
                        }
                        m_borrowedRuneSlots.Remove(slotIndex);
                    }
                }
            }

            // Display borrowed runes (this will now re-enable slots that were disabled)
            DisplayBorrowedRunes();

            //Debug.Log($"[NotebookUI] Runes page refreshed with {collectedRunes.Count} collected runes");
        }

        /// <summary>
        /// Called when a rune is clicked.
        /// </summary>
        private void OnRuneClicked(int runeIndex)
        {
            if (m_localPlayerNotebook == null)
                return;

            // Toggle the rune
            m_localPlayerNotebook.ToggleRune(runeIndex);

            // Update the visual state
            if (m_runeIndexToSlotIndex.TryGetValue(runeIndex, out int slotIndex))
            {
                GameObject slotObject = runesContainer.GetChild(slotIndex).gameObject;
                UpdateRuneToggleVisual(slotObject, m_localPlayerNotebook.IsRuneToggled(runeIndex));
            }

            //Debug.Log($"[NotebookUI] Rune {runeIndex} clicked");
        }

        /// <summary>
        /// Updates the visual state of a rune to show if it's toggled.
        /// </summary>
        private void UpdateRuneToggleVisual(GameObject runeSlot, bool isToggled)
        {
            Image slotImage = runeSlot.GetComponent<Image>();
            if (slotImage == null)
                return;

            // Reduce alpha by half when toggled, full alpha when not toggled
            Color currentColor = slotImage.color;
            currentColor.a = isToggled ? 0.5f : 1f;
            slotImage.color = currentColor;
        }

        /// <summary>
        /// Displays borrowed runes in available slots.
        /// </summary>
        private void DisplayBorrowedRunes()
        {
            if (m_localPlayerNotebook == null || runesContainer == null)
                return;

            m_borrowedRuneSlots.Clear();

            List<BorrowedRune> borrowedRunes = m_localPlayerNotebook.GetBorrowedRunes();

            foreach (var borrowedRune in borrowedRunes)
            {
                if (m_localPlayerNotebook.HasRune(borrowedRune.runeIndex))
                    continue;

                int slotIndex;

                // Check if we've already placed this borrowed rune
                if (m_runeIndexToSlotIndex.ContainsKey(borrowedRune.runeIndex))
                {
                    slotIndex = m_runeIndexToSlotIndex[borrowedRune.runeIndex];
                }
                else
                {
                    if (m_availableRuneSlots.Count == 0)
                    {
                        Debug.LogWarning("[NotebookUI] No more available slots for borrowed runes!");
                        break;
                    }

                    if (GameManager.Instance == null)
                        continue;

                    if (borrowedRune.runeIndex < 0 || borrowedRune.runeIndex >= GameManager.Instance.RuneDatabase.Count)
                    {
                        Debug.LogWarning($"[NotebookUI] Invalid borrowed rune index: {borrowedRune.runeIndex}");
                        continue;
                    }

                    int randomIndex = Random.Range(0, m_availableRuneSlots.Count);
                    slotIndex = m_availableRuneSlots[randomIndex];
                    m_availableRuneSlots.RemoveAt(randomIndex);

                    m_runeIndexToSlotIndex[borrowedRune.runeIndex] = slotIndex;
                }

                RuneSO rune = GameManager.Instance.RuneDatabase[borrowedRune.runeIndex];

                Transform slotTransform = runesContainer.GetChild(slotIndex);
                Image slotImage = slotTransform.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = rune.Sprite;
                    slotImage.enabled = true;

                    // Add Button component if it doesn't exist
                    Button runeButton = slotImage.gameObject.GetComponent<Button>();
                    if (runeButton == null)
                    {
                        runeButton = slotImage.gameObject.AddComponent<Button>();
                    }

                    // Store the rune index for the click handler
                    int capturedRuneIndex = borrowedRune.runeIndex; // Capture for closure

                    // Remove old listeners and add new one
                    runeButton.onClick.RemoveAllListeners();
                    runeButton.onClick.AddListener(() => OnRuneClicked(capturedRuneIndex));

                    // Update visual state based on toggle status
                    UpdateRuneToggleVisual(slotImage.gameObject, m_localPlayerNotebook.IsRuneToggled(borrowedRune.runeIndex));

                    // Remove old label if it exists
                    TMP_Text existingLabel = slotImage.GetComponentInChildren<TMP_Text>();
                    if (existingLabel != null)
                    {
                        Destroy(existingLabel.gameObject);
                    }

                    // Create the owner name label
                    GameObject nameLabel = new GameObject("OwnerNameLabel");
                    nameLabel.transform.SetParent(slotTransform, false);

                    TextMeshProUGUI nameText = nameLabel.AddComponent<TextMeshProUGUI>();
                    nameText.text = borrowedRune.ownerName;
                    nameText.fontSize = 30;
                    nameText.color = Color.white;
                    nameText.alignment = TextAlignmentOptions.Center;
                    nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    nameText.verticalAlignment = VerticalAlignmentOptions.Middle;

                    RectTransform nameRect = nameLabel.GetComponent<RectTransform>();
                    nameRect.anchorMin = Vector2.zero;
                    nameRect.anchorMax = Vector2.one;
                    nameRect.sizeDelta = Vector2.zero;
                    nameRect.anchoredPosition = Vector2.zero;

                    m_borrowedRuneSlots[slotIndex] = slotImage.gameObject;
                }
            }

            //Debug.Log($"[NotebookUI] Displayed {borrowedRunes.Count} borrowed runes");
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

            // Check if we already placed this rune (either as collected or borrowed)
            if (m_runeIndexToSlotIndex.ContainsKey(newRuneIndex))
            {
                // If it was borrowed, we need to remove the owner name label
                int existingSlotIndex = m_runeIndexToSlotIndex[newRuneIndex];
                Transform slotTransform = runesContainer.GetChild(existingSlotIndex);
                TMP_Text existingLabel = slotTransform.GetComponentInChildren<TMP_Text>();
                if (existingLabel != null)
                {
                    Destroy(existingLabel.gameObject);
                }

                // Remove from borrowed tracking
                m_borrowedRuneSlots.Remove(existingSlotIndex);
                return;
            }

            // Check if there are available slots
            if (m_availableRuneSlots.Count == 0)
            {
                Debug.LogWarning("[NotebookUI] No more available rune slots!");
                return;
            }

            // Get the rune data
            if (GameManager.Instance == null)
                return;

            if (newRuneIndex < 0 || newRuneIndex >= GameManager.Instance.RuneDatabase.Count)
            {
                Debug.LogWarning($"[NotebookUI] Invalid rune index: {newRuneIndex}");
                return;
            }

            RuneSO rune = GameManager.Instance.RuneDatabase[newRuneIndex];

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

            if (GameManager.Instance.currentRequiredRunes.Contains(rune))
            {
                this.LogInfo($"Task-required rune collected: {rune.name}");

                // Notify spawn manager
                if (EnemySpawnManager.Instance != null)
                {
                    EnemySpawnManager.Instance.OnTaskRuneCollected(m_localPlayerNotebook.GetComponentInParent<PlayerCharacter>().transform.position);
                }
            }

            //Debug.Log($"[NotebookUI] New rune collected and placed in slot {slotIndex}");
        }

        /// <summary>
        /// Called when borrowed runes change.
        /// Updates only the borrowed runes without affecting collected runes.
        /// </summary>
        private void OnBorrowedRunesChanged()
        {
            if (m_localPlayerNotebook == null || runesContainer == null)
                return;

            // Only update if we're currently viewing the runes page
            if (m_currentTab != NotebookTab.Runes)
                return;

            // Get current borrowed runes
            List<BorrowedRune> borrowedRunes = m_localPlayerNotebook.GetBorrowedRunes();
            HashSet<int> currentBorrowedRuneIndices = new HashSet<int>();

            foreach (var borrowedRune in borrowedRunes)
            {
                // Skip if player owns this rune
                if (m_localPlayerNotebook.HasRune(borrowedRune.runeIndex))
                    continue;

                currentBorrowedRuneIndices.Add(borrowedRune.runeIndex);
            }

            // Remove borrowed runes that are no longer nearby
            List<int> slotsToRemove = new List<int>();
            foreach (var kvp in m_borrowedRuneSlots)
            {
                int slotIndex = kvp.Key;

                // Find which rune is in this slot
                int runeInSlot = -1;
                foreach (var mapping in m_runeIndexToSlotIndex)
                {
                    if (mapping.Value == slotIndex)
                    {
                        runeInSlot = mapping.Key;
                        break;
                    }
                }

                // If this borrowed rune is no longer in the borrowed list, remove it
                if (runeInSlot != -1 && !currentBorrowedRuneIndices.Contains(runeInSlot))
                {
                    slotsToRemove.Add(slotIndex);
                }
            }

            // Remove slots
            foreach (int slotIndex in slotsToRemove)
            {
                // Find the rune index for this slot
                int runeIndexToRemove = -1;
                foreach (var mapping in m_runeIndexToSlotIndex)
                {
                    if (mapping.Value == slotIndex)
                    {
                        runeIndexToRemove = mapping.Key;
                        break;
                    }
                }

                if (runeIndexToRemove != -1)
                {
                    // Clear the slot
                    Image slotImage = runesContainer.GetChild(slotIndex).GetComponent<Image>();
                    if (slotImage != null)
                    {
                        slotImage.enabled = false;

                        // Remove owner name label
                        TMP_Text label = slotImage.GetComponentInChildren<TMP_Text>();
                        if (label != null)
                        {
                            Destroy(label.gameObject);
                        }
                    }

                    // Make slot available again
                    m_availableRuneSlots.Add(slotIndex);
                    m_runeIndexToSlotIndex.Remove(runeIndexToRemove);
                    m_borrowedRuneSlots.Remove(slotIndex);
                }
            }

            // Add new borrowed runes
            foreach (var borrowedRune in borrowedRunes)
            {
                // Skip if player owns this rune
                if (m_localPlayerNotebook.HasRune(borrowedRune.runeIndex))
                    continue;

                // Skip if already displayed
                if (m_runeIndexToSlotIndex.ContainsKey(borrowedRune.runeIndex))
                    continue;

                // Check if there are available slots
                if (m_availableRuneSlots.Count == 0)
                {
                    Debug.LogWarning("[NotebookUI] No more available slots for borrowed runes!");
                    break;
                }

                // Get the rune data
                if (GameManager.Instance == null)
                    continue;

                if (borrowedRune.runeIndex < 0 || borrowedRune.runeIndex >= GameManager.Instance.RuneDatabase.Count)
                    continue;

                RuneSO rune = GameManager.Instance.RuneDatabase[borrowedRune.runeIndex];

                // Pick a random available slot
                int randomIndex = Random.Range(0, m_availableRuneSlots.Count);
                int slotIndex = m_availableRuneSlots[randomIndex];
                m_availableRuneSlots.RemoveAt(randomIndex);

                // Store the mapping
                m_runeIndexToSlotIndex[borrowedRune.runeIndex] = slotIndex;

                // Update the slot with the rune sprite
                Transform slotTransform = runesContainer.GetChild(slotIndex);
                Image slotImage = slotTransform.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.sprite = rune.Sprite;
                    slotImage.enabled = true;

                    // Add Button component if it doesn't exist
                    Button runeButton = slotImage.gameObject.GetComponent<Button>();
                    if (runeButton == null)
                    {
                        runeButton = slotImage.gameObject.AddComponent<Button>();
                    }

                    // Store the rune index for the click handler
                    int capturedRuneIndex = borrowedRune.runeIndex; // Capture for closure

                    // Remove old listeners and add new one
                    runeButton.onClick.RemoveAllListeners();
                    runeButton.onClick.AddListener(() => OnRuneClicked(capturedRuneIndex));

                    // Update visual state based on toggle status
                    UpdateRuneToggleVisual(slotImage.gameObject, m_localPlayerNotebook.IsRuneToggled(borrowedRune.runeIndex));

                    // Create the owner name label
                    GameObject nameLabel = new GameObject("OwnerNameLabel");
                    nameLabel.transform.SetParent(slotTransform, false);

                    TextMeshProUGUI nameText = nameLabel.AddComponent<TextMeshProUGUI>();
                    nameText.text = borrowedRune.ownerName;
                    nameText.fontSize = 30;
                    nameText.color = Color.white;
                    nameText.alignment = TextAlignmentOptions.Center;
                    nameText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    nameText.verticalAlignment = VerticalAlignmentOptions.Middle;

                    // Set the RectTransform to fill the parent
                    RectTransform nameRect = nameLabel.GetComponent<RectTransform>();
                    nameRect.anchorMin = Vector2.zero;
                    nameRect.anchorMax = Vector2.one;
                    nameRect.sizeDelta = Vector2.zero;
                    nameRect.anchoredPosition = Vector2.zero;

                    // Track this borrowed rune slot
                    m_borrowedRuneSlots[slotIndex] = slotImage.gameObject;
                }
            }

            //Debug.Log($"[NotebookUI] Borrowed runes updated incrementally");
        }

        #endregion

        #region Spells Page

        private void RefreshSpellsPage()
        {
            //Debug.Log("[NotebookUI] Refreshing spells page");

            if (m_localPlayerNotebook == null || spellsContainer == null)
                return;

            // Clear existing spell slots
            foreach (Transform child in spellsContainer)
            {
                Destroy(child.gameObject);
            }

            List<SpellSO> equippedSpells = m_localPlayerNotebook.GetEquippedSpells();

            //Debug.Log("Equipped Spells Count: " + equippedSpells.Count);

            // Get lock state from NotebookManager
            bool isLocked = NotebookManager.Instance != null && NotebookManager.Instance.IsSpellLoadoutLocked;

            // Populate equipped spells
            foreach (SpellSO spell in equippedSpells)
            {
                if (spellSlotPrefab == null)
                    continue;

                //Debug.Log("[NotebookUI] Creating spell slot for spell: " + spell.Name);

                GameObject spellSlot = Instantiate(spellSlotPrefab, spellsContainer);

                // Get the spell index
                int spellIndex = GameManager.Instance.SpellDatabase.FindIndex(s => s.SpellInfo == spell);

                // Find the toggle component within the spell slot prefab
                Toggle spellToggle = spellSlot.GetComponent<Toggle>();
                if (spellToggle == null)
                {
                    spellToggle = spellSlot.GetComponentInChildren<Toggle>();
                }

                if (spellToggle != null)
                {
                    // Set initial toggle state
                    bool isToggled = m_localPlayerNotebook.IsSpellToggled(spellIndex);
                    spellToggle.isOn = isToggled;

                    // Apply initial visual state
                    UpdateSpellToggleVisual(spellSlot, isToggled, spellIndex == 0);

                    // Disable toggle for Impero (index 0) or if loadout is locked
                    if (spellIndex == 0)
                    {
                        spellToggle.interactable = false;
                        spellToggle.isOn = true; // Impero is always on
                    }
                    else
                    {
                        spellToggle.interactable = !isLocked; // Disable if locked

                        // Add listener for toggle changes
                        int capturedSpellIndex = spellIndex; // Capture for closure
                        spellToggle.onValueChanged.AddListener(isOn =>
                        {
                            OnSpellToggleChanged(capturedSpellIndex, isOn, spellSlot);
                        });
                    }
                }

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

            //Debug.Log($"[NotebookUI] Spells page refreshed with {equippedSpells.Count} spells (Locked: {isLocked})");
        }

        /// <summary>
        /// Called when a spell toggle is changed in the UI.
        /// </summary>
        private void OnSpellToggleChanged(int spellIndex, bool isOn, GameObject spellSlot)
        {
            if (m_localPlayerNotebook == null)
                return;

            // If trying to toggle on
            if (isOn)
            {
                bool success = m_localPlayerNotebook.ToggleSpell(spellIndex);

                // If toggle failed (limit reached or locked), revert the UI toggle
                if (!success)
                {
                    Toggle toggle = spellSlot.GetComponent<Toggle>();
                    if (toggle == null)
                        toggle = spellSlot.GetComponentInChildren<Toggle>();

                    if (toggle != null)
                    {
                        // Temporarily remove listener to avoid recursive calls
                        toggle.onValueChanged.RemoveAllListeners();
                        toggle.isOn = false;

                        // Re-add listener
                        toggle.onValueChanged.AddListener(on => OnSpellToggleChanged(spellIndex, on, spellSlot));

                        // Update visual to show it's not toggled
                        UpdateSpellToggleVisual(spellSlot, false, false);
                    }

                    //Debug.Log($"[NotebookUI] Failed to toggle spell {spellIndex}");
                    return;
                }

                // Update visual to show it's toggled
                UpdateSpellToggleVisual(spellSlot, true, false);
            }
            else
            {
                // Toggle off
                m_localPlayerNotebook.ToggleSpell(spellIndex);

                // Update visual to show it's not toggled
                UpdateSpellToggleVisual(spellSlot, false, false);
            }

            //Debug.Log($"[NotebookUI] Spell {spellIndex} toggle changed to {isOn}");
        }

        /// <summary>
        /// Updates the visual state of a spell slot to show if it's toggled.
        /// </summary>
        /// <param name="spellSlot">The spell slot GameObject</param>
        /// <param name="isToggled">Whether the spell is toggled on</param>
        /// <param name="isImpero">Whether this is the Impero spell (always on)</param>
        private void UpdateSpellToggleVisual(GameObject spellSlot, bool isToggled, bool isImpero)
        {
            // Find all Image components in the spell slot (including runes)
            Image[] images = spellSlot.GetComponentsInChildren<Image>();

            Color targetColor = isToggled ? spellToggledColor : spellUntoggledColor;

            // If it's Impero, always use toggled color since it's always active
            if (isImpero)
            {
                targetColor = spellToggledColor;
            }

            foreach (Image img in images)
            {
                // Skip the Toggle's background/checkmark images if you want to keep them unchanged
                if (img.gameObject.name == "Background" || img.gameObject.name == "Checkmark")
                    continue;

                img.color = targetColor;
            }
        }

        /// <summary>
        /// Helper method to get spell index from a spell slot transform.
        /// </summary>
        private int GetSpellIndexFromToggle(Transform spellSlot)
        {
            int siblingIndex = spellSlot.GetSiblingIndex();
            List<SpellSO> equippedSpells = m_localPlayerNotebook.GetEquippedSpells();

            if (siblingIndex >= 0 && siblingIndex < equippedSpells.Count)
            {
                SpellSO spell = equippedSpells[siblingIndex];
                return GameManager.Instance.SpellDatabase.FindIndex(s => s.SpellInfo == spell);
            }

            return -1;
        }

        #endregion

        #region Spell Loadout Lock

        /// <summary>
        /// Called when the spell loadout lock state changes.
        /// </summary>
        private void OnSpellLoadoutLockChanged(bool isLocked)
        {
            UpdateSpellTogglesInteractivity(isLocked);
        }

        /// <summary>
        /// Updates the interactivity of spell toggles based on lock state.
        /// </summary>
        private void UpdateSpellTogglesInteractivity(bool isLocked)
        {
            if (spellsContainer == null)
                return;

            foreach (Transform child in spellsContainer)
            {
                Toggle toggle = child.GetComponent<Toggle>();
                if (toggle == null)
                    toggle = child.GetComponentInChildren<Toggle>();

                if (toggle != null)
                {
                    // Get spell index to check if it's Impero
                    int spellIndex = GetSpellIndexFromToggle(child);

                    // Impero should always be non-interactable (it's always on)
                    if (spellIndex == 0)
                    {
                        toggle.interactable = false;
                    }
                    else
                    {
                        // Other spells are interactable only when unlocked
                        toggle.interactable = !isLocked;
                    }
                }

                // Add visual overlay when locked
                if (isLocked)
                {
                    AddLockedOverlay(child.gameObject);
                }
                else
                {
                    RemoveLockedOverlay(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Adds a visual overlay to indicate the spell is locked.
        /// </summary>
        private void AddLockedOverlay(GameObject spellSlot)
        {
            // Check if overlay already exists
            Transform existingOverlay = spellSlot.transform.Find("LockedOverlay");
            if (existingOverlay != null)
                return;

            // Create overlay
            GameObject overlay = new GameObject("LockedOverlay");
            overlay.transform.SetParent(spellSlot.transform, false);

            Image overlayImage = overlay.AddComponent<Image>();
            overlayImage.color = spellLockedOverlayColor;

            // Set RectTransform to fill parent
            RectTransform rectTransform = overlay.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            // Make sure overlay is on top
            overlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Removes the locked overlay from a spell slot.
        /// </summary>
        private void RemoveLockedOverlay(GameObject spellSlot)
        {
            Transform existingOverlay = spellSlot.transform.Find("LockedOverlay");
            if (existingOverlay != null)
            {
                Destroy(existingOverlay.gameObject);
            }
        }

        #endregion
    }
}
