using BenchmarkDotNet.Running;

namespace APThermo.Benchmarks.Runner;

/// <summary>
/// The command-line entry point (API.md, Entry point): a plain <c>BenchmarkSwitcher</c> over the benchmark groups of
/// the parent node's <c>BOOT.md</c>, Constraints, run with its one job (<see cref="BenchmarkEnvironment.Config"/>).
/// </summary>
internal static class Program
{
    /// <summary>Runs every benchmark group <paramref name="args"/> selects, or all of them, from the parent library's
    /// assembly, and returns 1 if any summary carries a critical validation error.</summary>
    public static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(BenchmarkEnvironment).Assembly).Run(args, BenchmarkEnvironment.Config);
        return summaries.Any(summary => summary.HasCriticalValidationErrors) ? 1 : 0;
    }
}
