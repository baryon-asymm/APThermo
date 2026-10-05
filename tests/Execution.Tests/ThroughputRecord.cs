using System.Globalization;

namespace APThermo.Execution.Tests;

/// <summary>
/// The per-iteration half of the throughput tripwire (Execution.Tests BOOT.md, 2026-10-04): the rocket sweep's CUDA kernel time per
/// Newton step against the approved record. The CPU/CUDA ratio moves with the CPU's load and with the transfer overhead; the kernel time
/// per step moves with the compiled kernel alone, so a change that makes every step slower shows here and a run-to-run variation of
/// 2 % does not. The rule is a function of the record's figures so that a fact without a GPU can show it red.
/// </summary>
internal static class ThroughputRecord
{
    /// <summary>The key of the approved record's figure: the CUDA kernel seconds per Newton step of the sweep, summed over every station of every case.</summary>
    public const string PerIterationKey = "cuda_kernel_seconds_per_iteration";

    /// <summary>The key of the Newton steps per case, summed over the stations, the workload the per-iteration figure divides by.</summary>
    public const string IterationsPerCaseKey = "iterations_per_case";

    /// <summary>1.15: the largest ratio of a measured per-iteration kernel time to the approved one (run-to-run variation is under 2 %, the 0.2.1 to 0.2.2 regression was 25 %).</summary>
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
        if (!approved.TryGetValue(PerIterationKey, out var text)
            || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var figure)
            || !(figure > 0.0))
        {
            return $"{fileName} carries no valid {PerIterationKey} line, so the kernel time per Newton step cannot be compared; "
                 + "re-approve the file from a Release run on this platform (Execution.Tests BOOT.md, the approved throughput file)";
        }

        var relative = measuredPerIteration / figure;
        return relative <= PerIterationLimit
            ? null
            : string.Create(CultureInfo.InvariantCulture,
                $"the CUDA kernel time per Newton step {measuredPerIteration:0.000e+00} s is {relative:P1} of the approved {figure:0.000e+00} s ({fileName}); "
              + $"the limit is {PerIterationLimit:P0}. A kernel slower per step is a regression of the code, not of the machine (root BOOT.md, Compile size)");
    }

    private static long Steps(RocketBatchResult result) => result.Iterations.Sum(count => (long)count);
}
