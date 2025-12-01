using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using RooseLabs.Core;
using RooseLabs.Gameplay;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Network;
using RooseLabs.UI;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(-97)]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerData))]
    public class PlayerCharacter : NetworkBehaviour, IDamageable
    {
        public static PlayerCharacter LocalCharacter;

        public PlayerConnection Player => PlayerHandler.GetPlayer(Owner);

        #region Serialized
        [field: SerializeField] public Transform ModelTransform { get; private set; }
        [field: SerializeField] public Camera Camera { get; private set; }

        [Tooltip("Meshes to hide from the local player (e.g. body, head, accessories)")]
        [SerializeField] private GameObject[] meshesToHide = Array.Empty<GameObject>();

        [field: SerializeField] public Transform RaycastTarget { get; private set; }

        [SerializeField] private GameObject droppedNotebookPrefab;
        #endregion

        #region References
        public PlayerInput Input { get; private set; }
        public PlayerData Data { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerWand Wand { get; private set; }
        public PlayerItems Items  { get; private set; }
        public PlayerAnimations Animations { get; private set; }
        public PlayerRagdoll Ragdoll { get; private set; }
        public PlayerNotebook Notebook { get; private set; }

        private Rigidbody m_rigidbody;
        #endregion

        private readonly Dictionary<Collider, int> m_characterColliders = new();

        private void Awake()
        {
            Input = GetComponent<PlayerInput>();
            Data = GetComponent<PlayerData>();
            Movement = GetComponent<PlayerMovement>();
            Wand = GetComponent<PlayerWand>();
            Items  = GetComponent<PlayerItems>();
            Animations = GetComponent<PlayerAnimations>();
            Ragdoll = GetComponent<PlayerRagdoll>();
            Notebook = GetComponentInChildren<PlayerNotebook>();

            m_rigidbody = GetComponent<Rigidbody>();
        }

        public override void OnStartNetwork()
        {
            PlayerHandler.RegisterCharacter(Owner, this);
        }

        // Called on each client when this object becomes visible to them.
        public override void OnStartClient()
        {
            if (!IsOwner) return;
            LocalCharacter = this;
            GUIManager.Instance.SetHUDActive(true);

            // Initialize look values based on spawn rotation
            Data.lookValues.x = transform.eulerAngles.y;
            m_rigidbody.rotation = Quaternion.identity;
            UpdateLookDirection();

            // Hide renderers for local player
            int layer = LayerMask.NameToLayer("CameraCull");
            foreach (var m in meshesToHide)
            {
                m.layer = layer;
            }

            // Populate character colliders dictionary, storing their original layers
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                m_characterColliders[col] = col.gameObject.layer;
            }

            Camera.gameObject.SetActive(true);
            InputHandler.Instance.EnableGameplayInput();
        }

        private void Update()
        {
            if (!IsOwner) return;
            Input.Sample();

            UpdateVariables();
        }

        private void UpdateVariables()
        {
            const float staminaRegenRate = 40f;

            Data.sinceUseStamina += Time.deltaTime;
            if (!CanRegenStamina()) return;
            Data.Stamina += staminaRegenRate * Time.deltaTime;
        }

        public void UpdateLookDirection()
        {
            Vector3 normalized = HelperFunctions.LookToDirection(Data.lookValues, Vector3.forward).normalized;
            Data.lookDirection = normalized;
            normalized.y = 0.0f;
            normalized.Normalize();
            Data.lookDirectionFlat = normalized;
        }

        [TargetRpc]
        public void SetPositionAndRotation(NetworkConnection _, Vector3 position, Quaternion rotation)
        {
            m_rigidbody.position = position;
            Data.lookValues.x = rotation.eulerAngles.y;
            UpdateLookDirection();
        }

        public bool UseStamina(float amount)
        {
            if (amount == 0.0f) return true;
            Data.Stamina -= amount;
            Data.sinceUseStamina = 0.0f;
            return Data.Stamina > 0.0f;
        }

        private bool CanRegenStamina()
        {
            return Data.sinceUseStamina >= (Data.Stamina > 0.0f ? 1.0f : 2.0f);
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (Data.Health <= 0) return false;
            if (IsServerInitialized)
            {
                ApplyDamage_ObserversRPC(damage);
            }
            else
            {
                ApplyDamage_ServerRPC(damage);
            }
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ApplyDamage_ServerRPC(DamageInfo damage)
        {
            ApplyDamage_ObserversRPC(damage);
        }

        [ObserversRpc]
        private void ApplyDamage_ObserversRPC(DamageInfo damage)
        {
            Data.Health -= damage.amount;
            if (Data.Health <= 0)
            {
                Data.isDead = true;
                if (IsServerInitialized)
                {
                    HandlePlayerDeath();
                }
                this.LogInfo($"Player '{Player.PlayerName}' died!");
            }
        }

        [Server]
        private void HandlePlayerDeath()
        {
            // Spawn dropped notebook
            GameObject droppedNotebook = Instantiate(droppedNotebookPrefab, transform.position + Vector3.up * 1.0f, Quaternion.identity);
            Spawn(droppedNotebook, null, GameManager.Instance.CurrentScene);
            droppedNotebook.GetComponent<DroppedNotebook>().Initialize(this);

            // Trigger ragdoll
            Ragdoll.TriggerRagdoll(Vector3.back * 500f, Ragdoll.HipsBone.position, false);
        }

        public void ResetState()
        {
            if (IsServerInitialized)
            {
                ResetState_ObserversRPC();
            }
            else if (IsOwner)
            {
                ResetState_ServerRPC();
                ResetState_Internal();
            }
        }

        [ServerRpc(RequireOwnership = true)]
        private void ResetState_ServerRPC()
        {
            ResetState_ObserversRPC();
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void ResetState_ObserversRPC()
        {
            ResetState_Internal();
        }

        private void ResetState_Internal()
        {
            Data.Health = Data.MaxHealth;
            Data.Stamina = Data.MaxStamina;
            if (Data.isDead)
            {
                Ragdoll.ToggleRagdoll(false);
                Data.IsRagdollActive = false;
                Data.isDead = false;
            }
            if (IsOwner)
            {
                CameraController.Instance.ResetPosition();
                Wand.RemoveTemporarySpell();
            }
            // Reset the runes in the notebook
            Notebook.ResetNotebook();
        }

        #region Utils
        public Transform GetBodypart(HumanBodyBones bone) => Ragdoll.partDict[bone];

        public bool RaycastIgnoreSelf(Ray ray, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return RaycastIgnoreSelf(ray.origin, ray.direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        public bool RaycastIgnoreSelf(Vector3 position, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            // Set all character colliders to the Ignore Raycast layer
            foreach (var col in m_characterColliders.Keys)
            {
                col.gameObject.layer = 2; // Ignore Raycast layer
            }
            bool hit = Physics.Raycast(position, direction, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
            // Restore original layers
            foreach (var (col, layer) in m_characterColliders)
            {
                col.gameObject.layer = layer;
            }
            return hit;
        }
        #endregion
    }
}
