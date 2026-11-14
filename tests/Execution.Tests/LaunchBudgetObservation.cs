namespace APThermo.Execution.Tests;

/// <summary>The per-launch target, with complete nonempty case coverage and a bounded maximum identity.</summary>
internal static class LaunchBudgetObservation
{
    internal static string? Violation(int cases, RunTimings timings, TimeSpan limit)
    {
        ArgumentNullException.ThrowIfNull(timings);
        if (cases <= 0 || timings.LaunchCount <= 0)
        {
            return "the workload has no successful synchronized launch observations";
        }

        if (timings.CoveredCases != cases || timings.LaunchCount > timings.CoveredCases)
        {
            return $"launch observations cover {timings.CoveredCases} cases in {timings.LaunchCount} launches, expected {cases} cases";
        }

        var invalidMaximum = timings.MaxLaunchOrdinal < 1 || timings.MaxLaunchOrdinal > timings.LaunchCount ||
            timings.MaxLaunchCases < 1 || timings.MaxLaunchCases > timings.CoveredCases ||
            timings.MaxLaunchDuration < TimeSpan.Zero || timings.MaxLaunchDuration > timings.Kernel;
        return invalidMaximum ? "launch observations have an inconsistent maximum identity or duration"
            : timings.MaxLaunchDuration > limit
            ? $"launch {timings.MaxLaunchOrdinal} of {timings.MaxLaunchCases} cases took {timings.MaxLaunchDuration.TotalMilliseconds:R} ms, above the budget of {limit.TotalMilliseconds:R} ms; aggregate {timings.Kernel.TotalMilliseconds:F1} ms in {timings.LaunchCount} launches covering {timings.CoveredCases} cases"
            : null;
    }

    internal static string Describe(RunTimings timings)
    {
        ArgumentNullException.ThrowIfNull(timings);
        return $"firstLaunchCases={timings.FirstLaunchCases}; reusedPrefix={timings.ReusedPrefix}; aggregate={timings.Kernel.TotalMilliseconds:F1} ms; launches={timings.LaunchCount}; coveredCases={timings.CoveredCases}; maximum={timings.MaxLaunchDuration.TotalMilliseconds:R} ms; maximumCases={timings.MaxLaunchCases}; maximumOrdinal={timings.MaxLaunchOrdinal}";
    }
}