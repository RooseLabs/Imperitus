using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    [Serializable]
    public class DefaultCustomizationData
    {
        [Tooltip("Default configurations for each renderer/material pair.")]
        public List<DefaultRendererData> defaultRendererData = new List<DefaultRendererData>();

        public bool IsValid()
        {
            return defaultRendererData != null && defaultRendererData.Count > 0 &&
                   defaultRendererData.TrueForAll(d => d.renderer != null);
        }
    }

    [Serializable]
    public class DefaultRendererData
    {
        [Tooltip("The renderer to restore.")]
        public Renderer renderer;

        [Tooltip("Which material index to restore (-1 = all materials, 0+ = specific index).")]
        public int materialIndex = -1;

        [Tooltip("Default mesh for this renderer (can be null for material-only).")]
        public Mesh defaultMesh;

        [Tooltip("Default material for this renderer (can be null if nothing shown by default).")]
        public Material defaultMaterial;
    }
}
