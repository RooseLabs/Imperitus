using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(BoxCollider))]
    public class BoxColliderCustomEditor : UnityEditor.Editor
    {
        private static bool s_handlerRegistered = false;

        [InitializeOnLoadMethod]
        private static void RegisterDropHandler()
        {
            if (!s_handlerRegistered)
            {
                DragAndDrop.AddDropHandler(HierarchyDropHandler);
                s_handlerRegistered = true;
            }
        }

        private static DragAndDropVisualMode HierarchyDropHandler(int dropTargetInstanceID, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is BoxCollider sourceCollider)
            {
                GameObject sourceGameObject = sourceCollider.gameObject;
                GameObject targetGameObject = null;

                if (dropTargetInstanceID != 0)
                {
                    targetGameObject = EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject;
                }
                else if (parentForDraggedObjects != null)
                {
                    targetGameObject = parentForDraggedObjects.gameObject;
                }

                if (targetGameObject != null && targetGameObject != sourceGameObject && perform)
                {
                    bool originalActiveState = sourceGameObject.activeSelf;
                    sourceGameObject.SetActive(true);
                    // Add a new BoxCollider to the target
                    BoxCollider newCollider = Undo.AddComponent<BoxCollider>(targetGameObject);

                    // Copy properties from source to new
                    ComponentUtility.CopyComponent(sourceCollider);
                    ComponentUtility.PasteComponentValues(newCollider);

                    // Override center and size with world-space values, adjusted for the new GameObject's local space
                    newCollider.center = sourceCollider.bounds.center - sourceCollider.transform.position;
                    newCollider.size = sourceCollider.bounds.size;

                    // Destroy the original (to simulate "move")
                    Undo.DestroyObjectImmediate(sourceCollider);
                    sourceGameObject.SetActive(originalActiveState);

                    return DragAndDropVisualMode.Move;
                }

                return DragAndDropVisualMode.Generic;
            }

            return DragAndDropVisualMode.None;
        }
    }
}
