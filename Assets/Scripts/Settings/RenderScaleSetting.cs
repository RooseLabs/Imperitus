using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RooseLabs.Settings
{
    public class RenderScaleSetting : FloatSetting
    {
        public override string DisplayName => "Render Scale";
        public override SettingCategory Category => SettingCategory.Graphics;

        protected override float MinValue => 0.1f;
        protected override float MaxValue => 3.0f;
        public override float ExposedMinValue => 0.5f;
        public override float ExposedMaxValue => 2.0f;
        public override int Precision => 1;

        public override float GetDefaultValue() => 1.0f;

        public override float GetValue()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset renderPipelineAsset)
            {
                return renderPipelineAsset.renderScale;
            }
            return 1.0f;
        }

        protected override void ApplyValueInternal(ref float value)
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset renderPipelineAsset)
            {
                renderPipelineAsset.renderScale = Mathf.Clamp(value, MinValue, MaxValue);
                value = renderPipelineAsset.renderScale;
            }
            value = 1.0f;
        }

        public override string FormatValue(float value)
        {
            return $"{value:F1}x";
        }
    }
}
