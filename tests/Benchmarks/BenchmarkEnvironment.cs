using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace APThermo.Benchmarks;

/// One job for every group (BOOT.md, Constraints): Release, x64, the in-process
/// toolchain, so that ILGPU's native libraries and the CUDA post-link load as in the
/// library, with `MemoryDiagnoser` on. The invocation count is pinned to one and the
/// unroll factor to one so that every measured call runs alone within its iteration:
/// several of the groups (`OneTimeCostBenchmarks`) rebuild their cold state in
/// `[IterationSetup]`, and an unrolled batch of invocations would hide behind the
/// first one's warm cache. Built over `DefaultConfig` so the usual loggers, exporters
/// and columns stay in place; only the job and the diagnoser are this node's own.
internal static class BenchmarkEnvironment
{
    public static IConfig Config { get; } = ManualConfig.Create(DefaultConfig.Instance)
        .AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Instance)
            .WithWarmupCount(5)
            .WithIterationCount(20)
            .WithInvocationCount(1)
            .WithUnrollFactor(1))
        .AddDiagnoser(MemoryDiagnoser.Default);
}
