using UnityEngine;

namespace RooseLabs.Utils
{
    public static class RoomCalculations
    {
        /// <summary>
        /// Calculate combined bounds of a room GameObject and all its children
        /// </summary>
        public static Bounds CalculateRoomBounds(GameObject room)
        {
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            Collider[] colliders = room.GetComponentsInChildren<Collider>();

            if (renderers.Length == 0 && colliders.Length == 0)
                return new Bounds(room.transform.position, Vector3.zero);

            Bounds bounds = new Bounds(room.transform.position, Vector3.zero);
            bool boundsInitialized = false;

            foreach (Renderer r in renderers)
            {
                if (!boundsInitialized)
                {
                    bounds = r.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            foreach (Collider c in colliders)
            {
                if (!boundsInitialized)
                {
                    bounds = c.bounds;
                    boundsInitialized = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return bounds;
        }
    }
}
