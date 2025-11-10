using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Player.Customization
{
    /// <summary>
    /// Manages character customization for a player prefab.
    /// </summary>
    public class PlayerCustomizationManager : NetworkBehaviour
    {
        private Logger Logger => Logger.GetLogger("PlayerCustomization");

        [Header("Renderer Mappings")]
        [Tooltip("Map string IDs to actual renderers in your prefab. These IDs are used in CustomizationItem slots.")]
        [SerializeField] private List<RendererMapping> rendererMappings = new List<RendererMapping>();

        [Header("Default Configurations")]
        [Tooltip("Define default meshes and materials for each category. Used when removing items.")]
        [SerializeField] private List<DefaultCustomizationData> defaultConfigurations = new List<DefaultCustomizationData>();

        [Header("Save/Load")]
        [Tooltip("Reference to the item database for save/load functionality.")]
        [SerializeField] private CustomizationItemDatabase itemDatabase;

        [Header("Runtime Data")]
        [Tooltip("Debug view of currently equipped items.")]
        [SerializeField] private List<string> equippedItemNames = new List<string>();

        [Header("Networking")]
        [Tooltip("If true, disable auto-save for network sync (server will handle saves).")]
        [SerializeField] private bool disableAutoSaveForNetworking = false;

        private readonly SyncVar<int[]> syncedCustomizationIndices = new SyncVar<int[]>(new int[0]);

        // Tracks currently equipped items: Key = equipment key (category or category_subcategory)
        private Dictionary<string, CustomizationItem> equippedItems = new Dictionary<string, CustomizationItem>();

        // Tracks instantiated GameObjects for InstantiateNew mode: Key = equipment key
        private Dictionary<string, List<GameObject>> instantiatedObjects = new Dictionary<string, List<GameObject>>();

        // Cached renderer lookup: Key = renderer ID
        private Dictionary<string, Renderer> rendererLookup = new Dictionary<string, Renderer>();

        // ADDED: Flag to track if we've done initial sync
        private bool hasInitializedCustomization = false;

        private const string SAVE_KEY = "PlayerCustomization";

        private void Awake()
        {
            BuildRendererLookup();
            ValidateDefaultConfigurations();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Subscribe to SyncVar changes
            syncedCustomizationIndices.OnChange += OnCustomizationSynced;

            if (base.Owner.IsLocalClient)
            {
                // This is the local player - load their saved customization
                LoadCustomization();

                Invoke(nameof(BroadcastCustomizationDelayed), 0.1f);
            }
            else
            {
                // This is a remote player - apply their synced customization if available
                if (syncedCustomizationIndices.Value != null && syncedCustomizationIndices.Value.Length > 0)
                {
                    ApplyNetworkedCustomization(syncedCustomizationIndices.Value);
                }
            }
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();

            // Unsubscribe from SyncVar changes
            syncedCustomizationIndices.OnChange -= OnCustomizationSynced;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (base.IsServerInitialized)
            {
                base.NetworkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes;
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            if (base.NetworkManager != null && base.NetworkManager.SceneManager != null)
            {
                base.NetworkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes;
            }
        }

        private void OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            // Only the owner should send their customization when new clients join
            if (base.Owner.IsLocalClient && asServer)
            {
                Debug.Log($"[PlayerCustomizationManager] Client {conn.ClientId} loaded scenes, re-broadcasting customization");
                Invoke(nameof(BroadcastCustomizationDelayed), 0.2f);
            }
        }

        // Delayed broadcast to ensure network is ready
        private void BroadcastCustomizationDelayed()
        {
            if (base.Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
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

            // Auto-save after equipping (only for owner)
            if (base.Owner.IsLocalClient && !disableAutoSaveForNetworking)
            {
                SaveCustomization();
            }

            // FIXED: Broadcast to network (only for owner)
            if (base.Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
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

            // Auto-save after removing (only for owner)
            if (base.Owner.IsLocalClient && !disableAutoSaveForNetworking)
            {
                SaveCustomization();
            }

            // Broadcast to network (only for owner)
            if (base.Owner.IsLocalClient)
            {
                BroadcastCustomization();
            }
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

        #region Save/Load Methods

        /// <summary>
        /// Saves the current customization to PlayerPrefs.
        /// </summary>
        public void SaveCustomization()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("Item database is not assigned! Cannot save customization.");
                return;
            }

            CustomizationSaveData saveData = new CustomizationSaveData();

            foreach (var kvp in equippedItems)
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

            Debug.Log($"[PlayerCustomizationManager] Saved {saveData.equippedItems.Count} equipped items.");
        }

        /// <summary>
        /// Loads and applies saved customization from PlayerPrefs.
        /// </summary>
        public void LoadCustomization()
        {
            if (itemDatabase == null)
            {
                Debug.LogError("Item database is not assigned! Cannot load customization.");
                return;
            }

            if (!PlayerPrefs.HasKey(SAVE_KEY))
            {
                Debug.Log("[PlayerCustomizationManager] No saved customization found.");
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
            int loadedCount = 0;
            foreach (var itemData in saveData.equippedItems)
            {
                CustomizationItem item = itemDatabase[itemData.itemIndex];

                if (item != null)
                {
                    EquipItemWithoutSaving(item);
                    loadedCount++;
                }
                else
                {
                    Debug.LogWarning($"[PlayerCustomizationManager] Could not find item at index {itemData.itemIndex}");
                }
            }

            Debug.Log($"[PlayerCustomizationManager] Loaded {loadedCount} equipped items.");
        }

        /// <summary>
        /// Clears all saved customization data.
        /// </summary>
        public void ClearSavedCustomization()
        {
            PlayerPrefs.DeleteKey(SAVE_KEY);
            PlayerPrefs.Save();
            Debug.Log("[PlayerCustomizationManager] Cleared saved customization.");
        }

        #endregion

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

        #region Networking Methods

        /// <summary>
        /// Called when the SyncVar changes. Applies customization from other players.
        /// </summary>
        private void OnCustomizationSynced(int[] prev, int[] next, bool asServer)
        {
            // FIXED: Only apply if we're not the owner
            if (!base.Owner.IsLocalClient && next != null && next.Length > 0)
            {
                Debug.Log($"[PlayerCustomizationManager] OnCustomizationSynced called - applying {next.Length} items");
                ApplyNetworkedCustomization(next);
                hasInitializedCustomization = true;
            }
        }

        /// <summary>
        /// Broadcasts current customization to all clients.
        /// Called by the owner when they equip/unequip items.
        /// </summary>
        private void BroadcastCustomization()
        {
            // Only the owner should broadcast
            if (!base.Owner.IsLocalClient)
            {
                Debug.LogWarning("[PlayerCustomizationManager] Only the owner can broadcast customization.");
                return;
            }

            // Convert current equipped items to index array
            int[] indices = GetEquippedItemIndices();

            Debug.Log($"[PlayerCustomizationManager] Broadcasting customization: {indices.Length} items");

            // Send to server using ServerRpc
            ServerReceiveCustomization(indices);
        }

        /// <summary>
        /// Server receives customization data from client and broadcasts to all.
        /// </summary>
        [ServerRpc(RequireOwnership = true)]
        private void ServerReceiveCustomization(int[] indices)
        {
            Debug.Log($"[PlayerCustomizationManager] Server received customization from client: {indices.Length} items");

            // Update the SyncVar - this automatically syncs to all clients including potencial late joiners
            syncedCustomizationIndices.Value = indices;
        }

        /// <summary>
        /// Applies customization received from the network.
        /// </summary>
        private void ApplyNetworkedCustomization(int[] indices)
        {
            if (itemDatabase == null)
            {
                Debug.LogError("[PlayerCustomizationManager] Item database is not assigned! Cannot apply networked customization.");
                return;
            }

            Debug.Log($"[PlayerCustomizationManager] Applying networked customization: {indices.Length} items");

            // Clear current customization (but don't save - this is from network)
            RemoveAllItemsWithoutSaving();

            // Apply each item by index
            foreach (int index in indices)
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
            if (itemDatabase == null) return new int[0];

            List<int> indices = new List<int>();

            foreach (var kvp in equippedItems)
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
        /// Equips an item without saving or broadcasting (used for networked sync).
        /// </summary>
        private void EquipItemWithoutSaving(CustomizationItem item)
        {
            if (item == null || !item.IsValid()) return;

            string key = item.GetEquipmentKey();

            // Remove existing item in this slot
            if (equippedItems.ContainsKey(key))
            {
                RemoveItemWithoutSaving(key);
            }

            // Apply the item based on its application mode
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
        }

        /// <summary>
        /// Removes an item without saving or broadcasting (used for networked sync).
        /// </summary>
        private void RemoveItemWithoutSaving(string key)
        {
            if (!equippedItems.ContainsKey(key)) return;

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
        }

        /// <summary>
        /// Removes all items without saving (used for networked sync).
        /// </summary>
        private void RemoveAllItemsWithoutSaving()
        {
            List<string> keys = new List<string>(equippedItems.Keys);
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
