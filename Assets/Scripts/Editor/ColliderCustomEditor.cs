using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RooseLabs.Editor
{
    [CustomEditor(typeof(Collider), true)]
    public class ColliderCustomEditor : UnityEditor.Editor
    {
        private static bool s_handlerRegistered = false;

        [InitializeOnLoadMethod]
        private static void RegisterDropHandler()
        {
            if (!s_handlerRegistered)
            {
                DragAndDrop.AddDropHandlerV2(HierarchyDropHandler);
                s_handlerRegistered = true;
            }
        }

        private static DragAndDropVisualMode HierarchyDropHandler(EntityId entityId, HierarchyDropFlags dropMode, Transform parentForDraggedObjects, bool perform)
        {
            if (DragAndDrop.objectReferences.Length <= 0)
                return DragAndDropVisualMode.None;

            // Verify the dragged object is one of the supported collider types
            Object draggedObjectRef = DragAndDrop.objectReferences[0];
            if (draggedObjectRef is not BoxCollider &&
                draggedObjectRef is not SphereCollider &&
                draggedObjectRef is not CapsuleCollider)
            {
                return DragAndDropVisualMode.None;
            }
            Collider sourceCollider = draggedObjectRef as Collider;

            GameObject sourceGameObject = sourceCollider.gameObject;
            GameObject targetGameObject = null;

            if (entityId != 0)
            {
                targetGameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;
            }
            else if (parentForDraggedObjects != null)
            {
                targetGameObject = parentForDraggedObjects.gameObject;
            }

            if (targetGameObject != null && targetGameObject != sourceGameObject && perform)
            {
                bool originalActiveState = sourceGameObject.activeSelf;
                sourceGameObject.SetActive(true);
                // Add a new collider of the same type to the target GameObject
                Collider newCollider = Undo.AddComponent(targetGameObject, sourceCollider.GetType()) as Collider;
                // Copy properties from source to new
                ComponentUtility.CopyComponent(sourceCollider);
                ComponentUtility.PasteComponentValues(newCollider);

                switch (newCollider)
                {
                    // Override center and size with world-space values, adjusted for the new GameObject's local space
                    case BoxCollider boxCollider when sourceCollider is BoxCollider sourceBox:
                        boxCollider.center = sourceBox.bounds.center - sourceGameObject.transform.position;
                        boxCollider.size = sourceBox.bounds.size;
                        break;
                    case SphereCollider sphereCollider when sourceCollider is SphereCollider sourceSphere:
                        sphereCollider.center = sourceSphere.bounds.center - sourceGameObject.transform.position;
                        sphereCollider.radius = Mathf.Max(sourceSphere.bounds.extents.x, sourceSphere.bounds.extents.y, sourceSphere.bounds.extents.z);
                        break;
                    case CapsuleCollider capsuleCollider when sourceCollider is CapsuleCollider sourceCapsule:
                        capsuleCollider.center = sourceCapsule.bounds.center - sourceGameObject.transform.position;
                        capsuleCollider.height = sourceCapsule.bounds.size.y;
                        capsuleCollider.radius = Mathf.Max(sourceCapsule.bounds.extents.x, sourceCapsule.bounds.extents.z);
                        break;
                }

                // Destroy the original (to simulate "move")
                Undo.DestroyObjectImmediate(sourceCollider);
                sourceGameObject.SetActive(originalActiveState);

                return DragAndDropVisualMode.Move;
            }

            return DragAndDropVisualMode.Generic;

        }
    }
}
