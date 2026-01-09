using System;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Button screenTabButton;
        [SerializeField] private Button graphicsTabButton;

        public event UnityAction OnCloseButtonPressed
        {
            add => closeButton.onClick.AddListener(value);
            remove => closeButton.onClick.RemoveListener(value);
        }

        public event Action OnClose = delegate {};

        private void OnEnable()
        {
            audioTabButton.onClick.AddListener(ShowAudioTab);
            screenTabButton.onClick.AddListener(ShowScreenTab);
            graphicsTabButton.onClick.AddListener(ShowGraphicsTab);

            // Default to first tab
            ShowAudioTab();
        }

        private void OnDisable()
        {
            audioTabButton.onClick.RemoveListener(ShowAudioTab);
            screenTabButton.onClick.RemoveListener(ShowScreenTab);
            graphicsTabButton.onClick.RemoveListener(ShowGraphicsTab);
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            closeButton.onClick.RemoveAllListeners();
            gameObject.SetActive(false);
            OnClose.Invoke();
        }

        private void ShowAudioTab()
        {
            audioComponent.gameObject.SetActive(true);
            screenComponent.gameObject.SetActive(false);
            graphicsComponent.gameObject.SetActive(false);
        }

        private void ShowScreenTab()
        {
            audioComponent.gameObject.SetActive(false);
            screenComponent.gameObject.SetActive(true);
            graphicsComponent.gameObject.SetActive(false);
        }

        private void ShowGraphicsTab()
        {
            audioComponent.gameObject.SetActive(false);
            screenComponent.gameObject.SetActive(false);
            graphicsComponent.gameObject.SetActive(true);
        }
    }
}
