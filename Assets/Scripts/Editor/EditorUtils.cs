using UnityEditor;

namespace RooseLabs.Editor
{
    public static class EditorUtils
    {
        public static SerializedProperty FindProperty(SerializedProperty rootProp, string propName)
        {
            var prop = rootProp.FindPropertyRelative(propName);
            if (prop == null)
                prop = rootProp.FindPropertyRelative($"<{propName}>k__BackingField");
            return prop;
        }
    }
}
