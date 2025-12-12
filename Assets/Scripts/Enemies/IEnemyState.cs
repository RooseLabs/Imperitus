namespace RooseLabs.Enemies
{
    public interface IEnemyState
    {
        void OnEnter();
        void Update();
        void OnExit();
    }
}
