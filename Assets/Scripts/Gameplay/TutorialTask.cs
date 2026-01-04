namespace RooseLabs.Gameplay
{
    public enum TaskType
    {
        WalkAround,
        SearchForRunes,
        OpenNotebook,
        CombineRunes,
        AimWithWand,
        CastImperoSpell,
        TutorialComplete
    }

    public class TutorialTask
    {
        public string Description { get; set; }
        public TaskType Type { get; set; }
        public bool Completed { get; private set; } = false;
        public string[] ActionNames { get; set; }

        public TutorialTask(string description, TaskType type, string[] actionNames = null)
        {
            Description = description;
            Type = type;
            ActionNames = actionNames ?? System.Array.Empty<string>();
        }

        public void MarkComplete()
        {
            Completed = true;
        }
    }
}
