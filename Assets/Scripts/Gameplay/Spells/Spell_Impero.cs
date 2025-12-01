using FishNet.Object;
using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Impero : SpellBase
    {
        #region Serialized
        [Header("Impero Spell Data")]
        [Tooltip("Maximum distance that the spell can reach.")]
        [SerializeField] private float maxDistance = 5f;

        [Header("Tube Visual")]
        [Tooltip("Radius of the 3D tube (thickness).")]
        [SerializeField] private float tubeRadius = 0.02f;
        [Tooltip("Material for the tube segments and caps.")]
        [SerializeField] private Material tubeMaterial;
        [Tooltip("Number of tube segments for the curve. Higher = smoother.")]
        [SerializeField, Range(10, 50)] private int numSegments = 10;
        #endregion

        private Draggable m_currentGrabbedObject;
        private Vector3 m_currentGrabbedLocalHitPoint;
        private float m_currentDragDistance;
        private float m_targetDragDistance;
        private float m_minSafeDragDistance;
        private const float MinDragDistanceBuffer = 0.5f;

        private GameObject[] m_tubeSegments;
        private GameObject m_startCap;
        private GameObject m_endCap;

        protected override bool OnCastFinished()
        {
            var character = PlayerCharacter.LocalCharacter;
            var cameraPosition = character.Camera.transform.position;
            if (!character.RaycastIgnoreSelf(cameraPosition, character.Data.lookDirection,
                    out RaycastHit hitInfo, maxDistance, HelperFunctions.AllPhysicalLayerMask))
            {
                Logger.Info("[Impero] No hit detected.");
                return false;
            }
            Logger.Info($"[Impero] Hit object: {hitInfo.collider.name}");
            if (!hitInfo.collider.TryGetComponent(out Draggable hitDraggable)) return false;

            if (!hitDraggable.IsDraggable) return false;
            if (hitDraggable.IsDoor)
            {
                m_minSafeDragDistance = 1.0f;
            }
            else
            {
                // Determine the minimum safe distance to avoid clipping with the object
                Vector3 closestPoint = hitInfo.collider.ClosestPoint(cameraPosition);
                float closestDistance = Vector3.Distance(cameraPosition, closestPoint);
                m_minSafeDragDistance = MinDragDistanceBuffer + (hitInfo.distance - closestDistance);
            }

            hitDraggable.HandleDragBegin(hitInfo.point);
            m_currentGrabbedLocalHitPoint = hitDraggable.transform.InverseTransformPoint(hitInfo.point);
            m_currentDragDistance = hitInfo.distance;
            m_targetDragDistance = Mathf.Clamp(m_currentDragDistance, m_minSafeDragDistance, maxDistance);
            if (IsServerInitialized)
            {
                SetGrabbedObject_ObserversRpc(hitDraggable, m_currentGrabbedLocalHitPoint);
            }
            else
            {
                SetGrabbedObject_ServerRpc(hitDraggable, m_currentGrabbedLocalHitPoint);
                m_currentGrabbedObject = hitDraggable;
            }
            return true;
        }

        protected override void OnContinueCastSustained()
        {
            if (!m_currentGrabbedObject || !m_currentGrabbedObject.IsOwner || !m_currentGrabbedObject.IsDraggable)
            {
                CancelCast();
                return;
            }

            var character = PlayerCharacter.LocalCharacter;

            Vector3 cameraPosition = character.Camera.transform.position;
            Vector3 grabbedWorldHitPoint = m_currentGrabbedObject.transform.TransformPoint(m_currentGrabbedLocalHitPoint);
            float currentGrabDistance = Vector3.Distance(cameraPosition, grabbedWorldHitPoint);
            if (m_currentGrabbedObject.IsDoor)
            {
                if (currentGrabDistance > maxDistance)
                {
                    // Door is out of range, cancel the cast
                    CancelCast();
                    return;
                }
            }
            else
            {
                // Update the minimum safe drag distance to avoid clipping with the object
                Vector3 closestPoint = m_currentGrabbedObject.Collider.ClosestPoint(cameraPosition);
                float closestDistance = Vector3.Distance(closestPoint, cameraPosition);
                m_minSafeDragDistance = MinDragDistanceBuffer + Mathf.Max(0.0f, currentGrabDistance - closestDistance);
                m_targetDragDistance = Mathf.Clamp(m_targetDragDistance, m_minSafeDragDistance, maxDistance);
            }

            m_currentDragDistance = Mathf.Lerp(m_currentDragDistance, m_targetDragDistance, Time.deltaTime * 5f);
            Vector3 desiredPosition = character.Camera.transform.position + character.Data.lookDirection * m_currentDragDistance;
            m_currentGrabbedObject.HandleDrag(desiredPosition);

            // Update tube positions
            UpdateVisuals(character);
        }

        protected override void OnCancelCastSustained()
        {
            // Release the dragged object
            m_currentGrabbedObject?.HandleDragEnd();

            if (IsServerInitialized)
            {
                SetGrabbedObject_ObserversRpc(null, Vector3.zero);
            }
            else
            {
                SetGrabbedObject_ServerRpc(null, Vector3.zero);
                m_currentGrabbedObject = null;
                m_currentGrabbedLocalHitPoint = Vector3.zero;
            }

            // Destroy visuals
            DestroyVisuals();
        }

        protected override void OnScrollBackwardHeld()
        {
            OnScroll(-0.1f);
        }

        protected override void OnScrollForwardHeld()
        {
            OnScroll(0.1f);
        }

        protected override void OnScroll(float value)
        {
            if (!m_currentGrabbedObject) return;
            m_targetDragDistance = Mathf.Clamp(m_targetDragDistance + value, m_minSafeDragDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if ((bool)tubeMaterial && (bool)m_currentGrabbedObject)
            {
                if (m_tubeSegments == null)
                {
                    CreateVisuals();
                }
                UpdateVisuals(CasterCharacter);
            }
            else if (m_tubeSegments != null)
            {
                DestroyVisuals();
            }
        }

        private void CreateVisuals()
        {
            m_tubeSegments = new GameObject[numSegments];

            for (int i = 0; i < numSegments; ++i)
            {
                GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.transform.SetParent(transform);
                cyl.GetComponent<MeshRenderer>().material = tubeMaterial;
                Destroy(cyl.GetComponent<Collider>());
                m_tubeSegments[i] = cyl;
            }

            // Start cap
            m_startCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_startCap.transform.SetParent(transform);
            m_startCap.GetComponent<MeshRenderer>().material = tubeMaterial;
            Destroy(m_startCap.GetComponent<Collider>());

            // End cap
            m_endCap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_endCap.transform.SetParent(transform);
            m_endCap.GetComponent<MeshRenderer>().material = tubeMaterial;
            Destroy(m_endCap.GetComponent<Collider>());
        }

        private static Vector3 GetQuadraticBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 p = uu * p0;
            p += 2 * u * t * p1;
            p += tt * p2;
            return p;
        }

        private void UpdateVisuals(PlayerCharacter character)
        {
            if (m_tubeSegments == null || !m_currentGrabbedObject) return;

            Vector3 castPos = transform.position;
            Vector3 grabPos = m_currentGrabbedObject.transform.TransformPoint(m_currentGrabbedLocalHitPoint);
            float dist = Vector3.Distance(castPos, grabPos);
            if (dist < 0.01f)
            {
                // Too close, hide visuals
                foreach (var seg in m_tubeSegments)
                {
                    seg.SetActive(false);
                }
                m_startCap.SetActive(false);
                m_endCap.SetActive(false);
                return;
            }

            Vector3 lookDir = CasterCharacter == PlayerCharacter.LocalCharacter
                ? character.Data.lookDirection
                : character.ModelTransform.forward;
            Vector3 midPoint = castPos + lookDir * (dist * 0.5f);

            int numPoints = numSegments + 1;
            Vector3[] points = new Vector3[numPoints];

            for (int i = 0; i < numPoints; ++i)
            {
                float t = i / (float)(numPoints - 1);
                points[i] = GetQuadraticBezierPoint(t, castPos, midPoint, grabPos);
            }

            // Update caps
            m_startCap.transform.position = points[0];
            m_startCap.transform.localScale = Vector3.one * (tubeRadius * 2f);
            m_startCap.SetActive(true);
            m_endCap.transform.position = points[numPoints - 1];
            m_endCap.transform.localScale = Vector3.one * (tubeRadius * 2f);
            m_endCap.SetActive(true);

            // Update tube segments
            for (int seg = 0; seg < numSegments; ++seg)
            {
                Vector3 pa = points[seg];
                Vector3 pb = points[seg + 1];
                Vector3 dirVec = pb - pa;
                float len = dirVec.magnitude;
                if (len < 0.001f)
                {
                    m_tubeSegments[seg].SetActive(false);
                    continue;
                }

                Vector3 dir = dirVec.normalized;
                Transform segTrans = m_tubeSegments[seg].transform;
                segTrans.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                segTrans.position = (pa + pb) * 0.5f;
                segTrans.localScale = new Vector3(tubeRadius * 2f, len * 0.5f, tubeRadius * 2f);
                m_tubeSegments[seg].SetActive(true);
            }
        }

        private void DestroyVisuals()
        {
            if (m_tubeSegments != null)
            {
                foreach (var seg in m_tubeSegments)
                {
                    Destroy(seg);
                }
                m_tubeSegments = null;
            }
            if (m_startCap)
            {
                Destroy(m_startCap);
                m_startCap = null;
            }
            if (m_endCap)
            {
                Destroy(m_endCap);
                m_endCap = null;
            }
        }

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void SetGrabbedObject_ServerRpc(Draggable draggable, Vector3 localHitPoint)
        {
            SetGrabbedObject_ObserversRpc(draggable, localHitPoint);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void SetGrabbedObject_ObserversRpc(Draggable draggable, Vector3 localHitPoint)
        {
            m_currentGrabbedObject = draggable;
            m_currentGrabbedLocalHitPoint = localHitPoint;
        }
        #endregion

        protected override void ResetData()
        {
            base.ResetData();
            DestroyVisuals();
            m_currentGrabbedObject = null;
            m_currentGrabbedLocalHitPoint = Vector3.zero;
            m_currentDragDistance = 0f;
            m_targetDragDistance = 0f;
            m_minSafeDragDistance = 0f;
        }
    }
}
