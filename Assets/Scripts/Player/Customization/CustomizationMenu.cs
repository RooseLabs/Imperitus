using System;
using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using RooseLabs.UI;
using RooseLabs.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Toggle = RooseLabs.UI.Elements.Toggle;
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
            public Sprite icon;
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
        private readonly List<Toggle> m_categoryTabs = new();
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

        private void CreateCategoryTabs()
        {
            // Create category tabs
            foreach (var uiCategory in categories)
            {
                GameObject tabObj = Instantiate(categoryTabPrefab, tabContainer);
                if (tabObj.TryGetComponent(out Toggle tabToggle) && tabObj.TryGetComponent(out Image backgroundImage))
                {
                    backgroundImage.color = uiCategory.color;
                    if (tabObj.transform.childCount > 0 && tabObj.transform.GetChild(0).TryGetComponent(out Image icon))
                    {
                        icon.sprite = uiCategory.icon;
                    }
                    tabToggle.colors = new ColorBlock
                    {
                        normalColor = new Color(uiCategory.color.r * 0.7f, uiCategory.color.g * 0.7f, uiCategory.color.b * 0.7f, 1f),
                        highlightedColor = Color.black,
                        pressedColor = Color.black,
                        selectedColor = Color.black,
                        disabledColor = Color.black,
                        colorMultiplier = 1f,
                        fadeDuration = 0.1f
                    };
                    tabToggle.onValueChanged.AddListener((isToggled) =>
                    {
                        if (isToggled)
                        {
                            OpenCategory(uiCategory, tabToggle);
                        }
                    });
                    m_categoryTabs.Add(tabToggle);
                }
            }
        }

        public void Open()
        {
            FindCustomizationManager();
            if (m_isFirstTimeOpening)
            {
                CreateCategoryTabs();
                // Toggle on the first tab
                if (m_categoryTabs.Count > 0)
                {
                    m_categoryTabs[0].IsOn = true;
                }
                m_isFirstTimeOpening = false;
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OpenCategory(UICustomizationCategory uiCategory, Toggle activeToggle = null)
        {
            // Untoggle all other tabs
            if (activeToggle)
            {
                foreach (var otherTab in m_categoryTabs)
                {
                    if (otherTab != activeToggle && otherTab.IsOn)
                    {
                        otherTab.SetIsOn(false, false);
                    }
                }
            }

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
