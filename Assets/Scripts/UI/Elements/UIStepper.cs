using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    public class UIStepper : MonoBehaviour
    {
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text selectedOptionText;
        [SerializeField] private bool wrapAround = false;

        private string[] m_options = Array.Empty<string>();

        public int SelectedIndex { get; private set; }

        public event Action<int> OnSelectionChanged = delegate {};

        private void OnEnable()
        {
            prevButton.onClick.AddListener(PreviousOption);
            nextButton.onClick.AddListener(NextOption);
        }

        private void OnDisable()
        {
            prevButton.onClick.RemoveAllListeners();
            nextButton.onClick.RemoveAllListeners();
        }

        public void SetOptions(string[] options)
        {
            m_options = options ?? Array.Empty<string>();
            SelectedIndex = 0;
            UpdateDisplayText();
        }

        public void SetOptions<TEnum>() where TEnum : Enum
        {
            m_options = Enum.GetNames(typeof(TEnum));
            SelectedIndex = 0;
            UpdateDisplayText();
        }

        public void SetSelectedIndex(int index, bool notify = true)
        {
            if (m_options.Length == 0) return;
            int newIndex = Mathf.Clamp(index, 0, m_options.Length - 1);
            if (newIndex == SelectedIndex) return;
            SelectedIndex = newIndex;
            UpdateDisplayText();
            if (notify)
            {
                OnSelectionChanged?.Invoke(SelectedIndex);
            }
        }

        private void PreviousOption()
        {
            if (m_options.Length == 0) return;

            int newIndex = SelectedIndex - 1;
            if (newIndex < 0)
            {
                newIndex = wrapAround ? m_options.Length - 1 : 0;
            }

            if (newIndex != SelectedIndex)
            {
                SelectedIndex = newIndex;
                UpdateDisplayText();
                OnSelectionChanged?.Invoke(SelectedIndex);
            }
        }

        private void NextOption()
        {
            if (m_options.Length == 0) return;

            int newIndex = SelectedIndex + 1;
            if (newIndex >= m_options.Length)
            {
                newIndex = wrapAround ? 0 : m_options.Length - 1;
            }

            if (newIndex != SelectedIndex)
            {
                SelectedIndex = newIndex;
                UpdateDisplayText();
                OnSelectionChanged?.Invoke(SelectedIndex);
            }
        }

        private void UpdateDisplayText()
        {
            if (m_options.Length > 0 && SelectedIndex < m_options.Length)
            {
                selectedOptionText.text = m_options[SelectedIndex];
            }
            else
            {
                selectedOptionText.text = string.Empty;
            }
        }

        public string DisplayText
        {
            get => selectedOptionText.text;
            set => selectedOptionText.text = value;
        }
    }
}
