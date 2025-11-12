using System.Collections;
using FishNet.Component.Transforming;
using FishNet.Object;
using RooseLabs.Player;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay.Interactables
{
    public enum ItemState
    {
        Ground,
        Held
    }

    public class Item : Draggable, IInteractable
    {
        private static Logger Logger => Logger.GetLogger("Interactions");

        public ItemState State { get; private set; } = ItemState.Ground;

        public PlayerCharacter HolderCharacter { get; private set; }

        private Coroutine m_pickupCoroutine;
        private NetworkTransform m_networkTransform;

        // Perlin noise parameters
        private float m_noiseTime = 0f;
        private const float NoiseFrequency = 1f;
        private const float NoiseAmplitude = 0.03f;
        private bool m_noiseActive = false;

        protected override void Awake()
        {
            base.Awake();
            TryGetComponent(out m_networkTransform);
        }

        private void LateUpdate()
        {
            if (State == ItemState.Held && HolderCharacter && m_noiseActive)
                ApplyHeldItemPerlinNoise();
        }

        private void ApplyHeldItemPerlinNoise()
        {
            m_noiseTime += Time.deltaTime * NoiseFrequency;
            float noiseX = (Mathf.PerlinNoise(m_noiseTime, 0f) - 0.5f) * 2f * NoiseAmplitude;
            float noiseY = (Mathf.PerlinNoise(m_noiseTime + 10f, 0f) - 0.5f) * 2f * NoiseAmplitude;
            float noiseZ = (Mathf.PerlinNoise(m_noiseTime + 20f, 0f) - 0.5f) * 2f * NoiseAmplitude;
            transform.localPosition = new Vector3(noiseX, noiseY, noiseZ);
        }

        private void SetNetworkTransformSync(bool enable)
        {
            if (!m_networkTransform) return;
            SynchronizedProperty props = enable ? SynchronizedProperty.Position | SynchronizedProperty.Rotation
                : SynchronizedProperty.None;
            m_networkTransform.SetSynchronizedProperties(props);
        }

        private void SetState(ItemState newState, PlayerCharacter character = null)
        {
            State = newState;
            switch (newState)
            {
                case ItemState.Ground:
                    m_noiseActive = false;
                    m_noiseTime = 0f;
                    HolderCharacter = null;
                    Collider.enabled = true;
                    rb.isKinematic = !IsController;
                    rb.interpolation = IsController ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
                    SetNetworkTransformSync(true);
                    break;
                case ItemState.Held:
                    m_noiseActive = false;
                    m_noiseTime = 0f;
                    HolderCharacter = character;
                    Collider.enabled = false;
                    rb.isKinematic = true;
                    rb.interpolation = RigidbodyInterpolation.None;
                    SetNetworkTransformSync(false);
                    break;
            }
        }

        // private bool m_hasDropped = false;
        // private void OnCollisionEnter(Collision collision)
        // {
        //     if (m_hasDropped) return; // Only emit once per drop
        //
        //     // Emit "ItemDropped" sound
        //     var soundEmitter = GetComponent<SoundEmitter>();
        //     if (soundEmitter != null)
        //     {
        //         Logger.Info("[Book] Emitting ItemDropped sound.");
        //         soundEmitter.RequestEmitFromClient("ItemDropped");
        //     }
        //
        //     m_hasDropped = true;
        // }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPickup(PlayerCharacter character)
        {
            if (State == ItemState.Held) return;
            GiveOwnership(character.Owner);
            PickupAccepted_ObserversRPC(character);
        }

        [ObserversRpc]
        private void PickupAccepted_ObserversRPC(PlayerCharacter character)
        {
            SetState(ItemState.Held, character);
            character.Items.PickupItem(this);
            m_pickupCoroutine = StartCoroutine(DoPickupSequence());
        }

        [ServerRpc(RequireOwnership = true)]
        public void RequestDrop()
        {
            if (State == ItemState.Ground) return;
            DropAccepted_ObserversRPC();
        }

        [ObserversRpc]
        private void DropAccepted_ObserversRPC()
        {
            // Cancel any in-progress pickup transition.
            if (m_pickupCoroutine != null)
            {
                StopCoroutine(m_pickupCoroutine);
                m_pickupCoroutine = null;
            }
            transform.SetParent(null);
            SetState(ItemState.Ground);
            OnDrop();
            // m_hasDropped = false;
        }

        private IEnumerator DoPickupSequence()
        {
            OnPickupStart();
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;
            const float duration = 1f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.localPosition = Vector3.Lerp(startPos, Vector3.zero, t);
                transform.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            OnPickupEnd();
            m_noiseTime = 0f;
            m_noiseActive = true;
            m_pickupCoroutine = null;
        }

        public virtual void OnPickupStart() {}
        public virtual void OnPickupEnd() {}
        public virtual void OnDrop() {}

        public override bool IsDraggable => State == ItemState.Ground;

        public virtual bool IsInteractable(PlayerCharacter interactor) => State == ItemState.Ground;

        public virtual void Interact(PlayerCharacter interactor)
        {
            RequestPickup(interactor);
        }

        public virtual string GetInteractionText() => string.Empty;
    }
}
