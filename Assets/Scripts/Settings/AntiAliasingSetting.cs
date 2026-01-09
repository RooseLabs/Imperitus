using RooseLabs.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RooseLabs.Settings
{
    public enum AntiAliasing
    {
        None,
        FXAA,
        SMAALow,
        SMAAMedium,
        SMAAHigh
    }

    public class AntiAliasingSetting : EnumSetting<AntiAliasing>
    {
        public override string DisplayName => "Anti-Aliasing";
        public override SettingCategory Category => SettingCategory.Graphics;

        public override AntiAliasing GetDefaultValue() => AntiAliasing.None;

        protected override void ApplyValueInternal(ref AntiAliasing value)
        {
            if ((bool)PlayerCharacter.LocalCharacter && (bool)PlayerCharacter.LocalCharacter.Camera)
            {
                ApplyToCamera(PlayerCharacter.LocalCharacter.Camera, value);
            }
        }

        public void ApplyToCamera(Camera camera)
        {
            ApplyToCamera(camera, Value);
        }

        private static void ApplyToCamera(Camera camera, AntiAliasing aaValue)
        {
            if (!camera) return;

            var cameraData = camera.GetUniversalAdditionalCameraData();
            if (!cameraData) return;

            switch (aaValue)
            {
                case AntiAliasing.None:
                    cameraData.antialiasing = AntialiasingMode.None;
                    break;
                case AntiAliasing.FXAA:
                    cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case AntiAliasing.SMAALow:
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    cameraData.antialiasingQuality = AntialiasingQuality.Low;
                    break;
                case AntiAliasing.SMAAMedium:
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    cameraData.antialiasingQuality = AntialiasingQuality.Medium;
                    break;
                case AntiAliasing.SMAAHigh:
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    cameraData.antialiasingQuality = AntialiasingQuality.High;
                    break;
            }
        }
    }
}
