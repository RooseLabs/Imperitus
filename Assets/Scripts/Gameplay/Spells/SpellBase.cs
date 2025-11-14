using System;
using FishNet;
using FishNet.Component.Ownership;
using FishNet.Object;
using RooseLabs.Network;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using UnityEngine;
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

        #region Serialized
        [field: SerializeField] public SpellSO SpellInfo { get; private set; }
        [Tooltip("Type of spell casting behavior.")]
        [SerializeField] private SpellCastType castType = SpellCastType.OneShot;
        [Tooltip("Time in seconds required to cast the spell.")]
        [SerializeField] private float castTime = 0f;
        [Tooltip("Stamina cost for casting the spell.")]
        [SerializeField] private float staminaCost = 0f;
        [Tooltip("When and how the stamina cost is applied.")]
        [SerializeField] private StaminaConsumptionType staminaConsumptionType = StaminaConsumptionType.LinearlyDuringCast;
        [Tooltip("For sustained spells: extra stamina cost per second while the spell is being sustained.")]
        [SerializeField] private float staminaCostPerSecond = 0f;
        #endregion

        #region Private Fields
        private float m_castProgress = 0f;
        #endregion

        public bool IsCasting { get; private set; }

        public override void OnStartClient()
        {
            PlayerCharacter ownerCharacter = PlayerHandler.GetCharacter(Owner);
            Debug.Assert(ownerCharacter != null, "[SpellBase] No owner character found for spell.");
            transform.SetParent(ownerCharacter.Wand.AttachmentPoint);
            transform.localPosition = ownerCharacter.Wand.SpellCastPointLocalPosition;
        }

        private void OnEnable()
        {
            ResetData();
        }

        #region Public API
        public bool CanAimToSustain => castType == SpellCastType.AimToSustain;
        public bool IsBeingSustained { get; private set; } = false;

        public void StartCast()
        {
            if (IsCasting) return;
            if (staminaConsumptionType == StaminaConsumptionType.OnCastStart)
            {
                if (PlayerCharacter.LocalCharacter.Data.Stamina < staminaCost) return;
                PlayerCharacter.LocalCharacter.UseStamina(staminaCost);
            }

            IsCasting = true;
            m_castProgress = 0f;

            OnStartCast();
        }

        public void CancelCast()
        {
            if (!IsCasting) return;

            IsCasting = false;
            m_castProgress = 0f;
            if (IsBeingSustained)
            {
                IsBeingSustained = false;
                OnCancelCastSustained();
            }
            else
            {
                OnCancelCast();
            }
        }

        public void ContinueCast()
        {
            if (!IsCasting) return;

            if (m_castProgress < castTime)
            {
                m_castProgress += Time.deltaTime;
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
            nob.transform.SetParent(localCharacter.Wand.AttachmentPoint);
            nob.transform.localPosition = localCharacter.Wand.SpellCastPointLocalPosition;
            nm.ServerManager.Spawn(nob, localCharacter.Owner);
            return nob.GetComponent<SpellBase>();
        }

        public void Destroy()
        {
            Despawn(NetworkObject, DespawnType.Pool);
        }
        #endregion

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
                m_castProgress = 0f;
            }
        }

        /// <summary>
        /// Called on button press to start casting the spell.
        /// </summary>
        protected virtual void OnStartCast()
        {
            Logger.Info($"Spell {SpellInfo.Name} Started Casting");
        }

        /// <summary>
        /// Called on button release to cancel the spell cast.
        /// </summary>
        protected virtual void OnCancelCast()
        {
            Logger.Info($"Spell {SpellInfo.Name} Cancelled Casting");
        }

        /// <summary>
        /// Called on button held to continue casting the spell.
        /// </summary>
        protected virtual void OnContinueCast()
        {
            Logger.Info($"Spell {SpellInfo.Name} Continuing Casting");
        }

        /// <summary>
        /// Called when the spell cast is finished.
        /// </summary>
        /// <returns>True if the spell was successfully cast, false otherwise.</returns>
        protected virtual bool OnCastFinished()
        {
            Logger.Info($"Spell {SpellInfo.Name} Cast Finished");
            return true;
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is held down. Used for sustained spells.
        /// </summary>
        protected virtual void OnContinueCastSustained()
        {
            Logger.Info($"Spell {SpellInfo.Name} Cast Held Continued");
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is released. Used for sustained spells.
        /// </summary>
        protected virtual void OnCancelCastSustained()
        {
            Logger.Info($"Spell {SpellInfo.Name} Cancel Held");
        }

        /// <summary>
        /// Called when a backward scroll input is pressed.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollBackwardPressed()
        {
            Logger.Info($"Spell {SpellInfo.Name} Scroll Backward Pressed");
        }

        /// <summary>
        /// Called when a forward scroll input is pressed.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollForwardPressed()
        {
            Logger.Info($"Spell {SpellInfo.Name} Scroll Forward Pressed");
        }

        /// <summary>
        /// Called when a backward scroll input is held.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollBackwardHeld()
        {
            Logger.Info($"Spell {SpellInfo.Name} Scroll Backward Held");
        }

        /// <summary>
        /// Called when a forward scroll input is held.
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScrollForwardHeld()
        {
            Logger.Info($"Spell {SpellInfo.Name} Scroll Forward Held");
        }

        /// <summary>
        /// Called with the scroll delta/value (e.g. mouse wheel delta or axis value).
        /// This is only possible on sustained spells.
        /// </summary>
        protected virtual void OnScroll(float value)
        {
            Logger.Info($"Spell {SpellInfo.Name} Scrolled: {value}");
        }

        protected virtual void ResetData()
        {
            IsCasting = false;
            m_castProgress = 0f;
            IsBeingSustained = false;
        }
    }
}
