namespace APThermo.Execution;

/// <summary>The phases of one run as named scopes, and the warm-up of its kernel; produces the timings the result carries.</summary>
internal sealed class RunTimer
{
    private readonly TimeSpan[] _phases = new TimeSpan[Enum.GetValues<RunPhase>().Length];
    private TimeSpan _warmUp;

    /// <summary>Kernel compilation and post-link, reported apart from the phases of the run and zero after the first run.</summary>
    public void AddWarmUp(TimeSpan elapsed) => _warmUp += elapsed;

    public RunPhaseScope Uploading() => new(this, RunPhase.Upload);

    public RunPhaseScope Launching() => new(this, RunPhase.Kernel);

    public RunPhaseScope Downloading() => new(this, RunPhase.Download);

    public RunTimings Timings() =>
        new(_warmUp, _phases[(int)RunPhase.Upload], _phases[(int)RunPhase.Kernel], _phases[(int)RunPhase.Download]);

    internal void Add(RunPhase phase, TimeSpan elapsed) => _phases[(int)phase] += elapsed;
}
