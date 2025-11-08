using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// Stores default mesh and material data for a category.
    /// Used to restore original appearance when items are removed.
    /// </summary>
    [System.Serializable]
    public class DefaultCustomizationData
    {
        [Tooltip("The renderer that holds the default appearance.")]
        public Renderer renderer;

        [Tooltip("Default mesh (can be null for material-only categories).")]
        public Mesh defaultMesh;

        [Tooltip("Default material (can be null if nothing should be shown by default).")]
        public Material defaultMaterial;

        /// <summary>
        /// Validates that this default data has minimum required info.
        /// </summary>
        public bool IsValid()
        {
            return renderer != null;
        }
    }
}
