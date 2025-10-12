using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerPickup : NetworkBehaviour
    {
        [SerializeField] private float raycastDistance;
        [SerializeField] private LayerMask pickupLayer;
        [SerializeField] private Transform pickupPosition;
        [SerializeField] public Vector3 crouchPickupOffset = new Vector3(0f, 0.5f, 0.85f);
        public Vector3 standingPickupPosition;
        private bool lastCrouchState;

        private Player m_player;
        private Book m_bookInHand;

        private bool m_hasObjectInHand;
        private GameObject m_objInHand;

        private void Awake()
        {
            m_player = GetComponent<Player>();
        }

        private void Start()
        {
            //Debug.Log("[PlayerPickup] Start called.");

            if (m_player == null)
            {
                //Debug.LogWarning("[PlayerPickup] No Player component found on the GameObject.");
            }

            standingPickupPosition = pickupPosition.localPosition;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
            {
                //Debug.Log("[PlayerPickup] Not owner, disabling script.");
                enabled = false;
            }
            else
            {
                //Debug.Log("[PlayerPickup] Is owner, script enabled.");
            }
        }

        private void Update()
        {
            // Prevent pickup if crawling
            if (m_player.Data.isCrawling)
                return;

            if (m_player.Input.interactWasPressed)
            {
                Debug.Log("[PlayerPickup] Interact input detected.");
                if (m_bookInHand != null)
                {
                    Debug.Log("[PlayerPickup] Interacting with book in hand.");
                    m_bookInHand.OnInteract(m_player, this);
                }
                else
                {
                    Debug.Log("[PlayerPickup] No book in hand, attempting pickup.");
                    Pickup();
                }
            }

            if (m_player.Input.dropWasPressed)
            {
                //Debug.Log("[PlayerPickup] Drop input detected.");
                Drop();
            }
        }

        private void Pickup()
        {
            if (m_hasObjectInHand) return;

            //Debug.Log("[PlayerPickup] Attempting pickup raycast.");
            if (Physics.Raycast(m_player.Camera.transform.position, m_player.Camera.transform.forward, out RaycastHit hit, raycastDistance, pickupLayer))
            {
                //Debug.Log($"[PlayerPickup] Raycast hit: {hit.transform.gameObject.name}");
                //Debug.Log("[PlayerPickup] No object in hand, picking up new object.");
                Pickup_ServerRPC(hit.transform.gameObject, gameObject);
                m_objInHand = hit.transform.gameObject;

                m_bookInHand = m_objInHand.GetComponent<Book>();
                if (m_bookInHand != null)
                {
                    m_bookInHand.OnPickup(m_player, this);
                }

                m_hasObjectInHand = true;
            }
            else
            {
                //Debug.Log("[PlayerPickup] Raycast did not hit any object.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void Pickup_ServerRPC(GameObject obj, GameObject player)
        {
            Pickup_ObserversRPC(obj, player);
        }

        [ObserversRpc]
        private void Pickup_ObserversRPC(GameObject obj, GameObject player)
        {
            var pickup = player.GetComponent<PlayerPickup>().pickupPosition;

            obj.transform.SetParent(pickup, false);
            SetObjectPositionAndOrRotation(obj, new Vector3(-0.11f, 0f, 0f), Quaternion.Euler(-116f, -180f, 90f));
            //obj.transform.localPosition = new Vector3(-0.11f, 0f, 0f);
            //obj.transform.localRotation = Quaternion.Euler(-90f, 0f, -90f);

            //Debug.Log($"[Pickup] World rotation: {obj.transform.rotation.eulerAngles}, Local rotation: {obj.transform.localRotation.eulerAngles}");

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;
        }

        public void SetObjectPositionAndOrRotation(GameObject obj, Vector3? localPosition = null, Quaternion? localRotation = null)
        {
            if (obj == null) return;
            if (localPosition.HasValue)
            {
                obj.transform.localPosition = localPosition.Value;
            }
            if (localRotation.HasValue)
            {
                obj.transform.localRotation = localRotation.Value;
            }
        }


        public void Drop()
        {
            if (!m_hasObjectInHand)
            {
                //Debug.Log("[PlayerPickup] Drop called but no object in hand.");
                return;
            }

            //Debug.Log("[PlayerPickup] Dropping object.");
            Drop_ServerRPC(m_objInHand);
            m_hasObjectInHand = false;
            m_objInHand = null;
            m_bookInHand = null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void Drop_ServerRPC(GameObject obj)
        {
            Drop_ObserversRPC(obj);
        }


        [ObserversRpc]
        private void Drop_ObserversRPC(GameObject obj)
        {
            // Always find WorldObjects locally (safe, since it exists in the scene everywhere)
            var worldHolder = GameObject.FindGameObjectWithTag("WorldObjects")?.transform;

            if (worldHolder != null)
            {
                obj.transform.parent = worldHolder;
            }
            else
            {
                obj.transform.parent = null; // fallback
                Debug.LogWarning("[PlayerPickup] Could not find WorldObjects holder, defaulting to root.");
            }

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }

        public bool HasItemInHand()
        {
            return m_hasObjectInHand && m_objInHand != null;
        }

        public void SetPickupPositionForCrouch(bool isCrouching)
        {
            if (pickupPosition == null) return;
            if (isCrouching == lastCrouchState) return; // skip duplicate

            lastCrouchState = isCrouching;

            pickupPosition.localPosition = isCrouching
                ? crouchPickupOffset
                : standingPickupPosition;

            UpdatePickupPosition_ServerRpc(isCrouching);
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdatePickupPosition_ServerRpc(bool isCrouching)
        {
            UpdatePickupPosition_ObserversRpc(isCrouching);
        }

        [ObserversRpc]
        private void UpdatePickupPosition_ObserversRpc(bool isCrouching)
        {
            if (pickupPosition == null)
                return;

            pickupPosition.localPosition = isCrouching
                ? crouchPickupOffset
                : standingPickupPosition;
        }
    }
}
