using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Rune", menuName = "Imperitus/Rune")]
    public class RuneSO : GuidScriptableObject
    {
        [SerializeField] private string runeName;
        [SerializeField] private Sprite runeIcon;

        public string Name => runeName;
        public Sprite Sprite => runeIcon;

        #if UNITY_EDITOR
        private void Awake()
        {
            if (string.IsNullOrEmpty(runeName)) runeName = GetAssetName();
        }

        protected override void Reset()
        {
            if (string.IsNullOrEmpty(runeName)) runeName = GetAssetName();
            base.Reset();
        }

        private string GetAssetName()
        {
            return System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GetAssetPath(this));
        }
        #endif
    }
}
