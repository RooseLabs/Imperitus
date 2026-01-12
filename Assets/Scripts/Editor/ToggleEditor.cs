using RooseLabs.UI.Elements;
using UnityEditor;
using UnityEditor.UI;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(Toggle), true), CanEditMultipleObjects]
    public class ToggleEditor : SelectableEditor
    {
        private SerializedProperty m_isOnProperty;
        private SerializedProperty m_toggledOnStateProperty;
        private SerializedProperty m_onValueChangedProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_isOnProperty = serializedObject.FindProperty("m_IsOn");
            m_toggledOnStateProperty = serializedObject.FindProperty("m_toggledOnState");
            m_onValueChangedProperty = serializedObject.FindProperty("m_OnValueChanged");
        }

        public override void OnInspectorGUI()
        {
            // Draw the base Selectable inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            serializedObject.Update();

            // Draw Toggle section
            EditorGUILayout.LabelField("Toggle Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_isOnProperty, EditorGUIUtility.TrTextContent("Is On", "Is the toggle currently on?"));
            EditorGUILayout.PropertyField(m_toggledOnStateProperty, EditorGUIUtility.TrTextContent("Toggled On State", "The selection state to show when toggled on."));

            EditorGUILayout.Space();

            // Draw the events
            EditorGUILayout.PropertyField(m_onValueChangedProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
