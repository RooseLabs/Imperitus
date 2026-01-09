using UnityEngine;

namespace RooseLabs.Settings
{
    public class VSyncSetting : BoolSetting
    {
        public override string DisplayName => "V-Sync";
        public override SettingCategory Category => SettingCategory.Screen;

        public override bool GetDefaultValue() => true;

        public override bool GetValue() => QualitySettings.vSyncCount == 1;

        protected override void ApplyValueInternal(ref bool value)
        {
            QualitySettings.vSyncCount = value ? 1 : 0;
        }
    }
}
