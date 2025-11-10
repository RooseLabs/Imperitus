namespace RooseLabs.Gameplay.Spells
{
    public class FailedSpell : SpellBase
    {
        protected override void OnStartCast()
        {
            base.OnStartCast();
        }

        protected override void OnCancelCast()
        {
            base.OnCancelCast();
        }

        protected override bool OnCastFinished()
        {
            // TODO: Do some random funny stuff here.
            return base.OnCastFinished();
        }
    }
}
