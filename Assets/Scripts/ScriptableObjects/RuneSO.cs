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
    }
}
