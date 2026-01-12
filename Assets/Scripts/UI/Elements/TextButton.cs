using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    /// <summary>
    /// A button that applies color tint transitions to both the button graphic and its text.
    /// </summary>
    [AddComponentMenu("RooseLabs/UI/Text Button")]
    public class TextButton : Button
    {
        #region Serialized Fields
        [SerializeField]
        private TextTransitionHelper m_TextTransition = new();
        #endregion

        #region Properties
        /// <summary>
        /// The text transition helper that handles text color and size transitions.
        /// </summary>
        public TextTransitionHelper TextTransition => m_TextTransition;
        #endregion

        protected override void Awake()
        {
            base.Awake();
            m_TextTransition.Initialize(this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_TextTransition.OnEnable();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            m_TextTransition.DoStateTransition(ToTextSelectionState(state), instant);
        }

        protected override void InstantClearState()
        {
            base.InstantClearState();
            m_TextTransition.InstantClearState();
        }

        private static TextSelectionState ToTextSelectionState(SelectionState state)
        {
            return state switch
            {
                SelectionState.Normal => TextSelectionState.Normal,
                SelectionState.Highlighted => TextSelectionState.Highlighted,
                SelectionState.Pressed => TextSelectionState.Pressed,
                SelectionState.Selected => TextSelectionState.Selected,
                SelectionState.Disabled => TextSelectionState.Disabled,
                _ => TextSelectionState.Normal
            };
        }

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (isActiveAndEnabled)
            {
                m_TextTransition.OnValidate(this, ToTextSelectionState(currentSelectionState));
            }
        }

        protected override void Reset()
        {
            base.Reset();
            m_TextTransition.Reset(this);
        }
        #endif
    }
}
