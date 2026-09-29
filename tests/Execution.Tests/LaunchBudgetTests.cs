using APThermo.Execution.Chunks;
using ILGPU.Runtime.Cuda;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the launch budget (BOOT.md, "A launch fits a time budget"; the second audit's Execution finding F2) — the pure
/// arithmetic of <see cref="LaunchBudget"/> and <see cref="ChunkPlan.FirstChunkCases"/>/<see cref="ChunkPlan.NextChunkCases"/>,
/// and the timeout translation of <see cref="BatchRun"/>, all driven with injected times and an injected failure, no GPU needed.
/// Joins <see cref="EngineFixture.CollectionName"/>, not for the shared fixture (every fact here builds its own engine or
/// none at all) but so its four <c>Cuda</c>-tagged facts never run concurrently with <see cref="EngineFixture.Shared"/>'s own
/// CUDA engine creation on another thread: constructing a real <see cref="CudaException(CudaError)"/> touches the driver
/// (<c>cuGetErrorString</c>) the same way <see cref="EngineFixture"/>'s lazy CUDA engine does, and a genuine CUDA run under
/// WSL found the two racing — <c>CUDA device 0 was requested, but 0 device(s) exist</c> on roughly a third of runs of the
/// full suite, never on a run of either fact alone. Two further facts below prove the third audit pass's finding 2
/// (the loss decision itself, and the refusal it drives) without any <c>CudaException</c>, so they carry no <c>Cuda</c>
/// trait and run under <c>APTHERMO_NO_CUDA=1</c> like any other fact of this node (2026-09-29, review).
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class LaunchBudgetTests
{
    /// <summary>No budget never bounds a chunk: the very first size, and every later one, is <see cref="int.MaxValue"/>
    /// regardless of what the previous chunk measured.</summary>
    [Fact]
    public void NoBudgetNeverBoundsAChunk()
    {
        Assert.False(LaunchBudget.None.IsBounded);
        Assert.Equal(int.MaxValue, LaunchBudget.None.NextChunkCases(16_384, TimeSpan.FromSeconds(1)));
        Assert.Equal(int.MaxValue, LaunchBudget.None.NextChunkCases(0, TimeSpan.Zero));
    }

    /// <summary>A bounded budget answers from the previous chunk's time per case: half the limit's worth of cases, at
    /// the previous chunk's own rate.</summary>
    [Fact]
    public void ABoundedBudgetScalesFromThePreviousChunksTimePerCase()
    {
        var budget = LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(2));   // a quarter of 2 s = 0.5 s
        Assert.True(budget.IsBounded);

        // 100 cases in 0.5 ms each (50 ms total): the 0.5 s budget then allows 1000 cases.
        Assert.Equal(1000, budget.NextChunkCases(100, TimeSpan.FromMilliseconds(50)));

        // A previous chunk of zero cases, or zero measured time, cannot scale: one case, so a run always makes progress.
        Assert.Equal(1, budget.NextChunkCases(0, TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, budget.NextChunkCases(100, TimeSpan.Zero));
        Assert.Equal(1, budget.NextChunkCases(-1, TimeSpan.FromMilliseconds(50)));
    }

    /// <summary>A budget's answer never goes below one case, even at an implausibly slow measured rate.</summary>
    [Fact]
    public void ABoundedBudgetNeverAnswersBelowOneCase()
    {
        var budget = LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(2));
        Assert.Equal(1, budget.NextChunkCases(1, TimeSpan.FromHours(1)));
    }

    /// <summary><see cref="ChunkPlan.FirstChunkCases"/>: with no budget it is the plan's static <see cref="ChunkPlan.Size"/>
    /// (or the count, if smaller); with a budget it is one wave of the device, clamped to the same bounds.</summary>
    [Fact]
    public void TheFirstChunkIsSizeWithNoBudgetAndOneWaveWithABudget()
    {
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 100 };
        var unbounded = ChunkPlan.For(1000, 8, 1, options);
        Assert.Equal(100, unbounded.FirstChunkCases(wave: 4096));

        var bounded = ChunkPlan.For(1000, 8, 1, options, LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(2)));
        Assert.Equal(64, bounded.FirstChunkCases(wave: 64));           // below Size and Count: the wave itself
        Assert.Equal(100, bounded.FirstChunkCases(wave: 4096));        // above Size: clamped to it
        Assert.Equal(1, bounded.FirstChunkCases(wave: 0));             // never below one case

        var smallBatch = ChunkPlan.For(10, 8, 1, options, LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(2)));
        Assert.Equal(10, smallBatch.FirstChunkCases(wave: 4096));      // above Count: clamped to it
    }

    /// <summary><see cref="ChunkPlan.NextChunkCases"/>: with no budget every chunk is <see cref="ChunkPlan.Size"/> (or the
    /// remainder); with a budget the answer still never exceeds <see cref="ChunkPlan.Size"/> or the remainder.</summary>
    [Fact]
    public void NextChunkCasesStaysWithinSizeAndTheRemainder()
    {
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 100 };
        var unbounded = ChunkPlan.For(150, 8, 1, options);
        Assert.Equal(100, unbounded.NextChunkCases(100, TimeSpan.FromMilliseconds(1), coveredSoFar: 0));
        Assert.Equal(50, unbounded.NextChunkCases(100, TimeSpan.FromMilliseconds(1), coveredSoFar: 100));

        // A budget answering far more cases than Size allows is still clamped to Size, and to the batch's remainder.
        var bounded = ChunkPlan.For(150, 8, 1, options, LaunchBudget.FromRunTimeLimit(TimeSpan.FromSeconds(2)));
        Assert.Equal(100, bounded.NextChunkCases(1, TimeSpan.FromTicks(1), coveredSoFar: 0));
        Assert.Equal(50, bounded.NextChunkCases(1, TimeSpan.FromTicks(1), coveredSoFar: 100));
    }

    /// <summary>
    /// The timeout translation (BOOT.md, "A launch fits a time budget"): an injected launch-timeout failure through
    /// <see cref="BatchRun.Execute"/> becomes <see cref="AcceleratorUnavailableException"/> naming the run-time limit,
    /// the chunk's own case count and the CPU accelerator as the remedy. No kernel is ever launched — the launch delegate
    /// throws the failure directly, on the CPU accelerator, without a real device or a real timeout. Tagged <c>Cuda</c>
    /// and gated on <see cref="Engine.CudaForbidden"/>, like every other fact that touches a real <c>CudaException</c>
    /// or <c>CudaError</c>: <see cref="CudaException(CudaError)"/>'s own constructor asks the CUDA driver for the
    /// error's text (<c>cuGetErrorString</c>), so building one at all needs nvcuda loadable, which a hosted runner with
    /// no GPU does not have — the same reason the library-discovery facts elsewhere in this node are gated the same way.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy()
    {
        if (Engine.CudaForbidden)
        {
            return;
        }

        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10 };
        var plan = ChunkPlan.For(25, 8, 1, options);
        using var buffers = new Chunks.ChunkBuffers(cpu.IlgpuAccelerator);
        var timer = new RunTimer();
        var failure = Assert.Throws<AcceleratorUnavailableException>(() => cpu.RunBatchLoop(
            plan, buffers, timer, _ => throw new CudaException(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT)));
        Assert.Contains("10", failure.Message, StringComparison.Ordinal);         // the chunk's own case count
        Assert.Contains("run-time limit", failure.Message, StringComparison.Ordinal);
        Assert.Contains("CPU accelerator", failure.Message, StringComparison.Ordinal);
        _ = Assert.IsType<CudaException>(failure.InnerException);
    }

    /// <summary>A non-timeout <see cref="CudaException"/> passes through unwrapped: only the run-time-limit error is
    /// translated. Gated the same way as <see cref="ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy"/>.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ANonTimeoutCudaFailurePassesThroughUnwrapped()
    {
        if (Engine.CudaForbidden)
        {
            return;
        }

        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var plan = ChunkPlan.For(4, 8, 1, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        using var buffers = new Chunks.ChunkBuffers(cpu.IlgpuAccelerator);
        var timer = new RunTimer();
        _ = Assert.Throws<CudaException>(() => cpu.RunBatchLoop(
            plan, buffers, timer, _ => throw new CudaException(CudaError.CUDA_ERROR_OUT_OF_MEMORY)));
    }

    /// <summary>
    /// The third audit pass's finding 2 (BOOT.md, "A lost context stays an AcceleratorUnavailableException"): once a
    /// launch timeout has left an engine's session lost, through the same seam as
    /// <see cref="ALaunchTimeoutBecomesAnAcceleratorUnavailableExceptionNamingTheLimitAndTheRemedy"/>, a later call
    /// (<see cref="Engine.ProbeMath"/> stands in for every public entry point <c>Guard</c>/<c>Upload</c>/<c>ProbeMath</c>
    /// itself checks) refuses with <see cref="AcceleratorUnavailableException"/> naming the earlier timeout, instead of
    /// touching the dead context again. The timeout cannot be provoked for real on the reference machine — it resets the
    /// display driver — so this and the disposal below inject their failures directly, on the CPU accelerator, exactly
    /// as the timeout fact above does.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ATimedOutEngineRefusesANewCall()
    {
        if (Engine.CudaForbidden)
        {
            return;
        }

        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10 };
        var plan = ChunkPlan.For(25, 8, 1, options);
        using var buffers = new Chunks.ChunkBuffers(cpu.IlgpuAccelerator);
        var timer = new RunTimer();
        _ = Assert.Throws<AcceleratorUnavailableException>(() => cpu.RunBatchLoop(
            plan, buffers, timer, _ => throw new CudaException(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT)));

        var refusal = Assert.Throws<AcceleratorUnavailableException>(() => cpu.ProbeMath([1.0]));
        Assert.Contains("earlier launch timeout", refusal.Message, StringComparison.Ordinal);
        _ = Assert.IsType<AcceleratorUnavailableException>(refusal.InnerException);
    }

    /// <summary>
    /// The other half of the same finding: disposal on a lost engine drops a <see cref="CudaException"/> carrying the
    /// sticky launch-timeout error instead of letting it replace whatever is already propagating, or surface at all
    /// from an ordinary dispose (BOOT.md, the same section). <see cref="Engine.DisposeAfterLoss"/> is the exact seam
    /// <see cref="UploadedTables"/>' own disposal reads; a disposable that always throws the sticky error stands in for
    /// ILGPU's own device-buffer cleanup on a context the timeout already killed, which this task cannot provoke for
    /// real on the reference machine.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ATimedOutEnginesDisposalDropsTheStickyFailure()
    {
        if (Engine.CudaForbidden)
        {
            return;
        }

        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10 };
        var plan = ChunkPlan.For(25, 8, 1, options);
        using var buffers = new Chunks.ChunkBuffers(cpu.IlgpuAccelerator);
        var timer = new RunTimer();
        _ = Assert.Throws<AcceleratorUnavailableException>(() => cpu.RunBatchLoop(
            plan, buffers, timer, _ => throw new CudaException(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT)));

        using var stillSticky = new StickyDisposable(new CudaException(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT));
        var disposal = Record.Exception(() => cpu.DisposeAfterLoss(stillSticky));
        Assert.Null(disposal);
    }

    /// <summary>
    /// The third audit pass's finding 2, decided without any ILGPU CUDA object (2026-09-29, review): a real
    /// <see cref="CudaException"/> loads the CUDA driver into the process even on the CPU accelerator, which
    /// <c>AcceleratorChoiceTests.NoCudaDriverIsLoadedInAProcessThatForbidsCuda</c> catches, so
    /// <see cref="AcceleratorSession.DropsAfterLoss(CudaError)"/>'s decision itself is proven here on the bare
    /// <see cref="CudaError"/> value: never lost, the sticky error; lost, the sticky error; lost, the wrong error.
    /// Carries no <c>Cuda</c> trait and runs under <c>APTHERMO_NO_CUDA=1</c>. The exception-typed half
    /// (<see cref="AcceleratorSession.DropsAfterLoss(CudaException)"/>) and the disposal that needs a real one stay
    /// the <c>Cuda</c>-tagged fact above.
    /// </summary>
    [Fact]
    public void DropsAfterLossMatchesOnlyTheStickyLaunchTimeoutOfALostSession()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        Assert.False(cpu.DropsAfterLoss(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT));
        cpu.MarkLost(new AcceleratorUnavailableException("injected for this fact"));
        Assert.True(cpu.DropsAfterLoss(CudaError.CUDA_ERROR_LAUNCH_TIMEOUT));
        Assert.False(cpu.DropsAfterLoss(CudaError.CUDA_ERROR_OUT_OF_MEMORY));
    }

    /// <summary>
    /// The other half of the same finding, injected the same way: once <see cref="Engine.MarkLost"/> has recorded a
    /// timeout, a later call (<see cref="Engine.ProbeMath"/> stands in for every public entry point
    /// <c>Guard</c>/<c>Upload</c>/<c>ProbeMath</c> itself checks) refuses with <see cref="AcceleratorUnavailableException"/>
    /// naming the earlier timeout, instead of touching the dead context again. Carries no <c>Cuda</c> trait for the
    /// same reason as the fact above; <see cref="ATimedOutEngineRefusesANewCall"/> proves the same refusal after the
    /// real translation, on the reference machine.
    /// </summary>
    [Fact]
    public void AnEngineMarkedLostRefusesANewCall()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var timeout = new AcceleratorUnavailableException("the launch of 10 case(s) exceeded the device's kernel run-time limit");
        cpu.MarkLost(timeout);
        var refusal = Assert.Throws<AcceleratorUnavailableException>(() => cpu.ProbeMath([1.0]));
        Assert.Contains("earlier launch timeout", refusal.Message, StringComparison.Ordinal);
        Assert.Same(timeout, refusal.InnerException);
    }

    /// <summary>A disposable whose first <see cref="Dispose"/> always throws the given failure, standing in for
    /// ILGPU's own device-buffer cleanup on a context a launch timeout already left sticky; idempotent like ILGPU's
    /// own dispose (BOOT.md, the pipelines' shared <c>using</c> declaration), so this test's own closing <c>using</c>
    /// finds it already disposed and does nothing, once <see cref="Engine.DisposeAfterLoss"/> has dropped the first
    /// throw.</summary>
    private sealed class StickyDisposable(CudaException failure) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            throw failure;
        }
    }
}
