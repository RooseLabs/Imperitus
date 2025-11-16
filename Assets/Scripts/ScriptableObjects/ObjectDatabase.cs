using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    public class ObjectDatabase<T> : ScriptableObject, IReadOnlyList<T>
    {
        [SerializeField] private T[] objects;

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

        public T[] ToArray() => (T[])objects.Clone();

        public int IndexOf(T item) => System.Array.IndexOf(objects, item);

        public int FindIndex(System.Predicate<T> match) => System.Array.FindIndex(objects, match);

        public T Find(System.Predicate<T> match) => System.Array.Find(objects, match);

        public T[] FindAll(System.Predicate<T> match) => System.Array.FindAll(objects, match);

        /// <summary>
        /// Get a random entry from the database, optionally filtered by a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to filter entries. If null, any entry can be returned.</param>
        /// <returns>A random entry from the database that matches the predicate, or default(T) if none found.</returns>
        public T GetRandomEntry(System.Predicate<T> predicate = null)
        {
            if (objects.Length == 0) return default;

            if (predicate == null)
                return objects[Random.Range(0, objects.Length)];

            var filtered = System.Array.FindAll(objects, predicate);
            return filtered.Length == 0 ? default : filtered[Random.Range(0, filtered.Length)];
        }

        /// <summary>
        /// Gets a random index from the database, optionally filtered by a predicate.
        /// </summary>
        /// <param name="predicate">The predicate to filter entries. If null, any index can be returned.</param>
        /// <returns>A random index from the database that matches the predicate, or -1 if none found.</returns>
        public int GetRandomIndex(System.Predicate<T> predicate = null)
        {
            if (objects.Length == 0) return -1;

            if (predicate == null)
                return Random.Range(0, objects.Length);

            var indices = new List<int>();
            for (int i = 0; i < objects.Length; ++i)
            {
                if (predicate(objects[i]))
                    indices.Add(i);
            }
            return indices.Count == 0 ? -1 : indices[Random.Range(0, indices.Count)];
        }

        public int Count => objects.Length;
    }
}
