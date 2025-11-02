using FishNet;
using FishNet.Component.Ownership;
using FishNet.Object;
using RooseLabs.Network;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    [RequireComponent(typeof(PredictedSpawn))]
    public abstract class SpellBase : NetworkBehaviour
    {
        [System.Serializable]
        protected enum StaminaConsumptionType
        {
            OnCastStart,
            LinearlyDuringCast,
            OnCastFinish
        }

        #region Serialized
        [SerializeField] private SpellSO spellInfo;
        [Tooltip("Whether the cast button should be held down after the spell finishes casting to maintain its effect.")]
        [SerializeField] private bool heldSpell = false;
        [Tooltip("Time in seconds required to cast the spell.")]
        [SerializeField] private float castTime = 0f;
        [Tooltip("Stamina cost for casting the spell.")]
        [SerializeField] private float staminaCost = 0f;
        [Tooltip("When and how the stamina cost is applied.")]
        [SerializeField] private StaminaConsumptionType staminaConsumptionType = StaminaConsumptionType.LinearlyDuringCast;
        [Tooltip("For held spells: extra stamina cost per second while the spell is held.")]
        [SerializeField] private float staminaCostPerSecond = 0f;
        #endregion

        public override void OnStartClient()
        {
            if (!IsOwner)
            {
                // Ensure that the position is correct for non-owners.
                PlayerCharacter ownerCharacter = PlayerHandler.GetCharacter(Owner);
                Debug.Assert(ownerCharacter != null, "[SpellBase] Spell has no owner.");
                transform.SetParent(ownerCharacter.Wand.transform, false);
                transform.position = ownerCharacter.Wand.SpellCastPoint.position;
                transform.rotation = Quaternion.identity;
            }
        }

        #region Public API
        public void StartCast()
        {
            OnStartCast();
        }

        public void CancelCast()
        {
            OnCancelCast();
        }

        public void ContinueCast()
        {
            OnContinueCast();
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

        public static SpellBase Instantiate(GameObject spellPrefab, Vector3 position)
        {
            var nm = InstanceFinder.NetworkManager;
            if (!nm) return null;
            var localCharacter = PlayerCharacter.LocalCharacter;
            if (!localCharacter) return null;
            NetworkObject nob = nm.GetPooledInstantiated(spellPrefab, position, Quaternion.identity, false);
            nob.SetParent(localCharacter.Wand);
            nm.ServerManager.Spawn(nob, localCharacter.Owner);
            return nob.GetComponent<SpellBase>();
        }

        public void Destroy()
        {
            Despawn(NetworkObject, DespawnType.Pool);
        }
        #endregion

        /// <summary>
        /// Called on button press to start casting the spell.
        /// </summary>
        protected virtual void OnStartCast()
        {
            Debug.Log($"Spell {spellInfo.Name} Started Casting");
        }

        /// <summary>
        /// Called on button release to cancel the spell cast.
        /// </summary>
        protected virtual void OnCancelCast()
        {
            Debug.Log($"Spell {spellInfo.Name} Cancelled Casting");
        }

        /// <summary>
        /// Called on button held to continue casting the spell.
        /// </summary>
        protected virtual void OnContinueCast()
        {
            Debug.Log($"Spell {spellInfo.Name} Continuing Casting");
        }

        /// <summary>
        /// Called when the spell cast is finished.
        /// </summary>
        protected virtual void OnCastFinished()
        {
            Debug.Log($"Spell {spellInfo.Name} Cast Finished");
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is held down. Used for held spells.
        /// </summary>
        protected virtual void OnContinueCastHeld()
        {
            Debug.Log($"Spell {spellInfo.Name} Cast Held Continued");
        }

        /// <summary>
        /// Called when the spell cast is finished and the cast button is released. Used for held spells.
        /// </summary>
        protected virtual void OnCancelCastHeld()
        {
            Debug.Log($"Spell {spellInfo.Name} Cancel Held");
        }

        /// <summary>
        /// Called when a backward scroll input is pressed.
        /// </summary>
        protected virtual void OnScrollBackwardPressed()
        {
            Debug.Log($"Spell {spellInfo.Name} Scroll Backward Pressed");
        }

        /// <summary>
        /// Called when a forward scroll input is pressed.
        /// </summary>
        protected virtual void OnScrollForwardPressed()
        {
            Debug.Log($"Spell {spellInfo.Name} Scroll Forward Pressed");
        }

        /// <summary>
        /// Called when a backward scroll input is held.
        /// </summary>
        protected virtual void OnScrollBackwardHeld()
        {
            Debug.Log($"Spell {spellInfo.Name} Scroll Backward Held");
        }

        /// <summary>
        /// Called when a forward scroll input is held.
        /// </summary>
        protected virtual void OnScrollForwardHeld()
        {
            Debug.Log($"Spell {spellInfo.Name} Scroll Forward Held");
        }

        /// <summary>
        /// Called with the scroll delta/value (e.g. mouse wheel delta or axis value).
        /// </summary>
        protected virtual void OnScroll(float value)
        {
            Debug.Log($"Spell {spellInfo.Name} Scrolled: {value}");
        }
    }
}
