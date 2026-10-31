using XEngine.Core.Base;

namespace XEngine.Core.Common.LifeTime
{
    public sealed class GLifeTime : GameComponent, IDisposable
    {
        public readonly GameTimer LifeTimer = new(float.MaxValue);
        public bool IsBlinking = false;

        public GLifeTime Init(float lifeTime)
        {
            LifeTimer.Duration = lifeTime;
            Owner.Scene.RegisterTimer(LifeTimer);
            LifeTimer.Start();
            LifeTimer.OnComplete += () => Owner.MarkDelete();
            return this;
        }

        public void Dispose()
        {
            Owner.Scene.UnregisterTimer(LifeTimer);
        }
    }
}
