using APThermo.Harness;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>L2 on the CPU accelerator: the engine's batches equal the numerical nodes called case by case, bit for bit, whatever the chunking.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class BatchTests
{
    /// <summary>The rocket family names as theory data, delegating to <see cref="FixtureBatches.FamilyNames"/>.</summary>
    public static TheoryData<string> Families() => FixtureBatches.FamilyNames(EngineFixture.SharedDatabase);

    /// <summary>A rocket family equals the host solver bit for bit.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void ARocketFamilyEqualsTheHostSolverBitForBit(string name)
    {
        var family = FixtureBatches.Family(EngineFixture.Shared.Database, name);
        var batch = family.Batch();
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        var differences = new List<string>();
        var stationCount = result.StationCount;
        for (var k = 0; k < batch.Count; k++)
        {
            var host = HostSolves.Rocket(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, k);
            var label = family.Members[k];
            if (host.Status != result.Status[k])
            {
                differences.Add($"{label}: status host {host.Status}, engine {result.Status[k]}");
            }

            Assert.Equal(CaseStatus.Ok, host.Status);
            for (var s = 0; s < stationCount; s++)
            {
                var index = k * stationCount + s;
                differences.AddRange(Bits.Differences(host.Stations[s], result.Stations[index], $"{label} station {s}"));
                differences.AddRange(Bits.Differences(host.Figures[s], result.Figures[index], $"{label} station {s}"));
                if (host.StationStatus[s] != result.StationStatus[index] || host.Iterations[s] != result.Iterations[index])
                {
                    differences.Add($"{label} station {s}: status or iterations differ");
                }

                differences.AddRange(StationMoleDifferences(host.Moles, result.Moles, s, index, family.Table, label));
            }
        }

        Assert.True(differences.Count == 0, string.Join("\n", differences.Take(30)));
    }

    /// <summary>The moles of one station, species by species, bit for bit against the host solve.</summary>
    private static IEnumerable<string> StationMoleDifferences(double[] hostMoles, double[] engineMoles, int station, long index, SpeciesTable table, string label)
    {
        var speciesCount = table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            if (!Bits.Same(hostMoles[station * speciesCount + j], engineMoles[index * speciesCount + j]))
            {
                yield return $"{label} station {station}: moles of {table.Species[j]} differ";
            }
        }
    }

    /// <summary>Chunking and repetition do not change a bit.</summary>
    [Fact]
    public void ChunkingAndRepetitionDoNotChangeABit()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        var batch = family.Batch();
        Assert.True(batch.Count >= 3);
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var first = EngineFixture.Shared.Cpu.Run(tables, batch);
        var second = EngineFixture.Shared.Cpu.Run(tables, batch);
        using var chunked = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu, ChunkSize = 2 });
        using var chunkedTables = chunked.Upload(family.Table, family.Transport);
        var third = chunked.Run(chunkedTables, batch);
        var fourth = chunked.Run(chunkedTables, batch);
        Assert.True(third.Timings.WarmUp > TimeSpan.Zero, "the first run of a fresh engine compiles the kernel");
        Assert.Equal(TimeSpan.Zero, fourth.Timings.WarmUp);
        Assert.Equal(TimeSpan.Zero, second.Timings.WarmUp);
        AssertSameRocketBits(first, second);
        AssertSameRocketBits(first, third);
        AssertSameRocketBits(first, fourth);

        var transport = TransportBatch.FromRocket(first);
        var transportFirst = EngineFixture.Shared.Cpu.Run(tables, transport);
        var transportChunked = chunked.Run(chunkedTables, transport);
        for (var i = 0; i < transport.Count; i++)
        {
            Assert.Equal(transportFirst.Status[i], transportChunked.Status[i]);
            Assert.Empty(Bits.Differences(transportFirst.Figures[i], transportChunked.Figures[i], $"station {i}"));
        }
    }

    /// <summary>The transport pass equals the host evaluation bit for bit.</summary>
    [Fact]
    public void TheTransportPassEqualsTheHostEvaluationBitForBit()
    {
        var checkedStations = 0;
        foreach (var family in FixtureBatches.RocketFamilies(EngineFixture.Shared.Database).Where(f => f.Inputs.Any(i => i.Transport)))
        {
            var batch = family.Batch();
            using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
            var rocket = EngineFixture.Shared.Cpu.Run(tables, batch);
            var transport = TransportBatch.FromRocket(rocket);
            var result = EngineFixture.Shared.Cpu.Run(tables, transport);
            Assert.Equal(transport.Count, result.Count);
            for (var i = 0; i < transport.Count; i++)
            {
                var host = HostSolves.Transport(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, tables.TransportBuffers!,
                                                                 transport.Temperature[i], transport.Moles, i * family.Table.SpeciesCount);
                Assert.Equal(host.Status, result.Status[i]);
                Assert.Empty(Bits.Differences(host.Figures, result.Figures[i], $"{family.Name} station {i}"));
                if (rocket.StationStatus[i] == CaseStatus.Ok)
                {
                    Assert.Equal(CaseStatus.Ok, result.Status[i]);
                    checkedStations++;
                }
            }
        }

        Assert.True(checkedStations > 100, $"only {checkedStations} stations checked");
    }

    /// <summary>An equilibrium family equals the host solver bit for bit.</summary>
    [Fact]
    public void AnEquilibriumFamilyEqualsTheHostSolverBitForBit()
    {
        var (batch, table, cases) = FixtureBatches.EquilibriumFamily(EngineFixture.Shared.Database, "lox-lh2_of6_pc7MPa");
        using var tables = EngineFixture.Shared.Cpu.Upload(table);
        var result = EngineFixture.Shared.Cpu.Run(tables, batch);
        Assert.Equal(cases.Count, result.Count);
        for (var k = 0; k < batch.Count; k++)
        {
            var host = HostSolves.Equilibrium(EngineFixture.Shared.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, k);
            Assert.Equal(CaseStatus.Ok, host.Status);
            Assert.Equal(host.Status, result.Status[k]);
            Assert.Equal(host.Iterations, result.Iterations[k]);
            Assert.Empty(Bits.Differences(host.State, result.State[k], cases[k].Name));
            for (var j = 0; j < table.SpeciesCount; j++)
            {
                Assert.True(Bits.Same(host.Moles[j], result.Moles[(long)k * table.SpeciesCount + j]), $"{cases[k].Name}: moles of {table.Species[j]}");
            }

            var reference = cases[k].Outputs.GetProperty("temperature").GetDouble();
            Assert.True(EngineFixture.Shared.Tolerances.Matches("temperature", reference, result.State[k].Temperature), $"{cases[k].Name}: temperature {result.State[k].Temperature} vs {reference}");
        }

        var transport = TransportBatch.FromEquilibrium(result);
        Assert.Equal(result.Count, transport.Count);
        Assert.Same(result.Moles, transport.Moles);
    }

    private static void AssertSameRocketBits(RocketBatchResult expected, RocketBatchResult actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.StationStatus, actual.StationStatus);
        Assert.Equal(expected.Iterations, actual.Iterations);
        for (var i = 0; i < expected.Stations.Length; i++)
        {
            Assert.Empty(Bits.Differences(expected.Stations[i], actual.Stations[i], $"station {i}"));
            Assert.Empty(Bits.Differences(expected.Figures[i], actual.Figures[i], $"station {i}"));
        }

        for (long j = 0; j < expected.Moles.LongLength; j++)
        {
            Assert.True(Bits.Same(expected.Moles[j], actual.Moles[j]), $"moles differ at {j}");
        }
    }
}
