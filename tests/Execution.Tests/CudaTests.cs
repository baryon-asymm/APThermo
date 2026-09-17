using System.Globalization;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>L2 and the benchmark on the reference machine: CUDA against the CPU accelerator within the table, determinism, throughput.</summary>
[Collection(EngineCollection.Name)]
public sealed class CudaTests(EngineFixture fixture)
{
    public static IEnumerable<object[]> Families() => FixtureBatches.FamilyNames(EngineFixture.SharedDatabase);

    [Theory]
    [MemberData(nameof(Families))]
    [Trait("Category", "Cuda")]
    public void A_rocket_family_on_cuda_matches_the_cpu_accelerator(string name)
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var family = FixtureBatches.Family(fixture.Database, name);
        var batch = family.Batch();
        using var cpuTables = fixture.Cpu.Upload(family.Table, family.Transport);
        using var cudaTables = cuda.Upload(family.Table, family.Transport);
        var cpu = fixture.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        var comparison = new GpuCpuComparison(fixture.Tolerances);
        var mismatches = comparison.Rocket(cpu, gpu, family);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + comparison.Worst());
        AssertDifferentStepShare(comparison.DifferentSteps, cpu.Stations.Length);

        var transport = TransportBatch.FromRocket(cpu);
        var cpuTransport = fixture.Cpu.Run(cpuTables, transport);
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

    [Fact]
    [Trait("Category", "Cuda")]
    public void An_equilibrium_family_on_cuda_matches_the_cpu_accelerator()
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var (batch, table, cases) = FixtureBatches.EquilibriumFamily(fixture.Database, "lox-rp1_of2.6_pc10MPa");
        using var cpuTables = fixture.Cpu.Upload(table);
        using var cudaTables = cuda.Upload(table);
        var cpu = fixture.Cpu.Run(cpuTables, batch);
        var gpu = cuda.Run(cudaTables, batch);
        var comparison = new GpuCpuComparison(fixture.Tolerances);
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

    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void The_sweep_of_100000_cases_on_cuda_matches_the_cpu_accelerator_and_is_deterministic()
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var sweep = fixture.Sweep;
        Assert.NotNull(sweep.Cuda);
        Assert.NotNull(sweep.CudaAgain);
        Assert.Equal(SweepRun.LongRunningCases, sweep.Batch.Count);
        var family = FixtureBatches.Family(fixture.Database, SweepRun.FamilyName);
        var comparison = new GpuCpuComparison(fixture.Tolerances);
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

    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("Category", "LongRunning")]
    public void Throughput_is_recorded_and_not_below_the_approved_ratio()
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var sweep = fixture.Sweep;
        var ratio = sweep.CpuSeconds.TotalSeconds / sweep.CudaSeconds.TotalSeconds;
        var directory = Path.GetDirectoryName(ThisFile())!;
        var actualLines = new[]
        {
            $"device: {cuda.Accelerator.DeviceName}",
            $"ilgpu: {cuda.Accelerator.IlgpuVersion}",
            $"cpu: {fixture.Cpu.Accelerator.DeviceName} with {fixture.Cpu.Accelerator.ThreadsOrMultiprocessors} threads",
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
