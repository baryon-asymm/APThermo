using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The entropy of a supercooled vapour (Recovery BOOT.md, "Dead-end floors" and the criterion of the sp warm-430 K states of AP/HTPB/Al).
/// Below the floors of <c>H2O(L)</c>, <c>NH4CL(II)</c> and <c>C(gr)</c> (273.15 K, 298.15 K, 300 K) the records of the condensed species are no
/// candidates, and the tp state of AP/HTPB/Al at 20 MPa is a vapour whose entropy, 2 217 to 2 567 J/(kg·K) from 200 to 290 K, is above that of every state
/// with its records in range from 300 K to the dehydration plateau of Al(OH)3, Al2O3 and H2O(L) at 415.948 K: the tp entropy is not monotone
/// in T. An sp problem at such an entropy has two states of the list, the vapour and the dehydrated one; the equilibrium of an assigned entropy
/// and pressure is the state of the least enthalpy, and the dehydrated one has it, by about 6e5 J/(kg) (the vapour is the metastable state the
/// missing data leave, not an equilibrium). The solver ends there, on the plateau or above it, never at the tp temperature.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class SupercooledVapourSpTests
{
    private const double Pressure = 2.0e7;

    /// <summary>The highest floor of the table, K: the state of an sp target of a vapour lies at or above it.</summary>
    private const double HighestFloor = 300.0;

    /// <summary>The tp temperatures, K, of the vapour states below every floor, 200 to 290 K, cold and seeded from 430 K.</summary>
    public static TheoryData<double, bool> Vapours()
    {
        var data = new TheoryData<double, bool>();
        foreach (var temperature in Enumerable.Range(0, 10).Select(i => 200.0 + 10.0 * i))
        {
            data.Add(temperature, false);
            data.Add(temperature, true);
        }

        return data;
    }

    /// <summary>
    /// The sp state at the entropy of a supercooled vapour is <c>Ok</c>, clear of the equilibrium conditions, at that entropy, above the floors,
    /// with a lower enthalpy than the vapour: the least-enthalpy state of the list at that entropy and pressure.
    /// </summary>
    [Theory]
    [MemberData(nameof(Vapours))]
    public void AnSpTargetOfASupercooledVapourEndsOnTheStateOfLeastEnthalpyAboveTheFloors(double temperature, bool seeded)
    {
        var c = HostSolver.Load("tp", "ap-htpb-al_pc7MPa_T430");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var moles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var tp = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, Pressure, temperature, 0.0, moles));
        var seed = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, Pressure, 430.0, 0.0, moles));
        Assert.Equal(CaseStatus.Ok, tp.Status);

        var solution = seeded
            ? HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedEntropyPressure, Pressure, 430.0, tp.State.Entropy, moles), (double[])seed.Moles.Clone())
            : HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedEntropyPressure, Pressure, 0.0, tp.State.Entropy, moles));

        var label = $"{temperature} K {(seeded ? "seeded" : "cold")}";
        Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
        Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
        Assert.Equal(tp.State.Entropy, solution.State.Entropy, Tolerances.SelfConsistency * tp.State.Entropy);
        Assert.True(solution.State.Temperature >= HighestFloor, $"{label}: T {solution.State.Temperature:R} under the floors");
        Assert.True(solution.State.Enthalpy < tp.State.Enthalpy, $"{label}: h {solution.State.Enthalpy:R} not below the vapour's {tp.State.Enthalpy:R}");
    }
}
