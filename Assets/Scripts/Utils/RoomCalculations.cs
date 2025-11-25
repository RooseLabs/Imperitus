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

        /// <summary>
        /// Find the biggest room by volume (bounds size)
        /// </summary>
        public static string FindBiggestRoom(string roomTag)
        {
            GameObject[] rooms = GameObject.FindGameObjectsWithTag(roomTag);

            if (rooms.Length == 0)
            {
                Debug.Log("No rooms found with tag: " + roomTag);
                return "Unknown";
            }

            string biggestRoomId = "Unknown";
            float biggestVolume = 0f;

            foreach (GameObject room in rooms)
            {
                Bounds roomBounds = CalculateRoomBounds(room);

                if (roomBounds.size == Vector3.zero)
                    continue;

                float volume = roomBounds.size.x * roomBounds.size.y * roomBounds.size.z;

                if (volume > biggestVolume)
                {
                    biggestVolume = volume;
                    biggestRoomId = room.name;
                }
            }

            if (biggestRoomId != "Unknown")
            {
                Debug.Log($"Biggest room identified: '{biggestRoomId}' with volume {biggestVolume:F2}");
            }

            return biggestRoomId;
        }

        /// <summary>
        /// Get the volume of a specific room
        /// </summary>
        public static float GetRoomVolume(string roomId, string roomTag)
        {
            GameObject[] rooms = GameObject.FindGameObjectsWithTag(roomTag);

            foreach (GameObject room in rooms)
            {
                if (room.name == roomId)
                {
                    Bounds roomBounds = CalculateRoomBounds(room);
                    if (roomBounds.size != Vector3.zero)
                    {
                        return roomBounds.size.x * roomBounds.size.y * roomBounds.size.z;
                    }
                }
            }

            return 0f;
        }
    }
}
