using System;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Maps a string ID to an actual Renderer in the player prefab.
    /// Configured in the PlayerCustomizationManager inspector.
    /// </summary>
    [Serializable]
    public class RendererMapping
    {
        [Tooltip("Unique ID for this renderer (e.g., Hair, UpperBody, Eyes).")]
        public RendererID id;

        [Tooltip("The actual renderer component in the player prefab.")]
        public Renderer renderer;

        public bool IsValid()
        {
            return renderer != null;
        }
    }
}
