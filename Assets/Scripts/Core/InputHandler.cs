using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RooseLabs.Core
{
    public class InputHandler : SingletonBehaviour<InputHandler>
    {
        private InputDevice m_currentDevice;

        private InputActionMap m_gameplayActionMap;
        private InputActionMap m_uiActionMap;

        public InputActionMap GameplayActions => m_gameplayActionMap;
        public InputActionMap UIActions => m_uiActionMap;

        #region Event Actions
        public event Action<InputDevice> InputDeviceChanged = delegate { };
        #endregion

        private void Awake()
        {
            DontDestroyOnLoad(this);
            m_gameplayActionMap = InputSystem.actions.FindActionMap("Gameplay");
            m_uiActionMap = InputSystem.actions.FindActionMap("UI");
        }

        private void OnEnable()
        {
            InputSystem.onEvent += OnInputEvent;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnInputEvent;
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            DisableAllInput();
        }

        private void OnInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            // Ignore anything that isn't a state event.
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;
            CurrentDevice = device;
        }

        public void EnableGameplayInput()
        {
            UIActions.Disable();
            GameplayActions.Enable();
            Cursor.visible = false;
        }

        public void EnableMenuInput()
        {
            GameplayActions.Disable();
            UIActions.Enable();
        }

        public void DisableAllInput()
        {
            GameplayActions.Disable();
            UIActions.Disable();
            Cursor.visible = false;
        }

        public InputDevice CurrentDevice
        {
            get => m_currentDevice;
            private set
            {
                // When updating the current device, reset the pointer move time
                // and set the cursor visibility based on the device type.
                if (m_currentDevice != value) InputDeviceChanged.Invoke(value);
                m_currentDevice = value;
                if (value is Gamepad) Cursor.visible = false;
            }
        }

        public bool IsCurrentDeviceKeyboardMouse()
        {
            return m_currentDevice is Keyboard or Pointer;
        }
    }
}
