using System.Globalization;
using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L2: the dead-end floors of a table (Recovery BOOT.md, "Dead-end floors"). RP-1311 example 12's list has <c>H2O(L)</c> from
/// 273.15 K and no <c>H2O(cr)</c>, so below that bound the list holds no water phase and a cold hp or sp solve could converge on
/// the supercooled vapour while a state of the same enthalpy or entropy holding the liquid lies above the bound. The state of
/// the list plus <c>H2O(cr)</c>, whose record is open below and adjoins the liquid, is the reference of what the list's states
/// must be.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class DeadEndFloorTests
{
    /// <summary>K: the lower bound of the <c>H2O(L)</c> record, the dead-end floor of example 12's list.</summary>
    private const double LiquidFloor = 273.15;

    /// <summary>The relative deviation of the temperature of a list state from the reference's.</summary>
    private const double TemperatureReproduction = 1.0e-9;

    /// <summary>The pressure factors applied to each table's own fixture pressure; the smallest reaches 689 Pa on the chamber.</summary>
    private static readonly double[] PressureFactors = [1.0, 1.0e-4, 1.0e-3, 1.0e-2, 0.1, 10.0];

    /// <summary>The temperatures of the tp states whose enthalpy and entropy are the targets, K: every 10 K from 170 to 400 (ice, supercooled and liquid water, gas) and 600.</summary>
    private static readonly double[] Temperatures = [.. Enumerable.Range(0, 24).Select(i => 170.0 + 10.0 * i), 600.0];

    /// <summary>
    /// Every hp and sp target taken from a tp state of the list plus <c>H2O(cr)</c>, put to the list alone from the cold start,
    /// ends <c>Ok</c> at the temperature of the list-plus-ice solve when that state holds no ice (the target has a state of
    /// the list, above the floor or in the gas); when it holds ice, the list has none below the floor, and the solve ends
    /// <c>TemperatureOutOfRange</c> or <c>Ok</c> on the vapour below the floor (tp below a dead-end floor reporting the
    /// supercooled gas is correct, the range rule) with the iterations of the original attempt alone: the recheck that found
    /// no state above the floor reran it to the same bits.
    /// Red before the recheck and the bracket's dead-end floors: the cold solve ended <c>Ok</c> on the supercooled vapour
    /// below the floor, or <c>TemperatureOutOfRange</c>, for targets whose state holds the liquid.
    /// </summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure)]
    [InlineData(ProblemKind.AssignedEntropyPressure)]
    public void EveryTargetOfTheListPlusIceEndsAtItsStateOrBelowTheFloor(ProblemKind kind)
    {
        var walked = 0;
        var liquid = 0;
        var iced = 0;
        var stood = 0;
        var failures = new List<string>();
        foreach (var (name, list, withIce, pressure, temperature) in Grid())
        {
            var tp = HostSolver.Solve(CpuFixture.Shared.Accelerator, withIce with { Pressure = pressure, Temperature = temperature });
            Assert.Equal(CaseStatus.Ok, tp.Status);
            var target = kind == ProblemKind.AssignedEnthalpyPressure ? tp.State.Enthalpy : tp.State.Entropy;
            var reference = HostSolver.Solve(CpuFixture.Shared.Accelerator, withIce with { Kind = kind, Pressure = pressure, Temperature = 0.0, Target = target });
            Assert.Equal(CaseStatus.Ok, reference.Status);
            var holdsIce = reference.Moles[withIce.Table.IndexOf("H2O(cr)")] > 0.0;
            var back = HostSolver.Solve(CpuFixture.Shared.Accelerator, list with { Kind = kind, Pressure = pressure, Temperature = 0.0, Target = target });
            walked++;
            liquid += holdsIce ? 0 : 1;
            iced += holdsIce ? 1 : 0;
            var label = string.Create(CultureInfo.InvariantCulture, $"{name} p={pressure:R} T={temperature:R}: list {back.Status} at {back.State.Temperature:R}, reference at {reference.State.Temperature:R}");
            var reproduces = back.Status == CaseStatus.Ok
                && Math.Abs(back.State.Temperature - reference.State.Temperature) <= TemperatureReproduction * reference.State.Temperature;
            var vapour = back.Status == CaseStatus.Ok && back.State.Temperature < LiquidFloor;
            var original = vapour && back.Iterations == GasPhaseRig.FirstAttemptIterations(list with { Kind = kind, Pressure = pressure, Temperature = 0.0, Target = target });
            stood += vapour ? 1 : 0;
            if (holdsIce ? !(back.Status == CaseStatus.TemperatureOutOfRange || original) : !reproduces)
            {
                failures.Add(label);
            }
        }

        Assert.Equal(CaseNames().Count * PressureFactors.Length * Temperatures.Length, walked);
        Assert.True(liquid > 0 && iced > 0 && stood > 0, $"{liquid} targets without ice, {iced} with ice, {stood} left on the vapour: all three classes must be walked");
        Assert.True(failures.Count == 0, $"{failures.Count} of {walked}: {string.Join(" | ", failures.Take(12))}");
    }

    /// <summary>
    /// The liquid's record is a dead-end floor of example 12's list, at its own lower bound, beside graphite's (300 K), and
    /// stops being one once <c>H2O(cr)</c> is listed: the ice record is open below and its upper bound adjoins the liquid's
    /// lower one.
    /// </summary>
    [Fact]
    public void TheLiquidIsADeadEndFloorExactlyWhenNoIceAdjoinsIt()
    {
        var (_, list, withIce, _, _) = Grid().First();
        using var listBuffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, list.Table);
        using var iceBuffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, withIce.Table);

        var deadEnds = Enumerable.Range(list.Table.GasCount, list.Table.CondensedCount).Where(j => DeadEnds.IsDeadEnd(listBuffers.View, j)).ToArray();
        var iceDeadEnds = Enumerable.Range(withIce.Table.GasCount, withIce.Table.CondensedCount).Where(j => DeadEnds.IsDeadEnd(iceBuffers.View, j)).ToArray();

        Assert.Equal(["H2O(L)", "C(gr)"], deadEnds.Select(j => list.Table.Species[j]));
        Assert.Equal(LiquidFloor, SpeciesFunctions.RecordLow(listBuffers.View, deadEnds[0]));
        Assert.Equal(["C(gr)"], iceDeadEnds.Select(j => withIce.Table.Species[j]));
    }

    /// <summary>
    /// The floor a downward step lands on is the highest dead-end floor below the probe, taken over the records whose elements
    /// are present whether the probe holds them or not, exactly at its lower bound; a probe standing on a floor goes on
    /// below it, and the liquid's record adjoined by ice is no floor.
    /// </summary>
    [Theory]
    [InlineData(false, 400.0, 300.0)]
    [InlineData(false, 300.0, LiquidFloor)]
    [InlineData(false, 280.0, LiquidFloor)]
    [InlineData(false, LiquidFloor, 0.0)]
    [InlineData(false, 250.0, 0.0)]
    [InlineData(true, 400.0, 300.0)]
    [InlineData(true, 290.0, 0.0)]
    public void ADownwardStepLandsOnTheHighestDeadEndFloorBelowTheProbe(bool withIce, double temperature, double expected)
    {
        var (_, list, ice, _, _) = Grid().First();

        var floor = GasPhaseRig.FloorBelow(withIce ? ice : list, temperature);

        Assert.Equal(expected, floor);
    }

    /// <summary>The fixture names of example 12's tp tables.</summary>
    private static List<string> CaseNames()
    {
        var names = HostSolver.CaseNames("tp").Where(n => n.StartsWith("rp1311-example12_", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(names);
        return names;
    }

    /// <summary>The grid: each table of example 12 as the list and as the list plus <c>H2O(cr)</c>, at each pressure and temperature.</summary>
    private static IEnumerable<(string Name, EquilibriumCase List, EquilibriumCase WithIce, double Pressure, double Temperature)> Grid()
    {
        foreach (var name in CaseNames())
        {
            var c = HostSolver.Load("tp", name);
            var moles = HostSolver.ElementMolesOf(c);
            var list = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
            var withIce = SpeciesTable.Build(CpuFixture.Shared.Database, HostSolver.ElementsOf(c), [.. HostSolver.ProductsOf(c), "H2O(cr)"]);
            foreach (var factor in PressureFactors)
            {
                foreach (var temperature in Temperatures)
                {
                    var pressure = HostSolver.PressureOf(c) * factor;
                    yield return (name, new EquilibriumCase(list, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles),
                                  new EquilibriumCase(withIce, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles), pressure, temperature);
                }
            }
        }
    }
}
