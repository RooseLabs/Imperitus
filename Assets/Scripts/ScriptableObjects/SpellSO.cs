using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Spell", menuName = "Imperitus/Spell")]
    public class SpellSO : ScriptableObject
    {
        [SerializeField] private string latinSpellName;
        [SerializeField] private string englishSpellName;
        [SerializeField] private RuneSO[] runes;

        [SerializeField, HideInInspector] private int spellSignature;

        public string Name => latinSpellName;
        public string EnglishName => englishSpellName;
        public RuneSO[] Runes => runes;
        public int Signature => spellSignature;

        #if UNITY_EDITOR
        public void OnValidate()
        {
            bool duplicatesFound = false;
            for (int i = 0; i < runes.Length; i++)
            {
                for (int j = i + 1; j < runes.Length; j++)
                {
                    if (runes[i] == runes[j])
                    {
                        duplicatesFound = true;
                        break;
                    }
                }

                if (duplicatesFound) break;
            }
            if (duplicatesFound)
            {
                Debug.LogWarning($"[SpellSO] Spell '{name}' has duplicate runes assigned. This is not supported.",
                    this);
                return;
            }

            // XOR the hash codes of the runes to create a unique signature for the spell
            // The rune order does not matter for the signature and there are no duplicate runes so
            // simply XORing the hash codes is sufficient for our needs.
            int signature = 0;
            foreach (var rune in runes)
            {
                if (rune != null)
                    signature ^= rune.GetHashCode();
            }
            spellSignature = signature;

            UnityEditor.EditorUtility.SetDirty(this);

            // All SpellDatabase instances need to be revalidated
            var databases = UnityEditor.AssetDatabase.FindAssets("t:SpellDatabase");
            foreach (var dbGUID in databases)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(dbGUID);
                var database = UnityEditor.AssetDatabase.LoadAssetAtPath<SpellDatabase>(path);
                if (database != null) database.OnValidate();
            }
        }
        #endif
    }
}
