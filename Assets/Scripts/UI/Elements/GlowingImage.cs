using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    [ExecuteAlways]
    [AddComponentMenu("RooseLabs/UI/Glowing Image")]
    public class GlowingImage : Image
    {
        private const string ShaderName = "RooseLabs/UI/UI-Glow";
        private static readonly int ShaderPropUVRect = Shader.PropertyToID("_UVRect");
        private static readonly int ShaderPropGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int ShaderPropGlowWidth = Shader.PropertyToID("_GlowWidth");
        private static readonly int ShaderPropGlowIntensity = Shader.PropertyToID("_GlowIntensity");
        private static readonly int ShaderPropUseColorAlpha = Shader.PropertyToID("_UseColorAlphaForGlow");

        [SerializeField, Range(0, 100)]
        private float glowWidth = 10f;

        [SerializeField, ColorUsage(false, true)]
        private Color glowColor = Color.white;

        [SerializeField, Range(0, 100)]
        private float glowIntensity = 1f;

        [SerializeField]
        private bool useColorAlphaForGlow = false;

        private Material m_customMaterial = null;

        public Color GlowColor
        {
            get => glowColor;
            set
            {
                if (glowColor == value) return;
                glowColor = value;
                UpdateGlowProperties();
            }
        }

        public float GlowWidth
        {
            get => glowWidth;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(glowWidth, clamped)) return;
                glowWidth = clamped;
                UpdateGlowProperties();
            }
        }

        public float GlowIntensity
        {
            get => glowIntensity;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(glowIntensity, clamped)) return;
                glowIntensity = clamped;
                UpdateGlowProperties();
            }
        }

        public bool UseColorAlphaForGlow
        {
            get => useColorAlphaForGlow;
            set
            {
                if (useColorAlphaForGlow == value) return;
                useColorAlphaForGlow = value;
                UpdateGlowProperties();
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                m_customMaterial ??= CreateGlowMaterial();
                return m_customMaterial;
            }
        }

        private Material CreateGlowMaterial()
        {
            Shader shader = Shader.Find(ShaderName);

            if (shader == null)
            {
                Debug.LogError($"[GlowingSprite] Shader '{ShaderName}' not found. Ensure the shader is included in the build.", this);
                return base.defaultMaterial;
            }

            Material mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            InitializeMaterialProperties(mat);
            return mat;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            UpdateMaterialProperties();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Clean up the created material to prevent memory leaks
            if (m_customMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_customMaterial);
                else
                    DestroyImmediate(m_customMaterial);
                m_customMaterial = null;
            }
        }

        public override void SetMaterialDirty()
        {
            base.SetMaterialDirty();
            UpdateMaterialProperties();
        }

        private void InitializeMaterialProperties(Material mat)
        {
            SetUVRect(mat);
            SetGlowProperties(mat);
        }

        private void UpdateMaterialProperties()
        {
            if (m_customMaterial == null) return;
            SetUVRect(m_customMaterial);
            SetGlowProperties(m_customMaterial);
        }

        private void SetUVRect(Material mat)
        {
            if (!mat.HasProperty(ShaderPropUVRect)) return;

            if (!sprite || !sprite.packed)
            {
                mat.SetVector(ShaderPropUVRect, new Vector4(0, 0, 1, 1));
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

            mat.SetVector(ShaderPropUVRect, uvRect);
        }

        private void SetGlowProperties(Material mat)
        {
            if (mat.HasProperty(ShaderPropGlowColor))
            {
                mat.SetColor(ShaderPropGlowColor, glowColor.linear);
            }

            if (mat.HasProperty(ShaderPropGlowWidth))
            {
                mat.SetFloat(ShaderPropGlowWidth, glowWidth);
            }

            if (mat.HasProperty(ShaderPropGlowIntensity))
            {
                mat.SetFloat(ShaderPropGlowIntensity, glowIntensity);
            }

            if (mat.HasProperty(ShaderPropUseColorAlpha))
            {
                mat.SetInteger(ShaderPropUseColorAlpha, useColorAlphaForGlow ? 1 : 0);
            }
        }

        private void UpdateGlowProperties()
        {
            if (m_customMaterial) SetGlowProperties(m_customMaterial);
        }

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Force material recreation when properties change in the inspector
            m_customMaterial = null;
            SetMaterialDirty();
        }
        #endif
    }
}
