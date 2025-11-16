using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace RooseLabs.Utils
{
    public static class Collections
    {
        /// <summary>
        /// Shuffles the elements of the list in place using the Fisher-Yates algorithm.
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Determines whether any element in the source collection matches any element
        /// in the target collection using the specified predicate.
        /// </summary>
        public static bool Intersects<TSource, TTarget>(
            this IEnumerable<TSource> source,
            IEnumerable<TTarget> target,
            Func<TSource, TTarget, bool> predicate)
        {
            foreach (var sourceItem in source)
            {
                foreach (var targetItem in target)
                {
                    if (predicate(sourceItem, targetItem))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Adds a collection of items to the HashSet.
        /// </summary>
        public static void AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> items)
        {
            foreach (T item in items)
                hashSet.Add(item);
        }
    }
}
