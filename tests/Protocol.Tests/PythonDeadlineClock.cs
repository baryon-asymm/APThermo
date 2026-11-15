namespace APThermo.Protocol.Tests;

/// <summary>A per-call monotonic test clock: advancing it fires the actual CTS timer without real-time setup races.</summary>
internal sealed class PythonDeadlineClock : TimeProvider, IDisposable
{
    private readonly Lock _gate = new();
    private readonly List<PythonDeadlineTimer> _timers = [];
    private readonly List<TimeSpan> _dueTimes = [];
    private long _ticks;
    private int _fired;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp()
    {
        lock (_gate)
        {
            return _ticks;
        }
    }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(GetTimestamp());
    public int Fired
    {
        get
        {
            lock (_gate)
            {
                return _fired;
            }
        }
    }
    public IReadOnlyList<TimeSpan> DueTimes
    {
        get
        {
            lock (_gate)
            {
                return [.. _dueTimes];
            }
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new PythonDeadlineTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
            _dueTimes.Add(dueTime);
            Schedule(timer, dueTime, period);
        }
        return timer;
    }

    public void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        PythonDeadlineTimer[] due;
        lock (_gate)
        {
            _ticks = checked(_ticks + elapsed.Ticks);
            due = [.. _timers.Where(timer => timer.DueTicks is { } ticks && ticks <= _ticks)];
            foreach (var timer in due)
            {
                timer.DueTicks = timer.Period > TimeSpan.Zero ? checked(_ticks + timer.Period.Ticks) : null;
                _fired++;
            }
        }
        foreach (var timer in due)
        {
            timer.Fire();
        }
    }

    internal bool Change(PythonDeadlineTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            if (!_timers.Contains(timer))
            {
                return false;
            }
            Schedule(timer, dueTime, period);
            return true;
        }
    }

    internal void Remove(PythonDeadlineTimer timer)
    {
        lock (_gate)
        {
            _ = _timers.Remove(timer);
            timer.DueTicks = null;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _timers.Clear();
        }
    }

    private void Schedule(PythonDeadlineTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        timer.DueTicks = dueTime == Timeout.InfiniteTimeSpan ? null : checked(_ticks + dueTime.Ticks);
        timer.Period = period;
    }
}
