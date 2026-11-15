namespace APThermo.Protocol.Tests;

/// <summary>One timer owned by a per-call controlled deadline clock.</summary>
internal sealed class PythonDeadlineTimer(PythonDeadlineClock owner, TimerCallback callback, object? state) : ITimer
{
    internal long? DueTicks { get; set; }
    internal TimeSpan Period { get; set; }
    public bool Change(TimeSpan dueTime, TimeSpan period) => owner.Change(this, dueTime, period);
    public void Dispose() => owner.Remove(this);
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
    internal void Fire() => callback(state);
}
