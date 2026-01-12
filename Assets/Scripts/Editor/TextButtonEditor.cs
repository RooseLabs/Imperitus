using RooseLabs.UI.Elements;
using UnityEditor;
using UnityEditor.UI;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(TextButton), true), CanEditMultipleObjects]
    public class TextButtonEditor : ButtonEditor
    {
        private TextTransitionEditorHelper.PropertyCache m_textTransitionCache;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_textTransitionCache = new TextTransitionEditorHelper.PropertyCache();
            m_textTransitionCache.Initialize(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space();

            serializedObject.Update();

            TextTransitionEditorHelper.DrawTextTransitionProperties(m_textTransitionCache, target, "button");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
