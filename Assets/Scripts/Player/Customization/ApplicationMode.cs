using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// Defines how a customization item should be applied to the player.
    /// </summary>
    public enum ApplicationMode
    {
        /// <summary>
        /// Only swaps the material on the existing renderer. Mesh remains unchanged.
        /// </summary>
        SwapMaterialOnly,

        /// <summary>
        /// Replaces both mesh and material on an existing renderer.
        /// </summary>
        SwapMeshAndMaterial,

        /// <summary>
        /// Instantiates a new GameObject with mesh and material at the attachment point.
        /// </summary>
        InstantiateNew
    }
}
