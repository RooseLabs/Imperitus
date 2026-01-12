using RooseLabs.UI.Elements;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(GlowingImage), true), CanEditMultipleObjects]
    public class GlowingImageEditor : ImageEditor
    {
        private SerializedProperty m_glowColorProp;
        private SerializedProperty m_glowWidthProp;
        private SerializedProperty m_glowIntensityProp;
        private SerializedProperty m_useColorAlphaProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_glowColorProp = serializedObject.FindProperty("glowColor");
            m_glowWidthProp = serializedObject.FindProperty("glowWidth");
            m_glowIntensityProp = serializedObject.FindProperty("glowIntensity");
            m_useColorAlphaProp = serializedObject.FindProperty("useColorAlphaForGlow");

        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw the default Image inspector
            base.OnInspectorGUI();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Glow", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(m_glowColorProp, new GUIContent("Glow Color"));
            EditorGUILayout.PropertyField(m_glowWidthProp, new GUIContent("Glow Width"));
            EditorGUILayout.PropertyField(m_glowIntensityProp, new GUIContent("Glow Intensity"));
            EditorGUILayout.PropertyField(m_useColorAlphaProp, new GUIContent("Use Color Alpha For Glow"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
