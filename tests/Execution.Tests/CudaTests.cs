using System.Globalization;
using APThermo.Execution.Chunks;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>L2 and the benchmark on the reference machine: CUDA against the CPU accelerator bit for bit, determinism, throughput.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CudaTests(ITestOutputHelper output)
{
    /// <summary>The most cases of the launch-budget fact: one wave of the device where it is smaller, so that the fact never launches more than the engine's own first chunk would.</summary>
    private const int LaunchBudgetCases = 16_384;

    /// <summary>The rocket family names as theory data, delegating to <see cref="FixtureBatches.FamilyNames"/>.</summary>
    public static TheoryData<string> Families() => FixtureBatches.FamilyNames(EngineFixture.SharedDatabase);

    /// <summary>The throat family names as theory data, delegating to <see cref="FixtureBatches.FamilyNames"/>.</summary>
    public static TheoryData<string> ThroatFamilies() => FixtureBatches.FamilyNames(EngineFixture.SharedDatabase, FixtureBatches.ThroatKind);

    /// <summary>The names of the 0.2.1 equilibrium families as theory data, delegating to <see cref="FixtureBatches.NamedEquilibriumFamilyNames"/>.</summary>
    public static TheoryData<string> NamedEquilibriumFamilies() => FixtureBatches.NamedEquilibriumFamilyNames();

    /// <summary>The names of the 0.2.2 gas-plateau families as theory data, delegating to <see cref="GasPlateauFamilies.Names"/>.</summary>
    public static TheoryData<string> GasPlateauFamilyNames() => GasPlateauFamilies.Names();

    /// <summary>The names of the 0.2.2 families of the gasless verdict and the temperature bracket as theory data, delegating to <see cref="RecoveryFamilies.Names"/>.</summary>
    public static TheoryData<string> BracketedFamilyNames() => RecoveryFamilies.Names();

    /// <summary>The names of the seeded families (2026-10-04) as theory data, delegating to <see cref="SeededFamilies.Names"/>.</summary>
    public static TheoryData<string> SeededFamilyNames() => SeededFamilies.Names();

    /// <summary>The names of the families of every equilibrium fixture table (2026-10-04) as theory data, delegating to <see cref="FixtureBatches.EquilibriumTableFamilyNames"/>.</summary>
    public static TheoryData<string> EquilibriumTableFamilyNames() => FixtureBatches.EquilibriumTableFamilyNames();

    /// <summary>A rocket family on cuda matches the cpu accelerator.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    [Trait("Category", "Cuda")]
    public void ARocketFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is not null)
        {
            AssertFamilyMatches(cuda, FixtureBatches.Family(EngineFixture.Shared.Database, name));
        }
    }

    /// <summary>A throat family on cuda matches the cpu accelerator: chambers and throats on phase plateaus, no exit stations.</summary>
    [Theory]
    [MemberData(nameof(ThroatFamilies))]
    [Trait("Category", "Cuda")]
    public void AThroatFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is not null)
        {
            AssertFamilyMatches(cuda, FixtureBatches.Family(EngineFixture.Shared.Database, name, FixtureBatches.ThroatKind));
        }
    }

    private static void AssertFamilyMatches(Engine cuda, RocketFamily family)
    {
        var batch = family.Batch();
        using var cpuTables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        using var cudaTables = cuda.Upload(family.Table, family.Transport);
        var cpu = EngineFixture.Shared.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        var mismatches = ExactComparison.Rocket(cpu, gpu, k => k < family.Members.Count ? family.Members[k] : $"case {k}");
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));

        var transport = TransportBatch.FromRocket(cpu);
        var transportMismatches = ExactComparison.Transport(EngineFixture.Shared.Cpu.Run(cpuTables, transport), cuda.Run(cudaTables, transport));
        Assert.True(transportMismatches.Count == 0, string.Join("\n", transportMismatches));
    }

    /// <summary>An equilibrium family on cuda matches the cpu accelerator.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void AnEquilibriumFamilyOnCudaMatchesTheCpuAccelerator()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        AssertEquilibriumFamilyMatches(cuda, FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-rp1_of2.6_pc10MPa"));
    }

    /// <summary>An equilibrium family of the 0.2.1 fixtures (three-element, threshold-flip and gas-column salts) on cuda matches the cpu accelerator.</summary>
    [Theory]
    [MemberData(nameof(NamedEquilibriumFamilies))]
    [Trait("Category", "Cuda")]
    public void ANamedEquilibriumFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        AssertEquilibriumFamilyMatches(cuda, FixtureBatches.NamedEquilibriumFamily(EngineFixture.Shared.Database, name));
    }

    /// <summary>The family of every equilibrium fixture sharing one table (every table of the tp, hp and sp fixtures) on cuda matches the cpu accelerator.</summary>
    [Theory]
    [MemberData(nameof(EquilibriumTableFamilyNames))]
    [Trait("Category", "Cuda")]
    public void AnEquilibriumTableFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        AssertEquilibriumFamilyMatches(cuda, FixtureBatches.EquilibriumTableFamily(EngineFixture.Shared.Database, name));
    }

    /// <summary>
    /// A family seeded by moles (2026-10-04, <see cref="SeededFamilies"/>) on cuda matches the cpu accelerator, both handed the same seeds:
    /// the <c>seeded</c> fixtures, tp warm starts at half pressure, and bracketed plateau states seeded above the plateau. The bracketed
    /// states sum the attempts of the temperature bracket into <c>Iterations</c> and are compared by the rule of that case.
    /// </summary>
    [Theory]
    [MemberData(nameof(SeededFamilyNames))]
    [Trait("Category", "Cuda")]
    public void ASeededFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = SeededFamilies.Family(EngineFixture.Shared.Database, name);
        Assert.True(family.Batch.IsSeeded);
        AssertEquilibriumFamilyMatches(cuda, family.Batch, family.Table, family.Labels, bracketed: family.SumsAttempts);
    }

    /// <summary>
    /// A gas-participating plateau family (the 0.2.2 families, <see cref="GasPlateauFamilies"/>) on cuda matches the cpu accelerator: states
    /// on the plateaus of boiling water, ammonium chloride, calcium hydroxide and calcium carbonate, whose <c>γ_s</c> comes from the
    /// isentropic system, inputs computed by the tree itself.
    /// </summary>
    [Theory]
    [MemberData(nameof(GasPlateauFamilyNames))]
    [Trait("Category", "Cuda")]
    public void AGasPlateauFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = GasPlateauFamilies.Family(EngineFixture.Shared.Database, name);
        AssertEquilibriumFamilyMatches(cuda, family.Batch, family.Table, family.Labels, bracketed: false);
    }

    /// <summary>
    /// A family of the 0.2.2 gasless verdict and temperature bracket (<see cref="RecoveryFamilies"/>) on cuda matches the cpu accelerator:
    /// equal statuses, <c>Ok</c> cases within the table, <c>NoGasPhase</c> cases with their moles within the table, their temperature and
    /// pressure exact where they are inputs and every other state field zero. The families keep the cases the CPU accelerator ends
    /// <c>Ok</c> or <c>NoGasPhase</c> as each stands for; the cases it ended otherwise are left out and counted in
    /// <see cref="BracketedFamily.Dropped"/>, which the CPU fact <c>BracketedFamiliesTests</c> reports.
    /// </summary>
    [Theory]
    [MemberData(nameof(BracketedFamilyNames))]
    [Trait("Category", "Cuda")]
    public void ABracketedFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, name);
        output.WriteLine($"{name}: {family.Batch.Count} cases, {family.Gasless} gasless, {family.Dropped} dropped");
        AssertEquilibriumFamilyMatches(cuda, family.Batch, family.Table, family.Labels, bracketed: true);
    }

    /// <summary>
    /// One launch of cases that all bracket (the hp and sp states of the gasless KO2 family, repeated) stays within the launch budget
    /// (Execution.Tests BOOT.md; the quarter of the default run-time limit, which <c>LaunchBudget</c> sizes later chunks from): the
    /// engine's first chunk is one wave of the device, so a family that all brackets must fit it, and the measured kernel time of the
    /// launch is below the budget. Every case ends <c>NoGasPhase</c> on CUDA; the figure is written to the test output.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void AFamilyOfCasesThatAllBracketStaysWithinTheLaunchBudget()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        var count = Math.Min(LaunchBudgetCases, cuda.IlgpuAccelerator.MaxNumThreads);
        var batch = RecoveryFamilies.Tiled(RecoveryFamilies.WithoutTp(family.Batch), count);
        using var tables = cuda.Upload(family.Table);
        _ = cuda.Run(tables, batch);
        var run = cuda.Run(tables, batch);
        var limit = LaunchBudget.DefaultRunTimeLimit / 4;
        output.WriteLine($"{count} bracketing cases in one launch: kernel {run.Timings.Kernel.TotalMilliseconds:F1} ms, the budget {limit.TotalMilliseconds:F0} ms; {run.Iterations.Average():F0} Newton steps per case on average");
        var violation = RecoveryFamilies.LaunchViolation(run, limit);
        Assert.True(violation is null, violation);
    }

    private void AssertEquilibriumFamilyMatches(Engine cuda, (EquilibriumBatch Batch, SpeciesTable Table, IReadOnlyList<CeaCase> Cases) family) =>
        AssertEquilibriumFamilyMatches(cuda, family.Batch, family.Table, [.. family.Cases.Select(c => c.Name)], bracketed: false);

    private void AssertEquilibriumFamilyMatches(Engine cuda, EquilibriumBatch batch, SpeciesTable table, IReadOnlyList<string> labels, bool bracketed)
    {
        using var cpuTables = EngineFixture.Shared.Cpu.Upload(table);
        using var cudaTables = cuda.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        if (!bracketed)
        {
            Assert.All(cpu.Status, status => Assert.Equal(CaseStatus.Ok, status));
        }

        var mismatches = ExactComparison.Equilibrium(cpu, gpu, k => labels[k]);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        output.WriteLine($"{batch.Count} cases, {cpu.Iterations.Sum(i => (long)i)} Newton steps, every field equal bit for bit");
    }

    /// <summary>The sweep of 100000 cases on cuda equals the cpu accelerator bit for bit and is deterministic.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void TheSweepOf100000CasesOnCudaEqualsTheCpuAcceleratorBitForBitAndIsDeterministic()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var sweep = EngineFixture.Shared.Sweep;
        Assert.NotNull(sweep.Cuda);
        Assert.NotNull(sweep.CudaAgain);
        Assert.Equal(SweepRun.LongRunningCases, sweep.Batch.Count);
        var mismatches = ExactComparison.Rocket(sweep.Cpu, sweep.Cuda, k => $"case {k}");
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        var repeated = ExactComparison.Rocket(sweep.Cuda, sweep.CudaAgain, k => $"case {k}");
        Assert.True(repeated.Count == 0, "two CUDA runs differ:\n" + string.Join("\n", repeated));
        Assert.True(sweep.Cpu.Status.All(s => s == CaseStatus.Ok), $"{sweep.Cpu.Status.Count(s => s != CaseStatus.Ok)} cases failed on the CPU accelerator");
        output.WriteLine($"{sweep.Cpu.Stations.Length} stations, {sweep.Cpu.Iterations.Sum(i => (long)i)} Newton steps, every field equal bit for bit");
    }

    /// <summary>Throughput is recorded, the CUDA/CPU ratio is not below 80 % of the approved one and the kernel time per Newton step is not above 115 % of the approved figure.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void ThroughputIsRecordedAndNotBelowTheApprovedRatio()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var sweep = EngineFixture.Shared.Sweep;
        var ratio = sweep.CpuSeconds.TotalSeconds / sweep.CudaTime.Elapsed.TotalSeconds;
        var perIteration = ThroughputRecord.KernelSecondsPerIteration(sweep.CudaTime.Kernel, sweep.Cuda!);
        var perRun = sweep.CudaTime.KernelRuns.Select(kernel => ThroughputRecord.KernelSecondsPerIteration(kernel, sweep.Cuda!));
        output.WriteLine("CUDA kernel seconds per Newton step of the timed runs: "
            + string.Join(", ", perRun.Select(figure => figure.ToString("0.000e+00", CultureInfo.InvariantCulture)))
            + $"; the median {perIteration.ToString("0.000e+00", CultureInfo.InvariantCulture)} is the figure compared");
        var directory = Path.GetDirectoryName(ThisFile())!;
        var actualLines = new[]
        {
            $"configuration: {BuildConfiguration.Current}",
            $"device: {cuda.Accelerator.DeviceName}",
            $"ilgpu: {cuda.Accelerator.IlgpuVersion}",
            $"cpu: {EngineFixture.Shared.Cpu.Accelerator.DeviceName} with {EngineFixture.Shared.Cpu.Accelerator.ThreadsOrMultiprocessors} threads",
            $"cases: {sweep.Batch.Count}",
            $"stations: {sweep.Cpu.StationCount}",
            $"species: {sweep.Cpu.SpeciesCount}",
            $"cuda_seconds: {sweep.CudaTime.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            $"cpu_seconds: {sweep.CpuSeconds.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            $"ratio: {ratio.ToString("F2", CultureInfo.InvariantCulture)}",
            $"cuda_kernel_seconds: {sweep.CudaTime.Kernel.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            ThroughputRecord.Line(ThroughputRecord.IterationsPerCaseKey, ThroughputRecord.IterationsPerCase(sweep.Cuda!)),
            ThroughputRecord.Line(ThroughputRecord.PerIterationKey, perIteration),
            $"date: {DateTime.Now:yyyy-MM-dd}",
        };
        var approvedPath = ApprovedSnapshot.ApprovedPathFor(directory, "Throughput");
        var actualPath = Path.Combine(directory, Path.GetFileName(approvedPath).Replace("approved", "actual", StringComparison.Ordinal));
        File.WriteAllLines(actualPath, actualLines);

        Assert.True(File.Exists(approvedPath), $"no approved throughput file at {approvedPath}; the measured figures are in {actualPath}");
        var approved = ThroughputRecord.Parse(File.ReadAllLines(approvedPath));
        var approvedConfiguration = approved.GetValueOrDefault("configuration");
        Assert.True(approvedConfiguration is not null,
            $"{Path.GetFileName(approvedPath)} carries no configuration: line; re-approve it from a Release run (BOOT.md)");
        Assert.True(string.Equals(approvedConfiguration, BuildConfiguration.Current, StringComparison.Ordinal),
            $"this run is {BuildConfiguration.Current}, but {Path.GetFileName(approvedPath)} was measured in {approvedConfiguration}; " +
            "the CPU accelerator executes the kernels from the assemblies' IL, so its speed depends on the build " +
            $"configuration (BOOT.md); run in {approvedConfiguration} to compare against it");
        var approvedRatio = double.Parse(approved["ratio"], CultureInfo.InvariantCulture);
        Assert.True(ratio >= 5.0, $"CUDA is only {ratio:F2}x faster than the CPU accelerator (root criterion: at least 5x); see {actualPath}");
        Assert.True(ratio >= 0.8 * approvedRatio,
            $"CUDA/CPU ratio {ratio:F2} fell below 80 % of the approved {approvedRatio:F2} ({Path.GetFileName(approvedPath)})");
        var violation = ThroughputRecord.Violation(approved, Path.GetFileName(approvedPath), perIteration);
        Assert.True(violation is null, violation);
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;
}
