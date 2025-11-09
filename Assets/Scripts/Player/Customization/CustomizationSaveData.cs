using System;
using System.Collections.Generic;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Serializable data structure for saving customization.
    /// </summary>
    [Serializable]
    public class CustomizationSaveData
    {
        public List<EquippedItemData> equippedItems = new List<EquippedItemData>();
    }

    [Serializable]
    public class EquippedItemData
    {
        public int itemIndex;           // Index in the database
        public string equipmentKey;     // Category or Category_Subcategory
    }
}