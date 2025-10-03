namespace RooseLabs.Enemies
{
    public interface IEnemyState
    {
        void Enter();
        void Exit();
        void Tick();
    }
}
