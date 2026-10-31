namespace XEngine.Core.Base
{
    public abstract class GameComponent
    {
        public Entity Owner { get; internal set; } = null!;
    }
}
