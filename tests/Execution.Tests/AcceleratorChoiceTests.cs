using System.Diagnostics;
using APThermo.Execution.Chunks;
using APThermo.Execution.Ptx;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Execution.Tests;

/// <summary>L0: accelerator choice, the environment variable, the reasons of a fallback, the ILGPU assertion, batch validation.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class AcceleratorChoiceTests
{
    /// <summary>The cpu engine names itself and the ilgpu version.</summary>
    [Fact]
    public void TheCpuEngineNamesItselfAndTheIlgpuVersion()
    {
        var info = EngineFixture.Shared.Cpu.Accelerator;
        Assert.Equal(AcceleratorKind.Cpu, info.Kind);
        Assert.Equal(PtxPostLink.ExpectedIlgpuVersion, info.IlgpuVersion);
        Assert.True(info.ThreadsOrMultiprocessors >= 1);
        Assert.False(string.IsNullOrWhiteSpace(info.DeviceName));
        Assert.Null(info.CudaSkippedBecause);   // an engine asked for the CPU never tried CUDA
    }

    /// <summary>
    /// An auto fallback says why cuda was skipped. Decided with <c>cudaForbidden: false</c> (2026-09-28, the guards audit's
    /// F7): on every hosted CI job <c>APTHERMO_NO_CUDA=1</c> made <see cref="AcceleratorChoice.Decide(EngineOptions)"/> refuse
    /// for that reason alone, so the refusals of the CUDA path itself ran only on a local machine. The injected seam reaches
    /// them on every runner, CUDA forbidden or not. The device index is far out of range (2026-10-05, no toolkit file to
    /// point at any more): a machine with CUDA refuses the index, a machine without it cannot create the context, and
    /// either way the reason is a message and the session is the CPU's.
    /// </summary>
    [Fact]
    public void AnAutoFallbackSaysWhyCudaWasSkipped()
    {
        var options = new EngineOptions { Accelerator = AcceleratorKind.Auto, CudaDeviceIndex = 9999 };
        var decision = AcceleratorChoice.Decide(options, cudaForbidden: false);
        using (decision.Session)
        {
            Assert.Equal(AcceleratorKind.Cpu, decision.Session.Info.Kind);
            var reason = decision.CudaSkippedBecause;
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.Equal(reason, decision.Session.Info.CudaSkippedBecause);
        }
    }

    /// <summary>An explicit cuda request that cannot be met fails instead of falling back. Decided with <c>cudaForbidden: false</c>, the same reason as <see cref="AnAutoFallbackSaysWhyCudaWasSkipped"/> (F7).</summary>
    [Fact]
    public void AnExplicitCudaRequestThatCannotBeMetFailsInsteadOfFallingBack()
    {
        var options = new EngineOptions { Accelerator = AcceleratorKind.Cuda, CudaDeviceIndex = 9999 };
        var refused = Assert.Throws<AcceleratorUnavailableException>(() => AcceleratorChoice.Decide(options, cudaForbidden: false));
        Assert.False(string.IsNullOrWhiteSpace(refused.Message));
    }

    /// <summary>
    /// The same two requests with <c>cudaForbidden: true</c> refuse for that reason alone, before any CUDA context is
    /// created: the other half of the injected seam (F7), proving <c>cudaForbidden</c> actually gates the CUDA path rather
    /// than being ignored.
    /// </summary>
    [Fact]
    public void CudaForbiddenRefusesBeforeAnyCudaContextIsCreated()
    {
        var options = new EngineOptions { Accelerator = AcceleratorKind.Auto };
        var decision = AcceleratorChoice.Decide(options, cudaForbidden: true);
        using (decision.Session)
        {
            Assert.Contains(EngineOptions.NoCudaVariable, decision.CudaSkippedBecause, StringComparison.Ordinal);
        }

        var refused = Assert.Throws<AcceleratorUnavailableException>(
            () => AcceleratorChoice.Decide(options with { Accelerator = AcceleratorKind.Cuda }, cudaForbidden: true));
        Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>The variable forbids cuda and auto falls back to the cpu.</summary>
    [Fact]
    public void TheVariableForbidsCudaAndAutoFallsBackToTheCpu()
    {
        var previous = Environment.GetEnvironmentVariable(EngineOptions.NoCudaVariable);
        try
        {
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, "1");
            Assert.True(Engine.CudaForbidden);
            using var auto = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Auto });
            Assert.Equal(AcceleratorKind.Cpu, auto.Accelerator.Kind);
            var refused = Assert.Throws<AcceleratorUnavailableException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda }));
            Assert.Contains(EngineOptions.NoCudaVariable, refused.Message, StringComparison.Ordinal);
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, "0");
            Assert.False(Engine.CudaForbidden);
        }
        finally
        {
            Environment.SetEnvironmentVariable(EngineOptions.NoCudaVariable, previous);
        }
    }

    /// <summary>No cuda driver is loaded in a process that forbids cuda.</summary>
    [Fact]
    public void NoCudaDriverIsLoadedInAProcessThatForbidsCuda()
    {
        if (!Engine.CudaForbidden)
        {
            return;   // the CUDA engine of this process may legitimately have loaded the driver
        }

        using var auto = Engine.Create();
        Assert.Equal(AcceleratorKind.Cpu, auto.Accelerator.Kind);
        var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(m => m.ModuleName).ToList();
        Assert.DoesNotContain(modules, name => name.Contains("nvcuda", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A CUDA engine runs with nothing of the CUDA Toolkit loaded (2026-10-05, item 13): the driver's own library is the only
    /// NVIDIA module of the process, and no libnvvm, no libdevice user and no toolkit runtime was ever needed to bind it,
    /// compile its kernels or run them.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void ACudaEngineLoadsNoLibraryOfTheCudaToolkit()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        _ = cuda.ProbeMath([1.0, 2.0]);
        var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Select(m => m.ModuleName).ToList();
        Assert.Contains(modules, name => name.Contains("nvcuda", StringComparison.OrdinalIgnoreCase) || name.StartsWith("libcuda", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(modules, name => name.Contains("nvvm", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(modules, name => name.Contains("cudart", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(modules, name => name.Contains("nvrtc", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The ilgpu assertion fails loudly for another version.</summary>
    [Fact]
    public void TheIlgpuAssertionFailsLoudlyForAnotherVersion()
    {
        var wrong = Assert.Throws<InvalidOperationException>(() => PtxPostLink.AssertIlgpu("9.9.9.0"));
        Assert.Contains("9.9.9.0", wrong.Message, StringComparison.Ordinal);
        Assert.Contains(PtxPostLink.ExpectedIlgpuVersion, wrong.Message, StringComparison.Ordinal);
        var assembly = PtxPostLink.AssertIlgpu(PtxPostLink.ExpectedIlgpuVersion);
        Assert.Equal("<PTXAssembly>k__BackingField", assembly.Name);
        Assert.Equal(typeof(string), assembly.FieldType);
    }

    /// <summary>Inconsistent batches are refused before any kernel runs.</summary>
    [Fact]
    public void InconsistentBatchesAreRefusedBeforeAnyKernelRuns()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var wrongElements = new RocketBatch(2, family.Table.ElementCount + 1, family.Inputs[0].Exits.Kinds);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongElements));
        var wrongSpecies = new TransportBatch(2, family.Table.SpeciesCount + 1);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongSpecies));
        var wrongEquilibrium = new EquilibriumBatch(2, family.Table.ElementCount + 1);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, wrongEquilibrium));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new RocketBatch(0, 2, []));

        using var other = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        _ = Assert.Throws<ArgumentException>(() => other.Run(tables, family.Batch()));
        using var withoutTransport = EngineFixture.Shared.Cpu.Upload(family.Table);
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(withoutTransport, new TransportBatch(1, family.Table.SpeciesCount)));
        var otherTable = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[1].Table;
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Upload(otherTable, family.Transport));
    }

    /// <summary>
    /// A seeded equilibrium batch (2026-10-04) whose seed does not fit the table is refused before any kernel runs: a seed stride of another
    /// species count, a NaN and an infinite value, the last two naming the case and the species; a negative seed value runs, as the host
    /// solver ignores it. Red once, 2026-10-04: with the seed checks removed from <c>EquilibriumBatch.Validate</c> every refusal is missing.
    /// </summary>
    [Fact]
    public void ASeededBatchWhoseSeedDoesNotFitTheTableIsRefusedBeforeAnyKernelRuns()
    {
        var (cold, table, _) = FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-lh2_of6_pc7MPa");
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        EquilibriumBatch Seeded(int stride)
        {
            var batch = new EquilibriumBatch(cold.Count, cold.ElementCount, stride);
            Array.Copy(cold.Kind, batch.Kind, cold.Count);
            Array.Copy(cold.Pressure, batch.Pressure, cold.Count);
            Array.Copy(cold.Temperature, batch.Temperature, cold.Count);
            Array.Copy(cold.Target, batch.Target, cold.Count);
            Array.Copy(cold.ElementMoles, batch.ElementMoles, cold.ElementMoles.Length);
            Array.Fill(batch.SeedMoles!, 0.1);
            return batch;
        }

        var stride = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, Seeded(table.SpeciesCount + 1)));
        Assert.Contains($"the batch seeds {table.SpeciesCount + 1} species per case, the table has {table.SpeciesCount}", stride.Message, StringComparison.Ordinal);

        var notANumber = Seeded(table.SpeciesCount);
        notANumber.SeedMoles![2 * table.SpeciesCount + 3] = double.NaN;
        var nan = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, notANumber));
        Assert.Contains("the seed of case 2, species 3, is NaN", nan.Message, StringComparison.Ordinal);

        var infinite = Seeded(table.SpeciesCount);
        infinite.SeedMoles![1 * table.SpeciesCount] = double.PositiveInfinity;
        var infinity = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, infinite));
        Assert.Contains("the seed of case 1, species 0, is", infinity.Message, StringComparison.Ordinal);

        var negative = Seeded(table.SpeciesCount);
        negative.SeedMoles![0] = -1.0;
        Assert.Equal(cold.Count, EngineFixture.Shared.Cpu.Run(tables, negative).Count);
    }

    /// <summary>
    /// A batch constructor refuses a count whose per-case array would overflow a 32-bit array length (BOOT.md, the
    /// second audit's observation 6), before attempting the allocation: plain <c>int</c> arithmetic wraps silently
    /// past <see cref="int.MaxValue"/> instead of throwing, so the checked computation must run first.
    /// </summary>
    [Fact]
    public void BatchConstructorsRefuseACountWhoseArrayOverflowsA32BitLength()
    {
        // 100 000 cases of 30 000 elements each is above int.MaxValue (~2.147e9): the array is never allocated.
        var equilibrium = Assert.Throws<ArgumentOutOfRangeException>(() => new EquilibriumBatch(100_000, 30_000));
        Assert.Contains("overflows a 32-bit array length", equilibrium.Message, StringComparison.Ordinal);

        var rocket = Assert.Throws<ArgumentOutOfRangeException>(() => new RocketBatch(100_000, 30_000, []));
        Assert.Contains("overflows a 32-bit array length", rocket.Message, StringComparison.Ordinal);

        var transport = Assert.Throws<ArgumentOutOfRangeException>(() => new TransportBatch(100_000, 30_000));
        Assert.Contains("overflows a 32-bit array length", transport.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bound the batch constructors call, <see cref="BatchLength.Of"/> (2026-09-30, "No test allocates what it measures"),
    /// is inclusive of <see cref="int.MaxValue"/> and refuses the next product, whichever factor carries the size: the check is on
    /// the product, not on either factor alone. Nothing is allocated. Red with the bound off by one either way.
    /// </summary>
    [Fact]
    public void TheBatchLengthBoundIsInclusiveOfTheLargestArrayLength()
    {
        Assert.Equal(int.MaxValue, BatchLength.Of(1, int.MaxValue));
        Assert.Equal(int.MaxValue - 1, BatchLength.Of(2, 1_073_741_823));
        Assert.Equal(int.MaxValue - 1, BatchLength.Of(1_073_741_823, 2));

        var justOver = Assert.Throws<ArgumentOutOfRangeException>(() => BatchLength.Of(2, 1_073_741_824));
        Assert.Contains("overflows a 32-bit array length", justOver.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BatchLength.Of(1_073_741_824, 2));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => BatchLength.Of(1, int.MaxValue + 1L));
    }

    /// <summary>
    /// A chunk buffer refuses to move a chunk the host array is too short for (BOOT.md, the second audit's
    /// observation 6), before the unsafe <c>ref</c> copy: <c>ArrayView&lt;T&gt;.CopyFromCPU</c>/<c>CopyToCPU</c> take
    /// a reference and a length with no bounds check of their own.
    /// </summary>
    [Fact]
    public void AChunkBufferRefusesAHostArrayShorterThanTheChunkNeeds()
    {
        using var buffers = new Chunks.ChunkBuffers(EngineFixture.Shared.Cpu.IlgpuAccelerator);
        var shortHost = new double[5];
        var input = buffers.Input(shortHost, perCase: 1);
        buffers.Allocate(10);
        var uploadFailure = Assert.Throws<ArgumentException>(() => input.UploadChunk(0, 10));
        Assert.Contains("the host array has 5 elements", uploadFailure.Message, StringComparison.Ordinal);

        using var downloadBuffers = new Chunks.ChunkBuffers(EngineFixture.Shared.Cpu.IlgpuAccelerator);
        var shortOutput = new double[5];
        var output = downloadBuffers.Output(shortOutput, perCase: 1);
        downloadBuffers.Allocate(10);
        var downloadFailure = Assert.Throws<ArgumentException>(() => output.DownloadChunk(0, 10));
        Assert.Contains("the host array has 5 elements", downloadFailure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The probe's output length, <see cref="MathProbe.OutputLength"/>, which <see cref="Engine.ProbeMath"/> calls (BOOT.md, the
    /// second audit's observation 7; 2026-09-30, "No test allocates what it measures"): <c>Kernels.Probe</c> strides its
    /// output by <see cref="MathProbe.FunctionCount"/> with 32-bit <c>Index1D</c> arithmetic, so the last count whose output
    /// fits is accepted and the next one refused. Asked of the count, without the 1.2 GB input array the engine's own
    /// refusal needs. Red with the bound off by one either way.
    /// </summary>
    [Fact]
    public void TheProbeOutputLengthBoundIsInclusiveOfTheLargestOffset()
    {
        var last = int.MaxValue / MathProbe.FunctionCount;
        Assert.Equal(last * MathProbe.FunctionCount, MathProbe.OutputLength(last));
        Assert.Equal(0, MathProbe.OutputLength(0));

        // One input more: index * FunctionCount then overflows.
        var failure = Assert.Throws<ArgumentException>(() => MathProbe.OutputLength(last + 1));
        Assert.Contains("overflows a 32-bit offset", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Chunks are bounded by the chunk size and the scratch memory.</summary>
    [Fact]
    public void ChunksAreBoundedByTheChunkSizeAndTheScratchMemory()
    {
        var small = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 10, ScratchBytes = 1000 };
        Assert.Equal(10, ChunkPlan.For(1000, 12, 1, small).Size);            // by chunk size
        Assert.Equal(5, ChunkPlan.For(1000, 200, 1, small).Size);            // by memory: 200 bytes per case against the 1000 allowed
        Assert.Equal(1, ChunkPlan.For(1000, 8000, 1, small).Size);           // never below one case
        Assert.Equal(3, ChunkPlan.For(3, 12, 1, small).Size);                // never above the count
        Assert.Equal([new Chunk(0, 10), new Chunk(10, 10), new Chunk(20, 5)], ChunkPlan.For(25, 12, 1, small).Chunks());
        _ = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 0 }));
        var refused = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = 0 }));
        Assert.Contains("scratch bound", refused.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ScratchBytes = -1 }));
    }

    /// <summary>
    /// A chunk's buffers stay within 32-bit offsets (BOOT.md, `Execution.Chunks`, the audit's F4): bytes alone bounded a chunk,
    /// so a table at the tree's own size limits with a large enough <see cref="EngineOptions.ScratchBytes"/> and
    /// <see cref="EngineOptions.ChunkSize"/> let <c>chunk × perCase</c> wrap a 32-bit <see cref="int"/> offset. 13 248 doubles
    /// per case is `Execution.Chunks`' own `BOOT.md` figure for a table at <c>TableLimits</c> (its worked example), not a value
    /// this fact derives from a neighbour's code.
    /// </summary>
    [Fact]
    public void ChunksStayWithinInt32OffsetsAtTableLimits()
    {
        const long perCase = 13_248;
        var huge = new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = int.MaxValue, ScratchBytes = 64L << 30 };
        var plan = ChunkPlan.For(1_000_000_000, perCase * sizeof(double), perCase, huge);
        Assert.True(plan.Size * perCase <= int.MaxValue, $"{plan.Size} * {perCase} overflows a 32-bit offset");

        // No other plan moves: the default options are far below the cap, so an ordinary chunk is unaffected by it.
        var ordinary = ChunkPlan.For(1_000_000, TableLimits.MaxSpecies * sizeof(double), TableLimits.MaxSpecies, new EngineOptions());
        Assert.Equal(EngineOptions.DefaultChunkSize, ordinary.Size);
    }

    /// <summary>Result layouts follow the station and species counts.</summary>
    [Fact]
    public void ResultLayoutsFollowTheStationAndSpeciesCounts()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var batch = family.Batch();
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        Assert.Equal(batch.Count, result.Count);
        Assert.Equal(RocketLayout.StationCount(batch.Exits), result.StationCount);
        Assert.Equal(batch.Count * result.StationCount, result.Stations.Length);
        Assert.Equal(batch.Count * result.StationCount, result.Figures.Length);
        Assert.Equal((long)batch.Count * result.StationCount * family.Table.SpeciesCount, result.Moles.LongLength);
        Assert.Equal(EngineFixture.Shared.Cpu.Accelerator, result.Accelerator);
        var transport = TransportBatch.FromRocket(result);
        Assert.Equal(result.Stations.Length, transport.Count);
        Assert.Same(result.Moles, transport.Moles);
        Assert.Equal(result.Stations.Select(s => s.Temperature), transport.Temperature);
        _ = Assert.IsType<TransportFigures[]>(EngineFixture.Shared.Cpu.Run(tables, transport).Figures);
    }

    /// <summary>
    /// The launch budget a bind actually builds (BOOT.md, "A launch fits a time budget"; the second audit's Execution
    /// finding F2): the CPU accelerator's is unbounded, and on the reference machine — a display GPU, its kernel
    /// run-time limit enabled under both Windows and WSL2 (the audit's own measurement) — the CUDA engine's is bounded.
    /// This is the one fact that reads <see cref="Engine.Budget"/> off a real bind rather than an injected
    /// <see cref="LaunchBudget"/>; every other launch-budget fact is in <c>LaunchBudgetTests</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void TheCpuAcceleratorsBudgetIsUnboundedAndTheReferenceDevicesIsBounded()
    {
        Assert.False(EngineFixture.Shared.Cpu.Budget.IsBounded);

        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        Assert.True(cuda.Budget.IsBounded);
    }
}
