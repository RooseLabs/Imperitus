using FishNet.Component.Transforming;
using FishNet.Object;
using RooseLabs.Core;
using UnityEngine;

namespace RooseLabs.Player
{
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerData))]
    public class Player : NetworkBehaviour
    {
        public static Player LocalPlayer;

        [field: SerializeField]
        public GameObject Camera { get; private set; }

        public PlayerMovement Movement { get; private set; }
        public PlayerInput Input { get; private set; }

        private void Awake()
        {
            Movement = GetComponent<PlayerMovement>();
            Input = GetComponent<PlayerInput>();
        }

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                LocalPlayer = this;
                Camera.SetActive(true);
                InputHandler.Instance.EnableGameplayInput();
                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        private void Update()
        {
            if (!IsOwner)
                return;
            Input.Sample();
        }
    }
}
