using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>L2 for the species-function batch: the CPU accelerator equals the host functions bit for bit, CUDA equals it bit for bit.</summary>
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

    /// <summary>Cuda equals the cpu accelerator bit for bit: the three functions and the range flag of every species of every family at every temperature.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    public void CudaEqualsTheCpuAcceleratorBitForBit()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            return;
        }

        var entries = 0;
        foreach (var family in FixtureBatches.RocketFamilies(EngineFixture.Shared.Database))
        {
            using var cpuTables = EngineFixture.Shared.Cpu.Upload(family.Table);
            using var cudaTables = cuda.Upload(family.Table);
            var batch = BatchOf(family.Table);
            var mismatches = ExactComparison.Functions(
                EngineFixture.Shared.Cpu.Run(cpuTables, batch), cuda.Run(cudaTables, batch),
                i => $"{family.Table.Species[batch.Species[i]]} at {batch.Temperature[i]} K");
            Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
            entries += batch.Count;
        }

        Assert.True(entries > 1000, $"only {entries} entries compared");
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
