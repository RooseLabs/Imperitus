using UnityEditor;

namespace RooseLabs.Editor
{
    public static class EditorUtils
    {
        /// <summary>
        /// Retrieves the SerializedProperty at a relative path to the current property,
        /// handling auto-implemented property backing fields.
        /// </summary>
        public static SerializedProperty FindProperty(this SerializedProperty p, string n)
        {
            return p.FindPropertyRelative(n) ?? p.FindPropertyRelative($"<{n}>k__BackingField");
        }
    }
}
