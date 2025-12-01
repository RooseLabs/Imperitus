using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    /// <summary>
    /// Central database for all SoundTypes in the game.
    /// Allows network-safe lookup by index.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundTypeDatabase", menuName = "Imperitus/Sound Type Database")]
    public class SoundTypeDatabase : ScriptableObject
    {
        private static SoundTypeDatabase _instance;
        public static SoundTypeDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<SoundTypeDatabase>("SoundTypeDatabase");
                    if (_instance == null)
                    {
                        Debug.LogError("[SoundTypeDatabase] No database found in Resources folder! Create one at Resources/SoundTypeDatabase.asset");
                    }
                    else
                    {
                        _instance.Initialize();
                    }
                }
                return _instance;
            }
        }

        [Tooltip("All available sound types. Index is used for network serialization.")]
        public List<SoundType> soundTypes = new List<SoundType>();

        // Lookup caches
        private Dictionary<string, int> _keyToIndex;
        private Dictionary<string, SoundType> _keyToType;

        /// <summary>
        /// Call this on game start to build lookup tables.
        /// </summary>
        public void Initialize()
        {
            _keyToIndex = new Dictionary<string, int>();
            _keyToType = new Dictionary<string, SoundType>();

            for (int i = 0; i < soundTypes.Count; i++)
            {
                if (soundTypes[i] == null)
                {
                    Debug.LogWarning($"[SoundTypeDatabase] Null entry at index {i}");
                    continue;
                }

                string key = soundTypes[i].key;

                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[SoundTypeDatabase] SoundType at index {i} has no key!");
                    continue;
                }

                if (_keyToIndex.ContainsKey(key))
                {
                    Debug.LogError($"[SoundTypeDatabase] Duplicate key '{key}' found! Indices {_keyToIndex[key]} and {i}");
                    continue;
                }

                _keyToIndex[key] = i;
                _keyToType[key] = soundTypes[i];
            }

            Debug.Log($"[SoundTypeDatabase] Initialized with {_keyToIndex.Count} sound types");
        }

        /// <summary>
        /// Get SoundType by network-safe index.
        /// </summary>
        public SoundType GetByIndex(int index)
        {
            if (index < 0 || index >= soundTypes.Count)
            {
                Debug.LogWarning($"[SoundTypeDatabase] Invalid index {index}");
                return null;
            }
            return soundTypes[index];
        }

        /// <summary>
        /// Get SoundType by key.
        /// </summary>
        public SoundType GetByKey(string key)
        {
            if (_keyToType == null) Initialize();
            return _keyToType.TryGetValue(key, out var type) ? type : null;
        }

        /// <summary>
        /// Get network-safe index for a SoundType key.
        /// </summary>
        public int GetIndex(string key)
        {
            if (_keyToIndex == null) Initialize();
            return _keyToIndex.TryGetValue(key, out var index) ? index : -1;
        }

        /// <summary>
        /// Get network-safe index for a SoundType instance.
        /// </summary>
        public int GetIndex(SoundType soundType)
        {
            if (soundType == null) return -1;
            return soundTypes.IndexOf(soundType);
        }

        private void OnEnable()
        {
            if (_instance == null)
            {
                _instance = this;
                Initialize();
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validate database in editor.
        /// </summary>
        private void OnValidate()
        {
            // Check for duplicate keys
            var keys = soundTypes.Where(s => s != null && !string.IsNullOrEmpty(s.key))
                                 .Select(s => s.key)
                                 .ToList();

            var duplicates = keys.GroupBy(k => k)
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToList();

            if (duplicates.Any())
            {
                Debug.LogError($"[SoundTypeDatabase] Duplicate keys found: {string.Join(", ", duplicates)}");
            }
        }
#endif
    }
}