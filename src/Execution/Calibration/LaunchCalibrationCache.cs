using APThermo.Execution.Chunks;

namespace APThermo.Execution.Calibration;

/// <summary>One consumed-before-match, weak, expiring empirical observation per session.</summary>
internal sealed class LaunchCalibrationCache(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private CalibrationEntry? _entry;
    private long _generation;

    public CalibrationRun Begin<TViews>(Delegate launcher, UploadedTables tables, ChunkPlan plan,
        string? inputIdentity, in TViews views) where TViews : struct
    {
        var previous = _entry;
        Clear();
        var key = CalibrationKey.Create(launcher, tables, plan, inputIdentity, in views);
        var candidate = key is not null && previous is not null && previous.Key.Matches(key) ? previous : null;
        return new CalibrationRun(this, plan, key is null ? null : new CalibrationEntry(key),
            candidate, _clock, _generation);
    }

    public void Clear()
    {
        _entry = null;
        unchecked { _generation++; }
    }

    internal bool IsCurrent(long generation) => generation == _generation;

    internal void Publish(CalibrationEntry entry, long generation)
    {
        if (generation == _generation)
        {
            _entry = entry;
        }
    }

    internal void Abandon(long generation)
    {
        if (generation == _generation)
        {
            Clear();
        }
    }
}