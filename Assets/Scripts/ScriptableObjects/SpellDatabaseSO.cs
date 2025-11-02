using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "SpellDatabase", menuName = "Imperitus/SpellDatabase")]
    public class SpellDatabaseSO : ScriptableObject, IReadOnlyList<GameObject>
    {
        [SerializeField] private GameObject[] spells;

        public IEnumerator<GameObject> GetEnumerator()
        {
            foreach (var spell in spells)
            {
                yield return spell;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public GameObject this[int index] => spells[index];

        public bool Contains(GameObject item) => Array.Exists(spells, spell => spell == item);

        public void CopyTo(GameObject[] array, int arrayIndex) => spells.CopyTo(array, arrayIndex);

        public int Count => spells.Length;
    }
}
