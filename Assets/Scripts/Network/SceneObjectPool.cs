using System;
using FishNet;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Utility.Performance;
using UnityEngine;

namespace RooseLabs.Network
{
    public class SceneObjectPool : MonoBehaviour
    {
        [Serializable]
        private struct PoolItem
        {
            [Tooltip("Prefab to cache.")]
            public NetworkObject prefab;
            [Tooltip("Number of instances to cache.")]
            public int count;
        }

        [SerializeField] private PoolItem[] poolItems = Array.Empty<PoolItem>();

        private DefaultObjectPool m_defaultObjectPool;

        private void Awake()
        {
            NetworkManager nm = InstanceFinder.NetworkManager;
            if (nm == null) return;
            m_defaultObjectPool = nm.ObjectPool as DefaultObjectPool;
            if (m_defaultObjectPool == null) return;
            foreach (var item in poolItems)
            {
                if (item.prefab == null || item.count <= 0) continue;
                m_defaultObjectPool.StorePrefabObjects(item.prefab, item.count, true);
            }
        }

        private void OnDestroy()
        {
            if (m_defaultObjectPool == null) return;
            foreach (var item in poolItems)
            {
                if (item.prefab == null || item.count <= 0) continue;
                m_defaultObjectPool.ClearPool(item.prefab);
            }
        }
    }
}
