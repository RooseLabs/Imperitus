using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    public class UISlider : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private TMP_Text sliderValueText;
        [SerializeField] private int decimalPlaces;

        private Func<float, string> m_customFormatter;

        public event Action<float> OnValueChanged = delegate {};

        public float Value => slider.value;

        public bool Interactable
        {
            get => slider.interactable;
            set => slider.interactable = value;
        }

        private void OnEnable()
        {
            slider.wholeNumbers = decimalPlaces == 0;
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        private void OnDisable()
        {
            slider.onValueChanged.RemoveAllListeners();
        }

        private void Start()
        {
            float roundedValue = decimalPlaces == 0 ? slider.value : (float)Math.Round(slider.value, decimalPlaces);
            SetValueText(roundedValue);
        }

        public void SetValue(float value)
        {
            float roundedValue = decimalPlaces == 0 ? value : (float)Math.Round(value, decimalPlaces);
            slider.SetValueWithoutNotify(Mathf.Clamp(roundedValue, slider.minValue, slider.maxValue));
            SetValueText(roundedValue);
        }

        public void SetRange(float min, float max)
        {
            slider.minValue = min;
            slider.maxValue = max;
        }

        public void SetCustomFormatter(Func<float, string> formatter)
        {
            m_customFormatter = formatter;
            SetValueText(slider.value);
        }

        private void OnSliderValueChanged(float value)
        {
            float roundedValue = decimalPlaces == 0 ? value : (float)Math.Round(value, decimalPlaces);
            SetValueText(roundedValue);
            OnValueChanged.Invoke(roundedValue);
            slider.SetValueWithoutNotify(roundedValue);
        }

        private void SetValueText(float value)
        {
            sliderValueText.text = m_customFormatter != null
                ? m_customFormatter(value)
                : value.ToString(decimalPlaces == 0 ? "F0" : $"F{decimalPlaces}");
        }
    }
}
