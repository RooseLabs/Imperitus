using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Spell", menuName = "Imperitus/Spell")]
    public class SpellSO : ScriptableObject
    {
        [SerializeField] private string latinSpellName;
        [SerializeField] private string englishSpellName;
        [SerializeField] private RuneSO[] runes;

        public string Name => latinSpellName;
        public string EnglishName => englishSpellName;
        public RuneSO[] Runes => runes;
    }
}
