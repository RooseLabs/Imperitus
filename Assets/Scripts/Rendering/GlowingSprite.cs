using UnityEngine;

namespace RooseLabs.Rendering
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class GlowingSprite : MonoBehaviour
    {
        private static readonly int ShaderPropUVRect = Shader.PropertyToID("_UVRect");
        private static readonly int ShaderPropGlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int ShaderPropGlowWidth = Shader.PropertyToID("_GlowWidth");
        private static readonly int ShaderPropGlowIntensity = Shader.PropertyToID("_GlowIntensity");

        [SerializeField] private Material glowMaterial;

        private float m_glowWidth = 10f;
        private Color m_glowColor = Color.white;
        private float m_glowIntensity = 1f;

        private SpriteRenderer m_spriteRenderer;
        private MaterialPropertyBlock m_propertyBlock;

        public Color GlowColor
        {
            get => m_glowColor;
            set
            {
                if (m_glowColor == value) return;
                m_glowColor = value;
                UpdateGlowProperties();
            }
        }

        public float GlowWidth
        {
            get => m_glowWidth;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_glowWidth, clamped)) return;
                m_glowWidth = clamped;
                UpdateGlowProperties();
            }
        }

        public float GlowIntensity
        {
            get => m_glowIntensity;
            set
            {
                var clamped = Mathf.Max(0f, value);
                if (Mathf.Approximately(m_glowIntensity, clamped)) return;
                m_glowIntensity = clamped;
                UpdateGlowProperties();
            }
        }

        public void SetMaterial(Material material)
        {
            if (!material)
            {
                Debug.LogWarning($"[GlowingSprite] Attempted to set null material on '{gameObject.name}'.", this);
                return;
            }

            glowMaterial = material;

            if (m_spriteRenderer)
            {
                m_spriteRenderer.sharedMaterial = material;
                ReadPropertiesFromMaterial(material);
                UpdateUVRect(m_spriteRenderer);
            }
        }

        private void ReadPropertiesFromMaterial(Material material)
        {
            // Read properties from the material if they exist
            if (material.HasProperty(ShaderPropGlowColor))
                m_glowColor = material.GetColor(ShaderPropGlowColor);

            if (material.HasProperty(ShaderPropGlowWidth))
                m_glowWidth = material.GetFloat(ShaderPropGlowWidth);

            if (material.HasProperty(ShaderPropGlowIntensity))
                m_glowIntensity = material.GetFloat(ShaderPropGlowIntensity);
        }

        private void Awake()
        {
            m_spriteRenderer = GetComponent<SpriteRenderer>();
            m_propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            if (glowMaterial) SetMaterial(glowMaterial);
            m_spriteRenderer?.RegisterSpriteChangeCallback(UpdateUVRect);
        }

        private void OnDisable()
        {
            m_spriteRenderer?.UnregisterSpriteChangeCallback(UpdateUVRect);
        }

        private void UpdateUVRect(SpriteRenderer spriteRenderer)
        {
            if (!spriteRenderer || m_propertyBlock == null) return;

            Sprite sprite = spriteRenderer.sprite;

            if (!sprite || !sprite.packed)
            {
                m_propertyBlock.SetVector(ShaderPropUVRect, new Vector4(0, 0, 1, 1));
            }
            else
            {
                Texture2D texture = sprite.texture;
                Rect rect = sprite.textureRect;

                // Convert pixel rect to UV (0-1) space
                Vector4 uvRect = new Vector4(
                    rect.x / texture.width,                     // min X
                    rect.y / texture.height,                    // min Y
                    (rect.x + rect.width) / texture.width,      // max X
                    (rect.y + rect.height) / texture.height  // max Y
                );

                m_propertyBlock.SetVector(ShaderPropUVRect, uvRect);
            }

            spriteRenderer.SetPropertyBlock(m_propertyBlock);
        }

        private void UpdateGlowProperties()
        {
            if (!m_spriteRenderer || m_propertyBlock == null) return;

            // Get existing property block values
            m_spriteRenderer.GetPropertyBlock(m_propertyBlock);

            m_propertyBlock.SetColor(ShaderPropGlowColor, m_glowColor);
            m_propertyBlock.SetFloat(ShaderPropGlowWidth, m_glowWidth);
            m_propertyBlock.SetFloat(ShaderPropGlowIntensity, m_glowIntensity);

            m_spriteRenderer.SetPropertyBlock(m_propertyBlock);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying && m_spriteRenderer == null)
            {
                m_spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (!m_spriteRenderer) return;

            m_propertyBlock ??= new MaterialPropertyBlock();
            if (glowMaterial) SetMaterial(glowMaterial);
        }
        #endif
    }
}
