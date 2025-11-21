using System;
using System.Collections.Generic;
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
        [Tooltip("Unique ID for this renderer group (e.g., SkinColor, Hair).")]
        public RendererID id;

        [Tooltip("Renderer and material index pairs.")]
        public List<RendererMaterialPair> rendererPairs = new List<RendererMaterialPair>();

        public bool IsValid()
        {
            return rendererPairs != null && rendererPairs.Count > 0 &&
                   rendererPairs.TrueForAll(p => p.renderer != null);
        }
    }

    [Serializable]
    public class RendererMaterialPair
    {
        [Tooltip("The renderer to affect.")]
        public Renderer renderer;

        [Tooltip("Which material index to replace on this renderer (-1 = all materials, 0+ = specific index).")]
        public int materialIndex = -1;
    }
}
