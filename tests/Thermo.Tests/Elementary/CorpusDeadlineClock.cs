namespace APThermo.Thermo.Tests.Elementary;

/// <summary>Lets the process reach readiness before the actual runner deadline fires.</summary>
internal sealed class CorpusDeadlineClock : TimeProvider, IDisposable
{
    private ControlledTimer? _timer;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        _timer = new ControlledTimer(callback, state);

    public void Dispose() => _timer?.Dispose();

    internal void Expire() => (_timer ?? throw new InvalidOperationException("deadline timer was not created")).Fire();

    private sealed class ControlledTimer(TimerCallback callback, object? state) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        internal void Fire() => callback(state);
    }
}
