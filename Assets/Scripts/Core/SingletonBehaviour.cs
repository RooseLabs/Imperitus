using System;
using UnityEngine;

namespace RooseLabs.Core
{
    public class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
    {
        private static T s_instance;
        private static readonly object s_lock = new();
        private static bool s_shuttingDown;

        public static T Instance
        {
            get
            {
                if (s_shuttingDown || (UnityEngine.Object)s_instance != (UnityEngine.Object)null)
                    return s_instance;
                lock (s_lock)
                {
                    if (!Application.isPlaying)
                        throw new Exception($"Instance of {typeof(T).Name} can only be accessed in play mode.");
                    s_instance = (T)FindAnyObjectByType(typeof(T));
                    GameObject singleton = new()
                    {
                        name = typeof(T).Name
                    };
                    s_instance = singleton.AddComponent<T>();
                    return s_instance;
                }
            }
        }

        protected virtual void OnApplicationQuit() => s_shuttingDown = true;
    }
}
