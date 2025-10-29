using UnityEngine;

namespace RooseLabs.Utils
{
    public class DebugManager : MonoBehaviour
    {
        [Header("Debug Settings")]
        [Tooltip("Enable or disable all Debug messages globally.")]
        public bool enableLogs = true;

        private static DebugManager _instance;
        public static bool EnableLogs => _instance != null && _instance.enableLogs;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        public static void Log(object message)
        {
            if (EnableLogs)
                Debug.Log(message);
        }

        public static void LogWarning(object message)
        {
            if (EnableLogs)
                Debug.LogWarning(message);
        }

        public static void LogError(object message)
        {
            if (EnableLogs)
                Debug.LogError(message);
        }

        public static void Log(object message, Object context)
        {
            if (EnableLogs)
                Debug.Log(message, context);
        }

        public static void LogWarning(object message, Object context)
        {
            if (EnableLogs)
                Debug.LogWarning(message, context);
        }

        public static void LogError(object message, Object context)
        {
            if (EnableLogs)
                Debug.LogError(message, context);
        }
    }
}
