using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// Maps a string ID to an actual Renderer in the player prefab.
    /// Configured in the PlayerCustomizationManager inspector.
    /// </summary>
    [System.Serializable]
    public class RendererMapping
    {
        [Tooltip("Unique ID for this renderer (e.g., 'Hair', 'UpperBody', 'Eyes').")]
        public string id;

        [Tooltip("The actual renderer component in the player prefab.")]
        public Renderer renderer;

        /// <summary>
        /// Validates that this mapping has required data.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(id) && renderer != null;
        }
    }
}
