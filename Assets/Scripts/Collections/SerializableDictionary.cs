using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Collections
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
        where TKey : IComparable<TKey>
    {
        [SerializeField, HideInInspector] private List<TKey> m_keys = new();
        [SerializeField, HideInInspector] private List<TValue> m_values = new();

        public void OnBeforeSerialize()
        {
            m_keys.Clear();
            m_values.Clear();
            foreach (var kvp in this)
            {
                m_keys.Add(kvp.Key);
                m_values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();
            for (int i = 0; i < m_keys.Count; i++)
            {
                Add(m_keys[i], m_values[i]);
            }
            m_keys.Clear();
            m_values.Clear();
        }
    }
}
