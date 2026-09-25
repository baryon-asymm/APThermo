using BenchmarkDotNet.Running;

namespace APThermo.Benchmarks;

/// The command-line entry point (API.md, Entry point): a plain `BenchmarkSwitcher`
/// over the groups of `BOOT.md`, Constraints, run with the one job of
/// `BenchmarkEnvironment`.
internal static class Program
{
    /// <summary>Runs every benchmark group `args` selects, or all of them, and returns 1 if any summary carries a critical
    /// validation error.</summary>
    public static int Main(string[] args)
    {
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkEnvironment.Config);
        return summaries.Any(summary => summary.HasCriticalValidationErrors) ? 1 : 0;
    }
}
