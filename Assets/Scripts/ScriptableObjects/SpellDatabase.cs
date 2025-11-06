using System.Collections.Generic;
using RooseLabs.Collections;
using RooseLabs.Gameplay.Spells;
using UnityEditor;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "Imperitus/Spell Database")]
    public class SpellDatabase : ObjectDatabase<SpellBase>
    {
        [SerializeField, HideInInspector] private SerializableDictionary<int, SpellBase> spellLookup;

        /// <summary>
        /// Retrieve a spell whose runes match the given combination of runes.
        /// </summary>
        /// <remarks>
        /// Internally, this computes the rune signature by XORing the hash codes of the provided runes.
        /// It then looks up the spell using this signature.
        /// </remarks>
        /// <param name="runes">A set of runes that make up the spell.</param>
        /// <returns>The spell that matches the given runes, or null if no such spell exists.</returns>
        public SpellBase GetSpellWithMatchingRunes(IEnumerable<RuneSO> runes)
        {
            int signature = 0;
            foreach (var rune in runes)
            {
                signature ^= rune.GetHashCode();
            }
            return GetSpellBySignature(signature);
        }

        /// <summary>
        /// Retrieve a spell by its rune signature.
        /// </summary>
        /// <param name="signature">The rune signature of the spell.</param>
        /// <returns>The spell that matches the given signature, or null if no such spell exists.</returns>
        public SpellBase GetSpellBySignature(int signature)
        {
            spellLookup.TryGetValue(signature, out SpellBase spell);
            return spell;
        }

        public int IndexOf(SpellSO spell) => System.Array.FindIndex(objects, s => s.SpellInfo == spell);

        #if UNITY_EDITOR
        public void OnValidate()
        {
            // Rebuild the lookup dictionary
            spellLookup = new SerializableDictionary<int, SpellBase>();
            foreach (var spell in this)
            {
                if (spell == null) continue;
                spellLookup[spell.SpellInfo.Signature] = spell;
            }

            EditorUtility.SetDirty(this);
        }
        #endif
    }
}
