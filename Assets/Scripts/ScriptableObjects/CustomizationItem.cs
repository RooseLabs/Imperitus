using RooseLabs.Player.Customization;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "NewCustomizationItem", menuName = "Imperitus/Customization Item")]
    public class CustomizationItem : ScriptableObject
    {
        [Header("Category Settings")]
        [Tooltip("The category this item belongs to.")]
        public CustomizationCategory category;

        [Header("Application Settings")]
        [Tooltip("Defines how this item is applied to the player.")]
        public ApplicationMode applicationMode;

        [Header("Visual Data")]
        [Tooltip("List of slots containing mesh and material data. Most items have 1 slot, outfits may have multiple.")]
        public CustomizationSlot[] slots;

        [Header("Item Info")]
        [Tooltip("Display name shown to the player.")]
        public string itemName;

        [Tooltip("Icon displayed in UI.")]
        public Sprite icon;

        public string GetEquipmentKey()
        {
            return category.ToString();
        }

        public bool IsValid()
        {
            if (slots == null || slots.Length == 0)
            {
                Debug.LogWarning($"CustomizationItem '{itemName}' has no slots defined.");
                return false;
            }
            return true;
        }
    }
}
