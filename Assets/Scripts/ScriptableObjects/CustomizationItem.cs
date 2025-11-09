using System.Collections.Generic;
using RooseLabs.Player.Customization;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    /// <summary>
    /// ScriptableObject that defines a single customization item.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCustomizationItem", menuName = "Imperitus/Customization Item")]
    public class CustomizationItem : ScriptableObject
    {
        [Header("Category Settings")]
        [Tooltip("The main category this item belongs to.")]
        public CustomizationCategory category;

        [Tooltip("Subcategory for organization within stackable categories.")]
        public string subCategory;

        [Tooltip("If true, multiple items of this category can be worn simultaneously (differentiated by subcategory).")]
        public bool allowStacking = false;

        [Header("Application Settings")]
        [Tooltip("Defines how this item is applied to the player.")]
        public ApplicationMode applicationMode;

        [Header("Visual Data")]
        [Tooltip("List of slots containing mesh and material data. Most items have 1 slot, outfits may have multiple.")]
        public List<CustomizationSlot> slots = new List<CustomizationSlot>();

        [Header("Item Info")]
        [Tooltip("Display name shown to the player.")]
        public string itemName;

        [Tooltip("Description of the item.")]
        [TextArea(3, 5)]
        public string description;

        [Tooltip("Icon displayed in UI.")]
        public Sprite icon;

        /// <summary>
        /// Generates a unique key for tracking equipped items.
        /// Non-stackable: just category name
        /// Stackable: category + subcategory
        /// </summary>
        public string GetEquipmentKey()
        {
            if (allowStacking && !string.IsNullOrEmpty(subCategory))
            {
                return $"{category}_{subCategory}";
            }
            return category.ToString();
        }

        /// <summary>
        /// Validates that this item has all required data configured.
        /// </summary>
        public bool IsValid()
        {
            if (slots == null || slots.Count == 0)
            {
                Debug.LogWarning($"CustomizationItem '{itemName}' has no slots defined.");
                return false;
            }

            foreach (var slot in slots)
            {
                if (!slot.IsValid())
                {
                    Debug.LogWarning($"CustomizationItem '{itemName}' has an invalid slot.");
                    return false;
                }
            }

            if (allowStacking && string.IsNullOrEmpty(subCategory))
            {
                Debug.LogWarning($"CustomizationItem '{itemName}' allows stacking but has no subcategory defined.");
                return false;
            }

            return true;
        }
    }
}
