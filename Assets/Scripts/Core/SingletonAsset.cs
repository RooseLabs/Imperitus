using UnityEngine;

namespace RooseLabs.Core
{
    public class SingletonAsset<T> : ScriptableObject where T : SingletonAsset<T>
    {
        private static T s_instance;

        public static T Instance
        {
            get
            {
                if (!s_instance)
                {
                    s_instance = Resources.Load<T>(typeof(T).Name);
                }
                return s_instance;
            }
        }
    }
}
