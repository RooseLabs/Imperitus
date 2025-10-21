using System;
using FishNet.Connection;
using FishNet.Object;
using RooseLabs.Core;
using RooseLabs.Network;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(-98)]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerData))]
    public class PlayerCharacter : NetworkBehaviour
    {
        public static PlayerCharacter LocalCharacter;

        public PlayerConnection Player => PlayerHandler.GetPlayer(Owner);

        [field: SerializeField] public Camera Camera { get; private set; }
        [Tooltip("Meshes to hide from the local player (e.g. body, head, accessories)")]
        [SerializeField] private GameObject[] meshesToHide = Array.Empty<GameObject>();

        public PlayerInput Input { get; private set; }
        public PlayerData Data { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerAnimations Animations { get; private set; }

        private Rigidbody m_rigidbody;

        private void Awake()
        {
            Input = GetComponent<PlayerInput>();
            Data = GetComponent<PlayerData>();
            Movement = GetComponent<PlayerMovement>();
            Animations = GetComponent<PlayerAnimations>();

            m_rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            PlayerHandler.RegisterCharacter(Owner, this);
        }

        // Called on each client when this object becomes visible to them.
        public override void OnStartClient()
        {
            // Initialize look values based on spawn rotation
            Data.lookValues.x = transform.eulerAngles.y;
            transform.rotation = Quaternion.identity;

            if (!IsOwner) return;
            LocalCharacter = this;

            UpdateLookDirection();

            // Hide renderers for local player
            int layer = LayerMask.NameToLayer("CameraCull");
            foreach (var m in meshesToHide)
            {
                m.layer = layer;
            }
            Camera.gameObject.SetActive(true);
            InputHandler.Instance.EnableGameplayInput();
        }

        private void Update()
        {
            if (!IsOwner) return;
            Input.Sample();
        }

        public void UpdateLookDirection()
        {
            Vector3 normalized = HelperFunctions.LookToDirection(Data.lookValues, Vector3.forward).normalized;
            Data.lookDirection = normalized;
            normalized.y = 0.0f;
            normalized.Normalize();
            Data.lookDirection_Flat = normalized;
        }

        [TargetRpc]
        public void SetPositionAndRotation(NetworkConnection _, Vector3 position, Quaternion rotation)
        {
            m_rigidbody.position = position;
            Data.lookValues.x = rotation.eulerAngles.y;
            UpdateLookDirection();
        }
    }
}
