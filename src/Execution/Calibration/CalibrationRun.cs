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
    private CalibrationObservation _observation;
    private CalibrationChoice _choice;
    private int _choiceChecks;
    private bool _firstLaunchObserved;
    private CalibrationNonpublication _nonpublication;

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

    internal void StartObservation(CalibrationObservation observation)
    {
        _observation = observation;
        _choice = new CalibrationChoice(0, observation.MatchReason, null);
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
                return RecordChoice(Math.Min(candidate.PrefixCases, Math.Min(_plan.Size, _plan.Count)),
                    CalibrationReason.ReusedPrefix, _clock.GetElapsedTime(candidate.CompletedAt, now));
            }

            _choice = new CalibrationChoice(0,
                now < candidate.CompletedAt ? CalibrationReason.BackwardClock : CalibrationReason.Expired,
                _clock.GetElapsedTime(candidate.CompletedAt, now));
            _candidate = null;
        }

        ReusedPrefix = false;
        var reason = !_cache.IsCurrent(_generation) ? CalibrationReason.Superseded :
            _finished ? CalibrationReason.Finished : _choice.Reason;
        return RecordChoice(_plan.FirstChunkCases(), reason, _choice.CandidateAge);
    }

    public void ObserveSuccessfulLaunch(int cases, TimeSpan elapsed)
    {
        if (!_firstLaunchObserved)
        {
            _firstLaunchObserved = true;
            _observation = _observation with { PhysicalFirstLaunch = _choice with { Cases = cases } };
            _cache.Record(_observation);
        }

        _candidate = null;
        if (_finished || _prepared is null || cases <= 0 || cases > _plan.Count - _covered || elapsed < TimeSpan.Zero ||
            _plan.Budget.TargetDuration is not { } target || elapsed > target)
        {
            _invalid = true;
            if (_nonpublication == CalibrationNonpublication.None)
            {
                _nonpublication = _prepared is null ? CalibrationNonpublication.UnsupportedIdentity :
                    _plan.Budget.TargetDuration is { } duration && elapsed > duration ? CalibrationNonpublication.OverTarget :
                    CalibrationNonpublication.InvalidObservation;
            }

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
        var current = _cache.IsCurrent(_generation);
        var outcome = CalibrationOutcome.NotPublished;
        if (!_invalid && _prepared is { PrefixCases: > 1 } entry && _covered == _plan.Count)
        {
            entry.CompletedAt = _clock.GetTimestamp();
            entry.PublicationId = _observation.TransactionId;
            _cache.Publish(entry, _generation);
            outcome = CalibrationOutcome.Published;
        }
        else
        {
            _cache.Abandon(_generation);
            _nonpublication = _invalid ? _nonpublication :
                _prepared is null ? CalibrationNonpublication.UnsupportedIdentity :
                _covered != _plan.Count ? CalibrationNonpublication.IncompleteCoverage : CalibrationNonpublication.NoUsefulPrefix;
        }

        RecordOutcome(current ? outcome : CalibrationOutcome.Superseded);
    }

    public void Dispose()
    {
        if (!_finished)
        {
            _finished = true;
            var current = _cache.IsCurrent(_generation);
            _cache.Abandon(_generation);
            RecordOutcome(current ? CalibrationOutcome.Abandoned : CalibrationOutcome.Superseded);
        }
    }

    private int RecordChoice(int cases, CalibrationReason reason, TimeSpan? age)
    {
        _choice = new CalibrationChoice(cases, reason, age);
        if (_choiceChecks == 0)
        {
            _observation = _observation with { BeforeUpload = _choice };
        }
        else if (_choiceChecks == 1)
        {
            _observation = _observation with { AfterUpload = _choice };
        }

        _choiceChecks = Math.Min(_choiceChecks + 1, 2);
        _cache.Record(_observation);
        return cases;
    }

    private void RecordOutcome(CalibrationOutcome outcome)
    {
        outcome = _plan.Budget.IsBounded ? outcome : CalibrationOutcome.NotApplicable;
        _observation = _observation with
        {
            Outcome = outcome,
            NonpublicationReason = outcome == CalibrationOutcome.NotPublished ? _nonpublication : CalibrationNonpublication.None,
            PublicationId = outcome == CalibrationOutcome.Published ? _observation.TransactionId : 0,
        };
        _cache.Record(_observation);
    }
}