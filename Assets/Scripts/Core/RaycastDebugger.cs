using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace RooseLabs
{
    /// <summary>
    /// Temporary debugging script to see what UI elements are being hit by raycasts.
    /// Attach this to any GameObject in the scene.
    /// Remove after debugging is complete.
    /// </summary>
    public class RaycastDebugger : MonoBehaviour
    {
        private Mouse m_mouse;

        private void Awake()
        {
            m_mouse = Mouse.current;
        }

        private void Update()
        {
            if (m_mouse == null)
            {
                UnityEngine.Debug.LogWarning("[RaycastDebug] Mouse not found!");
                return;
            }

            // Only check when left mouse button is clicked
            if (m_mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 mousePosition = m_mouse.position.ReadValue();

                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                UnityEngine.Debug.Log($"[RaycastDebug] Mouse clicked at: {mousePosition}");
                UnityEngine.Debug.Log($"[RaycastDebug] Found {results.Count} UI elements under cursor:");

                foreach (RaycastResult result in results)
                {
                    UnityEngine.Debug.Log($"  - {result.gameObject.name} (Layer: {LayerMask.LayerToName(result.gameObject.layer)}, Canvas: {result.gameObject.GetComponentInParent<Canvas>()?.name ?? "None"})");
                }

                if (results.Count == 0)
                {
                    UnityEngine.Debug.LogWarning("[RaycastDebug] No UI elements found under cursor! Check if EventSystem is working.");
                }
            }
        }
    }
}