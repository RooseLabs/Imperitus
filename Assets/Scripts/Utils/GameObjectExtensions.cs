using UnityEngine;

namespace RooseLabs.Utils
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Gets the component of type T attached to the GameObject if it exists; otherwise, adds a new component of type T and returns it.
        /// </summary>
        /// <typeparam name="T">The type of component to get or add. Must derive from UnityEngine.Component.</typeparam>
        /// <param name="go">The GameObject to operate on.</param>
        /// <returns>The existing or newly added component of type T.</returns>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// Tries to get the component of type T attached to the GameObject if it exists; otherwise, adds a new component of type T.
        /// </summary>
        /// <typeparam name="T">The type of component to get or add. Must derive from UnityEngine.Component.</typeparam>
        /// <param name="go">The GameObject to operate on.</param>
        /// <param name="component">When this method returns, contains the existing or newly added component of type T, or null if the operation failed.</param>
        /// <returns>true if the component was found or added successfully; otherwise, false.</returns>
        public static bool TryGetOrAddComponent<T>(this GameObject go, out T component) where T : Component
        {
            component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component != null;
        }

        /// <summary>
        /// Tries to get the component of type T from the GameObject or its parents.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <param name="go">The GameObject to operate on.</param>
        /// <param name="component">When this method returns, contains the component of type T found in the GameObject or its parents, or null if not found.</param>
        /// <returns>true if the component was found; otherwise, false.</returns>
        public static bool TryGetComponentInParent<T>(this GameObject go, out T component) where T : class
        {
            component = go.GetComponentInParent(typeof(T)) as T;
            return component != null;
        }

        /// <summary>
        /// Tries to get the component of type T from the GameObject or its children.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <param name="go">The GameObject to operate on.</param>
        /// <param name="component">When this method returns, contains the component of type T found in the GameObject or its children, or null if not found.</param>
        /// <returns>true if the component was found; otherwise, false.</returns>
        public static bool TryGetComponentInChildren<T>(this GameObject go, out T component) where T : class
        {
            component = go.GetComponentInChildren(typeof(T)) as T;
            return component != null;
        }
    }

    public static class ComponentExtensions
    {
        /// <summary>
        /// Gets the component of type T attached to the Component's GameObject if it exists; otherwise, adds a new component of type T and returns it.
        /// </summary>
        /// <typeparam name="T">The type of component to get or add. Must derive from UnityEngine.Component.</typeparam>
        /// <param name="component">The Component to operate on.</param>
        /// <returns>The existing or newly added component of type T.</returns>
        public static T GetOrAddComponent<T>(this Component component) where T : Component
        {
            return component.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Tries to get the component of type T attached to the Component's GameObject if it exists; otherwise, adds a new component of type T.
        /// </summary>
        /// <typeparam name="T">The type of component to get or add. Must derive from UnityEngine.Component.</typeparam>
        /// <param name="component">The Component to operate on.</param>
        /// <param name="result">When this method returns, contains the existing or newly added component of type T, or null if the operation failed.</param>
        /// <returns>true if the component was found or added successfully; otherwise, false.</returns>
        public static bool TryGetOrAddComponent<T>(this Component component, out T result) where T : Component
        {
            return component.gameObject.TryGetOrAddComponent<T>(out result);
        }

        /// <summary>
        /// Tries to get the component of type T from the Component's GameObject or its parents.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <param name="component">The Component to operate on.</param>
        /// <param name="result">When this method returns, contains the component of type T found in the GameObject or its parents, or null if not found.</param>
        /// <returns>true if the component was found; otherwise, false.</returns>
        public static bool TryGetComponentInParent<T>(this Component component, out T result) where T : class
        {
            return component.gameObject.TryGetComponentInParent<T>(out result);
        }

        /// <summary>
        /// Tries to get the component of type T from the Component's GameObject or its children.
        /// </summary>
        /// <typeparam name="T">The type of component to get.</typeparam>
        /// <param name="component">The Component to operate on.</param>
        /// <param name="result">When this method returns, contains the component of type T found in the GameObject or its children, or null if not found.</param>
        /// <returns>true if the component was found; otherwise, false.</returns>
        public static bool TryGetComponentInChildren<T>(this Component component, out T result) where T : class
        {
            return component.gameObject.TryGetComponentInChildren<T>(out result);
        }
    }
}
