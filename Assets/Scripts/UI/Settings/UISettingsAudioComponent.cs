using RooseLabs.Settings;
using RooseLabs.UI.Elements;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class UISettingsAudioComponent : MonoBehaviour
    {
        [Header("Settings Items")]
        [SerializeField] private UISlider masterVolumeSetting;
        [SerializeField] private UIStepper microphoneDeviceSetting;
        [SerializeField] private UIStepper pushToTalkSetting;

        [Header("Buttons")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button resetButton;

        private MasterVolumeSetting m_masterVolume;
        private MicrophoneDeviceSetting m_microphoneDevice;
        private PushToTalkSetting m_pushToTalk;

        private void OnEnable()
        {
            // Cache setting references
            m_masterVolume = SettingsHandler.GetSetting<MasterVolumeSetting>();
            m_microphoneDevice = SettingsHandler.GetSetting<MicrophoneDeviceSetting>();
            m_pushToTalk = SettingsHandler.GetSetting<PushToTalkSetting>();

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
            // Master Volume
            masterVolumeSetting.SetRange(m_masterVolume.ExposedMinValue, m_masterVolume.ExposedMaxValue);
            masterVolumeSetting.SetPrecision(m_masterVolume.Precision);
            masterVolumeSetting.SetCustomFormatter(m_masterVolume.FormatValue);

            // Microphone Device
            microphoneDeviceSetting.SetOptions(m_microphoneDevice.GetChoices());

            // Push To Talk
            pushToTalkSetting.SetOptions(m_pushToTalk.GetChoices());
        }

        private void LoadCurrentValues()
        {
            masterVolumeSetting.SetValue(m_masterVolume.GetValue());
            microphoneDeviceSetting.SetSelectedIndex(m_microphoneDevice.GetValue(), false);
            pushToTalkSetting.SetSelectedIndex((int)m_pushToTalk.GetValue(), false);
        }

        private void ApplySettings()
        {
            // Apply and save Master Volume
            m_masterVolume.ApplyValue(masterVolumeSetting.Value);
            m_masterVolume.Save();

            // Apply and save Microphone Device
            m_microphoneDevice.ApplyValue(microphoneDeviceSetting.SelectedIndex);
            m_microphoneDevice.Save();

            // Apply and save Push To Talk
            m_pushToTalk.ApplyValue((PushToTalkMode)pushToTalkSetting.SelectedIndex);
            m_pushToTalk.Save();
        }

        private void ResetSettings()
        {
            masterVolumeSetting.SetValue(m_masterVolume.GetDefaultValue());
            microphoneDeviceSetting.SetSelectedIndex(m_microphoneDevice.GetDefaultValue(), false);
            pushToTalkSetting.SetSelectedIndex((int)m_pushToTalk.GetDefaultValue(), false);
            ApplySettings();
        }
    }
}
