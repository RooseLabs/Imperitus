using UnityEngine;

namespace RooseLabs.Settings
{
    public class MicrophoneDeviceSetting : IntSetting
    {
        public override string DisplayName => "Microphone Device";
        public override SettingCategory Category => SettingCategory.Audio;

        public override int GetDefaultValue() => 0;

        protected override void ApplyValueInternal(ref int value)
        {
            // Microphone device is managed by the voice chat system
        }

        public override string[] GetChoices()
        {
            return GetMicrophoneDevices();
        }

        /// <summary>
        /// Returns available microphone devices.
        /// </summary>
        public static string[] GetMicrophoneDevices()
        {
            return Microphone.devices.Length > 0 ? Microphone.devices : new[] { "No Microphone" };
        }
    }
}
