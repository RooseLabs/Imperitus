using FishNet.Object;
using FishNet.Object.Synchronizing;
using RooseLabs.Gameplay;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Enemies
{
    public abstract class BaseEnemy : NetworkBehaviour, IDamageable
    {
        #region Serialized
        [Header("Base Stats")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float attackDamage = 10;
        #endregion

        private float m_health;
        private readonly SyncVar<bool> m_isDead = new();

        protected IEnemyState currentState;

        #region Properties
        public float MaxHealth
        {
            get => maxHealth;
            set => maxHealth = Mathf.Max(0f, value);
        }

        public float Health
        {
            get => m_health;
            private set => m_health = Mathf.Clamp(value, 0f, maxHealth);
        }

        public float AttackDamage
        {
            get => attackDamage;
            set => attackDamage = Mathf.Max(0, value);
        }

        public bool IsDead => m_isDead.Value;
        #endregion

        private void Awake()
        {
            m_health = maxHealth;
            Initialize();
        }

        protected abstract void Initialize();

        public void ChangeState(IEnemyState newState)
        {
            if (currentState == newState) return;
            currentState?.OnExit();
            currentState = newState;
            currentState?.OnEnter();
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (IsDead) return false;
            if (IsServerInitialized)
            {
                ApplyDamage_ObserversRpc(damage.amount);
            }
            else
            {
                ApplyDamage_ServerRpc(damage.amount);
            }
            return true;
        }

        protected abstract void OnDeath();

        [ServerRpc(RequireOwnership = false)]
        private void ApplyDamage_ServerRpc(float damageAmount)
        {
            ApplyDamage_ObserversRpc(damageAmount);
        }

        [ObserversRpc(ExcludeServer = true, RunLocally = true)]
        private void ApplyDamage_ObserversRpc(float damageAmount)
        {
            if (IsDead) return;

            Health -= damageAmount;

            this.LogInfo($"{gameObject.name} took {damageAmount} damage. Health: {Health}/{MaxHealth}");

            if (Health <= 0f)
            {
                m_isDead.Value = true;
                this.LogInfo($"{gameObject.name} has died.");
                OnDeath();
            }
        }
    }
}
