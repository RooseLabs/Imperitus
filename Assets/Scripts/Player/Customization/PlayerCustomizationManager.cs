using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Manages character customization for a player prefab.
    /// </summary>
    public class PlayerCustomizationManager : MonoBehaviour
    {
        [Header("Renderer Mappings")]
        [Tooltip("Map string IDs to actual renderers in your prefab. These IDs are used in CustomizationItem slots.")]
        [SerializeField] private List<RendererMapping> rendererMappings = new List<RendererMapping>();

        [Header("Default Configurations")]
        [Tooltip("Define default meshes and materials for each category. Used when removing items.")]
        [SerializeField] private List<DefaultCustomizationData> defaultConfigurations = new List<DefaultCustomizationData>();

        [Header("Runtime Data")]
        [Tooltip("Debug view of currently equipped items.")]
        [SerializeField] private List<string> equippedItemNames = new List<string>();

        // Tracks currently equipped items: Key = equipment key (category or category_subcategory)
        private Dictionary<string, CustomizationItem> equippedItems = new Dictionary<string, CustomizationItem>();

        // Tracks instantiated GameObjects for InstantiateNew mode: Key = equipment key
        private Dictionary<string, List<GameObject>> instantiatedObjects = new Dictionary<string, List<GameObject>>();

        // Cached renderer lookup: Key = renderer ID
        private Dictionary<string, Renderer> rendererLookup = new Dictionary<string, Renderer>();

        private void Awake()
        {
            BuildRendererLookup();
            ValidateDefaultConfigurations();
        }

        /// <summary>
        /// Builds the renderer lookup dictionary from the mappings.
        /// </summary>
        private void BuildRendererLookup()
        {
            rendererLookup.Clear();

            foreach (var mapping in rendererMappings)
            {
                if (!mapping.IsValid())
                {
                    Debug.LogWarning($"Invalid renderer mapping detected on '{gameObject.name}'.");
                    continue;
                }

                if (rendererLookup.ContainsKey(mapping.id))
                {
                    Debug.LogWarning($"Duplicate renderer ID '{mapping.id}' found on '{gameObject.name}'. Only the first will be used.");
                    continue;
                }

                rendererLookup[mapping.id] = mapping.renderer;
            }
        }

        /// <summary>
        /// Gets a renderer by its ID.
        /// </summary>
        private Renderer GetRendererById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("Renderer ID is null or empty.");
                return null;
            }

            if (!rendererLookup.ContainsKey(id))
            {
                Debug.LogError($"No renderer found with ID '{id}'. Make sure it's configured in PlayerCustomizationManager.");
                return null;
            }

            return rendererLookup[id];
        }

        /// <summary>
        /// Equips a customization item on the player.
        /// </summary>
        public void EquipItem(CustomizationItem item)
        {
            if (item == null)
            {
                Debug.LogError("Cannot equip null CustomizationItem.");
                return;
            }

            if (!item.IsValid())
            {
                Debug.LogError($"Cannot equip invalid CustomizationItem '{item.itemName}'.");
                return;
            }

            string key = item.GetEquipmentKey();

            // Remove existing item in this slot
            if (equippedItems.ContainsKey(key))
            {
                RemoveItem(key);
            }

            // Apply the new item based on its application mode
            switch (item.applicationMode)
            {
                case ApplicationMode.SwapMaterialOnly:
                    ApplySwapMaterialOnly(item);
                    break;
                case ApplicationMode.SwapMeshAndMaterial:
                    ApplySwapMeshAndMaterial(item);
                    break;
                case ApplicationMode.InstantiateNew:
                    ApplyInstantiateNew(item, key);
                    break;
            }

            // Track the equipped item
            equippedItems[key] = item;
            UpdateDebugList();

            Debug.Log($"Equipped: {item.itemName} ({key})");
        }

        /// <summary>
        /// Removes an item by category and optional subcategory.
        /// </summary>
        public void RemoveItem(CustomizationCategory category, string subCategory = null)
        {
            string key = GetEquipmentKey(category, subCategory);
            RemoveItem(key);
        }

        /// <summary>
        /// Removes an item by its equipment key.
        /// </summary>
        private void RemoveItem(string key)
        {
            if (!equippedItems.ContainsKey(key))
            {
                Debug.LogWarning($"No item equipped in slot '{key}' to remove.");
                return;
            }

            CustomizationItem item = equippedItems[key];

            // Handle removal based on application mode
            switch (item.applicationMode)
            {
                case ApplicationMode.SwapMaterialOnly:
                case ApplicationMode.SwapMeshAndMaterial:
                    RestoreDefaults(item);
                    break;
                case ApplicationMode.InstantiateNew:
                    DestroyInstantiatedObjects(key);
                    break;
            }

            equippedItems.Remove(key);
            UpdateDebugList();

            Debug.Log($"Removed: {item.itemName} ({key})");
        }

        /// <summary>
        /// Removes all equipped items and restores defaults.
        /// </summary>
        public void RemoveAllItems()
        {
            // Copy keys to avoid modification during iteration
            List<string> keys = new List<string>(equippedItems.Keys);

            foreach (string key in keys)
            {
                RemoveItem(key);
            }
        }

        /// <summary>
        /// Checks if an item is currently equipped.
        /// </summary>
        public bool IsItemEquipped(CustomizationItem item)
        {
            if (item == null) return false;
            string key = item.GetEquipmentKey();
            return equippedItems.ContainsKey(key) && equippedItems[key] == item;
        }

        /// <summary>
        /// Gets the currently equipped item for a category/subcategory.
        /// </summary>
        public CustomizationItem GetEquippedItem(CustomizationCategory category, string subCategory = null)
        {
            string key = GetEquipmentKey(category, subCategory);
            return equippedItems.ContainsKey(key) ? equippedItems[key] : null;
        }

        #region Application Methods

        private void ApplySwapMaterialOnly(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);

                if (renderer != null && slot.material != null)
                {
                    // Instance the material (creates a runtime copy)
                    renderer.material = slot.material;
                }
            }
        }

        private void ApplySwapMeshAndMaterial(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);

                if (renderer == null) continue;

                // Handle SkinnedMeshRenderer
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    if (slot.mesh != null)
                    {
                        skinnedRenderer.sharedMesh = slot.mesh;
                    }

                    if (slot.material != null)
                    {
                        skinnedRenderer.material = slot.material;
                    }
                }
                // Handle MeshRenderer
                else if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();

                    if (meshFilter != null && slot.mesh != null)
                    {
                        meshFilter.mesh = slot.mesh;
                    }

                    if (slot.material != null)
                    {
                        meshRenderer.material = slot.material;
                    }
                }
            }
        }

        private void ApplyInstantiateNew(CustomizationItem item, string key)
        {
            List<GameObject> spawnedObjects = new List<GameObject>();

            foreach (var slot in item.slots)
            {
                Renderer attachmentPoint = GetRendererById(slot.targetRendererId);

                if (attachmentPoint == null) continue;

                // Create new GameObject at the attachment point
                GameObject newObj = new GameObject($"{item.itemName}_Instance");
                newObj.transform.SetParent(attachmentPoint.transform, false);
                newObj.transform.localPosition = Vector3.zero;
                newObj.transform.localRotation = Quaternion.identity;
                newObj.transform.localScale = Vector3.one;

                // Add appropriate renderer and assign mesh/material
                if (slot.mesh != null)
                {
                    // Check if we need a SkinnedMeshRenderer or regular MeshRenderer
                    // For simplicity, we'll use MeshRenderer for InstantiateNew mode
                    // If we need SkinnedMeshRenderer for accessories, this logic can be expanded...
                    MeshFilter meshFilter = newObj.AddComponent<MeshFilter>();
                    MeshRenderer meshRenderer = newObj.AddComponent<MeshRenderer>();

                    meshFilter.mesh = slot.mesh;
                    meshRenderer.material = slot.material;
                }

                spawnedObjects.Add(newObj);
            }

            // Track spawned objects for later cleanup
            instantiatedObjects[key] = spawnedObjects;
        }

        #endregion

        #region Restoration Methods

        private void RestoreDefaults(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);

                if (renderer == null) continue;

                // Find matching default configuration
                DefaultCustomizationData defaultData = defaultConfigurations.Find(d => d.renderer == renderer);

                if (defaultData == null || !defaultData.IsValid())
                {
                    Debug.LogWarning($"No default configuration found for renderer '{renderer.name}'.");
                    continue;
                }

                // Handle SkinnedMeshRenderer
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    if (defaultData.defaultMesh != null)
                    {
                        skinnedRenderer.sharedMesh = defaultData.defaultMesh;
                    }

                    if (defaultData.defaultMaterial != null)
                    {
                        skinnedRenderer.material = defaultData.defaultMaterial;
                    }
                }
                // Handle MeshRenderer
                else if (renderer is MeshRenderer meshRenderer)
                {
                    MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();

                    if (meshFilter != null && defaultData.defaultMesh != null)
                    {
                        meshFilter.mesh = defaultData.defaultMesh;
                    }

                    if (defaultData.defaultMaterial != null)
                    {
                        meshRenderer.material = defaultData.defaultMaterial;
                    }
                }
            }
        }

        private void DestroyInstantiatedObjects(string key)
        {
            if (!instantiatedObjects.ContainsKey(key)) return;

            foreach (GameObject obj in instantiatedObjects[key])
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }

            instantiatedObjects.Remove(key);
        }

        #endregion

        #region Helper Methods

        private string GetEquipmentKey(CustomizationCategory category, string subCategory)
        {
            if (!string.IsNullOrEmpty(subCategory))
            {
                return $"{category}_{subCategory}";
            }
            return category.ToString();
        }

        private void ValidateDefaultConfigurations()
        {
            foreach (var config in defaultConfigurations)
            {
                if (!config.IsValid())
                {
                    Debug.LogWarning($"Invalid default configuration detected. Check PlayerCustomizationManager on '{gameObject.name}'.");
                }
            }
        }

        private void UpdateDebugList()
        {
            equippedItemNames.Clear();
            foreach (var kvp in equippedItems)
            {
                equippedItemNames.Add($"{kvp.Key}: {kvp.Value.itemName}");
            }
        }

        #endregion
    }

}
