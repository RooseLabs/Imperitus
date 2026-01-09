using System;
using UnityEngine;

namespace RooseLabs.Settings
{
    public class FrameRateLimitSetting : FloatSetting
    {
        public override string DisplayName => "FPS Limit";
        public override SettingCategory Category => SettingCategory.Screen;

        protected override bool ClampOnLoad => false;
        protected override float MinValue => 30f;
        protected override float MaxValue
        {
            get
            {
                int refreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
                return Math.Max(refreshRate, 240) + 1;
            }
        }

        public override float GetDefaultValue() => 0f;

        public override float GetValue() => Value <= 0f ? ExposedMaxValue : Value;

        protected override void ApplyValueInternal(ref float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                value = 0f;
                Application.targetFrameRate = -1;
                return;
            }

            int maxLimit = (int)MaxValue;
            int frameLimit = Mathf.RoundToInt(value);
            Application.targetFrameRate = frameLimit >= maxLimit ? -1 : Mathf.Clamp(frameLimit, (int)MinValue, maxLimit);

            // Disable VSync when using frame rate limit (Unity only respects targetFrameRate when vSyncCount is 0)
            SettingsHandler.GetSetting<VSyncSetting>().ApplyValue(false);

            // Update value to reflect actual applied value
            value = Application.targetFrameRate;
        }

        public string FormatValue(float value)
        {
            int intValue = Mathf.RoundToInt(value);
            return intValue >= ExposedMaxValue ? "Unlimited" : intValue.ToString();
        }
    }
}
