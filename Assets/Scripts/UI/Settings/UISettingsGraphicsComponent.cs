using RooseLabs.Settings;
using RooseLabs.UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class UISettingsGraphicsComponent : MonoBehaviour
    {
        [Header("Settings Items")]
        [SerializeField] private UIStepper textureQualitySetting;
        [SerializeField] private UIStepper antiAliasingSetting;
        [SerializeField] private UISlider renderScaleSetting;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;

        private TextureQualitySetting m_textureQuality;
        private AntiAliasingSetting m_antiAliasing;
        private RenderScaleSetting m_renderScale;

        private void OnEnable()
        {
            // Cache setting references
            m_textureQuality = SettingsHandler.GetSetting<TextureQualitySetting>();
            m_antiAliasing = SettingsHandler.GetSetting<AntiAliasingSetting>();
            m_renderScale = SettingsHandler.GetSetting<RenderScaleSetting>();

            InitializeSettings();
            LoadCurrentValues();

            applyButton.onClick.AddListener(ApplySettings);
            resetButton.onClick.AddListener(ResetSettings);
        }

        private void OnDisable()
        {
            applyButton.onClick.RemoveListener(ApplySettings);
            resetButton.onClick.RemoveListener(ResetSettings);
        }

        private void InitializeSettings()
        {
            // Texture Quality
            textureQualitySetting.SetOptions(m_textureQuality.GetChoices());

            // Anti-Aliasing
            antiAliasingSetting.SetOptions(m_antiAliasing.GetChoices());

            // Render Scale
            renderScaleSetting.SetRange(m_renderScale.ExposedMinValue, m_renderScale.ExposedMaxValue);
            renderScaleSetting.SetPrecision(m_renderScale.Precision);
            renderScaleSetting.SetCustomFormatter(m_renderScale.FormatValue);
        }

        private void LoadCurrentValues()
        {
            textureQualitySetting.SetSelectedIndex((int)m_textureQuality.GetValue(), false);
            antiAliasingSetting.SetSelectedIndex((int)m_antiAliasing.GetValue(), false);
            renderScaleSetting.SetValue(m_renderScale.GetValue());
        }

        private void ApplySettings()
        {
            // Apply and save Texture Quality
            m_textureQuality.ApplyValue((TextureQuality)textureQualitySetting.SelectedIndex);
            m_textureQuality.Save();

            // Apply and save Anti-Aliasing
            m_antiAliasing.ApplyValue((AntiAliasing)antiAliasingSetting.SelectedIndex);
            m_antiAliasing.Save();

            // Apply and save Render Scale
            m_renderScale.ApplyValue(renderScaleSetting.Value);
            m_renderScale.Save();
        }

        private void ResetSettings()
        {
            textureQualitySetting.SetSelectedIndex((int)m_textureQuality.GetDefaultValue(), false);
            antiAliasingSetting.SetSelectedIndex((int)m_antiAliasing.GetDefaultValue(), false);
            renderScaleSetting.SetValue(m_renderScale.GetDefaultValue());
            ApplySettings();
        }
    }
}
