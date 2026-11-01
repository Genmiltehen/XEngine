namespace XEngine.Core.Time;

public readonly struct GameTime(float deltaTime, float totalTime, long frameCount)
{
    public float DeltaTime { get; } = deltaTime;
    public float TotalTime { get; } = totalTime;
    public long FrameCount { get; } = frameCount;
}
