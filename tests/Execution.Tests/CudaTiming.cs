namespace APThermo.Execution.Tests;

/// <summary>
/// The timed CUDA runs of the sweep: the median of the elapsed times, the median of the kernel times (each taken on its own, so the
/// two figures are the same statistic of the same runs) and the kernel time of every run in the order of the runs.
/// </summary>
/// <param name="Elapsed">The median elapsed time of the timed runs.</param>
/// <param name="Kernel">The median of <see cref="KernelRuns"/>; the throughput tripwire's per-iteration figure is taken from it.</param>
/// <param name="KernelRuns">The kernel time of every timed run, in the order of the runs.</param>
internal sealed record CudaTiming(TimeSpan Elapsed, TimeSpan Kernel, IReadOnlyList<TimeSpan> KernelRuns)
{
    /// <summary>No CUDA run: every time is zero.</summary>
    public static CudaTiming None { get; } = new(TimeSpan.Zero, TimeSpan.Zero, []);

    /// <summary>The timing of runs whose elapsed and kernel times are given in the order of the runs: each median is taken on its own.</summary>
    public static CudaTiming From(TimeSpan[] elapsed, TimeSpan[] kernels) => new(Median(elapsed), Median(kernels), kernels);

    /// <summary>The middle value of the sorted times (the upper middle of an even count), whatever the order of the runs.</summary>
    public static TimeSpan Median(TimeSpan[] values)
    {
        var sorted = (TimeSpan[])values.Clone();
        Array.Sort(sorted);
        return sorted[sorted.Length / 2];
    }
}
