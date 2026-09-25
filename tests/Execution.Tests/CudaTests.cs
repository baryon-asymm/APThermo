using System.Globalization;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>L2 and the benchmark on the reference machine: CUDA against the CPU accelerator within the table, determinism, throughput.</summary>
public sealed class CudaTests
{
    /// <summary>The rocket family names as theory data, delegating to <see cref="FixtureBatches.FamilyNames"/>.</summary>
    public static TheoryData<string> Families() => FixtureBatches.FamilyNames(EngineFixture.SharedDatabase);

    /// <summary>A rocket family on cuda matches the cpu accelerator.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    [Trait("Category", "Cuda")]
    public void ARocketFamilyOnCudaMatchesTheCpuAccelerator(string name)
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = FixtureBatches.Family(EngineFixture.Shared.Database, name);
        var batch = family.Batch();
        using var cpuTables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        using var cudaTables = cuda.Upload(family.Table, family.Transport);
        var cpu = EngineFixture.Shared.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var mismatches = comparison.Rocket(cpu, gpu, family);
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

        var (batch, table, cases) = FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-rp1_of2.6_pc10MPa");
        using var cpuTables = EngineFixture.Shared.Cpu.Upload(table);
        using var cudaTables = cuda.Upload(table);
        var cpu = EngineFixture.Shared.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        var comparison = new GpuCpuComparison(EngineFixture.Shared.Tolerances);
        var mismatches = new List<string>();
        for (var k = 0; k < batch.Count; k++)
        {
            Assert.Equal(CaseStatus.Ok, cpu.Status[k]);
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{cases[k].Name}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            var sameSteps = cpu.Iterations[k] == gpu.Iterations[k];
            comparison.CountSteps(sameSteps);

            mismatches.AddRange(GpuCpuTolerances.Compare(cpu.State[k], gpu.State[k], cases[k].Name, comparison.Record));
            mismatches.AddRange(comparison.Moles(cpu.Moles, gpu.Moles, k, table, sameSteps, cases[k].Name));
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
        AssertDifferentStepShare(comparison.DifferentSteps, batch.Count);
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
        var mismatches = comparison.Rocket(sweep.Cpu, sweep.Cuda, family);
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

    /// <summary>The stations at which the accelerators stopped after different numbers of Newton steps must stay a rare threshold flip.</summary>
    private static void AssertDifferentStepShare(int differentSteps, int stations) =>
        Assert.True(differentSteps <= GpuCpuTolerances.DifferentStepShare * stations,
                    $"{differentSteps} of {stations} stations stopped after different numbers of Newton steps on CUDA and on the CPU accelerator");
}
