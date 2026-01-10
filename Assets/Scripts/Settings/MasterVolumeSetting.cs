using UnityEngine;

namespace RooseLabs.Settings
{
    public class MasterVolumeSetting : FloatSetting
    {
        public override string DisplayName => "Master Volume";
        public override SettingCategory Category => SettingCategory.Audio;

        protected override float MinValue => 0f;
        protected override float MaxValue => 1f;
        public override int Precision => 2;

        public override float GetDefaultValue() => 0.8f;

        public override float GetValue() => AudioListener.volume;

        protected override void ApplyValueInternal(ref float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            value = AudioListener.volume;
        }

        public override string FormatValue(float value)
        {
            return $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
