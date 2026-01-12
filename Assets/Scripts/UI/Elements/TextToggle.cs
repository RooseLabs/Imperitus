using UnityEngine;

namespace RooseLabs.UI.Elements
{
    /// <summary>
    /// A Toggle that applies color tint transitions to both the toggle graphic and its text.
    /// When toggled on, it keeps its "selected" visual state even when another UI element is selected.
    /// </summary>
    [AddComponentMenu("RooseLabs/UI/Text Toggle")]
    public class TextToggle : Toggle
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

            // Get the effective state (accounting for toggle state)
            SelectionState effectiveState = state;
            if (IsOn && state != SelectionState.Disabled)
            {
                effectiveState = m_toggledOnState;
            }

            m_TextTransition.DoStateTransition(ToTextSelectionState(effectiveState), instant);
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
        #endif
    }
}
