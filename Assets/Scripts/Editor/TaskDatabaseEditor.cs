using System.Reflection;
using GameKit.Dependencies.Utilities;
using RooseLabs.ScriptableObjects;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(TaskDatabase))]
    public class TaskDatabaseEditor : UnityEditor.Editor
    {
        private ReorderableList m_list;
        private string m_searchString = "";

        private void OnEnable()
        {
            m_list = new ReorderableList(
                serializedObject,
                serializedObject.FindProperty("objects"),
                true,
                true,
                true,
                true
            ) {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, "Tasks");
                }
            };

            m_list.drawElementCallback = (rect, index, _, _) =>
            {
                var element = m_list.serializedProperty.GetArrayElementAtIndex(index);
                if (!CheckMatch(element, m_searchString)) return;

                rect.x += 15f;
                rect.width -= 15f;

                EditorGUI.PropertyField(rect, element, new GUIContent("Task " + index), true);
            };

            m_list.elementHeightCallback = index =>
            {
                var element = m_list.serializedProperty.GetArrayElementAtIndex(index);
                if (!CheckMatch(element, m_searchString)) return 0f;
                return EditorGUI.GetPropertyHeight(element, true) + EditorGUIUtility.standardVerticalSpacing;
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            m_searchString = EditorGUILayout.TextField("Search", m_searchString);
            bool notSearching = string.IsNullOrEmpty(m_searchString);
            if (EditorGUI.EndChangeCheck())
            {
                m_list.displayAdd = notSearching;
                m_list.displayRemove = notSearching;
                m_list.draggable = notSearching;
                Repaint();
            }

            if (!notSearching)
            {
                int visibleCount = 0;
                for (int i = 0; i < m_list.serializedProperty.arraySize; ++i)
                {
                    if (CheckMatch(m_list.serializedProperty.GetArrayElementAtIndex(i), m_searchString))
                        visibleCount++;
                }
                EditorGUILayout.LabelField($"Showing {visibleCount} of {m_list.serializedProperty.arraySize} tasks");
            }

            m_list.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private static bool CheckMatch(SerializedProperty element, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;

            string lowerSearch = search.ToLower();

            SerializedProperty descProp = element.FindPropertyRelative("<Description>k__BackingField");
            if (descProp is { propertyType: SerializedPropertyType.String } && descProp.stringValue.ToLower().Contains(lowerSearch))
            {
                return true;
            }

            SerializedProperty condProp = element.FindPropertyRelative("<CompletionCondition>k__BackingField");
            if (condProp == null || string.IsNullOrEmpty(condProp.managedReferenceFullTypename))
            {
                return false;
            }

            if (condProp.managedReferenceFullTypename.EndsWith("CastSpellCondition"))
            {
                SerializedProperty spellProp = condProp.FindPropertyRelative("<Spell>k__BackingField");
                if (spellProp is { objectReferenceValue: SpellSO spell })
                {
                    if (spell.Name.ToLower().Contains(lowerSearch) || spell.EnglishName.ToLower().Contains(lowerSearch))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
