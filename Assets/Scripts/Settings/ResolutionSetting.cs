using System.Linq;
using UnityEngine;

namespace RooseLabs.Settings
{
    public class ResolutionSetting : IntSetting
    {
        public override string DisplayName => "Resolution";
        public override SettingCategory Category => SettingCategory.Screen;

        public override int GetDefaultValue()
        {
            var resolutions = GetResolutions();
            int nativeWidth = Display.main.systemWidth;
            int nativeHeight = Display.main.systemHeight;

            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == nativeWidth && resolutions[i].height == nativeHeight)
                    return i;
            }

            // If native resolution not found, return highest available resolution
            return resolutions.Length - 1;
        }

        public override int GetValue()
        {
            var resolutions = GetResolutions();
            var current = Screen.currentResolution;

            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == current.width && resolutions[i].height == current.height)
                    return i;
            }

            return 0;
        }

        protected override void ApplyValueInternal(ref int value)
        {
            var resolutions = GetResolutions();
            if (value >= 0 && value < resolutions.Length)
            {
                var res = resolutions[value];
                Screen.SetResolution(res.width, res.height, Screen.fullScreenMode, res.refreshRateRatio);
            }
        }

        public override string[] GetChoices()
        {
            return GetResolutions().Select(r => $"{r.width} x {r.height}").ToArray();
        }

        /// <summary>
        /// Returns all available resolutions grouped by width/height.
        /// </summary>
        private static Resolution[] GetResolutions()
        {
            return Screen.resolutions
                .GroupBy(r => new { r.width, r.height })
                .Select(g => g.OrderBy(r => r.refreshRateRatio.value).First())
                .OrderBy(r => r.width * r.height)
                .ThenBy(r => r.refreshRateRatio.value)
                .ToArray();
        }
    }
}
