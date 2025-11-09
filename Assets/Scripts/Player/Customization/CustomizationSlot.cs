using System;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Represents a single visual component that can be applied to a renderer.
    /// Multiple slots can be combined to create complete customization items.
    /// </summary>
    [Serializable]
    public class CustomizationSlot
    {
        [Tooltip("The mesh to apply. Can be null for material-only changes.")]
        public Mesh mesh;

        [Tooltip("The material to apply. Should never be null.")]
        public Material material;

        [Tooltip("The ID of the target renderer. This ID must match a Renderer Mapping in the PlayerCustomizationManager.")]
        public string targetRendererId;

        /// <summary>
        /// Validates that this slot has the minimum required data.
        /// </summary>
        public bool IsValid()
        {
            return material != null && !string.IsNullOrEmpty(targetRendererId);
        }
    }
}
