using System;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    [Serializable]
    public class CustomizationSlot
    {
        [Tooltip("The mesh to apply to the renderer.")]
        public Mesh mesh;

        [Tooltip("The materials to apply to the renderer, in order.")]
        public Material[] materials;

        [Tooltip("The ID of the target renderer group.")]
        public RendererID targetRendererId;
    }
}
