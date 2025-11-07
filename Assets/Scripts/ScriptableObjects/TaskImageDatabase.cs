using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs
{
    /// <summary>
    /// ScriptableObject database that holds all possible task images.
    /// </summary>
    [CreateAssetMenu(fileName = "TaskImageDatabase", menuName = "Imperitus/Task Image Database")]
    public class TaskImageDatabase : ScriptableObject
    {
        [System.Serializable]
        public class TaskImageEntry
        {
            public string imageId;
            public Sprite sprite;
        }

        [SerializeField] private List<TaskImageEntry> taskImages = new List<TaskImageEntry>();

        private Dictionary<string, Sprite> m_imageLookup;

        public void Initialize()
        {
            m_imageLookup = new Dictionary<string, Sprite>();
            foreach (var entry in taskImages)
            {
                if (!string.IsNullOrEmpty(entry.imageId) && entry.sprite != null)
                {
                    m_imageLookup[entry.imageId] = entry.sprite;
                }
            }
        }

        /// <summary>
        /// Get a sprite by its ID. Returns null if not found.
        /// </summary>
        public Sprite GetSprite(string imageId)
        {
            if (m_imageLookup == null)
            {
                Initialize();
            }

            return m_imageLookup != null && m_imageLookup.TryGetValue(imageId, out Sprite sprite)
                ? sprite
                : null;
        }

        /// <summary>
        /// Get all available image IDs.
        /// </summary>
        public List<string> GetAllImageIds()
        {
            List<string> ids = new List<string>();
            foreach (var entry in taskImages)
            {
                if (!string.IsNullOrEmpty(entry.imageId))
                {
                    ids.Add(entry.imageId);
                }
            }
            return ids;
        }

    #if UNITY_EDITOR
        /// <summary>
        /// Editor helper to validate that all IDs are unique.
        /// </summary>
        public void ValidateUniqueIds()
        {
            HashSet<string> seenIds = new HashSet<string>();
            foreach (var entry in taskImages)
            {
                if (!string.IsNullOrEmpty(entry.imageId))
                {
                    if (seenIds.Contains(entry.imageId))
                    {
                        Debug.LogError($"Duplicate image ID found: {entry.imageId}", this);
                    }
                    seenIds.Add(entry.imageId);
                }
            }
        }
    #endif
    }
}