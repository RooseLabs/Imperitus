using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using MetaVoiceChat;
using RooseLabs.Core;
using RooseLabs.Gameplay;
using RooseLabs.Gameplay.Interactables;
using RooseLabs.Gameplay.Notebook;
using RooseLabs.Network;
using RooseLabs.Settings;
using RooseLabs.UI;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    [DefaultExecutionOrder(-97)]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(PlayerData))]
    public class PlayerCharacter : NetworkBehaviour, IDamageable, IPetrifiable
    {
        #region Serialized
        [field: SerializeField] public Transform ModelTransform { get; private set; }
        [field: SerializeField] public Camera Camera { get; private set; }
        [field: SerializeField] public MetaVc VoiceChat { get; private set; }

        [Tooltip("Meshes to hide from the local player (e.g. body, head, accessories)")]
        [SerializeField] private GameObject[] meshesToHide = Array.Empty<GameObject>();

        [field: SerializeField] public Transform RaycastTarget { get; private set; }

        [SerializeField] private GameObject droppedNotebookPrefab;

        [Header("Death Sound Effects")]
        [SerializeField] private string deathAnnouncerSoundKey = "Player_Death_Announcer";
        [SerializeField] private string deathLocalSoundKey = "Player_Death";
        [SerializeField] private float deathLocalSoundDelay = 3f;
        #endregion

        #region References
        public PlayerInput Input { get; private set; }
        public PlayerData Data { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerWand Wand { get; private set; }
        public PlayerItems Items { get; private set; }
        public PlayerAnimations Animations { get; private set; }
        public PlayerRagdoll Ragdoll { get; private set; }
        public PlayerNotebook Notebook { get; private set; }
        #endregion

        public static PlayerCharacter LocalCharacter { get; private set; }
        public static PlayerCharacter ObservedCharacter => CameraController.SpectatedCharacter ?? LocalCharacter;

        public PlayerConnection Player => PlayerHandler.GetPlayer(Owner);

        private Rigidbody m_rigidbody;
        private readonly Dictionary<HumanBodyBones, Bodypart> m_bodyparts = new();
        private readonly Dictionary<Collider, int> m_characterColliders = new();

        private Vector3 m_lastSpawnPosition;
        private float m_lastSpawnLookX;

        // Petrify state
        private float m_petrifyDuration;
        private float m_petrifyElapsed;
        private float m_originalAnimatorSpeed;
        private Coroutine m_petrifyCoroutine;

        public bool IsPetrified => Data.IsPetrified;

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

            // Populate bodyparts dictionary
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform boneTransform = Animations.Animator.GetBoneTransform(bone);
                if (!boneTransform) continue;
                m_bodyparts[bone] = new Bodypart(bone, boneTransform);
            }

            // Populate character colliders dictionary, storing their original layers
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                m_characterColliders[col] = col.gameObject.layer;
            }
        }

        public override void OnStartNetwork()
        {
            PlayerHandler.RegisterCharacter(Owner, this);
        }

        public override void OnStartClient()
        {
            if (!IsOwner) return;
            LocalCharacter = this;
            GUIManager.Instance.SetHUDActive(true);

            // Initialize look values based on spawn rotation
            Data.lookValues.x = transform.eulerAngles.y;
            m_rigidbody.rotation = Quaternion.identity;
            UpdateLookDirection();
            m_lastSpawnPosition = transform.position;
            m_lastSpawnLookX = Data.lookValues.x;

            // Hide renderers for local player
            ToggleMeshesVisibility(false);

            // Apply settings to camera and enable it
            SettingsHandler.GetSetting<AntiAliasingSetting>().ApplyToCamera(Camera);
            Camera.gameObject.SetActive(true);

            InputHandler.Instance.EnableGameplayInput();

            // Set microphone device for voice chat based on settings
            int micDevice = SettingsHandler.GetSetting<MicrophoneDeviceSetting>().GetValue();
            var vcMicAudioInput = VoiceChat.audioInput as MetaVoiceChat.Input.Mic.VcMicAudioInput;
            if (vcMicAudioInput) vcMicAudioInput.SetSelectedDevice(Microphone.devices[micDevice]);
        }

        private void Update()
        {
            if (!IsOwner) return;
            Input.Sample(Data.IsPetrified);

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

        public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            m_rigidbody.position = position;
            Data.lookValues.x = rotation.eulerAngles.y;
            UpdateLookDirection();
            m_lastSpawnPosition = position;
            m_lastSpawnLookX = Data.lookValues.x;
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

        public void ToggleMeshesVisibility(bool visible)
        {
            int layer = visible ? LayerMask.NameToLayer("Default") : LayerMask.NameToLayer("CameraCull");
            foreach (var m in meshesToHide)
            {
                m.layer = layer;
            }
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

        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
        private void ApplyDamage_ObserversRPC(DamageInfo damage)
        {
            Data.Health -= damage.amount;
            if (Data.Health <= 0)
            {
                Data.isDead = true;
                if (IsServerInitialized && !Data.isDead)
                {
                    HandlePlayerDeath(damage);
                }
                Items.DropCurrentItem();
                this.LogInfo($"Player '{Player.PlayerName}' died!");
            }
        }

        [Server]
        private void HandlePlayerDeath(DamageInfo? damage = null)
        {
            // Play death announcer sound for all players (3D positional)
            PlayDeathAnnouncerSound_ObserversRpc(transform.position);

            if (GameManager.Instance.IsHeistOngoing)
            {
                // Spawn dropped notebook
                GameObject droppedNotebook = Instantiate(droppedNotebookPrefab, transform.position + Vector3.up * 1.0f, Quaternion.identity);
                Spawn(droppedNotebook, null, GameManager.Instance.CurrentScene);
                droppedNotebook.GetComponent<DroppedNotebook>().Initialize(this);
            }

            // Trigger ragdoll
            Ragdoll.TriggerRagdoll(
                (damage?.hitDirection ?? -ModelTransform.forward) * 500f,
                damage?.hitPoint ?? Center,
                false
            );

            if (!GameManager.Instance.IsHeistOngoing)
            {
                // Start dormitory revive coroutine
                StartCoroutine(DormitoryReviveCoroutine());
            }
        }

        private IEnumerator DormitoryReviveCoroutine(float delay = 5.0f)
        {
            yield return new WaitForSeconds(delay);
            // Only revive if heist is not ongoing, we check again in case a heist started during the wait
            if (GameManager.Instance.IsHeistOngoing) yield break;
            transform.position = m_lastSpawnPosition;
            Data.lookValues.x = m_lastSpawnLookX;
            Data.lookValues.y = 0.0f;
            UpdateLookDirection();
            ResetState();
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

        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
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
            VoiceChat.RestartClient();
            if (IsOwner)
            {
                CameraController.Instance.ResetPosition();
                Wand.RemoveTemporarySpell();
            }
            Items.DestroyCurrentItem();
            // Reset the runes in the notebook
            Notebook.ResetNotebook();
        }

        #region IPetrifiable Implementation
        public void Petrify(float duration)
        {
            if (Data.isDead) return;
            if (Data.IsPetrified) return; // Already petrified, no effect

            Data.IsPetrified = true;
            m_petrifyDuration = duration;
            m_petrifyElapsed = 0f;

            // Freeze animator at current frame
            if (Animations && Animations.Animator)
            {
                m_originalAnimatorSpeed = Animations.Animator.speed;
                Animations.Animator.speed = 0f;
            }

            this.LogInfo($"Player '{Player?.PlayerName ?? "Unknown"}' petrified for {duration}s");

            // Sync to all clients
            if (IsServerInitialized)
            {
                Petrify_ObserversRpc(duration);
            }

            // Start unpetrify timer
            if (m_petrifyCoroutine != null)
            {
                StopCoroutine(m_petrifyCoroutine);
            }
            m_petrifyCoroutine = StartCoroutine(PetrifyTimerCoroutine());
        }

        public void Unpetrify()
        {
            if (!Data.IsPetrified) return;

            Data.IsPetrified = false;

            // Resume animator
            if (Animations && Animations.Animator)
            {
                Animations.Animator.speed = m_originalAnimatorSpeed;
            }

            this.LogInfo($"Player '{Player?.PlayerName ?? "Unknown"}' unpetrified");

            // Sync to all clients
            if (IsServerInitialized)
            {
                Unpetrify_ObserversRpc();
            }

            if (m_petrifyCoroutine != null)
            {
                StopCoroutine(m_petrifyCoroutine);
                m_petrifyCoroutine = null;
            }
        }

        private IEnumerator PetrifyTimerCoroutine()
        {
            while (m_petrifyElapsed < m_petrifyDuration)
            {
                m_petrifyElapsed += Time.deltaTime;
                yield return null;
            }

            Unpetrify();
        }

        [ObserversRpc(ExcludeServer = true)]
        private void Petrify_ObserversRpc(float duration)
        {
            if (Data.IsPetrified) return;

            Data.IsPetrified = true;
            m_petrifyDuration = duration;
            m_petrifyElapsed = 0f;

            if (Animations && Animations.Animator)
            {
                m_originalAnimatorSpeed = Animations.Animator.speed;
                Animations.Animator.speed = 0f;
            }

            if (m_petrifyCoroutine != null)
            {
                StopCoroutine(m_petrifyCoroutine);
            }
            m_petrifyCoroutine = StartCoroutine(PetrifyTimerCoroutine());
        }

        [ObserversRpc(ExcludeServer = true)]
        private void Unpetrify_ObserversRpc()
        {
            if (!Data.IsPetrified) return;

            Data.IsPetrified = false;

            if (Animations && Animations.Animator)
            {
                Animations.Animator.speed = m_originalAnimatorSpeed;
            }

            if (m_petrifyCoroutine != null)
            {
                StopCoroutine(m_petrifyCoroutine);
                m_petrifyCoroutine = null;
            }
        }
        #endregion

        #region Death Sound Effects
        /// <summary>
        /// Plays the death announcer sound for all players (3D positional).
        /// </summary>
        [ObserversRpc]
        private void PlayDeathAnnouncerSound_ObserversRpc(Vector3 position)
        {
            if (string.IsNullOrEmpty(deathAnnouncerSoundKey)) return;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) return;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(deathAnnouncerSoundKey);
            if (soundType == null) return;

            // Play announcer sound at the player's death position (3D)
            SoundManager.Instance.PlaySoundLocal(soundType, position);

            // If this is the local player who died, start coroutine to play death sound after delay
            if (IsOwner)
            {
                StartCoroutine(PlayLocalDeathSoundAfterDelay());
            }
        }

        /// <summary>
        /// Plays the local death sound after a delay (only for the player who died).
        /// </summary>
        private IEnumerator PlayLocalDeathSoundAfterDelay()
        {
            yield return new WaitForSeconds(deathLocalSoundDelay);

            // Cancel if heist has ended (player was teleported to lobby)
            if (!GameManager.Instance.IsHeistOngoing) yield break;

            if (string.IsNullOrEmpty(deathLocalSoundKey)) yield break;
            if (SoundManager.Instance == null || SoundManager.Instance.soundDatabase == null) yield break;

            var soundType = SoundManager.Instance.soundDatabase.GetByKey(deathLocalSoundKey);
            if (soundType == null) yield break;

            // Play as 2D sound (centered, no spatial positioning) for the local player only
            var audioSource2D = SoundManager.Instance.audioSource2D;
            if (audioSource2D != null)
            {
                AudioClip clip = soundType.GetRandomClip();
                if (clip != null)
                {
                    audioSource2D.clip = clip;
                    audioSource2D.volume = soundType.volume;
                    audioSource2D.spatialBlend = 0f;
                    audioSource2D.Play();
                }
            }
        }
        #endregion

        #region Utils
        public Vector3 Center => ModelTransform.position + ModelTransform.up * 1.7f * 0.5f;

        public Bodypart GetBodypart(HumanBodyBones type) => m_bodyparts[type];
        public ICollection<Bodypart> GetAllBodyparts() => m_bodyparts.Values;

        private T PhysicsCastIgnoreSelf<T>(Func<T> castFunc)
        {
            // Set all character colliders to the Ignore Raycast layer
            foreach (var col in m_characterColliders.Keys)
            {
                col.gameObject.layer = 2; // Ignore Raycast layer
            }
            try
            {
                return castFunc();
            }
            finally
            {
                // Restore original layers
                foreach (var (col, layer) in m_characterColliders)
                {
                    col.gameObject.layer = layer;
                }
            }
        }

        public bool RaycastIgnoreSelf(Ray ray, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            RaycastHit hit = default;
            bool result = PhysicsCastIgnoreSelf(() => Physics.Raycast(ray, out hit, maxDistance, layerMask, queryTriggerInteraction));
            hitInfo = hit;
            return result;
        }

        public bool RaycastIgnoreSelf(Vector3 origin, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return RaycastIgnoreSelf(new Ray(origin, direction), out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }

        public bool SphereCastIgnoreSelf(Ray ray, float radius, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            RaycastHit hit = default;
            bool result = PhysicsCastIgnoreSelf(() => Physics.SphereCast(ray, radius, out hit, maxDistance, layerMask, queryTriggerInteraction));
            hitInfo = hit;
            return result;
        }

        public bool SphereCastIgnoreSelf(Vector3 origin, float radius, Vector3 direction, out RaycastHit hitInfo, float maxDistance = Mathf.Infinity, int layerMask = Physics.DefaultRaycastLayers, QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.UseGlobal)
        {
            return SphereCastIgnoreSelf(new Ray(origin, direction), radius, out hitInfo, maxDistance, layerMask, queryTriggerInteraction);
        }
        #endregion
    }
}
