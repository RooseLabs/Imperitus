using TMPro;
using UnityEditor;
using UnityEngine;

namespace RooseLabs.Editor
{
    /// <summary>
    /// Helper class for drawing TextTransitionHelper properties in custom editors.
    /// </summary>
    public static class TextTransitionEditorHelper
    {
        /// <summary>
        /// Stores cached SerializedProperties for TextTransitionHelper.
        /// </summary>
        public struct PropertyCache
        {
            public SerializedProperty TextTransitionProperty;
            public SerializedProperty TargetTextProperty;
            public SerializedProperty TextColorsProperty;
            public SerializedProperty FontSizesProperty;
            public SerializedProperty AutoFitModeProperty;
            public SerializedProperty AutoFitPaddingProperty;

            public void Initialize(SerializedObject serializedObject, string textTransitionPropertyName = "m_TextTransition")
            {
                TextTransitionProperty = serializedObject.FindProperty(textTransitionPropertyName);
                TargetTextProperty = TextTransitionProperty.FindPropertyRelative("m_TargetText");
                TextColorsProperty = TextTransitionProperty.FindPropertyRelative("m_TextColors");
                FontSizesProperty = TextTransitionProperty.FindPropertyRelative("m_FontSizes");
                AutoFitModeProperty = TextTransitionProperty.FindPropertyRelative("m_AutoFitMode");
                AutoFitPaddingProperty = TextTransitionProperty.FindPropertyRelative("m_AutoFitPadding");
            }
        }

        /// <summary>
        /// Draws the TextTransitionHelper properties in the inspector.
        /// </summary>
        /// <param name="cache">The cached properties</param>
        /// <param name="target">The target object being edited</param>
        /// <param name="autoFitLabel">Label for the auto-fit help box (e.g., "button" or "toggle")</param>
        public static void DrawTextTransitionProperties(PropertyCache cache, Object target, string autoFitLabel = "element")
        {
            // Check if target text exists and show a warning if not
            TMP_Text targetText = cache.TargetTextProperty.objectReferenceValue as TMP_Text;
            if (!targetText)
            {
                // Try to find it in children
                if (target is MonoBehaviour mb)
                {
                    targetText = mb.GetComponentInChildren<TMP_Text>();
                    if (targetText)
                    {
                        cache.TargetTextProperty.objectReferenceValue = targetText;
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
            EditorGUILayout.PropertyField(cache.TextColorsProperty, new GUIContent("Text Colors"));

            EditorGUILayout.Space();

            // Draw Text Size Transition section
            EditorGUILayout.LabelField("Text Size Transition", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cache.FontSizesProperty, new GUIContent("Font Sizes"));

            EditorGUILayout.Space();

            // Draw Auto-Fit section
            EditorGUILayout.LabelField("Auto-Fit Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cache.AutoFitModeProperty, new GUIContent("Auto-Fit Mode"));

            // Only show padding if auto-fit is enabled
            if (cache.AutoFitModeProperty.enumValueIndex != 0) // 0 = None
            {
                EditorGUILayout.PropertyField(cache.AutoFitPaddingProperty, new GUIContent("Padding"));

                EditorGUILayout.HelpBox(
                    $"Auto-fit will adjust the {autoFitLabel}'s size based on the text. " +
                    "Padding adds extra space around the text.",
                    MessageType.Info
                );
            }
        }
    }
}
