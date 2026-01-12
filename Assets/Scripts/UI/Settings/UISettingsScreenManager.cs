using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class UISettingsScreenManager : MonoBehaviour, IWindow
    {
        [SerializeField] private Button closeButton;

        [Header("Components")]
        [SerializeField] private UISettingsAudioComponent audioComponent;
        [SerializeField] private UISettingsScreenComponent screenComponent;
        [SerializeField] private UISettingsGraphicsComponent graphicsComponent;

        [Header("Tabs")]
        [SerializeField] private Elements.Toggle audioTabButton;
        [SerializeField] private Elements.Toggle screenTabButton;
        [SerializeField] private Elements.Toggle graphicsTabButton;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference nextTabAction;
        [SerializeField] private InputActionReference previousTabAction;

        private Elements.Toggle[] m_allTabs;
        private GameObject[] m_allComponents;
        private int m_currentTabIndex;

        public event UnityAction OnCloseButtonPressed
        {
            add => closeButton.onClick.AddListener(value);
            remove => closeButton.onClick.RemoveListener(value);
        }

        public event Action OnClosed = delegate {};

        private void Awake()
        {
            m_allTabs = new[] { audioTabButton, screenTabButton, graphicsTabButton };
            m_allComponents = new[] { audioComponent.gameObject, screenComponent.gameObject, graphicsComponent.gameObject };
        }

        private void OnEnable()
        {
            audioTabButton.onValueChanged.AddListener(OnTabToggled);
            screenTabButton.onValueChanged.AddListener(OnTabToggled);
            graphicsTabButton.onValueChanged.AddListener(OnTabToggled);

            // Default to first tab
            SelectTab(0);
        }

        private void OnDisable()
        {
            audioTabButton.onValueChanged.RemoveListener(OnTabToggled);
            screenTabButton.onValueChanged.RemoveListener(OnTabToggled);
            graphicsTabButton.onValueChanged.RemoveListener(OnTabToggled);

            OnClosed.Invoke();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (nextTabAction?.action?.WasPerformedThisFrame() ?? false)
            {
                SelectNextTab();
            }

            if (previousTabAction?.action?.WasPerformedThisFrame() ?? false)
            {
                SelectPreviousTab();
            }
        }

        private void SelectNextTab()
        {
            int nextTabIndex = m_currentTabIndex + 1;
            if (nextTabIndex < m_allTabs.Length)
            {
                SelectTab(nextTabIndex);
            }
        }

        private void SelectPreviousTab()
        {
            int previousTabIndex = m_currentTabIndex - 1;
            if (previousTabIndex >= 0)
            {
                SelectTab(previousTabIndex);
            }
        }

        private void OnTabToggled(bool isOn)
        {
            if (!isOn) return;

            // Find which tab was toggled on
            for (int i = 0; i < m_allTabs.Length; i++)
            {
                // Checking here that it's not the current tab is what makes this work correctly
                // Because only one tab can be on at a time, the first one we find that is on that is not the
                // current tab must be the one that was just toggled
                if (m_allTabs[i].IsOn && i != m_currentTabIndex)
                {
                    SelectTab(i);
                    return;
                }
            }
        }

        private void SelectTab(int tabIndex)
        {
            m_currentTabIndex = tabIndex;

            // Untoggle all tabs
            for (int i = 0; i < m_allTabs.Length; i++)
            {
                m_allTabs[i].SetIsOn(i == tabIndex, false);
            }

            // Show only the selected component
            for (int i = 0; i < m_allComponents.Length; i++)
            {
                m_allComponents[i].SetActive(i == tabIndex);
            }
        }
    }
}
