namespace SoBar.Widgets;

internal sealed class FocusTimer(TimeProvider time)
{
    private long started;
    private TimeSpan accumulated;
    public bool Running { get; private set; }
    public bool IsBreak { get; private set; }
    public bool Finished { get; private set; }
    public string Mode => IsBreak ? "Pausa" : "Foco";
    private TimeSpan Duration => TimeSpan.FromMinutes(IsBreak ? 5 : 25);

    public TimeSpan Remaining
    {
        get
        {
            var elapsed = accumulated + (Running ? time.GetElapsedTime(started) : TimeSpan.Zero);
            if (elapsed < Duration) return Duration - elapsed;
            Running = false;
            Finished = true;
            accumulated = Duration;
            return TimeSpan.Zero;
        }
    }

    public void Toggle()
    {
        _ = Remaining;
        if (Finished)
        {
            IsBreak = !IsBreak;
            Reset();
        }
        if (Running)
        {
            accumulated += time.GetElapsedTime(started);
            Running = false;
        }
        else
        {
            started = time.GetTimestamp();
            Running = true;
        }
    }

    public void Reset()
    {
        accumulated = TimeSpan.Zero;
        Finished = false;
        Running = false;
    }
}
