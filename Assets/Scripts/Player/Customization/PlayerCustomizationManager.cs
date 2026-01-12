using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Player.Customization
{
    public class PlayerCustomizationManager : NetworkBehaviour
    {
        [Header("Renderer Mappings")]
        [Tooltip("Map RendererIDs to actual renderers in your prefab. These IDs are used in CustomizationItem slots.")]
        [SerializeField] private List<RendererMapping> rendererMappings = new();

        [Header("Default Configurations")]
        [Tooltip("Define default meshes and materials for each renderer. Used when removing items.")]
        [SerializeField] private List<DefaultRendererData> defaultConfigurations = new();

        [Header("Save/Load")]
        [Tooltip("Reference to the item database for save/load functionality.")]
        [SerializeField] private CustomizationItemDatabase itemDatabase;

        [Header("Runtime Data")]
        [Tooltip("Debug view of currently equipped items.")]
        [SerializeField] private List<string> equippedItemNames = new();

        [Header("Networking")]
        [Tooltip("If true, disable auto-save for network sync (server will handle saves).")]
        [SerializeField] private bool disableAutoSaveForNetworking = false;

        private readonly SyncList<int> m_syncedCustomizationIndices = new();

        private readonly Dictionary<string, CustomizationItem> m_equippedItems = new();

        private readonly Dictionary<RendererID, Renderer> m_rendererLookup = new();

        private const string SAVE_KEY = "PlayerCustomization";

        private void Awake()
        {
            BuildRendererLookup();
        }

        public override void OnStartNetwork()
        {
            // Subscribe to SyncList changes
            m_syncedCustomizationIndices.OnChange += OnCustomizationSynced;

            if (Owner.IsLocalClient)
            {
                // This is the local player - load their saved customization
                LoadCustomization();
                Invoke(nameof(BroadcastCustomizationDelayed), 0.1f);
            }
            else
            {
                // This is a remote player - apply their synced customization if available
                if (m_syncedCustomizationIndices.Count > 0)
                {
                    ApplyNetworkedCustomization();
                }
            }
        }

        public override void OnStopNetwork()
        {
            // Unsubscribe from SyncList changes
            m_syncedCustomizationIndices.OnChange -= OnCustomizationSynced;
        }

        public override void OnStartServer()
        {
            if (IsServerInitialized)
            {
                NetworkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            }
        }

        public override void OnStopServer()
        {
            if (NetworkManager != null && NetworkManager.SceneManager != null)
            {
                NetworkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            }
        }

        private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            // Only the owner should send their customization when new clients join
            if (Owner.IsLocalClient && asServer)
            {
                Invoke(nameof(BroadcastCustomizationDelayed), 0.2f);
            }
        }

        // Delayed broadcast to ensure network is ready
        private void BroadcastCustomizationDelayed()
        {
            if (Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
        }

        /// <summary>
        /// Builds the renderer lookup dictionary from the mappings.
        /// </summary>
        private void BuildRendererLookup()
        {
            m_rendererLookup.Clear();

            foreach (var mapping in rendererMappings)
            {
                if (mapping.renderer == null)
                {
                    Debug.LogWarning($"Invalid renderer mapping detected on '{gameObject.name}': null renderer for ID '{mapping.id}'.");
                    continue;
                }

                if (m_rendererLookup.ContainsKey(mapping.id))
                {
                    Debug.LogWarning($"Duplicate renderer mapping for ID '{mapping.id}' on '{gameObject.name}'. Using first mapping.");
                    continue;
                }

                m_rendererLookup[mapping.id] = mapping.renderer;
            }
        }

        private Renderer GetRendererById(RendererID id)
        {
            if (!m_rendererLookup.TryGetValue(id, out Renderer renderer))
            {
                Debug.LogError($"No renderer found with ID '{id}'.");
                return null;
            }

            return renderer;
        }

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
            if (m_equippedItems.ContainsKey(key))
            {
                RemoveItem(key);
            }

            // Apply the new item based on its application mode
            ApplyItem(item);

            // Track the equipped item
            m_equippedItems[key] = item;
            UpdateDebugList();

            // Auto-save after equipping (only for owner)
            if (Owner.IsLocalClient && !disableAutoSaveForNetworking)
            {
                SaveCustomization();
            }

            // Broadcast to network (only for owner)
            if (Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
        }

        public void RemoveItem(CustomizationCategory category, string subCategory = null)
        {
            string key = GetEquipmentKey(category, subCategory);
            RemoveItem(key);
        }

        private void RemoveItem(string key)
        {
            if (!m_equippedItems.TryGetValue(key, out var item))
            {
                Debug.LogWarning($"No item equipped in slot '{key}' to remove.");
                return;
            }

            // Restore defaults for the slots used by this item
            RestoreDefaults(item);

            m_equippedItems.Remove(key);
            UpdateDebugList();

            // Auto-save after removing (only for owner)
            if (Owner.IsLocalClient && !disableAutoSaveForNetworking)
            {
                SaveCustomization();
            }

            // Broadcast to network (only for owner)
            if (Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
        }

        private void RemoveAllItems()
        {
            // Copy keys to avoid modification during iteration
            List<string> keys = new List<string>(m_equippedItems.Keys);

            foreach (string key in keys)
            {
                RemoveItem(key);
            }
        }

        public bool IsItemEquipped(CustomizationItem item)
        {
            if (item == null) return false;
            string key = item.GetEquipmentKey();
            return m_equippedItems.ContainsKey(key) && m_equippedItems[key] == item;
        }

        #region Save/Load Methods
        /// <summary>
        /// Saves the current customization to PlayerPrefs.
        /// </summary>
        private void SaveCustomization()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("Item database is not assigned! Cannot save customization.");
                return;
            }

            CustomizationSaveData saveData = new CustomizationSaveData();

            foreach (var kvp in m_equippedItems)
            {
                int itemIndex = itemDatabase.IndexOf(kvp.Value);

                if (itemIndex >= 0)
                {
                    saveData.equippedItems.Add(new EquippedItemData
                    {
                        itemIndex = itemIndex,
                        equipmentKey = kvp.Key
                    });
                }
            }

            string json = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(SAVE_KEY, json);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Loads and applies saved customization from PlayerPrefs.
        /// </summary>
        private void LoadCustomization()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("Item database is not assigned! Cannot load customization.");
                return;
            }

            if (!PlayerPrefs.HasKey(SAVE_KEY))
            {
                return;
            }

            string json = PlayerPrefs.GetString(SAVE_KEY);
            CustomizationSaveData saveData = JsonUtility.FromJson<CustomizationSaveData>(json);

            if (saveData == null || saveData.equippedItems == null)
            {
                Debug.LogWarning("[PlayerCustomizationManager] Failed to load customization data.");
                return;
            }

            // Clear current customization before loading
            RemoveAllItems();

            // Apply each saved item
            foreach (var itemData in saveData.equippedItems)
            {
                CustomizationItem item = itemDatabase[itemData.itemIndex];

                if (item != null)
                {
                    EquipItemWithoutSaving(item);
                }
                else
                {
                    Debug.LogWarning($"[PlayerCustomizationManager] Could not find item at index {itemData.itemIndex}");
                }
            }
        }

        public void ClearSavedCustomization()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region Application Methods
        /// <summary>
        /// Applies a customization item based on its application mode.
        /// </summary>
        private void ApplyItem(CustomizationItem item)
        {
            switch (item.applicationMode)
            {
                case ApplicationMode.SwapMeshOnly:
                    ApplySwapMeshOnly(item);
                    break;
                case ApplicationMode.SwapMaterialOnly:
                    ApplySwapMaterialOnly(item);
                    break;
                case ApplicationMode.SwapMeshAndMaterial:
                    ApplySwapMeshAndMaterial(item);
                    break;
            }
        }

        /// <summary>
        /// Applies only the mesh from the customization item slots.
        /// </summary>
        private void ApplySwapMeshOnly(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);
                if (renderer == null) continue;

                ApplyMesh(renderer, slot.mesh);
            }
        }

        /// <summary>
        /// Applies only the materials from the customization item slots.
        /// </summary>
        private void ApplySwapMaterialOnly(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);
                if (renderer == null) continue;

                ApplyMaterials(renderer, slot.materials);
            }
        }

        /// <summary>
        /// Applies both mesh and materials from the customization item slots.
        /// </summary>
        private void ApplySwapMeshAndMaterial(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);
                if (renderer == null) continue;

                ApplyMesh(renderer, slot.mesh);
                ApplyMaterials(renderer, slot.materials);
            }
        }

        /// <summary>
        /// Applies a mesh to a renderer.
        /// </summary>
        private void ApplyMesh(Renderer renderer, Mesh mesh)
        {
            if (mesh == null) return;

            if (renderer is SkinnedMeshRenderer skinnedRenderer)
            {
                skinnedRenderer.sharedMesh = mesh;
            }
            else if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = mesh;
                }
            }
        }

        /// <summary>
        /// Applies materials to a renderer, clearing existing materials and assigning new ones in order.
        /// </summary>
        private void ApplyMaterials(Renderer renderer, Material[] materials)
        {
            if (materials == null || materials.Length == 0) return;

            renderer.sharedMaterials = materials;
        }

        #endregion

        #region Restoration Methods
        /// <summary>
        /// Restores default mesh and materials for the slots used by an item.
        /// </summary>
        private void RestoreDefaults(CustomizationItem item)
        {
            foreach (var slot in item.slots)
            {
                Renderer renderer = GetRendererById(slot.targetRendererId);
                if (renderer == null) continue;

                // Find the default configuration for this renderer ID
                DefaultRendererData defaultData = defaultConfigurations.Find(d => d.rendererId == slot.targetRendererId);

                if (defaultData == null)
                {
                    Debug.LogWarning($"No default configuration found for renderer ID '{slot.targetRendererId}'.");
                    continue;
                }

                // Restore based on the item's application mode
                switch (item.applicationMode)
                {
                    case ApplicationMode.SwapMeshOnly:
                        ApplyMesh(renderer, defaultData.mesh);
                        break;
                    case ApplicationMode.SwapMaterialOnly:
                        ApplyMaterials(renderer, defaultData.materials);
                        break;
                    case ApplicationMode.SwapMeshAndMaterial:
                        ApplyMesh(renderer, defaultData.mesh);
                        ApplyMaterials(renderer, defaultData.materials);
                        break;
                }
            }
        }
        #endregion

        #region Networking Methods
        /// <summary>
        /// Called when the SyncList changes. Applies customization from other players.
        /// </summary>
        private void OnCustomizationSynced(SyncListOperation op, int index, int oldItem, int newItem, bool asServer)
        {
            // Only apply if we're not the owner
            if (!Owner.IsLocalClient && m_syncedCustomizationIndices.Count > 0)
            {
                ApplyNetworkedCustomization();
            }
        }

        /// <summary>
        /// Broadcasts current customization to all clients.
        /// Called by the owner when they equip/unequip items.
        /// </summary>
        private void BroadcastCustomization()
        {
            // Only the owner should broadcast
            if (!Owner.IsLocalClient)
            {
                Debug.LogWarning("[PlayerCustomizationManager] Only the owner can broadcast customization.");
                return;
            }

            // Convert current equipped items to index array
            int[] indices = GetEquippedItemIndices();

            // Send to server using ServerRpc
            ServerReceiveCustomization(indices);
        }

        /// <summary>
        /// Server receives customization data from client and broadcasts to all.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void ServerReceiveCustomization(int[] indices)
        {
            // Update the SyncList - this automatically syncs to all clients including potential late joiners
            m_syncedCustomizationIndices.Clear();
            m_syncedCustomizationIndices.AddRange(indices);
        }

        /// <summary>
        /// Applies customization received from the network.
        /// </summary>
        private void ApplyNetworkedCustomization()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("[PlayerCustomizationManager] Item database is not assigned! Cannot apply networked customization.");
                return;
            }

            // Clear current customization (but don't save - this is from network)
            RemoveAllItemsWithoutSaving();

            // Apply each item by index
            foreach (int index in m_syncedCustomizationIndices)
            {
                CustomizationItem item = itemDatabase[index];

                if (item != null)
                {
                    EquipItemWithoutSaving(item);
                }
                else
                {
                    Debug.LogWarning($"[PlayerCustomizationManager] Could not find item at index {index}");
                }
            }
        }

        /// <summary>
        /// Gets array of currently equipped item indices.
        /// </summary>
        private int[] GetEquippedItemIndices()
        {
            if (itemDatabase == null) return Array.Empty<int>();

            List<int> indices = new List<int>();

            foreach (var kvp in m_equippedItems)
            {
                int index = itemDatabase.IndexOf(kvp.Value);
                if (index >= 0)
                {
                    indices.Add(index);
                }
            }

            return indices.ToArray();
        }

        /// <summary>
        /// Equips an item without saving or broadcasting (used for networked sync and loading).
        /// </summary>
        private void EquipItemWithoutSaving(CustomizationItem item)
        {
            if (item == null || !item.IsValid()) return;

            string key = item.GetEquipmentKey();

            // Remove existing item in this slot
            if (m_equippedItems.ContainsKey(key))
            {
                RemoveItemWithoutSaving(key);
            }

            // Apply the item based on its application mode
            ApplyItem(item);

            // Track the equipped item
            m_equippedItems[key] = item;
            UpdateDebugList();
        }

        /// <summary>
        /// Removes an item without saving or broadcasting (used for networked sync).
        /// </summary>
        private void RemoveItemWithoutSaving(string key)
        {
            if (!m_equippedItems.TryGetValue(key, out var item)) return;

            // Restore defaults for the slots used by this item
            RestoreDefaults(item);

            m_equippedItems.Remove(key);
            UpdateDebugList();
        }

        /// <summary>
        /// Removes all items without saving (used for networked sync).
        /// </summary>
        private void RemoveAllItemsWithoutSaving()
        {
            List<string> keys = new List<string>(m_equippedItems.Keys);
            foreach (string key in keys)
            {
                RemoveItemWithoutSaving(key);
            }
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

        private void UpdateDebugList()
        {
            equippedItemNames.Clear();
            foreach (var kvp in m_equippedItems)
            {
                equippedItemNames.Add($"{kvp.Key}: {kvp.Value.itemName}");
            }
        }
        #endregion
    }
}
