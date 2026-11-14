using System.Globalization;

namespace APThermo.Execution.Tests;

/// <summary>
/// Throughput record tripwires: the original CUDA/CPU ratio floors and the rocket sweep's CUDA kernel time per Newton step
/// against the approved record (Execution.Tests BOOT.md, per-step criterion of 2026-10-04). This synchronized wall-time tripwire
/// includes every timed launch. Launch partition,
/// device clocks and competing load can affect it; a breach does not identify its cause. The rule is a function of the record's
/// figures so that a fact without a GPU can show it red.
/// </summary>
internal static class ThroughputRecord
{
    /// <summary>The key of the approved record's figure: the CUDA kernel seconds per Newton step of the sweep, summed over every station of every case.</summary>
    public const string PerIterationKey = "cuda_kernel_seconds_per_iteration";

    /// <summary>The key of the Newton steps per case, summed over the stations, the workload the per-iteration figure divides by.</summary>
    public const string IterationsPerCaseKey = "iterations_per_case";

    /// <summary>1.15: the unchanged inclusive ceiling on measured per-iteration kernel time divided by the finite positive approved figure.</summary>
    public const double PerIterationLimit = 1.15;

    /// <summary>The Newton steps of the batch, summed over every station of every case, per case.</summary>
    public static double IterationsPerCase(RocketBatchResult result) => (double)Steps(result) / result.Count;

    /// <summary>
    /// A kernel wall time over the Newton steps of the batch, in seconds. The caller passes the median kernel time of the timed runs: the
    /// first run's is not representative (2026-10-05, Execution.Tests BOOT.md), and the steps are the same in every run.
    /// </summary>
    public static double KernelSecondsPerIteration(TimeSpan kernel, RocketBatchResult result) => kernel.TotalSeconds / Steps(result);

    /// <summary>The figure as a line of the record: the per-iteration time in scientific notation with four significant digits, the step count with three decimals.</summary>
    public static string Line(string key, double value) =>
        key + ": " + value.ToString(string.Equals(key, PerIterationKey, StringComparison.Ordinal) ? "0.000e+00" : "F3", CultureInfo.InvariantCulture);

    /// <summary>The key-value pairs of an approved record, each line "key: value" split at its first colon.</summary>
    public static Dictionary<string, string> Parse(IEnumerable<string> lines) =>
        lines.Select(line => line.Split(':', 2))
             .Where(parts => parts.Length == 2)
             .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);

    /// <summary>
    /// Null when the measured kernel time per step is within <see cref="PerIterationLimit"/> of the approved one; otherwise the message of
    /// the failure. A record without the figure fails with a message of its own, never skips: a platform whose file was written before
    /// the figure existed is re-approved from a run of that platform.
    /// </summary>
    public static string? Violation(IReadOnlyDictionary<string, string> approved, string fileName, double measuredPerIteration)
    {
        if (!TryApprovedScalar(approved, PerIterationKey, out var figure))
        {
            return $"{fileName} carries no valid {PerIterationKey} line, so the kernel time per Newton step cannot be compared; "
                 + "re-approve the file from a Release run on this platform (Execution.Tests BOOT.md, the approved throughput file)";
        }

        if (!double.IsFinite(measuredPerIteration) || measuredPerIteration < 0.0)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"the measured CUDA kernel time per Newton step {measuredPerIteration:R} s must be finite and nonnegative ({fileName}); the synchronized wall-time tripwire cannot compare an invalid measurement");
        }

        var relative = measuredPerIteration / figure;
        return relative <= PerIterationLimit
            ? null
            : string.Create(CultureInfo.InvariantCulture,
                $"the CUDA kernel time per Newton step {measuredPerIteration:0.000e+00} s is {relative:P1} of the approved {figure:0.000e+00} s ({fileName}); "
              + $"the limit is {PerIterationLimit:P0}. This synchronized wall-time tripwire does not identify the cause of the increase");
    }

    /// <summary>The existing 5x and 80 percent comparisons, extracted unchanged for portable record controls.</summary>
    public static string? RatioViolation(IReadOnlyDictionary<string, string> approved, string fileName, double measuredRatio) =>
        TryApprovedScalar(approved, "ratio", out var approvedRatio)
            ? RatioFloorViolation(approvedRatio, fileName, measuredRatio)
            : $"{fileName} carries no valid finite positive ratio line; re-approve the file from a Release run on this platform (BOOT.md)";

    private static string? RatioFloorViolation(double approvedRatio, string fileName, double measuredRatio) =>
        !(measuredRatio >= 5.0)
            ? string.Create(CultureInfo.InvariantCulture,
                $"CUDA is only {measuredRatio:F2}x faster than the CPU accelerator (root criterion: at least 5x)")
            : measuredRatio >= 0.8 * approvedRatio
                ? null
                : string.Create(CultureInfo.InvariantCulture,
                    $"CUDA/CPU ratio {measuredRatio:F2} fell below 80 % of the approved {approvedRatio:F2} ({fileName})");

    private static bool TryApprovedScalar(IReadOnlyDictionary<string, string> approved, string key, out double figure)
    {
        figure = 0.0;
        return approved.TryGetValue(key, out var text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out figure)
            && double.IsFinite(figure)
            && figure > 0.0;
    }

    private static long Steps(RocketBatchResult result) => result.Iterations.Sum(count => (long)count);
}
