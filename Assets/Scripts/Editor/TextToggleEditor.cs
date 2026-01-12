using RooseLabs.UI.Elements;
using UnityEditor;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(TextToggle), true), CanEditMultipleObjects]
    public class TextToggleEditor : ToggleEditor
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

            TextTransitionEditorHelper.DrawTextTransitionProperties(m_textTransitionCache, target, "toggle");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
