using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RooseLabs.Core
{
    public class LoggerManager : SingletonBehaviour<LoggerManager>
    {
        [SerializeField] private List<Logger> loggers = new();

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void AddLogger(Logger logger) => loggers.Add(logger);
    }

    [Serializable]
    public class Logger
    {
        private enum MessageType
        {
            Debug = 0,
            Info = 1,
            Warning = 2,
            Error = 3
        }

        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public bool Enabled { get; set; }

        private static readonly Dictionary<string, Logger> Instances = new(StringComparer.OrdinalIgnoreCase);

        public static Logger GetLogger(string name, bool enableOnCreate = false)
        {
            if (Instances.TryGetValue(name, out var logger)) return logger;
            logger = new Logger(name, enableOnCreate);
            Instances[name] = logger;
            LoggerManager.Instance.AddLogger(logger);
            return logger;
        }

        private Logger(string name, bool enabled)
        {
            Name = name;
            Enabled = enabled;
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
