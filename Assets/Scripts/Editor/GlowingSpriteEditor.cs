using RooseLabs.UI.Elements;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(GlowingSprite), true), CanEditMultipleObjects]
    public class GlowingSpriteEditor : ImageEditor
    {
        private SerializedProperty m_glowWidthProp;
        private SerializedProperty m_glowColorProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_glowWidthProp = serializedObject.FindProperty("glowWidth");
            m_glowColorProp = serializedObject.FindProperty("glowColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the default Image inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Glow", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_glowWidthProp, new GUIContent("Glow Width"));
            EditorGUILayout.PropertyField(m_glowColorProp, new GUIContent("Glow Color"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
