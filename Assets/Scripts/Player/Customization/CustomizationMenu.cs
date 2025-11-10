using System;
using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Manages the customization menu UI, displaying and filtering customization items.
    /// </summary>
    public class CustomizationMenu : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Database containing all available customization items.")]
        [SerializeField] private CustomizationItemDatabase itemDatabase;

        [Header("UI Elements")]
        [Tooltip("Container where item buttons will be spawned (should have GridLayoutGroup).")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("Title text that displays the current category name.")]
        [SerializeField] private TMP_Text categoryTitleText;

        [Tooltip("Text displayed when no items are found for the selected category.")]
        [SerializeField] private TMP_Text noItemsFoundText;

        [Header("Button Appearance")]
        [Tooltip("Color for the equipped item border.")]
        [SerializeField] private Color equippedBorderColor = Color.black;

        [Tooltip("Width of the equipped item border.")]
        [SerializeField] private float equippedBorderWidth = 5f;

        // Currently selected category filter
        private CustomizationCategory? currentCategory = null;

        // Track spawned buttons: Key = CustomizationItem, Value = Button GameObject
        private Dictionary<CustomizationItem, GameObject> spawnedButtons = new Dictionary<CustomizationItem, GameObject>();

        // Runtime reference to the local player's customization manager
        private PlayerCustomizationManager customizationManager;

        /// <summary>
        /// Finds the local player's PlayerCustomizationManager in the scene.
        /// Called when the menu is opened.
        /// </summary>
        private void FindCustomizationManager()
        {
            if (customizationManager == null)
            {
                customizationManager = PlayerConnection.LocalPlayer.Character.GetComponentInChildren<PlayerCustomizationManager>();

                if (customizationManager == null)
                {
                    Debug.LogError("[CustomizationMenu] No local PlayerCustomizationManager found in scene!");
                }
            }
        }

        /// <summary>
        /// Called when the menu is opened. Finds the local player and shows all items.
        /// </summary>
        public void OnMenuOpened()
        {
            FindCustomizationManager();
            ShowAllItems();
        }

        /// <summary>
        /// Shows all items from every category.
        /// </summary>
        public void ShowAllItems()
        {
            currentCategory = null;
            UpdateCategoryTitle("All");
            PopulateItems(itemDatabase);
        }

        /// <summary>
        /// Filters items by a specific category.
        /// Pass empty string or null to show all items.
        /// </summary>
        public void FilterByCategory(string categoryName)
        {
            // If category name is empty or null, show all items
            if (string.IsNullOrEmpty(categoryName))
            {
                ShowAllItems();
                return;
            }

            // Try to parse the category name
            if (Enum.TryParse(categoryName, out CustomizationCategory category))
            {
                currentCategory = category;
                UpdateCategoryTitle(categoryName);
                PopulateItems(itemDatabase.GetItemsByCategory(category));
            }
            else
            {
                Debug.LogError($"Invalid category name: {categoryName}");
            }
        }

        /// <summary>
        /// Populates the container with item buttons.
        /// </summary>
        private void PopulateItems(IEnumerable<CustomizationItem> items)
        {
            // Clear existing buttons
            ClearContainer();

            // Check if no items were found
            if (itemDatabase.Count == 0)
            {
                noItemsFoundText.gameObject.SetActive(true);
                return;
            }

            noItemsFoundText.gameObject.SetActive(false);

            // Create a button for each item
            foreach (var item in items)
            {
                CreateItemButton(item);
            }
        }

        /// <summary>
        /// Creates a button for a customization item.
        /// </summary>
        private void CreateItemButton(CustomizationItem item)
        {
            // Create button GameObject
            GameObject buttonObj = new GameObject($"ItemButton_{item.itemName}");
            buttonObj.transform.SetParent(itemContainer, false);

            // Add Image component (this will display the icon)
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.sprite = item.icon;
            buttonImage.preserveAspect = true;

            // Add Button component
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => OnItemButtonClicked(item));

            // Track the button
            spawnedButtons[item] = buttonObj;
        }

        /// <summary>
        /// Handles when an item button is clicked.
        /// </summary>
        private void OnItemButtonClicked(CustomizationItem item)
        {
            if (customizationManager == null)
            {
                Debug.LogError("CustomizationManager is not assigned!");
                return;
            }

            // Check if item is already equipped
            bool isEquipped = customizationManager.IsItemEquipped(item);

            if (isEquipped)
            {
                // Unequip the item
                customizationManager.RemoveItem(item.category, item.allowStacking ? item.subCategory : null);
            }
            else
            {
                // Equip the item (will automatically replace any existing item in that slot)
                customizationManager.EquipItem(item);
            }
        }

        /// <summary>
        /// Updates the border state of a button based on equipped status.
        /// </summary>
        private void UpdateButtonBorderState(CustomizationItem item, GameObject buttonObj)
        {
            if (customizationManager == null || buttonObj == null) return;

            bool isEquipped = customizationManager.IsItemEquipped(item);

            // Find the border child object
            Transform borderTransform = buttonObj.transform.Find("EquippedBorder");
            if (borderTransform != null)
            {
                borderTransform.gameObject.SetActive(isEquipped);
            }
        }

        /// <summary>
        /// Updates the category title text.
        /// </summary>
        private void UpdateCategoryTitle(string categoryName)
        {
            if (categoryTitleText != null)
            {
                categoryTitleText.text = categoryName;
            }
        }

        /// <summary>
        /// Clears all spawned buttons from the container.
        /// </summary>
        private void ClearContainer()
        {
            foreach (var buttonObj in spawnedButtons.Values)
            {
                if (buttonObj != null)
                {
                    Destroy(buttonObj);
                }
            }

            spawnedButtons.Clear();
        }

        /// <summary>
        /// Refreshes the current view (useful if items are equipped externally).
        /// </summary>
        public void RefreshView()
        {
            if (currentCategory.HasValue)
            {
                FilterByCategory(currentCategory.Value.ToString());
            }
            else
            {
                ShowAllItems();
            }
        }
    }
}
