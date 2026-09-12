using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Execution.Tests;

/// <summary>L2 on the CPU accelerator: the engine's batches equal the numerical nodes called case by case, bit for bit, whatever the chunking.</summary>
[Collection(EngineCollection.Name)]
public sealed class BatchTests(EngineFixture fixture)
{
    public static IEnumerable<object[]> Families() => BatchBuilders.FamilyNames(EngineFixture.SharedDatabase);

    [Theory]
    [MemberData(nameof(Families))]
    public void A_rocket_family_equals_the_host_solver_bit_for_bit(string name)
    {
        var family = BatchBuilders.Family(fixture.Database, name);
        var batch = family.Batch();
        using var tables = fixture.Cpu.Upload(family.Table, family.Transport);
        var result = fixture.Cpu.Run(tables, batch);
        var differences = new List<string>();
        var speciesCount = family.Table.SpeciesCount;
        var stationCount = result.StationCount;
        for (var k = 0; k < batch.Count; k++)
        {
            var host = BatchBuilders.SolveRocketOnHost(fixture.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, k);
            var label = family.Members[k];
            if (host.Status != result.Status[k])
            {
                differences.Add($"{label}: status host {host.Status}, engine {result.Status[k]}");
            }

            Assert.Equal(CaseStatus.Ok, host.Status);
            for (var s = 0; s < stationCount; s++)
            {
                var index = k * stationCount + s;
                differences.AddRange(BatchBuilders.BitDifferences(host.Stations[s], result.Stations[index], $"{label} station {s}"));
                differences.AddRange(BatchBuilders.BitDifferences(host.Figures[s], result.Figures[index], $"{label} station {s}"));
                if (host.StationStatus[s] != result.StationStatus[index] || host.Iterations[s] != result.Iterations[index])
                {
                    differences.Add($"{label} station {s}: status or iterations differ");
                }

                for (var j = 0; j < speciesCount; j++)
                {
                    if (!BatchBuilders.SameBits(host.Moles[s * speciesCount + j], result.Moles[(long)index * speciesCount + j]))
                    {
                        differences.Add($"{label} station {s}: moles of {family.Table.Species[j]} differ");
                    }
                }
            }
        }

        Assert.True(differences.Count == 0, string.Join("\n", differences.Take(30)));
    }

    [Fact]
    public void Chunking_and_repetition_do_not_change_a_bit()
    {
        var family = BatchBuilders.RocketFamilies(fixture.Database)[0];
        var batch = family.Batch();
        Assert.True(batch.Count >= 3);
        using var tables = fixture.Cpu.Upload(family.Table, family.Transport);
        var first = fixture.Cpu.Run(tables, batch);
        var second = fixture.Cpu.Run(tables, batch);
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
        var transportFirst = fixture.Cpu.Run(tables, transport);
        var transportChunked = chunked.Run(chunkedTables, transport);
        for (var i = 0; i < transport.Count; i++)
        {
            Assert.Equal(transportFirst.Status[i], transportChunked.Status[i]);
            Assert.Empty(BatchBuilders.BitDifferences(transportFirst.Figures[i], transportChunked.Figures[i], $"station {i}"));
        }
    }

    [Fact]
    public void The_transport_pass_equals_the_host_evaluation_bit_for_bit()
    {
        var checkedStations = 0;
        foreach (var family in BatchBuilders.RocketFamilies(fixture.Database).Where(f => f.Inputs.Any(i => i.Transport)))
        {
            var batch = family.Batch();
            using var tables = fixture.Cpu.Upload(family.Table, family.Transport);
            var rocket = fixture.Cpu.Run(tables, batch);
            var transport = TransportBatch.FromRocket(rocket);
            var result = fixture.Cpu.Run(tables, transport);
            Assert.Equal(transport.Count, result.Count);
            for (var i = 0; i < transport.Count; i++)
            {
                var host = BatchBuilders.EvaluateTransportOnHost(fixture.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, tables.TransportBuffers!,
                                                                 transport.Temperature[i], transport.Moles, i * family.Table.SpeciesCount);
                Assert.Equal(host.Status, result.Status[i]);
                Assert.Empty(BatchBuilders.BitDifferences(host.Figures, result.Figures[i], $"{family.Name} station {i}"));
                if (rocket.StationStatus[i] == CaseStatus.Ok)
                {
                    Assert.Equal(CaseStatus.Ok, result.Status[i]);
                    checkedStations++;
                }
            }
        }

        Assert.True(checkedStations > 100, $"only {checkedStations} stations checked");
    }

    [Fact]
    public void An_equilibrium_family_equals_the_host_solver_bit_for_bit()
    {
        var (batch, table, cases) = BatchBuilders.EquilibriumFamily(fixture.Database, "lox-lh2_of6_pc7MPa");
        using var tables = fixture.Cpu.Upload(table);
        var result = fixture.Cpu.Run(tables, batch);
        Assert.Equal(cases.Count, result.Count);
        for (var k = 0; k < batch.Count; k++)
        {
            var host = BatchBuilders.SolveEquilibriumOnHost(fixture.Cpu.IlgpuAccelerator, tables.SpeciesBuffers, batch, k);
            Assert.Equal(CaseStatus.Ok, host.Status);
            Assert.Equal(host.Status, result.Status[k]);
            Assert.Equal(host.Iterations, result.Iterations[k]);
            Assert.Empty(BatchBuilders.BitDifferences(host.State, result.State[k], cases[k].Name));
            for (var j = 0; j < table.SpeciesCount; j++)
            {
                Assert.True(BatchBuilders.SameBits(host.Moles[j], result.Moles[(long)k * table.SpeciesCount + j]), $"{cases[k].Name}: moles of {table.Species[j]}");
            }

            var reference = cases[k].Outputs.GetProperty("temperature").GetDouble();
            Assert.True(fixture.Tolerances.Matches("temperature", reference, result.State[k].Temperature), $"{cases[k].Name}: temperature {result.State[k].Temperature} vs {reference}");
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
            Assert.Empty(BatchBuilders.BitDifferences(expected.Stations[i], actual.Stations[i], $"station {i}"));
            Assert.Empty(BatchBuilders.BitDifferences(expected.Figures[i], actual.Figures[i], $"station {i}"));
        }

        for (long j = 0; j < expected.Moles.LongLength; j++)
        {
            Assert.True(BatchBuilders.SameBits(expected.Moles[j], actual.Moles[j]), $"moles differ at {j}");
        }
    }
}
