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
        private InputAction m_actionCast;
        private InputAction m_actionCrouch;
        private InputAction m_actionSprint;
        private InputAction m_actionJump;
        private InputAction m_actionInteract;
        private InputAction m_actionDrop;
        private InputAction m_actionPrevious;
        private InputAction m_actionNext;
        private InputAction m_actionScroll;
        private InputAction m_actionScrollButton;
        private InputAction m_actionScrollBackward;
        private InputAction m_actionScrollForward;
        private InputAction m_actionPushToTalk;
        private InputAction m_actionOpenNotebook;

        public bool pauseWasPressed;
        public Vector2 movementInput;
        public Vector2 lookInput;
        public bool aimIsPressed;
        public bool castWasPressed;
        public bool castIsPressed;
        public bool castWasReleased;
        public bool crouchWasPressed;
        public bool crouchIsPressed;
        public bool sprintWasPressed;
        public bool sprintIsPressed;
        public bool jumpWasPressed;
        public bool interactWasPressed;
        public bool dropWasPressed;
        public bool previousWasPressed;
        public bool previousIsPressed;
        public bool nextWasPressed;
        public bool nextIsPressed;
        public float scrollInput;
        public bool scrollButtonWasPressed;
        public bool scrollBackwardWasPressed;
        public bool scrollBackwardIsPressed;
        public bool scrollForwardWasPressed;
        public bool scrollForwardIsPressed;
        public bool pushToTalkIsPressed;
        public bool openNotebookWasPressed;

        private bool m_isNotebookOpen = false;

        private void Awake()
        {
            m_gameplayActionMap = InputHandler.Instance.GameplayActions;
            m_actionPause = m_gameplayActionMap.FindAction("Pause");
            m_actionMove = m_gameplayActionMap.FindAction("Move");
            m_actionLook = m_gameplayActionMap.FindAction("Look");
            m_actionAim = m_gameplayActionMap.FindAction("Aim");
            m_actionCast = m_gameplayActionMap.FindAction("Cast");
            m_actionCrouch = m_gameplayActionMap.FindAction("Crouch");
            m_actionSprint = m_gameplayActionMap.FindAction("Sprint");
            m_actionJump = m_gameplayActionMap.FindAction("Jump");
            m_actionInteract = m_gameplayActionMap.FindAction("Interact");
            m_actionDrop = m_gameplayActionMap.FindAction("Drop");
            m_actionPrevious = m_gameplayActionMap.FindAction("Previous");
            m_actionNext = m_gameplayActionMap.FindAction("Next");
            m_actionScroll = m_gameplayActionMap.FindAction("Scroll");
            m_actionScrollButton = m_gameplayActionMap.FindAction("ScrollButton");
            m_actionScrollBackward = m_gameplayActionMap.FindAction("ScrollBackward");
            m_actionScrollForward = m_gameplayActionMap.FindAction("ScrollForward");
            m_actionPushToTalk = m_gameplayActionMap.FindAction("PushToTalk");
            m_actionOpenNotebook = m_gameplayActionMap.FindAction("OpenNotebook");
        }

        /// <summary>
        /// Set whether the notebook is open. When open, movement and look inputs are blocked.
        /// </summary>
        public void SetNotebookOpen(bool isOpen)
        {
            m_isNotebookOpen = isOpen;
        }

        public void Sample()
        {
            ResetInput();
            pauseWasPressed = m_actionPause.WasPressedThisFrame();

            // Always allow notebook toggle
            openNotebookWasPressed = m_actionOpenNotebook.WasPressedThisFrame();

            // Block gameplay inputs when notebook is open
            if (!m_isNotebookOpen)
            {
                movementInput = m_actionMove.ReadValue<Vector2>();
                lookInput = m_actionLook.ReadValue<Vector2>();
                aimIsPressed = m_actionAim.IsPressed();
                castWasPressed = m_actionCast.WasPressedThisFrame();
                castIsPressed = m_actionCast.IsPressed();
                castWasReleased = m_actionCast.WasReleasedThisFrame();
                crouchWasPressed = m_actionCrouch.WasPressedThisFrame();
                crouchIsPressed = m_actionCrouch.IsPressed();
                sprintWasPressed = m_actionSprint.WasPressedThisFrame();
                sprintIsPressed = m_actionSprint.IsPressed();
                jumpWasPressed = m_actionJump.WasPressedThisFrame();
                interactWasPressed = m_actionInteract.WasPressedThisFrame();
                dropWasPressed = m_actionDrop.WasPressedThisFrame();
                previousWasPressed = m_actionPrevious.WasPressedThisFrame();
                previousIsPressed = m_actionPrevious.IsPressed();
                nextWasPressed = m_actionNext.WasPressedThisFrame();
                nextIsPressed = m_actionNext.IsPressed();
                scrollInput = m_actionScroll.ReadValue<float>();
                scrollButtonWasPressed = m_actionScrollButton.WasPressedThisFrame();
                scrollBackwardWasPressed = m_actionScrollBackward.WasPressedThisFrame();
                scrollBackwardIsPressed = m_actionScrollBackward.IsPressed();
                scrollForwardWasPressed = m_actionScrollForward.WasPressedThisFrame();
                scrollForwardIsPressed = m_actionScrollForward.IsPressed();
                pushToTalkIsPressed = m_actionPushToTalk.IsPressed();
                openNotebookWasPressed = m_actionOpenNotebook.WasPressedThisFrame();
            }
        }

        private void ResetInput()
        {
            pauseWasPressed = false;
            movementInput = Vector2.zero;
            lookInput = Vector2.zero;
            aimIsPressed = false;
            castWasPressed = false;
            castIsPressed = false;
            castWasReleased = false;
            crouchWasPressed = false;
            crouchIsPressed = false;
            sprintWasPressed = false;
            sprintIsPressed = false;
            jumpWasPressed = false;
            interactWasPressed = false;
            dropWasPressed = false;
            previousWasPressed = false;
            previousIsPressed = false;
            nextWasPressed = false;
            nextIsPressed = false;
            scrollInput = 0.0f;
            scrollButtonWasPressed = false;
            scrollBackwardWasPressed = false;
            scrollBackwardIsPressed = false;
            scrollForwardWasPressed = false;
            scrollForwardIsPressed = false;
            pushToTalkIsPressed = false;
            openNotebookWasPressed = false;
        }
    }
}
