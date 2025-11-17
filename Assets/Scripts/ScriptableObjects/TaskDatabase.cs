using System;
using UnityEngine;

namespace RooseLabs.ScriptableObjects
{
    [Serializable]
    public abstract class CompletionCondition { }

    /// <summary>
    /// The player must cast a specific spell during the heist to complete the task.
    /// </summary>
    [Serializable]
    public class CastSpellCondition : CompletionCondition
    {
        [field: SerializeField] public SpellSO Spell { get; private set; }
    }

    [Serializable]
    public class AssignmentTask
    {
        [field: SerializeField][field: TextArea]
        public string Description { get; private set; }

        [field: SerializeField]
        public Sprite Image { get; private set; }

        [field: SerializeReference][field: SubclassSelector]
        public CompletionCondition CompletionCondition { get; private set; }

        public bool IsCompleted { get; set; } = false;
    }

    [CreateAssetMenu(fileName = "TaskDatabase", menuName = "Imperitus/Task Database")]
    public class TaskDatabase : ObjectDatabase<AssignmentTask> { }
}
