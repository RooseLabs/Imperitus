using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Switch;
using UnityEngine.InputSystem.XInput;

namespace RooseLabs.Core
{
    public class InputHandler : SingletonBehaviour<InputHandler>
    {
        private InputDevice m_currentDevice;
        private InputScheme m_currentScheme;
        private GamepadType m_gamepadType;

        public static InputDevice CurrentInputDevice => Instance.m_currentDevice;
        public static InputScheme CurrentInputScheme => Instance.m_currentScheme;
        public static GamepadType GamepadType => Instance.m_gamepadType;

        private InputActionMap m_gameplayActionMap;
        private InputActionMap m_uiActionMap;

        public static InputActionMap GameplayActions => Instance.m_gameplayActionMap;
        public static InputActionMap UIActions => Instance.m_uiActionMap;

        #region Event Actions
        public event Action<InputDevice> InputDeviceChanged = delegate { };
        public event Action<InputScheme> InputSchemeChanged = delegate { };
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

            if (m_currentDevice != device)
            {
                m_currentDevice = device;
                InputScheme scheme = InputScheme.Unknown;
                switch (device)
                {
                    case Keyboard or Pointer:
                        scheme = InputScheme.KeyboardMouse;
                        break;
                    case Gamepad:
                        scheme = InputScheme.Gamepad;
                        m_gamepadType = GetGamepadType(device);
                        break;
                }
                if (m_currentScheme != scheme)
                {
                    m_currentScheme = scheme;
                    InputSchemeChanged.Invoke(scheme);
                }
                InputDeviceChanged.Invoke(device);
            }
        }

        public void EnableGameplayInput()
        {
            UIActions.Disable();
            GameplayActions.Enable();
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        public void EnableMenuInput()
        {
            GameplayActions.Disable();
            UIActions.Enable();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = m_currentScheme == InputScheme.KeyboardMouse;
        }

        public void DisableAllInput()
        {
            GameplayActions.Disable();
            UIActions.Disable();
            Cursor.visible = false;
        }

        public static GamepadType GetGamepadType(InputDevice device)
        {
            return device switch
            {
                XInputController => GamepadType.Xbox,
                DualSenseGamepadHID => GamepadType.DualSense,
                DualShockGamepad => GamepadType.DualShock,
                SwitchProControllerHID => GamepadType.SwitchPro,
                _ => GamepadType.Unknown
            };
        }
    }
}
