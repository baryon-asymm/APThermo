using APThermo.Execution.Chunks;

namespace APThermo.Execution.Calibration;

/// <summary>Records current successful launch boundaries, publishing only after the parent commits its result.</summary>
internal sealed class CalibrationRun : IDisposable
{
    private readonly LaunchCalibrationCache _cache;
    private readonly ChunkPlan _plan;
    private readonly CalibrationEntry? _prepared;
    private readonly TimeProvider _clock;
    private readonly long _generation;
    private CalibrationEntry? _candidate;
    private int _covered;
    private long _prefixTicks;
    private bool _invalid;
    private bool _finished;

    internal CalibrationRun(LaunchCalibrationCache cache, ChunkPlan plan, CalibrationEntry? prepared,
        CalibrationEntry? candidate, TimeProvider clock, long generation)
    {
        _cache = cache;
        _plan = plan;
        _prepared = prepared;
        _candidate = candidate;
        _clock = clock;
        _generation = generation;
    }

    public bool ReusedPrefix { get; private set; }

    public int FirstChunkCases()
    {
        if (!_finished && _cache.IsCurrent(_generation) && _candidate is { } candidate && _plan.Budget.TargetDuration is { } target)
        {
            var now = _clock.GetTimestamp();
            if (now >= candidate.CompletedAt && _clock.GetElapsedTime(candidate.CompletedAt, now) <= target)
            {
                ReusedPrefix = true;
                return Math.Min(candidate.PrefixCases, Math.Min(_plan.Size, _plan.Count));
            }

            _candidate = null;
        }

        ReusedPrefix = false;
        return _plan.FirstChunkCases();
    }

    public void ObserveSuccessfulLaunch(int cases, TimeSpan elapsed)
    {
        _candidate = null;
        if (_finished || _prepared is null || cases <= 0 || cases > _plan.Count - _covered || elapsed < TimeSpan.Zero ||
            _plan.Budget.TargetDuration is not { } target || elapsed > target)
        {
            _invalid = true;
            return;
        }

        _covered += cases;
        if (_prefixTicks <= target.Ticks && elapsed.Ticks <= target.Ticks - _prefixTicks)
        {
            _prefixTicks += elapsed.Ticks;
            if (_covered <= _plan.Size)
            {
                _prepared.PrefixCases = _covered;
            }
        }
        else
        {
            _prefixTicks = long.MaxValue;
        }
    }

    public void Complete()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        if (!_invalid && _prepared is { PrefixCases: > 1 } entry && _covered == _plan.Count)
        {
            entry.CompletedAt = _clock.GetTimestamp();
            _cache.Publish(entry, _generation);
        }
        else
        {
            _cache.Abandon(_generation);
        }
    }

    public void Dispose()
    {
        if (!_finished)
        {
            _finished = true;
            _cache.Abandon(_generation);
        }
    }
}