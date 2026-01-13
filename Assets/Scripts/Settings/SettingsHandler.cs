using System;
using System.Collections.Generic;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Settings
{
    public static class SettingsHandler
    {
        private static readonly Logger Logger = Logger.GetLogger("SettingsHandler");

        private static readonly Dictionary<Type, Setting> Settings = new();
        private static bool s_initialized;

        public static void Initialize()
        {
            if (s_initialized) return;

            // Register all settings
            Register(new MasterVolumeSetting());
            Register(new MicrophoneDeviceSetting());
            Register(new PushToTalkSetting());
            Register(new ResolutionSetting());
            Register(new WindowModeSetting());
            Register(new VSyncSetting());
            Register(new FrameRateLimitSetting());
            Register(new TextureQualitySetting());
            Register(new AntiAliasingSetting());
            Register(new RenderScaleSetting());

            s_initialized = true;

            // Load and apply all settings
            foreach (var setting in Settings.Values)
            {
                setting.Load();
                setting.ApplyValue();
            }
        }

        private static void Register<T>(T setting) where T : Setting
        {
            Settings[typeof(T)] = setting;
        }

        /// <summary>
        /// Gets a setting instance by type.
        /// </summary>
        public static T GetSetting<T>() where T : Setting
        {
            if (!s_initialized)
            {
                Logger.Warning("SettingsHandler not initialized. Call Initialize() first.");
                return null;
            }

            if (Settings.TryGetValue(typeof(T), out var setting))
            {
                return setting as T;
            }

            Logger.Error($"Setting of type {typeof(T).Name} not found.");
            return null;
        }
    }
}
