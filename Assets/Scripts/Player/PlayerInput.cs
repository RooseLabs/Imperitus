using RooseLabs.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.Player
{
    public class PlayerInput : MonoBehaviour
    {
        private InputActionMap m_gameplayActionMap;
        private InputAction m_actionMove;
        private InputAction m_actionLook;
        private InputAction m_actionJump;
        private InputAction m_actionSprint;

        public Vector2 movementInput;
        public Vector2 lookInput;
        public bool jumpWasPressed;
        public bool sprintIsPressed;

        private void Awake()
        {
            m_gameplayActionMap = InputHandler.Instance.GameplayActions;
            m_actionMove = m_gameplayActionMap.FindAction("Move");
            m_actionLook = m_gameplayActionMap.FindAction("Look");
            m_actionJump = m_gameplayActionMap.FindAction("Jump");
            m_actionSprint = m_gameplayActionMap.FindAction("Sprint");
        }

        public void Sample()
        {
            ResetInput();
            movementInput = m_actionMove.ReadValue<Vector2>();
            lookInput = m_actionLook.ReadValue<Vector2>();
            jumpWasPressed = m_actionJump.WasPressedThisFrame();
            sprintIsPressed = m_actionSprint.IsPressed();
        }

        private void ResetInput()
        {
            movementInput = Vector2.zero;
            lookInput = Vector2.zero;
            jumpWasPressed = false;
            sprintIsPressed = false;
        }
    }
}
