using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    [ExecuteAlways]
    [AddComponentMenu("RooseLabs/UI/Glowing Sprite")]
    public class GlowingSprite : Image
    {
        private const string ShaderName = "RooseLabs/UI/SpriteGlow";
        private static readonly int ShaderPropUVRect = Shader.PropertyToID("_UVRect");
        private static readonly int ShaderPropGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int ShaderPropGlowWidth = Shader.PropertyToID("_GlowWidth");

        [SerializeField, Range(0, 100)]
        private float glowWidth = 10f;

        [SerializeField, ColorUsage(false, true)]
        private Color glowColor = Color.white;

        private static Shader s_cachedShader;
        private static bool s_shaderWarnedMissing;

        public float GlowWidth
        {
            get => glowWidth;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(glowWidth, clamped)) return;
                glowWidth = clamped;
                UpdateGlowProperties();
                SetMaterialDirty();
            }
        }

        public Color GlowColor
        {
            get => glowColor;
            set
            {
                if (glowColor == value) return;
                glowColor = value;
                UpdateGlowProperties();
                SetMaterialDirty();
            }
        }

        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();
            UpdateUVRect();
            UpdateGlowProperties();
        }

        public override Material material
        {
            get
            {
                if (m_Material) return m_Material;

                if (!s_cachedShader)
                {
                    s_cachedShader = Shader.Find(ShaderName);
                    if (!s_cachedShader && !s_shaderWarnedMissing)
                    {
                        Debug.LogWarning($"[GlowingSprite] Shader '{ShaderName}' not found. Ensure the shader is included in the build.", this);
                        s_shaderWarnedMissing = true;
                    }
                }

                if (!s_cachedShader) return base.material;

                m_Material = new Material(s_cachedShader) { hideFlags = HideFlags.HideAndDontSave };
                return m_Material;
            }
        }

        private void UpdateUVRect()
        {
            if (!m_Material) return;
            if (!material.HasProperty(ShaderPropUVRect)) return;

            if (!sprite || !sprite.packed)
            {
                material.SetVector(ShaderPropUVRect, new Vector4(0, 0, 1, 1));
                return;
            }

            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;

            // Convert pixel rect to UV (0-1) space
            Vector4 uvRect = new Vector4(
                rect.x / texture.width,                     // min X
                rect.y / texture.height,                    // min Y
                (rect.x + rect.width) / texture.width,      // max X
                (rect.y + rect.height) / texture.height  // max Y
            );

            material.SetVector(ShaderPropUVRect, uvRect);
        }

        private void UpdateGlowProperties()
        {
            if (!m_Material) return;

            if (material.HasProperty(ShaderPropGlowWidth))
            {
                material.SetFloat(ShaderPropGlowWidth, glowWidth);
            }

            if (material.HasProperty(ShaderPropGlowColor))
            {
                // Pass linear HDR color to the shader. The ColorUsage attribute above enables the HDR color picker in the inspector.
                material.SetColor(ShaderPropGlowColor, glowColor.linear);
            }
        }
    }
}
