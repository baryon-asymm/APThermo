using APThermo.Equilibrium;
using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// L0: the exact comparison of two results is not degenerate (AGENTS.md section 13): two runs of the same batch on the CPU accelerator
/// compare clean, and every kind of difference, down to one unit in the last place of one value, is refused with a message that names
/// the case and the field. Each break is shown red once, on a result of its own kind: an equilibrium batch, a rocket batch, a
/// transport batch and a species-function batch.
/// </summary>
[Collection(EngineFixture.CollectionName)]
public sealed class ExactComparisonTests
{
    private static string Label(int k) => $"case {k}";

    /// <summary>Two runs of one batch compare clean, on a result of each kind, and the comparisons look at something.</summary>
    [Fact]
    public void TwoRunsOfOneBatchCompareClean()
    {
        var recovery = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var recoveryTables = EngineFixture.Shared.Cpu.Upload(recovery.Table);
        Assert.Empty(ExactComparison.Equilibrium(
            EngineFixture.Shared.Cpu.Run(recoveryTables, recovery.Batch), EngineFixture.Shared.Cpu.Run(recoveryTables, recovery.Batch), Label));

        var rocket = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var rocketTables = EngineFixture.Shared.Cpu.Upload(rocket.Table, rocket.Transport);
        var first = EngineFixture.Shared.Cpu.Run(rocketTables, rocket.Batch());
        Assert.Empty(ExactComparison.Rocket(first, EngineFixture.Shared.Cpu.Run(rocketTables, rocket.Batch()), Label));
        var transport = TransportBatch.FromRocket(first);
        Assert.Empty(ExactComparison.Transport(EngineFixture.Shared.Cpu.Run(rocketTables, transport), EngineFixture.Shared.Cpu.Run(rocketTables, transport)));
        Assert.NotEmpty(first.Stations);
    }

    /// <summary>Each kind of difference of an equilibrium result is refused with its own message.</summary>
    [Fact]
    public void AnEquilibriumResultDifferingInOneThingIsRefusedWithItsOwnMessage()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var species = family.Table.SpeciesCount;
        var hp = family.Batch.Kind.ToList().IndexOf(ProblemKind.AssignedEnthalpyPressure);
        Assert.True(hp >= 0);

