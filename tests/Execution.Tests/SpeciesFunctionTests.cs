using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>L2 for the species-function batch: the CPU accelerator equals the host functions bit for bit, CUDA matches within the table.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class SpeciesFunctionTests
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

    /// <summary>The cpu accelerator equals the host functions bit for bit.</summary>
    [Fact]
    public void TheCpuAcceleratorEqualsTheHostFunctionsBitForBit()
    {
        var checkedEntries = 0;
        foreach (var family in FixtureBatches.RocketFamilies(EngineFixture.Shared.Database))
        {
            using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
            var batch = BatchOf(family.Table);
            var result = EngineFixture.Shared.Cpu.Run(tables, batch);
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

    /// <summary>Cuda matches the cpu accelerator within the table.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void CudaMatchesTheCpuAcceleratorWithinTheTable()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var relative = GpuCpuTolerances.Entries["functions"].Relative;
        var worst = 0.0;
        var mismatches = new List<string>();
        foreach (var family in FixtureBatches.RocketFamilies(EngineFixture.Shared.Database))
        {
            using var cpuTables = EngineFixture.Shared.Cpu.Upload(family.Table);
            using var cudaTables = cuda.Upload(family.Table);
            var batch = BatchOf(family.Table);
            var cpu = EngineFixture.Shared.Cpu.Run(cpuTables, batch);
            var gpu = cuda.Run(cudaTables, batch);
            for (var i = 0; i < batch.Count; i++)
            {
                var label = $"{family.Table.Species[batch.Species[i]]} at {batch.Temperature[i]} K";
                if (cpu.InRange[i] != gpu.InRange[i])
                {
                    mismatches.Add($"{label}: in range {cpu.InRange[i]} on the CPU, {gpu.InRange[i]} on CUDA");
                }

                var functions = new[] { ("Cp/R", cpu.CpOverR[i], gpu.CpOverR[i]), ("H/RT", cpu.HOverRT[i], gpu.HOverRT[i]), ("S/R", cpu.SOverR[i], gpu.SOverR[i]) };
                var (functionMismatches, sampleWorst) = CompareFunctions(relative, label, functions);
                mismatches.AddRange(functionMismatches);
                worst = Math.Max(worst, sampleWorst);
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches.Take(20)) + $"\nworst {worst:E1}");
    }

    /// <summary>CUDA against the CPU accelerator for the three dimensionless species functions of one species-temperature sample.
    /// NaN-aware (2026-09-28, the guards audit's O2): a plain <c>deviation &gt; relative</c> is false for a NaN deviation, which
    /// would let a value that is NaN on one accelerator and not the other pass silently, the way <see cref="GpuCpuTolerances.UlpDistance"/>
    /// and the probe's own comparison already guard against for their own fields.</summary>
    internal static (List<string> Mismatches, double Worst) CompareFunctions(double relative, string label, (string Name, double Cpu, double Gpu)[] functions)
    {
        var mismatches = new List<string>();
        var worst = 0.0;
        foreach (var (name, a, b) in functions)
        {
            if (double.IsNaN(a) && double.IsNaN(b))
            {
                continue;
            }

            if (double.IsNaN(a) || double.IsNaN(b))
            {
                mismatches.Add($"{label} {name}: cpu {a:R}, cuda {b:R}");
                worst = double.PositiveInfinity;
                continue;
            }

            // Dimensionless functions of order 1 to 100 that cancel to zero at a reference point: the bound is on max(1, |value|).
            var deviation = Math.Abs(a - b) / Math.Max(1.0, Math.Abs(a));
            worst = Math.Max(worst, deviation);
            if (deviation > relative)
            {
                mismatches.Add($"{label} {name}: cpu {a:R}, cuda {b:R}");
            }
        }

        return (mismatches, worst);
    }

    /// <summary>
    /// The comparison is NaN-aware (2026-09-28, the guards audit's O2), proven non-degenerate directly against
    /// <see cref="CompareFunctions"/> with hand-built values, without a device: a value that is NaN on one accelerator
    /// and an ordinary number on the other is a mismatch, two NaNs on both sides are not, and an ordinary deviation past
    /// the tolerance is still caught. Before this fix <c>deviation &gt; relative</c> was false for a NaN deviation, so the
    /// first case here would have passed silently.
    /// </summary>
    [Fact]
    public void TheComparisonIsNaNAwareAndCatchesAMismatchOnlyOneSideMakesNaN()
    {
        var (oneSidedNaN, worst1) = CompareFunctions(1e-10, "label", [("Cp/R", double.NaN, 5.0)]);
        _ = Assert.Single(oneSidedNaN);
        Assert.Equal(double.PositiveInfinity, worst1);

        var (bothNaN, worst2) = CompareFunctions(1e-10, "label", [("Cp/R", double.NaN, double.NaN)]);
        Assert.Empty(bothNaN);
        Assert.Equal(0.0, worst2);

        var (ordinary, worst3) = CompareFunctions(1e-10, "label", [("Cp/R", 1.0, 1.0 + 1e-5)]);
        _ = Assert.Single(ordinary);
        Assert.True(worst3 > 1e-10);
    }

    /// <summary>A species index outside the table is refused before any kernel runs.</summary>
    [Fact]
    public void ASpeciesIndexOutsideTheTableIsRefusedBeforeAnyKernelRuns()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var batch = new SpeciesFunctionBatch(2);
        batch.Species[1] = family.Table.SpeciesCount;
        batch.Temperature[0] = batch.Temperature[1] = 1000.0;
        _ = Assert.Throws<ArgumentException>(() => EngineFixture.Shared.Cpu.Run(tables, batch));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SpeciesFunctionBatch(0));
    }
}
