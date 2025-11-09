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

        /// <summary>
        /// Converts the save data to an array of indices for network transmission.
        /// </summary>
        public int[] ToIndexArray()
        {
            int[] indices = new int[equippedItems.Count];
            for (int i = 0; i < equippedItems.Count; i++)
            {
                indices[i] = equippedItems[i].itemIndex;
            }
            return indices;
        }

        /// <summary>
        /// Creates save data from an array of indices.
        /// </summary>
        public static CustomizationSaveData FromIndexArray(int[] indices)
        {
            CustomizationSaveData data = new CustomizationSaveData();
            foreach (int index in indices)
            {
                data.equippedItems.Add(new EquippedItemData
                {
                    itemIndex = index,
                    equipmentKey = ""
                });
            }
            return data;
        }
    }

    [Serializable]
    public class EquippedItemData
    {
        public int itemIndex;           // Index in the database
        public string equipmentKey;     // Category or Category_Subcategory
    }
}