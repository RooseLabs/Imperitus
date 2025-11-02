using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace RooseLabs.Core
{
    public class LoggerManager : SingletonBehaviour<LoggerManager>
    {
        [Serializable]
        public class LoggerToggle
        {
            public string name;
            public bool enabled;
        }

        [SerializeField]
        private List<LoggerToggle> loggers = new();

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void OnValidate()
        {
            foreach (var toggle in loggers)
            {
                var logger = Logger.GetLogger(toggle.name);
                logger.Enabled = toggle.enabled;
            }
        }

        public void AddLogger(string loggerName)
        {
            if (loggers.All(t => t.name != loggerName))
            {
                loggers.Add(new LoggerToggle { name = loggerName, enabled = true });
            }
        }
    }

    public class Logger
    {
        private enum MessageType
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        public string Name { get; }

        public bool Enabled { get; set; }

        private static readonly Dictionary<string, Logger> Instances = new(StringComparer.OrdinalIgnoreCase);

        public static Logger GetLogger(string name)
        {
            if (Instances.TryGetValue(name, out var logger)) return logger;
            logger = new Logger(name);
            Instances[name] = logger;
            LoggerManager.Instance.AddLogger(name);
            return logger;
        }

        private Logger(string name)
        {
            Name = name;
            Enabled = true;
        }

        [HideInCallstack]
        public void Debug(string message)
        {
            if (!Enabled) return;
            Log(MessageType.Debug, message);
        }

        [HideInCallstack]
        [Conditional("UNITY_EDITOR")]
        public void Info(string message)
        {
            if (!Enabled) return;
            Log(MessageType.Info, message);
        }

        [HideInCallstack]
        public void Warning(string message)
        {
            if (!Enabled) return;
            Log(MessageType.Warning, message);
        }

        [HideInCallstack]
        public void Error(string message)
        {
            if (!Enabled) return;
            Log(MessageType.Error, message);
        }

        public Logger Enable()
        {
            Enabled = true;
            return this;
        }

        public Logger Disable()
        {
            Enabled = false;
            return this;
        }

        [HideInCallstack]
        private void Log(MessageType messageType, string message)
        {
            string timestamp = Application.isEditor ? "" : $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ";
            string msg = $"{timestamp}[{messageType}] [{Name}] {message}";

            switch (messageType)
            {
                case MessageType.Debug:
                case MessageType.Info:
                    UnityEngine.Debug.Log(msg);
                    break;
                case MessageType.Warning:
                    UnityEngine.Debug.LogWarning(msg);
                    break;
                case MessageType.Error:
                    UnityEngine.Debug.LogError(msg);
                    break;
            }
        }
    }
}
