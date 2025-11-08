using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// Database that holds references to all available customization items.
    /// </summary>
    [CreateAssetMenu(fileName = "Customization Item Database", menuName = "Imperitus/CustomizationItem Database")]
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
    }
}
