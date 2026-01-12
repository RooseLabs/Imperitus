using System;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    [Serializable]
    public class DefaultRendererData
    {
        public RendererID rendererId;

        [Tooltip("Default mesh for this renderer.")]
        public Mesh mesh;

        [Tooltip("Default materials for this renderer, in order.")]
        public Material[] materials;
    }
}
