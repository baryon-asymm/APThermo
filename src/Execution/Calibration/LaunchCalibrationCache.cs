using APThermo.Execution.Chunks;

namespace APThermo.Execution.Calibration;

/// <summary>One consumed-before-match, weak, expiring empirical observation per session.</summary>
internal sealed class LaunchCalibrationCache(TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private CalibrationEntry? _entry;
    private long _sequence;

    public CalibrationObservation Observation { get; private set; }

    public CalibrationRun Begin<TViews>(Delegate launcher, UploadedTables tables, ChunkPlan plan,
        string? inputIdentity, in TViews views) where TViews : struct
    {
        var previous = _entry;
        var priorObservation = Observation;
        var previousGeneration = Generation;
        Clear();
        var key = CalibrationKey.Create(launcher, tables, plan, inputIdentity, in views);
        var reason = key is null ? CalibrationReason.UnsupportedIdentity :
            previous is null ? CalibrationReason.NoPreviousEntry : previous.Key.Compare(key);
        var candidate = reason == CalibrationReason.Matched ? previous : null;
        unchecked { _sequence++; }
        Observation = new CalibrationObservation
        {
            TransactionId = _sequence,
            PreviousTransactionId = priorObservation.TransactionId,
            PreviousOutcome = priorObservation.Outcome,
            PreviousNonpublicationReason = priorObservation.NonpublicationReason,
            PreviousPublicationId = previous?.PublicationId ?? 0,
            PreviousGeneration = previousGeneration,
            BeginGeneration = Generation,
            CurrentGeneration = Generation,
            PreviousEntryPresent = previous is not null,
            PriorPrefixCases = previous?.PrefixCases ?? 0,
            MatchReason = reason,
            Outcome = plan.Budget.IsBounded ? CalibrationOutcome.Pending : CalibrationOutcome.NotApplicable,
        };
        var run = new CalibrationRun(this, plan, key is null ? null : new CalibrationEntry(key),
            candidate, _clock, Generation);
        run.StartObservation(Observation);
        return run;
    }

    public void Clear()
    {
        _entry = null;
        unchecked { Generation++; }
        // Existing bypass/lifecycle calls must not look like a newly produced CUDA transaction.
        Observation = Observation.TransactionId == 0 ? default : Observation with
        {
            CurrentGeneration = Generation,
            Outcome = CalibrationOutcome.Invalidated,
        };
    }

    internal bool IsCurrent(long generation) => generation == Generation;

    internal void Record(CalibrationObservation observation)
    {
        if (observation.TransactionId == _sequence)
        {
            Observation = observation with { CurrentGeneration = Generation };
        }
    }

    internal void Publish(CalibrationEntry entry, long generation)
    {
        if (generation == Generation)
        {
            _entry = entry;
        }
    }

    internal void Abandon(long generation)
    {
        if (generation == Generation)
        {
            Clear();
        }
    }

    internal long Generation { get; private set; }
}
