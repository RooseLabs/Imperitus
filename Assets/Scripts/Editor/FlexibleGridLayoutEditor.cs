using RooseLabs.UI.Elements;
using UnityEditor;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(FlexibleGridLayout), true)]
    [CanEditMultipleObjects]
    public class FlexibleGridLayoutEditor : UnityEditor.Editor
    {
        private SerializedProperty m_padding;
        private SerializedProperty m_spacing;
        private SerializedProperty m_startCorner;
        // private SerializedProperty m_startAxis;
        private SerializedProperty m_childAlignment;
        private SerializedProperty m_minColumnCount;
        private SerializedProperty m_minRowCount;
        private SerializedProperty m_maintainCellRatio;
        private SerializedProperty m_cellRatio;
        private SerializedProperty m_autoResizeGrid;
        private SerializedProperty m_expandMode;
        private SerializedProperty m_flowMode;

        private void OnEnable()
        {
            m_padding = serializedObject.FindProperty("m_Padding");
            m_spacing = serializedObject.FindProperty("m_Spacing");
            m_startCorner = serializedObject.FindProperty("m_StartCorner");
            // m_startAxis = serializedObject.FindProperty("m_StartAxis");
            m_childAlignment = serializedObject.FindProperty("m_ChildAlignment");
            m_minColumnCount = serializedObject.FindProperty("minColumnCount");
            m_minRowCount = serializedObject.FindProperty("minRowCount");
            m_maintainCellRatio = serializedObject.FindProperty("maintainCellRatio");
            m_cellRatio = serializedObject.FindProperty("cellRatio");
            m_autoResizeGrid = serializedObject.FindProperty("autoResizeGrid");
            m_expandMode = serializedObject.FindProperty("expandMode");
            m_flowMode = serializedObject.FindProperty("flowMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_padding, true);
            EditorGUILayout.PropertyField(m_spacing, true);
            EditorGUILayout.PropertyField(m_startCorner, true);
            // EditorGUILayout.PropertyField(m_startAxis, true);
            EditorGUILayout.PropertyField(m_childAlignment, true);
            EditorGUILayout.PropertyField(m_minColumnCount, true);
            EditorGUILayout.PropertyField(m_minRowCount, true);
            EditorGUILayout.PropertyField(m_maintainCellRatio, true);

            // Make cellRatio read-only when maintainCellRatio is disabled
            EditorGUI.BeginDisabledGroup(!m_maintainCellRatio.boolValue);
            EditorGUILayout.PropertyField(m_cellRatio, true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(m_autoResizeGrid, true);

            // Make expandMode read-only when autoResizeGrid is disabled
            EditorGUI.BeginDisabledGroup(!m_autoResizeGrid.boolValue);
            EditorGUILayout.PropertyField(m_expandMode, true);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(m_flowMode, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
