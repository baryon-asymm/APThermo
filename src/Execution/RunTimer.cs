namespace APThermo.Execution;

/// <summary>Additive run phases and constant-size observations of successful synchronized launches.</summary>
internal sealed class RunTimer
{
    private readonly TimeSpan[] _phases = new TimeSpan[Enum.GetValues<RunPhase>().Length];
    private TimeSpan _warmUp;
    private int _launchCount;
    private int _coveredCases;
    private TimeSpan _maxLaunchDuration;
    private int _maxLaunchCases;
    private int _maxLaunchOrdinal;
    private int _firstLaunchCases;
    private bool _reusedPrefix;

    /// <summary>Kernel compilation and post-link, apart from run phases and zero after the first run.</summary>
    public void AddWarmUp(TimeSpan elapsed) => _warmUp += elapsed;

    public RunPhaseScope Uploading() => new(this, RunPhase.Upload);

    public RunPhaseScope Launching() => new(this, RunPhase.Kernel);

    public RunPhaseScope Downloading() => new(this, RunPhase.Download);

    public RunTimings Timings() =>
        new(_warmUp, _phases[(int)RunPhase.Upload], _phases[(int)RunPhase.Kernel], _phases[(int)RunPhase.Download])
        {
            FirstLaunchCases = _firstLaunchCases,
            ReusedPrefix = _reusedPrefix,
            LaunchCount = _launchCount,
            CoveredCases = _coveredCases,
            MaxLaunchDuration = _maxLaunchDuration,
            MaxLaunchCases = _maxLaunchCases,
            MaxLaunchOrdinal = _maxLaunchOrdinal,
        };

    /// <summary>Records the existing launch-plus-synchronization interval; first occurrence wins an equal maximum.</summary>
    internal void RecordSuccessfulLaunch(int cases, TimeSpan elapsed, bool reusedPrefix = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cases);
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        Add(RunPhase.Kernel, elapsed);
        _launchCount++;
        if (_launchCount == 1)
        {
            _firstLaunchCases = cases;
            _reusedPrefix = reusedPrefix;
        }
        _coveredCases = checked(_coveredCases + cases);
        if (_launchCount == 1 || elapsed > _maxLaunchDuration)
        {
            _maxLaunchDuration = elapsed;
            _maxLaunchCases = cases;
            _maxLaunchOrdinal = _launchCount;
        }
    }

    internal void Add(RunPhase phase, TimeSpan elapsed) => _phases[(int)phase] += elapsed;
}