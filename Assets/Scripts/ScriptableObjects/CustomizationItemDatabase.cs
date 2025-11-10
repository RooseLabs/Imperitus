using System.Collections.Generic;
using RooseLabs.Player.Customization;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "CustomizationItemDatabase", menuName = "Imperitus/Customization Item Database")]
    public class CustomizationItemDatabase : ObjectDatabase<CustomizationItem>
    {
        public IEnumerable<CustomizationItem> GetItemsByCategory(CustomizationCategory category)
        {
            foreach (var item in this)
            {
                if (item.category == category)
                    yield return item;
            }
        }
    }
}
