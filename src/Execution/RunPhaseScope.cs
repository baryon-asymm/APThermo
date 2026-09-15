using System.Diagnostics;

namespace APThermo.Execution;

/// <summary>
/// One phase of a run, measured for as long as the scope lives. The phase is named where the work is done instead of being chosen
/// by a reference to a field, so an upload cannot be booked as kernel time.
/// </summary>
internal readonly struct RunPhaseScope : IDisposable
{
    private readonly RunTimer _timer;
    private readonly RunPhase _phase;
    private readonly long _start;

    internal RunPhaseScope(RunTimer timer, RunPhase phase)
    {
        _timer = timer;
        _phase = phase;
        _start = Stopwatch.GetTimestamp();
    }

    /// <summary>Adds the time the scope lived to its phase.</summary>
    public void Dispose() => _timer.Add(_phase, Stopwatch.GetElapsedTime(_start));
}
