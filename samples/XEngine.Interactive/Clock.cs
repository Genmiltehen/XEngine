using System.Diagnostics;

namespace XEngine.Interactive;

public sealed class Clock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _previous;

    public float Delta
    {
        get
        {
            TimeSpan current = _stopwatch.Elapsed;
            var delta = (float)(current - _previous).TotalSeconds;
            _previous = current;
            return delta;
        }
    }
}
