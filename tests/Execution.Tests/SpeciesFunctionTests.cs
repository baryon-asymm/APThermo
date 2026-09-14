using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>L2 for the species-function batch: the CPU accelerator equals the host functions bit for bit, CUDA matches within the table.</summary>
[Collection(EngineCollection.Name)]
public sealed class SpeciesFunctionTests(EngineFixture fixture)
{
    /// <summary>Inside, at and beyond the interval bounds of the committed records.</summary>
    private static readonly double[] Temperatures = [150.0, 298.15, 1000.0, 1000.0007, 3500.0, 7000.0];

    private static SpeciesFunctionBatch BatchOf(SpeciesTable table)
    {
        var batch = new SpeciesFunctionBatch(table.SpeciesCount * Temperatures.Length);
        var i = 0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            foreach (var t in Temperatures)
            {
                batch.Species[i] = j;
                batch.Temperature[i] = t;
                i++;
            }
        }

        return batch;
    }

    [Fact]
    public void The_cpu_accelerator_equals_the_host_functions_bit_for_bit()
    {
        var checkedEntries = 0;
        foreach (var family in FixtureBatches.RocketFamilies(fixture.Database))
        {
            using var tables = fixture.Cpu.Upload(family.Table);
            var batch = BatchOf(family.Table);
            var result = fixture.Cpu.Run(tables, batch);
            Assert.Equal(batch.Count, result.Count);
            var view = tables.SpeciesBuffers.View;
            for (var i = 0; i < batch.Count; i++)
            {
                var j = batch.Species[i];
                var t = batch.Temperature[i];
                var label = $"{family.Table.Species[j]} at {t} K";
                Assert.True(Bits.Same(SpeciesFunctions.CpOverR(in view, j, t), result.CpOverR[i]), $"{label}: Cp/R");
                Assert.True(Bits.Same(SpeciesFunctions.HOverRT(in view, j, t), result.HOverRT[i]), $"{label}: H/RT");
                Assert.True(Bits.Same(SpeciesFunctions.SOverR(in view, j, t), result.SOverR[i]), $"{label}: S/R");
                Assert.Equal(SpeciesFunctions.IsInRange(in view, j, t), result.InRange[i]);
                checkedEntries++;
            }
        }

        Assert.True(checkedEntries > 1000, $"only {checkedEntries} entries checked");
    }

    [Fact]
    [Trait("Category", "Cuda")]
    public void Cuda_matches_the_cpu_accelerator_within_the_table()
    {
        var cuda = fixture.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var relative = GpuCpuTolerances.Entries["functions"].Relative;
        var worst = 0.0;
        var mismatches = new List<string>();
        foreach (var family in FixtureBatches.RocketFamilies(fixture.Database))
        {
            using var cpuTables = fixture.Cpu.Upload(family.Table);
            using var cudaTables = cuda.Upload(family.Table);
            var batch = BatchOf(family.Table);
            var cpu = fixture.Cpu.Run(cpuTables, batch);
            var gpu = cuda.Run(cudaTables, batch);
            for (var i = 0; i < batch.Count; i++)
            {
                var label = $"{family.Table.Species[batch.Species[i]]} at {batch.Temperature[i]} K";
                if (cpu.InRange[i] != gpu.InRange[i])
                {
                    mismatches.Add($"{label}: in range {cpu.InRange[i]} on the CPU, {gpu.InRange[i]} on CUDA");
                }

                foreach (var (name, a, b) in new[] { ("Cp/R", cpu.CpOverR[i], gpu.CpOverR[i]), ("H/RT", cpu.HOverRT[i], gpu.HOverRT[i]), ("S/R", cpu.SOverR[i], gpu.SOverR[i]) })
                {
                    // Dimensionless functions of order 1 to 100 that cancel to zero at a reference point: the bound is on max(1, |value|).
                    var deviation = Math.Abs(a - b) / Math.Max(1.0, Math.Abs(a));
                    worst = Math.Max(worst, deviation);
                    if (deviation > relative)
                    {
                        mismatches.Add($"{label} {name}: cpu {a:R}, cuda {b:R}");
                    }
                }
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(20)) + $"\nworst {worst:E1}");
    }

    [Fact]
    public void A_species_index_outside_the_table_is_refused_before_any_kernel_runs()
    {
        var family = FixtureBatches.RocketFamilies(fixture.Database)[0];
        using var tables = fixture.Cpu.Upload(family.Table);
        var batch = new SpeciesFunctionBatch(2);
        batch.Species[1] = family.Table.SpeciesCount;
        batch.Temperature[0] = batch.Temperature[1] = 1000.0;
        Assert.Throws<ArgumentException>(() => fixture.Cpu.Run(tables, batch));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpeciesFunctionBatch(0));
    }
}
