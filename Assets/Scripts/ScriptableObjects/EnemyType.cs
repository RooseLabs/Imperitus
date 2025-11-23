using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Enemy Type", menuName = "Imperitus/Enemy Type")]
    public class EnemyType : ScriptableObject
    {
        [Header("Health Settings")]
        [Tooltip("Maximum health for this enemy type")]
        public float maxHealth = 100f;

        [Header("Combat Settings")]
        [Tooltip("Base attack damage")]
        public int attackDamage = 10;
    }
}
