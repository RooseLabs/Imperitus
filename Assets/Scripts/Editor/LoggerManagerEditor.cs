using System.Linq;
using RooseLabs.Core;
using UnityEditor;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(LoggerManager))]
    public class LoggerManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty loggersProp = serializedObject.FindProperty("loggers");

            EditorGUILayout.LabelField("Loggers", EditorStyles.boldLabel);

            // Create sorted indices array based on logger names
            int[] sortedIndices = Enumerable.Range(0, loggersProp.arraySize)
                .OrderBy(i => FindPropertyField(loggersProp.GetArrayElementAtIndex(i), "Name").stringValue)
                .ToArray();

            // Use sorted indices to display loggers
            foreach (int i in sortedIndices)
            {
                SerializedProperty logger = loggersProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = FindPropertyField(logger, "Name");
                SerializedProperty enabledProp = FindPropertyField(logger, "Enabled");

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
                EditorGUILayout.LabelField(nameProp.stringValue, GUILayout.Width(200f));
                GUILayout.FlexibleSpace();
                enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(20f));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All"))
            {
                for (int i = 0; i < loggersProp.arraySize; i++)
                {
                    SerializedProperty logger = loggersProp.GetArrayElementAtIndex(i);
                    SerializedProperty enabledProp = FindPropertyField(logger, "Enabled");
                    enabledProp.boolValue = true;
                }
            }

            if (GUILayout.Button("Disable All"))
            {
                for (int i = 0; i < loggersProp.arraySize; i++)
                {
                    SerializedProperty logger = loggersProp.GetArrayElementAtIndex(i);
                    SerializedProperty enabledProp = FindPropertyField(logger, "Enabled");
                    enabledProp.boolValue = false;
                }
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private static SerializedProperty FindPropertyField(SerializedProperty rootProp, string propName)
        {
            return rootProp.FindPropertyRelative($"<{propName}>k__BackingField");
        }
    }
}
