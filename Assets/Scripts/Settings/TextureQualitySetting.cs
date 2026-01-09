using UnityEngine;

namespace RooseLabs.Settings
{
    public enum TextureQuality
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    public class TextureQualitySetting : EnumSetting<TextureQuality>
    {
        public override string DisplayName => "Texture Quality";
        public override SettingCategory Category => SettingCategory.Graphics;

        public override TextureQuality GetDefaultValue() => TextureQuality.VeryHigh;

        public override TextureQuality GetValue()
        {
            // Inverse mapping back from Unity's globalTextureMipmapLimit
            int mipMapLimit = QualitySettings.globalTextureMipmapLimit;
            int texQuality = 3 - Mathf.Clamp(mipMapLimit, 0, 3);
            return (TextureQuality)texQuality;
        }

        protected override void ApplyValueInternal(ref TextureQuality value)
        {
            // Inverse mapping: Low=0->3, Medium=1->2, High=2->1, VeryHigh=3->0
            // Unity's globalTextureMipmapLimit: 0=best quality, higher=worse quality
            int texQuality = (int)value;
            QualitySettings.globalTextureMipmapLimit = 3 - Mathf.Clamp(texQuality, 0, 3);
        }
    }
}
