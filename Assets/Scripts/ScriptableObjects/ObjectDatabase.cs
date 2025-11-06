using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    public class ObjectDatabase<T> : ScriptableObject, IReadOnlyList<T>
    {
        [SerializeField] protected T[] objects;

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var obj in objects)
            {
                yield return obj;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int index] => objects[index];

        public bool Contains(T item) => System.Array.Exists(objects, obj => EqualityComparer<T>.Default.Equals(obj, item));

        public void CopyTo(T[] array, int arrayIndex) => objects.CopyTo(array, arrayIndex);

        public int IndexOf(T item) => System.Array.IndexOf(objects, item);

        public int Count => objects.Length;
    }
}
