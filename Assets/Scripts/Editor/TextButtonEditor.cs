using RooseLabs.UI.Elements;
using TMPro;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace RooseLabs.Editor
{
    /// <summary>
    /// Custom Editor for the TextButton Component.
    /// </summary>
    [CustomEditor(typeof(TextButton), true)]
    [CanEditMultipleObjects]
    public class TextButtonEditor : ButtonEditor
    {
        private SerializedProperty m_textColorsProperty;
        private SerializedProperty m_fontSizesProperty;
        private SerializedProperty m_targetTextProperty;
        private SerializedProperty m_autoFitModeProperty;
        private SerializedProperty m_autoFitPaddingProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_textColorsProperty = serializedObject.FindProperty("m_TextColors");
            m_fontSizesProperty = serializedObject.FindProperty("m_FontSizes");
            m_targetTextProperty = serializedObject.FindProperty("m_TargetText");
            m_autoFitModeProperty = serializedObject.FindProperty("m_AutoFitMode");
            m_autoFitPaddingProperty = serializedObject.FindProperty("m_AutoFitPadding");
        }

        public override void OnInspectorGUI()
        {
            // Draw the base Button inspector (includes Selectable properties and OnClick event)
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            serializedObject.Update();

            // Check if target text exists and show a warning if not
            TMP_Text targetText = m_targetTextProperty.objectReferenceValue as TMP_Text;
            if (!targetText)
            {
                // Try to find it in children
                TextButton textButton = target as TextButton;
                if (textButton)
                {
                    targetText = textButton.GetComponentInChildren<TMP_Text>();
                    if (targetText)
                    {
                        m_targetTextProperty.objectReferenceValue = targetText;
                    }
                }

                if (!targetText)
                {
                    EditorGUILayout.HelpBox(
                        "No TMP_Text component found in children. Text color transitions will not be applied. " +
                        "Add a TMP_Text component as a child to enable text color transitions.",
                        MessageType.Info
                    );
                }
            }
            else
            {
                // Show which text component is being targeted
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField("Target Text", targetText, typeof(TMP_Text), true);
                EditorGUI.EndDisabledGroup();
            }

            // Draw Text Color Transition section
            EditorGUILayout.LabelField("Text Color Transition", EditorStyles.boldLabel);

            // Draw the ColorBlock property
            EditorGUILayout.PropertyField(m_textColorsProperty, new GUIContent("Text Colors"));

            EditorGUILayout.Space();

            // Draw Text Size Transition section
            EditorGUILayout.LabelField("Text Size Transition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_fontSizesProperty, new GUIContent("Font Sizes"));

            EditorGUILayout.Space();

            // Draw Auto-Fit section
            EditorGUILayout.LabelField("Auto-Fit Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_autoFitModeProperty, new GUIContent("Auto-Fit Mode"));

            // Only show padding if auto-fit is enabled
            if (m_autoFitModeProperty.enumValueIndex != 0) // 0 = None
            {
                EditorGUILayout.PropertyField(m_autoFitPaddingProperty, new GUIContent("Padding"));

                EditorGUILayout.HelpBox(
                    "Auto-fit will adjust the button's size based on the text. " +
                    "Padding adds extra space around the text.",
                    MessageType.Info
                );
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
