using UnityEngine;
using RooseLabs.Gameplay;
using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System;

namespace RooseLabs.Enemies
{
    public interface IEnemyAI
    {
        void OnEnemyDeath();
    }

    [DefaultExecutionOrder(1)]
    public class EnemyData : NetworkBehaviour, IDamageable
    {
        [Header("Enemy Type")]
        [SerializeField] private EnemyType m_enemyType;
        private IEnemyAI enemyAI;
        private float m_maxHealth;

        // Synchronized health across network using Fishnet's SyncVar<T>
        private readonly SyncVar<float> m_health = new SyncVar<float>();

        private int m_attackDamage;
        private bool m_isDead = false;

        #region Properties
        public EnemyType EnemyType
        {
            get => m_enemyType;
            set
            {
                m_enemyType = value;
                InitializeFromType();
            }
        }

        public float MaxHealth
        {
            get => m_maxHealth;
            set => m_maxHealth = Mathf.Max(0f, value);
        }

        public float Health
        {
            get => m_health.Value;
            private set
            {
                m_health.Value = Mathf.Clamp(value, 0f, m_maxHealth);
            }
        }

        public int AttackDamage
        {
            get => m_attackDamage;
            set => m_attackDamage = Mathf.Max(0, value);
        }

        public bool IsDead => m_isDead;
        #endregion

        private void Awake()
        {
            InitializeFromType();

            enemyAI = GetComponent<IEnemyAI>();

            if (enemyAI == null)
                Debug.LogWarning($"{gameObject.name} has no component implementing IEnemyAI interface!");

            m_health.OnChange += OnHealthChanged;
        }

        private void OnDestroy()
        {
            m_health.OnChange -= OnHealthChanged;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            if (IsServerInitialized)
            {
                m_health.Value = m_maxHealth;
            }
        }

        private void InitializeFromType()
        {
            if (m_enemyType == null)
            {
                Debug.LogWarning($"EnemyData on {gameObject.name} has no EnemyType assigned!");
                return;
            }

            m_maxHealth = m_enemyType.maxHealth;
            m_health.Value = m_maxHealth;
            m_attackDamage = m_enemyType.attackDamage;
            m_isDead = false;
        }

        private void OnHealthChanged(float oldHealth, float newHealth, bool asServer)
        {
            if (newHealth <= 0f && !m_isDead)
            {
                m_isDead = true;
                Debug.Log($"{gameObject.name} health reached 0 (as {(asServer ? "server" : "client")})");

                if (asServer && enemyAI != null)
                    enemyAI.OnEnemyDeath();
            }
        }

        public bool ApplyDamage(DamageInfo damage)
        {
            if (m_isDead)
                return false;

            ApplyDamage_ServerRpc(damage.Amount, damage.SourceName ?? "Unknown");

            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void ApplyDamage_ServerRpc(float damageAmount, string sourceName)
        {
            if (m_isDead)
                return;

            Health -= damageAmount;

            Debug.Log($"{gameObject.name} took {damageAmount} damage from {sourceName}. Health: {Health}/{MaxHealth}");
        }
    }
}