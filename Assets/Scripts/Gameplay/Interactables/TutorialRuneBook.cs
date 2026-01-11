namespace RooseLabs.Gameplay.Interactables
{
    public class TutorialRuneBook : RuneBook
    {
        private bool m_hasCollectedRune = false;

        public override void OnPickupEnd()
        {
            if (!IsOwner) return;
            if (m_hasCollectedRune) return;
            if (RuneIndex > -1 && HolderCharacter.Notebook.CollectRune(RuneIndex))
            {
                m_hasCollectedRune = true;
                PlayRuneCollectedSound();
                SetRuneTexture(null);
            }
        }
    }
}
