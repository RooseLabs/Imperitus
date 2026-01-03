using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Component.Ownership;
using FishNet.Object;
using RooseLabs.Network;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using UnityEngine.Animations;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Gameplay.Spells
{
    [Serializable]
    public enum SpellCastType
    {
        OneShot,       // After cast completes, effect happens immediately
        CastToSustain, // After cast completes, effect persists while cast button is held (aim button must also be held)
        AimToSustain   // After cast completes, effect persists while aim button is held (cast button can be released)
    }

    [Serializable]
    public enum StaminaConsumptionType
    {
        OnCastStart,       // Stamina cost applied immediately when casting starts
        LinearlyDuringCast // Stamina cost applied gradually over the cast time
    }

    [RequireComponent(typeof(PredictedSpawn))]
    public abstract class SpellBase : NetworkBehaviour
    {
        protected static Logger Logger => Logger.GetLogger("SpellCasting");

        public static event Action<SpellSO> OnSpellCast = delegate { };

        #region Serialized
        [field: SerializeField]
        public SpellSO SpellInfo { get; private set; }

        [SerializeField, Tooltip("Type of spell casting behavior.")]
        private SpellCastType castType = SpellCastType.OneShot;

        [SerializeField, Tooltip("Time in seconds required to cast the spell.")]
        private float castTime = 0f;

        [SerializeField, Tooltip("Stamina cost for casting the spell.")]
        private float staminaCost = 0f;

        [SerializeField, Tooltip("When and how the stamina cost is applied.")]
        private StaminaConsumptionType staminaConsumptionType = StaminaConsumptionType.LinearlyDuringCast;

        [SerializeField, Tooltip("For sustained spells: extra stamina cost per second while the spell is sustained.")]
        private float staminaCostPerSecond = 0f;

        [SerializeField, Tooltip("Cooldown time in seconds after casting the spell, during which the spell cannot be cast again.")]
        private float cooldownTime = 0f;
        #endregion

        protected PlayerCharacter CasterCharacter { get; private set; }
        protected float CastTime => castTime;
        protected float CastProgress { get; private set; } = 0f;
        protected float CastProgressNormalized => castTime > 0f ? Mathf.Clamp01(CastProgress / castTime) : 1f;

        private float CooldownEndTime {
            get => Cooldowns.GetValueOrDefault(GetType(), 0f);
            set => Cooldowns[GetType()] = value;
        }

        /// <summary>
        /// Static dictionary to track cooldown end times for each spell type.
        /// This ensures that cooldowns are shared across all instances of the same spell type.
        /// For instance, if a player has both "Lux" and "Lux Variant" spells, casting one will put both on cooldown.
        /// </summary>
        private static readonly Dictionary<Type, float> Cooldowns = new();

        public override void OnStartClient()
        {
            CasterCharacter = PlayerHandler.GetCharacter(Owner);
            Debug.Assert(CasterCharacter != null, "[SpellBase] No owner character found for spell.");
            SetupParentConstraint(CasterCharacter.Wand.AttachmentPoint, CasterCharacter.Wand.SpellCastPointLocalPosition);
            gameObject.name = $"Spell_{GetType().Name} ({CasterCharacter.Player.PlayerName})";
        }

        #region Public API
        public bool CanAimToSustain => castType == SpellCastType.AimToSustain;
        public bool IsBeingSustained { get; private set; } = false;
        public bool IsAiming { get; private set; }
        public bool IsCasting { get; private set; }
        public bool IsOnCooldown => Time.time < CooldownEndTime;

        public void Aim()
        {
            if (!IsAiming)
            {
                IsAiming = true;
                OnStartAim();
            }

            OnAim();
        }

        public void StopAim()
        {
            if (!IsAiming) return;
            IsAiming = false;
            OnStopAim();
        }

        public void StartCast()
        {
            if (IsCasting || IsOnCooldown) return;
            if (PlayerCharacter.LocalCharacter.Data.Stamina <= 0f) return;
            if (staminaConsumptionType == StaminaConsumptionType.OnCastStart)
            {
                if (PlayerCharacter.LocalCharacter.Data.Stamina < staminaCost) return;
                PlayerCharacter.LocalCharacter.UseStamina(staminaCost);
            }

            IsCasting = true;
            CastProgress = 0f;

            OnStartCast();
        }

        public void CancelCast()
        {
            if (!IsCasting) return;

            IsCasting = false;
            CastProgress = 0f;
            if (IsBeingSustained)
            {
                IsBeingSustained = false;
                OnCancelCastSustained();
                if (cooldownTime > 0f)
                    CooldownEndTime = Time.time + cooldownTime;
            }
            else
            {
                OnCancelCast();
            }
        }

        public void ContinueCast()
        {
            if (!IsCasting) return;

            if (CastProgress < castTime)
            {
                CastProgress += Time.deltaTime;
                if (castTime > 0f && staminaCost > 0f && staminaConsumptionType == StaminaConsumptionType.LinearlyDuringCast)
                {
                    float staminaThisFrame = (staminaCost / castTime) * Time.deltaTime;
                    if (!PlayerCharacter.LocalCharacter.UseStamina(staminaThisFrame))
                    {
                        // Not enough stamina to continue casting
                        CancelCast();
                        return;
                    }
                }
                OnContinueCast();
            }
            else
            {
                if (IsBeingSustained)
                {
                    if (staminaCostPerSecond > 0f)
                    {
                        float staminaThisFrame = staminaCostPerSecond * Time.deltaTime;
                        if (!PlayerCharacter.LocalCharacter.UseStamina(staminaThisFrame))
                        {
                            // Not enough stamina to sustain the spell
                            CancelCast();
                            return;
                        }
                    }
                    OnContinueCastSustained();
                }
                else
                {
                    CompleteCast();
                }
            }
        }

        public void ScrollBackwardPressed()
        {
            OnScrollBackwardPressed();
        }

        public void ScrollForwardPressed()
        {
            OnScrollForwardPressed();
        }

        public void ScrollBackwardHeld()
        {
            OnScrollBackwardHeld();
        }

        public void ScrollForwardHeld()
        {
            OnScrollForwardHeld();
        }

        public void Scroll(float value)
        {
            OnScroll(value);
        }

        public static SpellBase Instantiate(SpellBase spellPrefab)
        {
            var nm = InstanceFinder.NetworkManager;
            if (!nm) return null;
            var localCharacter = PlayerCharacter.LocalCharacter;
            if (!localCharacter) return null;
            NetworkObject nob = nm.GetPooledInstantiated(spellPrefab.gameObject, false);
            var spellComponent = nob.GetComponent<SpellBase>();
            spellComponent.SetupParentConstraint(localCharacter.Wand.AttachmentPoint, localCharacter.Wand.SpellCastPointLocalPosition);
            nm.ServerManager.Spawn(nob, localCharacter.Owner);
            return spellComponent;
        }

        public void Destroy()
        {
            Despawn(NetworkObject, DespawnType.Pool);
        }

        public static void ClearCooldowns()
        {
            Cooldowns.Clear();
        }
        #endregion

        private void SetupParentConstraint(Transform parent, Vector3 offsetPosition)
        {
            ParentConstraint parentConstraint = GetComponent<ParentConstraint>();
            if (parentConstraint) return; // Already set up
            parentConstraint = gameObject.AddComponent<ParentConstraint>();

            // Add the parent source
            ConstraintSource source = new ConstraintSource { sourceTransform = parent, weight = 1f };
            parentConstraint.AddSource(source);

            // Set the offset
            parentConstraint.SetTranslationOffset(0, offsetPosition);

            // Enable the constraint
            parentConstraint.constraintActive = true;
        }

        private void CompleteCast()
        {
            bool successfulCast = OnCastFinished();

            if (successfulCast && castType != SpellCastType.OneShot)
            {
                IsBeingSustained = true;
            }
            else
            {
                IsCasting = false;
                CastProgress = 0f;
            }

            if (successfulCast)
            {
                OnSpellCast.Invoke(SpellInfo);
                if (castType == SpellCastType.OneShot && cooldownTime > 0f)
                {
                    CooldownEndTime = Time.time + cooldownTime;
                }
            }
        }

        /// <summary>
        /// Called when the spell starts being aimed (first frame of aiming).
        /// </summary>
        protected virtual void OnStartAim()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Started Aiming");
        }

        /// <summary>
        /// Called every frame while the spell is being aimed.
        /// Use this for custom aim effects like targeting indicators, trajectories, etc.
        /// This will be called even while the spell is being cast.
        /// If you want to do Aim-only logic, check IsCasting flag.
        /// </summary>
        protected virtual void OnAim()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Aiming");
        }

        /// <summary>
        /// Called when the spell stops being aimed.
        /// </summary>
        protected virtual void OnStopAim()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Stopped Aiming");
        }

        /// <summary>
        /// Called on button press to start casting the spell.
        /// </summary>
        protected virtual void OnStartCast()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Started Casting");
        }

        /// <summary>
        /// Called on button release to cancel the spell cast.
        /// </summary>
        protected virtual void OnCancelCast()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Cancelled Casting");
        }

        /// <summary>
        /// Called on button held to continue casting the spell.
        /// </summary>
        protected virtual void OnContinueCast()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Continuing Casting");
        }

        /// <summary>
        /// Called when the spell cast is finished.
        /// </summary>
        /// <returns>True if the spell was successfully cast, false otherwise.</returns>
        protected virtual bool OnCastFinished()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Cast Finished");
            return true;
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is held down. Used for sustained spells.
        /// </summary>
        protected virtual void OnContinueCastSustained()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Cast Held Continued");
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is released. Used for sustained spells.
        /// </summary>
        protected virtual void OnCancelCastSustained()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Cancel Held");
        }

        /// <summary>
        /// Called when a backward scroll input is pressed.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollBackwardPressed()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Scroll Backward Pressed");
        }

        /// <summary>
        /// Called when a forward scroll input is pressed.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollForwardPressed()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Scroll Forward Pressed");
        }

        /// <summary>
        /// Called when a backward scroll input is held.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollBackwardHeld()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Scroll Backward Held");
        }

        /// <summary>
        /// Called when a forward scroll input is held.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollForwardHeld()
        {
            // Logger.Info($"Spell {SpellInfo.Name} Scroll Forward Held");
        }

        /// <summary>
        /// Called with the scroll delta/value (e.g. mouse wheel delta or axis value).
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScroll(float value)
        {
            // Logger.Info($"Spell {SpellInfo.Name} Scrolled: {value}");
        }

        protected virtual void ResetData()
        {
            IsAiming = false;
            IsCasting = false;
            CastProgress = 0f;
            CooldownEndTime = 0f;
            IsBeingSustained = false;
        }

        public override void ResetState(bool asServer)
        {
            ResetData();
            // Remove Parent Constraint
            if (TryGetComponent(out ParentConstraint parentConstraint))
                Destroy(parentConstraint);
            base.ResetState(asServer);
        }
    }
}
