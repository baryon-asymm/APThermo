using System.Globalization;
using APThermo.Execution.Chunks;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>L2 and the benchmark on the reference machine: CUDA against the CPU accelerator within the table, determinism, throughput.</summary>
[Collection(EngineFixture.CollectionName)]
[TestCaseOrderer("APThermo.Execution.Tests.LastFactOrderer", "APThermo.Execution.Tests")]
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
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var mismatches = comparison.Rocket(cpu, gpu, family, batch);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
        AssertDifferentStepShare(comparison.DifferentSteps, cpu.Stations.Length);

        var transport = TransportBatch.FromRocket(cpu);
        var cpuTransport = EngineFixture.Shared.Cpu.Run(cpuTables, transport);
        var gpuTransport = cuda.Run(cudaTables, transport);
        var transportMismatches = new List<string>();
        for (var i = 0; i < transport.Count; i++)
        {
            if (cpuTransport.Status[i] != gpuTransport.Status[i])
            {
                transportMismatches.Add($"station {i}: status cpu {cpuTransport.Status[i]}, cuda {gpuTransport.Status[i]}");
            }

            transportMismatches.AddRange(GpuCpuTolerances.Compare(cpuTransport.Figures[i], gpuTransport.Figures[i], $"station {i}", comparison.Record));
        }

        Assert.True(transportMismatches.Count == 0, string.Join("\n", transportMismatches.Take(30)) + "\nworst: " + comparison.Worst());
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

        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances) { IterationsSumAttempts = bracketed };
        var sensitivities = BalanceSensitivities.Measure(EngineFixture.Shared.Cpu, cpuTables, batch, table);
        var mismatches = comparison.Equilibrium(cpu, gpu, batch, table, labels, sensitivities);
        if (bracketed)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"worst noGasPhaseTemperature deviation {comparison.WorstOf("noGasPhaseTemperature"):E2}, worst gas mole-fraction deviation {comparison.WorstOf("moleFraction"):E2}"));
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
        if (!bracketed)
        {
            AssertDifferentStepShare(comparison.DifferentSteps, batch.Count);
        }
    }

    /// <summary>The sweep of 100000 cases on cuda matches the cpu accelerator and is deterministic.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void TheSweepOf100000CasesOnCudaMatchesTheCpuAcceleratorAndIsDeterministic()
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
        var family = FixtureBatches.Family(EngineFixture.Shared.Database, SweepRun.FamilyName);
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var mismatches = comparison.Rocket(sweep.Cpu, sweep.Cuda, family, sweep.Batch);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n" + string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
        AssertDifferentStepShare(comparison.DifferentSteps, sweep.Cpu.Stations.Length);
        Assert.True(comparison.DifferentSteps > 0, "no station of the sweep stopped after different numbers of Newton steps; the second tier of the mole-fraction tolerance was not exercised");

        Assert.Equal(sweep.Cuda.Status, sweep.CudaAgain.Status);
        for (var i = 0; i < sweep.Cuda.Stations.Length; i++)
        {
            Assert.Empty(Bits.Differences(sweep.Cuda.Stations[i], sweep.CudaAgain.Stations[i], $"station {i}"));
            Assert.Empty(Bits.Differences(sweep.Cuda.Figures[i], sweep.CudaAgain.Figures[i], $"station {i}"));
        }

        for (long j = 0; j < sweep.Cuda.Moles.LongLength; j++)
        {
            Assert.True(Bits.Same(sweep.Cuda.Moles[j], sweep.CudaAgain.Moles[j]), $"moles differ at {j}");
        }

        Assert.True(sweep.Cpu.Status.All(s => s == CaseStatus.Ok), $"{sweep.Cpu.Status.Count(s => s != CaseStatus.Ok)} cases failed on the CPU accelerator");
    }

    /// <summary>Throughput is recorded and not below the approved ratio.</summary>
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
        var ratio = sweep.CpuSeconds.TotalSeconds / sweep.CudaSeconds.TotalSeconds;
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
            $"cuda_seconds: {sweep.CudaSeconds.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            $"cpu_seconds: {sweep.CpuSeconds.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            $"ratio: {ratio.ToString("F2", CultureInfo.InvariantCulture)}",
            $"cuda_kernel_seconds: {sweep.Cuda!.Timings.Kernel.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}",
            $"date: {DateTime.Now:yyyy-MM-dd}",
        };
        var approvedPath = ApprovedSnapshot.ApprovedPathFor(directory, "Throughput");
        var actualPath = Path.Combine(directory, Path.GetFileName(approvedPath).Replace("approved", "actual", StringComparison.Ordinal));
        File.WriteAllLines(actualPath, actualLines);

        Assert.True(File.Exists(approvedPath), $"no approved throughput file at {approvedPath}; the measured figures are in {actualPath}");
        var approved = File.ReadAllLines(approvedPath)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
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
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    /// <summary>
    /// The stations at which the accelerators stopped after different numbers of Newton steps are added to the run's ledger; one family is
    /// held only to the coarse guard of <see cref="StepShareLedger.Allowed"/>, and the table's share is held over the whole run by
    /// <see cref="TheStepShareOverTheWholeRun"/>.
    /// </summary>
    private static void AssertDifferentStepShare(int differentSteps, int stations)
    {
        EngineFixture.Shared.StepShare.Add(differentSteps, stations);
        var violation = StepShareLedger.CoarseViolation(differentSteps, stations);
        Assert.True(violation is null, violation);
    }

    /// <summary>
    /// The stations at which the accelerators stopped after different numbers of Newton steps stay a rare threshold flip over the whole
    /// run: at most the table's share (<see cref="GpuCpuTolerances.DifferentStepShare"/>) of every station of every family. Ordered last
    /// in the class by <see cref="LastFactOrderer"/>; a run of this fact alone fails, its ledger being empty.
    /// </summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void TheStepShareOverTheWholeRun()
    {
        if (EngineFixture.Shared.RequireCuda() is null)
        {
            return;
        }

        var ledger = EngineFixture.Shared.StepShare;
        var (different, stations) = (ledger.Different, ledger.Stations);
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{different} of {stations} stations differ (share {(stations == 0 ? 0.0 : (double)different / stations):E2}, bound {GpuCpuTolerances.DifferentStepShare.ToString("0e+0", CultureInfo.InvariantCulture)})"));
        var violation = ledger.RunViolation();
        Assert.True(violation is null, violation);
    }
}
