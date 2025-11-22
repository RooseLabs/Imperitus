using RooseLabs.Enemies;
using UnityEditor;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(EnemySpawner))]
    public class EnemySpawnerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EnemySpawner spawner = (EnemySpawner)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Quick Room Assignment", EditorStyles.boldLabel);

            // Button to auto-assign room based on edit-mode detection
            if (GUILayout.Button("Auto-Detect Room (Edit Mode)"))
            {
                string detectedRoom = DetectRoomInEditMode(spawner);
                if (!string.IsNullOrEmpty(detectedRoom))
                {
                    SerializedProperty roomProp = serializedObject.FindProperty("roomIdentifier");
                    roomProp.stringValue = detectedRoom;
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(spawner);
                    Debug.Log($"Assigned '{spawner.gameObject.name}' to room '{detectedRoom}'");
                }
                else
                {
                    Debug.LogWarning($"Could not detect room for '{spawner.gameObject.name}'. Try manual assignment.");
                }
            }

            // Button to clear room assignment
            if (GUILayout.Button("Clear Room (Use Auto-Detection at Runtime)"))
            {
                SerializedProperty roomProp = serializedObject.FindProperty("roomIdentifier");
                roomProp.stringValue = "";
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawner);
                Debug.Log($"Cleared room assignment for '{spawner.gameObject.name}'");
            }

            EditorGUILayout.Space(5);

            // Display available rooms
            GameObject[] rooms = GameObject.FindGameObjectsWithTag("Room");
            if (rooms.Length > 0)
            {
                EditorGUILayout.LabelField($"Available Rooms ({rooms.Length}):", EditorStyles.miniLabel);

                EditorGUI.indentLevel++;
                foreach (GameObject room in rooms)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(room.name, EditorStyles.miniLabel);

                    if (GUILayout.Button("Assign", GUILayout.Width(60)))
                    {
                        SerializedProperty roomProp = serializedObject.FindProperty("roomIdentifier");
                        roomProp.stringValue = room.name;
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(spawner);
                        Debug.Log($"Manually assigned '{spawner.gameObject.name}' to room '{room.name}'");
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox("No GameObjects with 'Room' tag found. Make sure your rooms are tagged correctly.", MessageType.Warning);
            }

            EditorGUILayout.Space(10);

            // Bulk operations
            if (Selection.gameObjects.Length > 1)
            {
                EditorGUILayout.LabelField("Bulk Operations", EditorStyles.boldLabel);

                if (GUILayout.Button($"Auto-Detect All Selected Spawners ({Selection.gameObjects.Length})"))
                {
                    int assigned = 0;
                    foreach (GameObject obj in Selection.gameObjects)
                    {
                        EnemySpawner sp = obj.GetComponent<EnemySpawner>();
                        if (sp != null)
                        {
                            string detectedRoom = DetectRoomInEditMode(sp);
                            if (!string.IsNullOrEmpty(detectedRoom))
                            {
                                SerializedObject so = new SerializedObject(sp);
                                SerializedProperty roomProp = so.FindProperty("roomIdentifier");
                                roomProp.stringValue = detectedRoom;
                                so.ApplyModifiedProperties();
                                EditorUtility.SetDirty(sp);
                                assigned++;
                            }
                        }
                    }
                    Debug.Log($"Auto-assigned {assigned} out of {Selection.gameObjects.Length} selected spawners");
                }
            }
        }

        /// <summary>
        /// Detect room in edit mode using multiple methods
        /// </summary>
        private string DetectRoomInEditMode(EnemySpawner spawner)
        {
            GameObject[] rooms = GameObject.FindGameObjectsWithTag("Room");

            if (rooms.Length == 0)
                return null;

            // Method 1: Check hierarchy (is spawner a child of a room?)
            Transform parent = spawner.transform.parent;
            while (parent != null)
            {
                if (parent.CompareTag("Room"))
                {
                    return parent.gameObject.name;
                }
                parent = parent.parent;
            }

            // Method 2: Raycast downward
            RaycastHit hit;
            if (Physics.Raycast(spawner.transform.position, Vector3.down, out hit, 100f))
            {
                foreach (GameObject room in rooms)
                {
                    if (IsChildOf(hit.collider.transform, room.transform))
                    {
                        return room.name;
                    }
                }
            }

            // Method 3: Bounds check
            foreach (GameObject room in rooms)
            {
                Bounds bounds = CalculateRoomBounds(room);
                if (bounds.size != Vector3.zero && bounds.Contains(spawner.transform.position))
                {
                    return room.name;
                }
            }

            // Method 4: Closest room by bounds
            float closestDist = float.MaxValue;
            GameObject closestRoom = null;

            foreach (GameObject room in rooms)
            {
                Bounds bounds = CalculateRoomBounds(room);
                if (bounds.size != Vector3.zero)
                {
                    Vector3 closestPoint = bounds.ClosestPoint(spawner.transform.position);
                    float dist = Vector3.Distance(spawner.transform.position, closestPoint);

                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestRoom = room;
                    }
                }
            }

            return closestRoom != null ? closestRoom.name : null;
        }

        private bool IsChildOf(Transform child, Transform parent)
        {
            Transform current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private Bounds CalculateRoomBounds(GameObject room)
        {
            Renderer[] renderers = room.GetComponentsInChildren<Renderer>();
            Collider[] colliders = room.GetComponentsInChildren<Collider>();

            if (renderers.Length == 0 && colliders.Length == 0)
                return new Bounds(room.transform.position, Vector3.zero);

            Bounds bounds = new Bounds(room.transform.position, Vector3.zero);
            bool init = false;

            foreach (Renderer r in renderers)
            {
                if (!init)
                {
                    bounds = r.bounds;
                    init = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            foreach (Collider c in colliders)
            {
                if (!init)
                {
                    bounds = c.bounds;
                    init = true;
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