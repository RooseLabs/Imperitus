using RooseLabs.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.Player
{
    public class PlayerInput : MonoBehaviour
    {
        #region Gameplay Input Actions
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
        public bool interactIsPressed;
        public bool interactWasReleased;
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
        #endregion

        #region UI Input Actions
        private InputAction m_actionResume;
        private InputAction m_actionCloseNotebook;

        public bool resumeWasPressed;
        public bool closeNotebookWasPressed;
        #endregion

        // Reference to voice spell caster for input injection
        private VoiceSpellCaster m_voiceSpellCaster;

        private void Awake()
        {
            var gameplayActionMap = InputHandler.GameplayActions;
            m_actionPause = gameplayActionMap.FindAction("Pause");
            m_actionMove = gameplayActionMap.FindAction("Move");
            m_actionLook = gameplayActionMap.FindAction("Look");
            m_actionAim = gameplayActionMap.FindAction("Aim");
            m_actionCast = gameplayActionMap.FindAction("Cast");
            m_actionCrouch = gameplayActionMap.FindAction("Crouch");
            m_actionSprint = gameplayActionMap.FindAction("Sprint");
            m_actionJump = gameplayActionMap.FindAction("Jump");
            m_actionInteract = gameplayActionMap.FindAction("Interact");
            m_actionDrop = gameplayActionMap.FindAction("Drop");
            m_actionPrevious = gameplayActionMap.FindAction("Previous");
            m_actionNext = gameplayActionMap.FindAction("Next");
            m_actionScroll = gameplayActionMap.FindAction("Scroll");
            m_actionScrollButton = gameplayActionMap.FindAction("ScrollButton");
            m_actionScrollBackward = gameplayActionMap.FindAction("ScrollBackward");
            m_actionScrollForward = gameplayActionMap.FindAction("ScrollForward");
            m_actionPushToTalk = gameplayActionMap.FindAction("PushToTalk");
            m_actionOpenNotebook = gameplayActionMap.FindAction("OpenNotebook");

            var uiActionMap = InputHandler.UIActions;
            m_actionResume = uiActionMap.FindAction("Resume");
            m_actionCloseNotebook = uiActionMap.FindAction("CloseNotebook");

            // Get reference to VoiceSpellCaster (if it exists)
            m_voiceSpellCaster = GetComponent<VoiceSpellCaster>();
        }

        public void Sample()
        {
            ResetInput();

            // Sample all real input from the input system
            pauseWasPressed = m_actionPause.WasPressedThisFrame();
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
            interactIsPressed = m_actionInteract.IsPressed();
            interactWasReleased = m_actionInteract.WasReleasedThisFrame();
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
            resumeWasPressed = m_actionResume.WasPressedThisFrame();
            closeNotebookWasPressed = m_actionCloseNotebook.WasPressedThisFrame();

            // CRITICAL: Allow voice spell caster to inject simulated input AFTER sampling real input
            if (m_voiceSpellCaster != null && m_voiceSpellCaster.IsSimulatingCastHold)
            {
                m_voiceSpellCaster.InjectSimulatedInput(this);
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
            interactIsPressed = false;
            interactWasReleased = false;
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
            resumeWasPressed = false;
            closeNotebookWasPressed = false;
        }
    }
}