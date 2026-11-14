using System.Runtime.CompilerServices;
using APThermo.Thermo;
using ILGPU;
using ILGPU.Runtime;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the compile of the rocket kernel is bounded (BOOT.md, "The rocket kernel's compile is bounded and released"; the root's
/// Compile size constraint). ILGPU 1.5.3 inlines every call site of a method that holds a whole solve as a full copy, so
/// the rocket program's seven call sites of <c>StationSolve.At</c> once compiled into 11 GB and 50 s where three had taken
/// 1.2 GB and 6 s. Joins <see cref="EngineFixture.CollectionName"/>, not for the shared fixture (every fact builds its own
/// engine) but so a compile never runs beside the CUDA throughput tripwire.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class RocketCompileTests(ITestOutputHelper output)
{
    /// <summary>
    /// The bound on what one first compile of the rocket kernel allocates in the whole process, 7 GiB. ILGPU compiles on a
    /// pool of threads sized by the machine's core count, so the share that lands on the calling thread is a property of the
    /// machine (1.0 GB on 16 cores, 1.6 GB on 2, 2.57 GB on a hosted 4-core runner), while the bytes allocated by every
    /// thread together are the compile's own size: 3 576 MB in Release and 3 890 MB in Debug at every processor count from
    /// 2 to 16, a spread of 0.05 %. The bound is twice the Release figure and 1.93 times the Debug one; the red figures
    /// (the attribute on <c>StationSolve.At</c> removed) are in the <c>BOOT.md</c> of this node, which also records the runs.
    /// The metric is bytes, not seconds: not moved by the load of a machine, and since 2026-10-05 not by its core count.
    /// </summary>
    private const long CompileAllocationBound = 7L * 1024 * 1024 * 1024;

    /// <summary>
    /// The first rocket run of a fresh CPU engine, one case, allocates less than <see cref="CompileAllocationBound"/> bytes in
    /// the whole process, every thread counted (<see cref="GC.GetTotalAllocatedBytes"/>, precise). The compile dominates the
    /// run: the case itself allocates the size of its buffers. The class joins the engine collection, so no other fact of that
    /// collection allocates beside it; the three classes outside it allocate under a megabyte in the same window (BOOT.md
    /// records the run of the whole fast set against the fact alone). Red with the attribute of
    /// <c>StationSolve.At</c> removed.
    /// </summary>
    [Fact]
    public void TheRocketKernelCompilesWithinItsAllocationBound()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.SharedDatabase)[0];
        var batch = (family with { Inputs = [family.Inputs[0]] }).Batch();
        using var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var tables = engine.Upload(family.Table, family.Transport);

        Assert.Equal(0, engine.Launchers.Count);
        var totalBefore = GC.GetTotalAllocatedBytes(precise: true);
        var callingThreadBefore = GC.GetAllocatedBytesForCurrentThread();
        var result = engine.Run(tables, batch);
        var callingThread = GC.GetAllocatedBytesForCurrentThread() - callingThreadBefore;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - totalBefore;

        output.WriteLine($"the rocket kernel's first run allocated {allocated} bytes in the process ({callingThread} of them on the calling thread, a figure of this machine's {Environment.ProcessorCount} processors), warm-up {result.Timings.WarmUp.TotalSeconds:F2} s");
        Assert.Equal(CaseStatus.Ok, result.Status[0]);
        Assert.True(result.Timings.WarmUp > TimeSpan.Zero, "the run did not compile the rocket kernel, so there is nothing to bound");
        Assert.Equal(1, engine.Launchers.Count);
        var launcher = TheCachedRocketLauncher(engine);
        _ = HeapAfterCollection();
        Assert.True(launcher.IsAlive, "the compiled rocket launcher is retained while its engine is live");
        GC.KeepAlive(engine);
        Assert.True(allocated <= CompileAllocationBound,
                    $"the rocket kernel's compile allocated {allocated} bytes in the process, over the bound of {CompileAllocationBound}: a stage that holds a whole solve is inlined at too many call sites (root BOOT.md, Compile size)");
    }

    /// <summary>
    /// A disposed engine that stays reachable holds no launcher (BOOT.md, "Release at dispose"): the cache is empty after
    /// <see cref="Engine.Dispose"/>, and a launcher taken before it is dead after a collection while the engine object is
    /// still referenced. Red without <see cref="KernelCache.Clear"/> in <see cref="Engine.Dispose"/>.
    /// </summary>
    [Fact]
    public void ADisposedEngineHoldsNoLauncher()
    {
        var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var launcher = CompileTheProbeKernel(engine);
        Assert.Equal(1, engine.Launchers.Count);
        Assert.True(launcher.IsAlive, "the launcher must be alive while its engine is in use");

        engine.Dispose();

        Assert.Equal(0, engine.Launchers.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(launcher.IsAlive, "the disposed engine, still referenced, keeps its compiled launcher alive");
        GC.KeepAlive(engine);
    }

    /// <summary>
    /// A disposed engine that stays reachable holds none of the program its rocket kernel compiled (BOOT.md, "Release at
    /// dispose"): the managed heap after <see cref="Engine.Dispose"/> and a full collection is under a quarter of what it held
    /// while the engine was in use. Measured in <see cref="GC.GetTotalMemory"/> against the heap before the run, so no constant
    /// is a machine's: 143 MiB kept live and none after, at the time of writing, 3.9 GB kept live before the inlining bound.
    /// A live cached launcher proves a real program; a one-MiB noise allowance permits smaller compiler output.
    /// Red without the context's cache cleared by <see cref="KernelCache.Clear"/>,
    /// which dropping the launchers alone does not do.
    /// </summary>
    [Fact]
    public void ADisposedEngineKeepsNoCompiledProgram()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.SharedDatabase)[0];
        var batch = (family with { Inputs = [family.Inputs[0]] }).Batch();
        var engine = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var tables = engine.Upload(family.Table, family.Transport);
        var baseline = HeapAfterCollection();

        var result = engine.Run(tables, batch);
        Assert.True(result.Timings.WarmUp > TimeSpan.Zero, "the fresh engine must compile the requested rocket kernel");
        Assert.Equal(1, engine.Launchers.Count);
        var launcher = TheCachedRocketLauncher(engine);
        var kept = HeapAfterCollection() - baseline;
        Assert.True(launcher.IsAlive, "the live engine must retain the compiled rocket launcher");
        tables.Dispose();
        engine.Dispose();
        var keptAfterDispose = HeapAfterCollection() - baseline;

        output.WriteLine($"the rocket kernel's program kept {kept} bytes of managed heap while the engine was in use and {keptAfterDispose} after Dispose");
        Assert.Equal(CaseStatus.Ok, result.Status[0]);
        Assert.Equal(0, engine.Launchers.Count);
        Assert.False(launcher.IsAlive, "the disposed engine must release its rocket launcher");
        var retainedBound = Math.Max(1L << 20, kept / 4);
        Assert.True(keptAfterDispose <= retainedBound, $"the disposed engine, still referenced, keeps {keptAfterDispose} of the {kept} program bytes, over the noise-aware bound {retainedBound}");
        GC.KeepAlive(engine);
    }

    /// <summary>Observes the already compiled rocket launcher without retaining a strong caller reference.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference TheCachedRocketLauncher(Engine engine)
    {
        var launcher = engine.Launchers.Get<Delegate>(nameof(Kernels.Rocket), out var warmUp);
        Assert.Equal(TimeSpan.Zero, warmUp);
        return new WeakReference(launcher);
    }

    /// <summary>The managed heap after every collection and finalizer has run, so a garbage object is not counted.</summary>
    private static long HeapAfterCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>Compiles the probe kernel through the engine's own path and returns a weak reference to its launcher, so no local of the caller keeps the launcher alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompileTheProbeKernel(Engine engine)
    {
        _ = engine.ProbeMath([1.0]);
        var launcher = engine.Launchers.Get<Action<AcceleratorStream, Index1D, ArrayView<double>, ArrayView<double>>>(nameof(Kernels.Probe), out _);
        return new WeakReference(launcher);
    }
}
