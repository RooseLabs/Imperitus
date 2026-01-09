using UnityEngine;

namespace RooseLabs.Settings
{
    public enum WindowMode
    {
        Fullscreen,
        Borderless,
        Windowed
    }

    public class WindowModeSetting : EnumSetting<WindowMode>
    {
        public override string DisplayName => "Window Mode";
        public override SettingCategory Category => SettingCategory.Screen;

        public override WindowMode GetDefaultValue() => WindowMode.Borderless;

        public override WindowMode GetValue()
        {
            return Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => WindowMode.Fullscreen,
                FullScreenMode.FullScreenWindow => WindowMode.Borderless,
                FullScreenMode.Windowed => WindowMode.Windowed,
                _ => WindowMode.Borderless
            };
        }

        protected override void ApplyValueInternal(ref WindowMode value)
        {
            Screen.fullScreenMode = value switch
            {
                WindowMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
                WindowMode.Borderless => FullScreenMode.FullScreenWindow,
                WindowMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            value = GetValue();
        }
    }
}
