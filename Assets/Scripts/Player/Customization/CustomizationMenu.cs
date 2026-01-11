using System;
using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using RooseLabs.UI;
using RooseLabs.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Player.Customization
{
    public class CustomizationMenu : MonoBehaviour, IWindow
    {
        private Logger Logger => Logger.GetLogger("CustomizationMenu");

        [Serializable]
        private struct UICustomizationCategory
        {
            public CustomizationCategory category;
            public Color color;
        }

        [Header("References")]
        [Tooltip("Database containing all available customization items.")]
        [SerializeField] private CustomizationItemDatabase itemDatabase;

        [Tooltip("Prefab for category tab buttons.")]
        [SerializeField] private GameObject categoryTabPrefab;

        [Header("UI Elements")]
        [Tooltip("Container for category tabs.")]
        [SerializeField] private Transform tabContainer;

        [Tooltip("Title text that displays the current category name.")]
        [SerializeField] private TMP_Text categoryTitleText;

        [Tooltip("Background graphics for the category title.")]
        [SerializeField] private Graphic[] categoryTitleBackgrounds;

        [Tooltip("Container where item buttons will be spawned.")]
        [SerializeField] private Transform itemContainer;

        [Tooltip("Text displayed when no items are found for the selected category.")]
        [SerializeField] private TMP_Text noItemsFoundText;

        [Header("Categories")]
        [Tooltip("List of UI customization categories with their associated colors.")]
        [SerializeField] private UICustomizationCategory[] categories;

        private readonly List<GameObject> m_itemButtons = new();
        private bool m_isFirstTimeOpening = true;
        private PlayerCustomizationManager m_customizationManager;

        private void FindCustomizationManager()
        {
            if (m_customizationManager) return;
            if (!PlayerCharacter.LocalCharacter.TryGetComponentInChildren(out m_customizationManager))
            {
                Logger.Error("No local PlayerCustomizationManager found in scene!");
            }
        }

        private void Start()
        {
            // Create category tabs
            foreach (var uiCategory in categories)
            {
                GameObject tabObj = Instantiate(categoryTabPrefab, tabContainer);
                TMP_Text tabText = tabObj.GetComponentInChildren<TMP_Text>();
                if (tabText)
                {
                    tabText.text = uiCategory.category.ToString();
                }
                Button tabButton = tabObj.GetComponent<Button>();
                if (tabButton)
                {
                    tabButton.targetGraphic.color = uiCategory.color;
                    tabButton.onClick.AddListener(() => OpenCategory(uiCategory));
                }
            }
        }

        public void Open()
        {
            FindCustomizationManager();
            if (m_isFirstTimeOpening)
            {
                OpenCategory(categories[0]);
                m_isFirstTimeOpening = false;
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OpenCategory(UICustomizationCategory uiCategory)
        {
            categoryTitleText.text = uiCategory.category.ToString();
            foreach (var graphic in categoryTitleBackgrounds)
            {
                graphic.color = uiCategory.color;
            }
            PopulateItems(itemDatabase.GetItemsByCategory(uiCategory.category));
        }

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

        private void CreateItemButton(CustomizationItem item)
        {
            // Create button GameObject
            GameObject buttonObj = new GameObject($"ItemButton_{item.itemName}");
            buttonObj.transform.SetParent(itemContainer, false);

            // Add Image component (this will display the icon)
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.sprite = item.icon;
            buttonImage.useSpriteMesh = true;
            buttonImage.preserveAspect = true;

            // Add Button component
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => OnItemButtonClicked(item));

            m_itemButtons.Add(buttonObj);
        }

        private void OnItemButtonClicked(CustomizationItem item)
        {
            if (!m_customizationManager)
            {
                Logger.Error("CustomizationManager not found!");
                return;
            }

            // Check if item is already equipped
            bool isEquipped = m_customizationManager.IsItemEquipped(item);

            if (isEquipped)
            {
                // Unequip the item
                m_customizationManager.RemoveItem(item.category);
                //customizationManager.RemoveItem(item.category, item.allowStacking ? item.subCategory : null);
            }
            else
            {
                // Equip the item (will automatically replace any existing item in that slot)
                m_customizationManager.EquipItem(item);
            }
        }

        private void ClearContainer()
        {
            foreach (var buttonObj in m_itemButtons)
            {
                if (buttonObj) Destroy(buttonObj);
            }
            m_itemButtons.Clear();
        }
    }
}
