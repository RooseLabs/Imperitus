using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs
{
    public class PlayerHitbox : MonoBehaviour
    {
        [Header("Hitbox Settings")]
        [SerializeField] private LayerMask hitboxLayer;
        [SerializeField] private bool makeTriggersByDefault = true;

        private List<Collider> hitboxColliders = new List<Collider>();
        private bool hitboxesEnabled = true;

        private void Awake()
        {
            // Find all hitbox colliders and enable them by default
            FindHitboxColliders();
            EnableHitboxes();
        }

        /// <summary>
        /// Finds all colliders in child GameObjects that are on the PlayerHitbox layer
        /// </summary>
        private void FindHitboxColliders()
        {
            hitboxColliders.Clear();

            // Get all colliders in children
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);

            // Get the layer index from the LayerMask
            int layerIndex = GetLayerIndex(hitboxLayer);

            // Filter by layer
            foreach (Collider col in allColliders)
            {
                if (col.gameObject.layer == layerIndex)
                {
                    hitboxColliders.Add(col);

                    // Set as trigger if enabled
                    if (makeTriggersByDefault)
                    {
                        col.isTrigger = true;
                    }
                }
            }

            Debug.Log($"Found {hitboxColliders.Count} hitbox colliders on layer index {layerIndex}");
        }

        /// <summary>
        /// Converts a LayerMask to a layer index
        /// </summary>
        private int GetLayerIndex(LayerMask mask)
        {
            int layerNumber = 0;
            int layer = mask.value;
            while (layer > 1)
            {
                layer = layer >> 1;
                layerNumber++;
            }
            return layerNumber;
        }

        /// <summary>
        /// Enables all hitbox colliders
        /// </summary>
        public void EnableHitboxes()
        {
            SetHitboxState(true);
        }

        /// <summary>
        /// Disables all hitbox colliders
        /// </summary>
        public void DisableHitboxes()
        {
            SetHitboxState(false);
        }

        /// <summary>
        /// Toggles hitbox colliders on/off
        /// </summary>
        public void ToggleHitboxes()
        {
            SetHitboxState(!hitboxesEnabled);
        }

        /// <summary>
        /// Sets the enabled state of all hitbox colliders
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public void SetHitboxState(bool enabled)
        {
            hitboxesEnabled = enabled;

            foreach (Collider col in hitboxColliders)
            {
                if (col != null)
                {
                    col.enabled = enabled;
                }
            }

            Debug.Log($"Hitboxes {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Returns the current state of the hitboxes
        /// </summary>
        public bool AreHitboxesEnabled()
        {
            return hitboxesEnabled;
        }

        /// <summary>
        /// Refreshes the list of hitbox colliders (useful if you add/remove colliders at runtime)
        /// </summary>
        public void RefreshHitboxColliders()
        {
            FindHitboxColliders();
        }
    }
}