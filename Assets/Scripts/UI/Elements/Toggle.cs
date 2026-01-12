using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    /// <summary>
    /// A toggle that maintains a toggle state. When toggled on, it keeps its "selected" visual state
    /// even when another UI element is selected.
    /// </summary>
    [AddComponentMenu("RooseLabs/UI/Toggle")]
    public class Toggle : Selectable, IPointerClickHandler, ISubmitHandler
    {
        #region Serialized Fields
        [SerializeField]
        private bool m_IsOn;

        [SerializeField]
        private ToggleEvent m_OnValueChanged = new();

        [SerializeField]
        protected SelectionState m_toggledOnState = SelectionState.Selected;
        #endregion

        #region Properties
        /// <summary>
        /// Is the toggle currently on?
        /// Setting this property will invoke the onValueChanged callback.
        /// </summary>
        public bool IsOn
        {
            get => m_IsOn;
            set => SetIsOn(value, true);
        }

        /// <summary>
        /// Event fired when the toggle state changes. Passes the new state as a parameter.
        /// </summary>
        public ToggleEvent onValueChanged
        {
            get => m_OnValueChanged;
            set => m_OnValueChanged = value;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Sets the toggle state with an option to suppress the callback.
        /// </summary>
        /// <param name="value">The new toggle state</param>
        /// <param name="sendCallback">Whether to invoke the onValueChanged event</param>
        public void SetIsOn(bool value, bool sendCallback = true)
        {
            if (m_IsOn == value)
                return;

            m_IsOn = value;

            // Update visuals
            DoStateTransition(currentSelectionState, false);

            if (sendCallback)
            {
                m_OnValueChanged?.Invoke(m_IsOn);
            }
        }

        /// <summary>
        /// Toggles the current state.
        /// </summary>
        public void ToggleState()
        {
            IsOn = !IsOn;
        }
        #endregion

        #region Click Handlers
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            Press();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            Press();
        }

        private void Press()
        {
            if (!IsActive() || !IsInteractable())
                return;

            ToggleState();
        }
        #endregion

        #region State Transition
        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (!gameObject.activeInHierarchy)
                return;

            // If toggled on and not disabled, force the configured toggled-on state visuals
            SelectionState effectiveState = state;
            if (m_IsOn && state != SelectionState.Disabled)
            {
                effectiveState = m_toggledOnState;
            }

            // Call base implementation which handles all transition types
            base.DoStateTransition(effectiveState, instant);
        }
        #endregion

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Update visuals when IsOn is changed in the inspector
            if (isActiveAndEnabled)
            {
                DoStateTransition(currentSelectionState, true);
            }
        }
        #endif

        #region Nested Types
        /// <summary>
        /// Unity Event that passes a bool parameter for the toggle state.
        /// </summary>
        [Serializable]
        public class ToggleEvent : UnityEvent<bool> { }
        #endregion
    }
}
