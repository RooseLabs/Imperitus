using System.Collections.Generic;
using RooseLabs.Player.Customization;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    /// <summary>
    /// Database that holds references to all available customization items.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomizationItemDatabase", menuName = "Imperitus/Customization Item Database")]
    public class CustomizationItemDatabase : ScriptableObject
    {
        [Tooltip("List of all available customization items in the game.")]
        public List<CustomizationItem> allItems = new List<CustomizationItem>();

        /// <summary>
        /// Gets all items in a specific category.
        /// </summary>
        public List<CustomizationItem> GetItemsByCategory(CustomizationCategory category)
        {
            List<CustomizationItem> filteredItems = new List<CustomizationItem>();

            foreach (var item in allItems)
            {
                if (item != null && item.category == category)
                {
                    filteredItems.Add(item);
                }
            }

            return filteredItems;
        }

        /// <summary>
        /// Gets all items in the database.
        /// </summary>
        public List<CustomizationItem> GetAllItems()
        {
            List<CustomizationItem> validItems = new List<CustomizationItem>();

            foreach (var item in allItems)
            {
                if (item != null)
                {
                    validItems.Add(item);
                }
            }

            return validItems;
        }

        /// <summary>
        /// Gets the index of an item in the database.
        /// Returns -1 if not found.
        /// </summary>
        public int GetItemIndex(CustomizationItem item)
        {
            if (item == null) return -1;
            return allItems.IndexOf(item);
        }

        /// <summary>
        /// Gets an item by its index in the database.
        /// Returns null if index is invalid.
        /// </summary>
        public CustomizationItem GetItemByIndex(int index)
        {
            if (index < 0 || index >= allItems.Count) return null;
            return allItems[index];
        }
    }
}
