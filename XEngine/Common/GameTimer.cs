namespace XEngine.Core.Common
{
    public class GameTimer
    {
        public bool IsRunning { get; internal set; }
        public float Elapsed { get; internal set; }
        public bool IsLooped;
        public float Duration;

        public GameTimer(float duration, bool isLooped = false)
        {
            Duration = duration;
            IsLooped = isLooped;
            Reset();
        }

        internal void Tick(float deltaTime)
        {
            if (!IsRunning) return;
            Elapsed += deltaTime;
            if (Elapsed >= Duration)
            {
                Elapsed = Duration;
                IsRunning = false;
                OnComplete?.Invoke();
                if (IsLooped) Start();
            }
        }

        public GameTimer Start()
        {
            Elapsed = 0;
            IsRunning = true;
            return this;
        }

        public GameTimer Reset()
        {
            Elapsed = 0;
            IsRunning = false;
            return this;
        }

        public GameTimer ForceEnd()
        {
            Elapsed = Duration;
            return this;
        }

        public float Left => Duration - Elapsed;
        public bool IsFinished => Elapsed >= Duration;
        public float Progress => Elapsed / Duration;

        public event Action? OnComplete;
    }
}