        Assert.Contains("status NoGasPhase on the CPU accelerator, NotConverged", Broken(tables, family, r => r.Status[hp] = CaseStatus.NotConverged), StringComparison.Ordinal);
        Assert.Contains("iterations", Broken(tables, family, r => r.Iterations[hp]++), StringComparison.Ordinal);
        Assert.Contains("Temperature", Broken(tables, family, r => r.State[hp] = r.State[hp] with { Temperature = Math.BitIncrement(r.State[hp].Temperature) }), StringComparison.Ordinal);
        Assert.Contains("Pressure", Broken(tables, family, r => r.State[hp] = r.State[hp] with { Pressure = Math.BitDecrement(r.State[hp].Pressure) }), StringComparison.Ordinal);
        Assert.Contains($"case {hp} [{species - 1}]", Broken(tables, family, r => r.Moles[hp * species + species - 1] = Math.BitIncrement(r.Moles[hp * species + species - 1])), StringComparison.Ordinal);
    }

    /// <summary>A signed zero and a NaN are told apart from the number the other side holds, and from each other by their bits.</summary>
    [Fact]
    public void ASignedZeroAndANaNAreTold()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var gas = 0;
        var zeroOnBothSides = Broken(tables, family, r => r.Moles[gas] = r.Moles[gas] == 0.0 ? -0.0 : r.Moles[gas]);
        Assert.Contains("[0]", zeroOnBothSides, StringComparison.Ordinal);
        Assert.Contains("NaN", Broken(tables, family, r => r.Moles[gas] = double.NaN), StringComparison.Ordinal);
    }

    /// <summary>Two results of different shapes are refused before any value is read, with the shapes in the message.</summary>
    [Fact]
    public void ResultsOfDifferentShapesAreRefusedBeforeAnyValueIsRead()
    {
        var recovery = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(recovery.Table);
        var whole = EngineFixture.Shared.Cpu.Run(tables, recovery.Batch);
        var part = EngineFixture.Shared.Cpu.Run(tables, FixtureBatches.CaseOf(recovery.Batch, 0));
        var refused = Assert.Single(ExactComparison.Equilibrium(whole, part, Label));
        Assert.Contains("shape", refused, StringComparison.Ordinal);
    }

    /// <summary>Each kind of difference of a rocket result is refused with its own message.</summary>
    [Fact]
    public void ARocketResultDifferingInOneThingIsRefusedWithItsOwnMessage()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var batch = family.Batch();
        var reference = EngineFixture.Shared.Cpu.Run(tables, batch);
        var stations = reference.StationCount;

        string Rocket(Action<RocketBatchResult> breakIt)
        {
            var other = EngineFixture.Shared.Cpu.Run(tables, batch);
            breakIt(other);
            return string.Join('\n', ExactComparison.Rocket(reference, other, Label));
        }

        Assert.Contains("status", Rocket(r => r.Status[1] = CaseStatus.NotConverged), StringComparison.Ordinal);
        Assert.Contains("station 2: station status", Rocket(r => r.StationStatus[stations + 2] = CaseStatus.NotConverged), StringComparison.Ordinal);
        Assert.Contains("case 1 station 1: iterations", Rocket(r => r.Iterations[stations + 1]++), StringComparison.Ordinal);
        Assert.Contains("case 1 station 0.Density", Rocket(r => r.Stations[stations] = r.Stations[stations] with { Density = Math.BitIncrement(r.Stations[stations].Density) }), StringComparison.Ordinal);
        Assert.Contains("case 1 station 1.", Rocket(r => r.Figures[stations + 1] = r.Figures[stations + 1] with { CharacteristicVelocity = Math.BitDecrement(r.Figures[stations + 1].CharacteristicVelocity) }), StringComparison.Ordinal);
        var amount = (stations + 1) * family.Table.SpeciesCount;
        Assert.Contains("case 1 station 1", Rocket(r => r.Moles[amount] = Math.BitIncrement(r.Moles[amount])), StringComparison.Ordinal);
    }

    /// <summary>A transport result and a species-function result differing in one value are refused.</summary>
    [Fact]
    public void ATransportAndAFunctionResultDifferingInOneValueAreRefused()
    {
        var family = FixtureBatches.RocketFamilies(EngineFixture.Shared.Database)[0];
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table, family.Transport);
        var transport = TransportBatch.FromRocket(EngineFixture.Shared.Cpu.Run(tables, family.Batch()));
        var reference = EngineFixture.Shared.Cpu.Run(tables, transport);
        var other = EngineFixture.Shared.Cpu.Run(tables, transport);
        other.Figures[1] = other.Figures[1] with { Viscosity = Math.BitIncrement(other.Figures[1].Viscosity) };
        Assert.Contains("station 1.Viscosity", string.Join('\n', ExactComparison.Transport(reference, other)), StringComparison.Ordinal);

        var request = new SpeciesFunctionBatch(2);
        request.Species[1] = 1;
        request.Temperature[0] = request.Temperature[1] = 1500.0;
        var functions = EngineFixture.Shared.Cpu.Run(tables, request);
        var moved = EngineFixture.Shared.Cpu.Run(tables, request);
        Assert.Empty(ExactComparison.Functions(functions, moved, i => $"entry {i}"));
        moved.HOverRT[1] = Math.BitIncrement(moved.HOverRT[1]);
        Assert.Contains("entry 1 H/RT", string.Join('\n', ExactComparison.Functions(functions, moved, i => $"entry {i}")), StringComparison.Ordinal);
        moved.InRange[0] = !moved.InRange[0];
        Assert.Contains("in range", string.Join('\n', ExactComparison.Functions(functions, moved, i => $"entry {i}")), StringComparison.Ordinal);
    }

    /// <summary>The comparison returns at most <see cref="ExactComparison.Shown"/> mismatches, however many values differ.</summary>
    [Fact]
    public void AComparisonReturnsAtMostTheShownMismatches()
    {
        var family = RecoveryFamilies.Family(EngineFixture.Shared.Database, "gasless-ko2");
        using var tables = EngineFixture.Shared.Cpu.Upload(family.Table);
        var reference = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        for (var i = 0; i < other.Moles.Length; i++)
        {
            other.Moles[i] = Math.BitIncrement(other.Moles[i]);
        }

        var mismatches = ExactComparison.Equilibrium(reference, other, Label);
        Assert.Equal(ExactComparison.Shown, mismatches.Count);
    }

    /// <summary>The message of the comparison of the family's result with a copy broken by <paramref name="breakIt"/>.</summary>
    private static string Broken(UploadedTables tables, BracketedFamily family, Action<EquilibriumBatchResult> breakIt)
    {
        var reference = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        var other = EngineFixture.Shared.Cpu.Run(tables, family.Batch);
        breakIt(other);
        var mismatches = ExactComparison.Equilibrium(reference, other, Label);
        Assert.NotEmpty(mismatches);
        return string.Join('\n', mismatches);
    }
}
