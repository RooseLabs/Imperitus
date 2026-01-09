using RooseLabs.Settings;
using RooseLabs.UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class UISettingsScreenComponent : MonoBehaviour
    {
        [Header("Setting Items")]
        [SerializeField] private UIStepper resolutionSetting;
        [SerializeField] private UIStepper windowModeSetting;
        [SerializeField] private UIStepper vSyncSetting;
        [SerializeField] private UISlider frameRateLimitSetting;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;

        private ResolutionSetting m_resolution;
        private WindowModeSetting m_windowMode;
        private VSyncSetting m_vSync;
        private FrameRateLimitSetting m_frameRateLimit;

        private void OnEnable()
        {
            // Cache setting references
            m_resolution = SettingsHandler.GetSetting<ResolutionSetting>();
            m_windowMode = SettingsHandler.GetSetting<WindowModeSetting>();
            m_vSync = SettingsHandler.GetSetting<VSyncSetting>();
            m_frameRateLimit = SettingsHandler.GetSetting<FrameRateLimitSetting>();

            InitializeSettings();
            LoadCurrentValues();

            applyButton.onClick.AddListener(ApplySettings);
            resetButton.onClick.AddListener(ResetSettings);
            vSyncSetting.OnSelectionChanged += OnVSyncChanged;

            // Set initial frame limit interactability based on VSync state
            UpdateFrameLimitInteractability();
        }

        private void OnDisable()
        {
            applyButton.onClick.RemoveListener(ApplySettings);
            resetButton.onClick.RemoveListener(ResetSettings);
            vSyncSetting.OnSelectionChanged -= OnVSyncChanged;
        }

        private void InitializeSettings()
        {
            // Resolution
            resolutionSetting.SetOptions(m_resolution.GetChoices());

            // Window Mode
            windowModeSetting.SetOptions(m_windowMode.GetChoices());

            // VSync
            vSyncSetting.SetOptions(m_vSync.GetChoices());

            // Frame Rate Limit
            frameRateLimitSetting.SetRange(m_frameRateLimit.ExposedMinValue, m_frameRateLimit.ExposedMaxValue);
            frameRateLimitSetting.SetCustomFormatter(m_frameRateLimit.FormatValue);
        }

        private void LoadCurrentValues()
        {
            resolutionSetting.SetSelectedIndex(m_resolution.GetValue(), false);
            windowModeSetting.SetSelectedIndex((int)m_windowMode.GetValue(), false);
            vSyncSetting.SetSelectedIndex(m_vSync.GetValue() ? 1 : 0, false);
            frameRateLimitSetting.SetValue(m_frameRateLimit.GetValue());
        }

        private void ApplySettings()
        {
            // Apply and save Resolution
            m_resolution.ApplyValue(resolutionSetting.SelectedIndex);
            m_resolution.Save();

            // Apply and save Window Mode
            m_windowMode.ApplyValue((WindowMode)windowModeSetting.SelectedIndex);
            m_windowMode.Save();

            // Apply and save VSync
            m_vSync.ApplyValue(vSyncSetting.SelectedIndex == 1);
            m_vSync.Save();

            // Apply and save Frame Rate Limit
            m_frameRateLimit.ApplyValue(frameRateLimitSetting.Value);
            m_frameRateLimit.Save();
        }

        private void OnVSyncChanged(int index)
        {
            UpdateFrameLimitInteractability();
        }

        private void UpdateFrameLimitInteractability()
        {
            // Frame limit slider is only interactable when VSync is Off
            bool vSyncEnabled = vSyncSetting.SelectedIndex == 1;
            frameRateLimitSetting.Interactable = !vSyncEnabled;
            frameRateLimitSetting.SetValue(m_frameRateLimit.GetValue());
        }

        private void ResetSettings()
        {
            resolutionSetting.SetSelectedIndex(m_resolution.GetDefaultValue(), false);
            windowModeSetting.SetSelectedIndex((int)m_windowMode.GetDefaultValue(), false);
            vSyncSetting.SetSelectedIndex(m_vSync.GetDefaultValue() ? 1 : 0, false);
            frameRateLimitSetting.SetValue(m_frameRateLimit.GetDefaultValue());
            ApplySettings();
        }
    }
}
