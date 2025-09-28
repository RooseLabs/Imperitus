using RooseLabs.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.Player
{
    public class PlayerInput : MonoBehaviour
    {
        private InputActionMap m_gameplayActionMap;
        private InputAction m_actionPause;
        private InputAction m_actionMove;
        private InputAction m_actionLook;
        private InputAction m_actionAim;
        private InputAction m_actionAttack;
        private InputAction m_actionCrouch;
        private InputAction m_actionSprint;
        private InputAction m_actionJump;
        private InputAction m_actionInteract;
        private InputAction m_actionDrop;
        private InputAction m_actionPrevious;
        private InputAction m_actionNext;
        private InputAction m_actionPushToTalk;

        public bool pauseWasPressed;
        public Vector2 movementInput;
        public Vector2 lookInput;
        public bool aimIsPressed;
        public bool attackWasPressed;
        public bool attackIsPressed;
        public bool crouchWasPressed;
        public bool crouchIsPressed;
        public bool sprintWasPressed;
        public bool sprintIsPressed;
        public bool jumpWasPressed;
        public bool interactWasPressed;
        public bool dropWasPressed;
        public bool previousWasPressed;
        public bool nextWasPressed;
        public bool pushToTalkIsPressed;

        private void Awake()
        {
            m_gameplayActionMap = InputHandler.Instance.GameplayActions;
            m_actionPause = m_gameplayActionMap.FindAction("Pause");
            m_actionMove = m_gameplayActionMap.FindAction("Move");
            m_actionLook = m_gameplayActionMap.FindAction("Look");
            m_actionAim = m_gameplayActionMap.FindAction("Aim");
            m_actionAttack = m_gameplayActionMap.FindAction("Attack");
            m_actionCrouch = m_gameplayActionMap.FindAction("Crouch");
            m_actionSprint = m_gameplayActionMap.FindAction("Sprint");
            m_actionJump = m_gameplayActionMap.FindAction("Jump");
            m_actionInteract = m_gameplayActionMap.FindAction("Interact");
            m_actionDrop = m_gameplayActionMap.FindAction("Drop");
            m_actionPrevious = m_gameplayActionMap.FindAction("Previous");
            m_actionNext = m_gameplayActionMap.FindAction("Next");
            m_actionPushToTalk = m_gameplayActionMap.FindAction("PushToTalk");
        }

        public void Sample()
        {
            ResetInput();
            pauseWasPressed = m_actionPause.WasPressedThisFrame();
            movementInput = m_actionMove.ReadValue<Vector2>();
            lookInput = m_actionLook.ReadValue<Vector2>();
            aimIsPressed = m_actionAim.IsPressed();
            attackWasPressed = m_actionAttack.WasPressedThisFrame();
            attackIsPressed = m_actionAttack.IsPressed();
            crouchWasPressed = m_actionCrouch.WasPressedThisFrame();
            crouchIsPressed = m_actionCrouch.IsPressed();
            sprintWasPressed = m_actionSprint.WasPressedThisFrame();
            sprintIsPressed = m_actionSprint.IsPressed();
            jumpWasPressed = m_actionJump.WasPressedThisFrame();
            interactWasPressed = m_actionInteract.WasPressedThisFrame();
            dropWasPressed = m_actionDrop.WasPressedThisFrame();
            previousWasPressed = m_actionPrevious.WasPressedThisFrame();
            nextWasPressed = m_actionNext.WasPressedThisFrame();
            pushToTalkIsPressed = m_actionPushToTalk.IsPressed();
        }

        private void ResetInput()
        {
            pauseWasPressed = false;
            movementInput = Vector2.zero;
            lookInput = Vector2.zero;
            aimIsPressed = false;
            attackWasPressed = false;
            attackIsPressed = false;
            crouchWasPressed = false;
            crouchIsPressed = false;
            sprintWasPressed = false;
            sprintIsPressed = false;
            jumpWasPressed = false;
            interactWasPressed = false;
            dropWasPressed = false;
            previousWasPressed = false;
            nextWasPressed = false;
            pushToTalkIsPressed = false;
        }
    }
}
