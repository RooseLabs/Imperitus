using System;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    public class UIConfirmPanel : MonoBehaviour
    {
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        public event Action OnConfirmButtonPressed = delegate {};
        public event Action OnCancelButtonPressed = delegate {};

        private void OnEnable()
        {
            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            confirmButton.onClick.RemoveAllListeners();
            cancelButton.onClick.RemoveAllListeners();
        }

        private void Confirm()
        {
            OnConfirmButtonPressed.Invoke();
        }

        private void Cancel()
        {
            OnCancelButtonPressed.Invoke();
        }
    }
}
