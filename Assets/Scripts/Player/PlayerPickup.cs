using FishNet.Object;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerPickup : NetworkBehaviour
    {
        [SerializeField] private float raycastDistance;
        [SerializeField] private LayerMask pickupLayer;
        [SerializeField] private Transform pickupPosition;

        // Reference to player and player input
        private Player m_player;

        private bool m_hasObjectInHand;
        private GameObject m_objInHand;
        private Transform m_worldObjectHolder;

        private void Awake()
        {
            m_player = GetComponent<Player>();
        }

        private void Start()
        {
            Debug.Log("[PlayerPickup] Start called.");

            if (m_player == null)
            {
                Debug.LogWarning("[PlayerPickup] No Player component found on the GameObject.");
            }

            var worldObj = GameObject.FindGameObjectWithTag("WorldObjects");
            if (worldObj != null)
            {
                m_worldObjectHolder = worldObj.transform;
                Debug.Log("[PlayerPickup] WorldObjects holder found and assigned.");
            }
            else
            {
                Debug.LogWarning("[PlayerPickup] No GameObject with tag 'WorldObjects' found.");
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!IsOwner)
            {
                Debug.Log("[PlayerPickup] Not owner, disabling script.");
                enabled = false;
            }
            else
            {
                Debug.Log("[PlayerPickup] Is owner, script enabled.");
            }
        }

        private void Update()
        {
            if (m_player.Input.interactWasPressed)
            {
                Debug.Log("[PlayerPickup] Interact input detected.");
                Pickup();
            }

            if (m_player.Input.dropWasPressed)
            {
                Debug.Log("[PlayerPickup] Drop input detected.");
                Drop();
            }
        }

        private void Pickup()
        {
            Debug.Log("[PlayerPickup] Attempting pickup raycast.");
            if (Physics.Raycast(m_player.Camera.transform.position, m_player.Camera.transform.forward, out RaycastHit hit, raycastDistance, pickupLayer))
            {
                Debug.Log($"[PlayerPickup] Raycast hit: {hit.transform.gameObject.name}");

                if (!m_hasObjectInHand)
                {
                    Debug.Log("[PlayerPickup] No object in hand, picking up new object.");
                    SetObjectInHandServer(hit.transform.gameObject, pickupPosition.position, pickupPosition.rotation, gameObject);
                    m_objInHand = hit.transform.gameObject;
                    m_hasObjectInHand = true;
                }
                else if (m_hasObjectInHand)
                {
                    Debug.Log("[PlayerPickup] Already holding object, dropping and picking up new object.");
                    Drop();

                    SetObjectInHandServer(hit.transform.gameObject, pickupPosition.position, pickupPosition.rotation, gameObject);
                    m_objInHand = hit.transform.gameObject;
                    m_hasObjectInHand = true;
                }
            }
            else
            {
                Debug.Log("[PlayerPickup] Raycast did not hit any object.");
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetObjectInHandServer(GameObject obj, Vector3 position, Quaternion rotation, GameObject player)
        {
            Debug.Log($"[PlayerPickup] [ServerRpc] SetObjectInHandServer called for {obj.name}");
            SetObjectInHandObserver(obj, position, rotation, player);
        }

        [ObserversRpc]
        private void SetObjectInHandObserver(GameObject obj, Vector3 position, Quaternion rotation, GameObject player)
        {
            Debug.Log($"[PlayerPickup] [ObserversRpc] SetObjectInHandObserver called for {obj.name}");
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.parent = player.transform;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                Debug.Log("[PlayerPickup] Rigidbody set to kinematic.");
            }
        }

        private void Drop()
        {
            if (!m_hasObjectInHand)
            {
                Debug.Log("[PlayerPickup] Drop called but no object in hand.");
                return;
            }

            Debug.Log("[PlayerPickup] Dropping object.");
            DropObjectServer(m_objInHand, m_worldObjectHolder);
            m_hasObjectInHand = false;
            m_objInHand = null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void DropObjectServer(GameObject obj, Transform worldHolder)
        {
            Debug.Log($"[PlayerPickup] [ServerRpc] DropObjectServer called for {obj.name}");
            DropObjectObserver(obj, worldHolder);
        }

        [ObserversRpc]
        private void DropObjectObserver(GameObject obj, Transform worldHolder)
        {
            Debug.Log($"[PlayerPickup] [ObserversRpc] DropObjectObserver called for {obj.name}");
            obj.transform.parent = worldHolder;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                Debug.Log("[PlayerPickup] Rigidbody set to non-kinematic.");
            }
        }
    }
}
