using System.Globalization;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>One case's or station's moles on each accelerator, located within the batch's parallel arrays by its table and index.</summary>
internal sealed record MoleSample(double[] CpuMoles, double[] GpuMoles, long Index, SpeciesTable Table);

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
        var worst = new Dictionary<string, double>(StringComparer.Ordinal);
        var differentSteps = 0;
        var mismatches = CompareRocket(cpu, gpu, family, fixture.Tolerances, worst, ref differentSteps);
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + Worst(worst));
        AssertDifferentStepShare(differentSteps, cpu.Stations.Length);

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

            transportMismatches.AddRange(GpuCpuTolerances.Compare(cpuTransport.Figures[i], gpuTransport.Figures[i], $"station {i}", (f, d) => Record(worst, f, d)));
        }

        Assert.True(transportMismatches.Count == 0, string.Join("\n", transportMismatches.Take(30)) + "\nworst: " + Worst(worst));
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
        var worst = new Dictionary<string, double>(StringComparer.Ordinal);
        var mismatches = new List<string>();
        var differentSteps = 0;
        for (var k = 0; k < batch.Count; k++)
        {
            Assert.Equal(CaseStatus.Ok, cpu.Status[k]);
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{cases[k].Name}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            var sameSteps = cpu.Iterations[k] == gpu.Iterations[k];
            if (!sameSteps)
            {
                differentSteps++;
            }

            mismatches.AddRange(GpuCpuTolerances.Compare(cpu.State[k], gpu.State[k], cases[k].Name, (f, d) => Record(worst, f, d)));
            mismatches.AddRange(CompareMoles(new MoleSample(cpu.Moles, gpu.Moles, k, table), fixture.Tolerances, sameSteps, cases[k].Name, worst));
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(30)) + "\nworst: " + Worst(worst));
        AssertDifferentStepShare(differentSteps, batch.Count);
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
        var worst = new Dictionary<string, double>(StringComparer.Ordinal);
        var differentSteps = 0;
        var mismatches = CompareRocket(sweep.Cpu, sweep.Cuda, family, fixture.Tolerances, worst, ref differentSteps);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches:\n" + string.Join("\n", mismatches.Take(30)) + "\nworst: " + Worst(worst));
        AssertDifferentStepShare(differentSteps, sweep.Cpu.Stations.Length);
        Assert.True(differentSteps > 0, "no station of the sweep stopped after different numbers of Newton steps; the second tier of the mole-fraction tolerance was not exercised");

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
        File.WriteAllLines(Path.Combine(directory, "Throughput.actual.txt"), actualLines);

        var approvedPath = Path.Combine(directory, "Throughput.approved.txt");
        Assert.True(File.Exists(approvedPath), $"no approved throughput file at {approvedPath}; the measured figures are in Throughput.actual.txt");
        var approved = File.ReadAllLines(approvedPath)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
        var approvedRatio = double.Parse(approved["ratio"], CultureInfo.InvariantCulture);
        Assert.True(ratio >= 5.0, $"CUDA is only {ratio:F2}x faster than the CPU accelerator (root criterion: at least 5x); see Throughput.actual.txt");
        Assert.True(ratio >= 0.8 * approvedRatio, $"CUDA/CPU ratio {ratio:F2} fell below 80 % of the approved {approvedRatio:F2}");
    }

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    /// <summary>The stations at which the accelerators stopped after different numbers of Newton steps must stay a rare threshold flip.</summary>
    private static void AssertDifferentStepShare(int differentSteps, int stations) =>
        Assert.True(differentSteps <= GpuCpuTolerances.DifferentStepShare * stations,
                    $"{differentSteps} of {stations} stations stopped after different numbers of Newton steps on CUDA and on the CPU accelerator");

    private static List<string> CompareRocket(RocketBatchResult cpu, RocketBatchResult gpu, RocketFamily family, ToleranceTable tolerances,
                                              Dictionary<string, double> worst, ref int differentSteps)
    {
        var mismatches = new List<string>();
        var stationCount = cpu.StationCount;
        for (var k = 0; k < cpu.Count; k++)
        {
            var label = k < family.Members.Count && cpu.Count == family.Members.Count ? family.Members[k] : $"case {k}";
            if (cpu.Status[k] != gpu.Status[k])
            {
                mismatches.Add($"{label}: status cpu {cpu.Status[k]}, cuda {gpu.Status[k]}");
                continue;
            }

            for (var s = 0; s < stationCount; s++)
            {
                var index = k * stationCount + s;
                if (cpu.StationStatus[index] != gpu.StationStatus[index])
                {
                    mismatches.Add($"{label} station {s}: status cpu {cpu.StationStatus[index]}, cuda {gpu.StationStatus[index]}");
                    continue;
                }

                if (cpu.StationStatus[index] != CaseStatus.Ok)
                {
                    continue;
                }

                var sameSteps = cpu.Iterations[index] == gpu.Iterations[index];
                if (!sameSteps)
                {
                    differentSteps++;
                }

                var where = $"{label} station {s} ({cpu.Iterations[index]}/{gpu.Iterations[index]} steps)";
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Stations[index], gpu.Stations[index], where, (f, d) => Record(worst, f, d)));
                mismatches.AddRange(GpuCpuTolerances.Compare(cpu.Figures[index], gpu.Figures[index], where, (f, d) => Record(worst, f, d)));
                mismatches.AddRange(CompareMoles(new MoleSample(cpu.Moles, gpu.Moles, index, family.Table), tolerances, sameSteps, where, worst));
            }
        }

        return mismatches;
    }

    /// <summary>Mole fractions of one case or station, relative to the total moles, within the tier of the mole-fraction tolerance above the floor.</summary>
    private static IEnumerable<string> CompareMoles(MoleSample sample, ToleranceTable tolerances, bool sameSteps, string label, Dictionary<string, double> worst)
    {
        var speciesCount = sample.Table.SpeciesCount;
        var offset = sample.Index * speciesCount;
        var cpuTotal = 0.0;
        var gpuTotal = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            cpuTotal += sample.CpuMoles[offset + j];
            gpuTotal += sample.GpuMoles[offset + j];
        }

        var relative = GpuCpuTolerances.MoleFractionRelative(tolerances, sameSteps);
        var floor = GpuCpuTolerances.MoleFractionFloor(tolerances);
        for (var j = 0; j < speciesCount; j++)
        {
            var x = sample.CpuMoles[offset + j] / cpuTotal;
            var y = sample.GpuMoles[offset + j] / gpuTotal;
            if (x < floor && y < floor)
            {
                continue;
            }

            Record(worst, sameSteps ? "moleFraction" : "moleFractionAfterDifferentSteps", Math.Abs(x - y) / Math.Max(x, y));
            if (!GpuCpuTolerances.Matches(relative, x, y))
            {
                yield return $"{label} x({sample.Table.Species[j]}): cpu {x:R}, cuda {y:R}";
            }
        }
    }

    private static void Record(Dictionary<string, double> worst, string field, double deviation)
    {
        if (!worst.TryGetValue(field, out var current) || deviation > current)
        {
            worst[field] = deviation;
        }
    }

    private static string Worst(Dictionary<string, double> worst) =>
        string.Join(", ", worst.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} {kv.Value:E1}"));
}
