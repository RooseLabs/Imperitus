namespace RooseLabs.Gameplay
{
    public enum TaskType
    {
        WalkAround,
        OpenNotebook,
        AimWithWand,
        CastImperoSpell
    }

    public class TutorialTask
    {
        public string Description { get; set; }
        public TaskType Type { get; set; }
        public bool Completed { get; private set; } = false;
        public string[] SpriteTags { get; set; }

        public TutorialTask(string description, TaskType type, string[] spriteTags = null)
        {
            Description = description;
            Type = type;
            SpriteTags = spriteTags ?? System.Array.Empty<string>();
        }

        public void MarkComplete()
        {
            Completed = true;
        }
    }
}
