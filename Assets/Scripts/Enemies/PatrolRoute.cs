using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Enemies
{
    public class PatrolRoute : MonoBehaviour
    {
        [Tooltip("Waypoints the enemy will patrol through. Order matters.")]
        public List<Transform> waypoints = new List<Transform>();

        public Transform GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0) return null;
            index = Mathf.Clamp(index, 0, waypoints.Count - 1);
            return waypoints[index];
        }

        public int Count => waypoints == null ? 0 : waypoints.Count;
    }
}
