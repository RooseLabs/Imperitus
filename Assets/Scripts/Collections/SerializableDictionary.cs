using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Collections
{
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
        where TKey : IComparable<TKey>
    {
        [SerializeField, HideInInspector] private List<TKey> m_keys;
        [SerializeField, HideInInspector] private List<TValue> m_values;

        public void OnBeforeSerialize()
        {
            m_keys = new List<TKey>();
            m_values = new List<TValue>();
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
            m_keys = null;
            m_values = null;
        }
    }
}
