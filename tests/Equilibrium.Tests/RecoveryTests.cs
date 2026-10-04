using System.Globalization;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L1: the temperature bracket of an hp or sp case (Recovery BOOT.md, acceptance criteria), the state of a gasless tp case
/// asked for again as an hp and an sp problem. The test computes the enthalpy and the entropy of the tp state from its moles
/// and the species functions, and the bracket must find the temperature again.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class RecoveryTests
{
    /// <summary>The gasless tp states asked for again: (system name, T, p, ε).</summary>
    public static TheoryData<string, double, double, double, ProblemKind> GaslessStates()
    {
        var data = new TheoryData<string, double, double, double, ProblemKind>();
        (string Name, double Temperature, double Pressure, double Epsilon)[] states =
        [
            ("KO2", 500.0, 1.0e5, 0.0), ("KO2", 800.0, 1.0e5, -1.0e-2), ("KO2", 300.0, 1.0e5, -1.0e-6), ("Al2O3", 300.0, 1.0e5, 0.0),
            ("Al2O3", 300.0, 1.0e5, -1.0e-2), ("KCl", 800.0, 1.0e5, 0.0), ("KCl+KO2", 600.0, 1.0e5, -1.0e-6), ("thermite", 1200.0, 1.0e5, 0.0),
            ("CaCO3", 500.0, 1.0e5, -1.0e-2), ("MgO", 300.0, 1.0e5, 0.0), ("Li2O", 300.0, 1.0e5, -1.0e-2), ("H2O", 300.0, 1.0e5, 0.0),
        ];
        foreach (var (name, temperature, pressure, epsilon) in states)
        {
            data.Add(name, temperature, pressure, epsilon, ProblemKind.AssignedEnthalpyPressure);
            data.Add(name, temperature, pressure, epsilon, ProblemKind.AssignedEntropyPressure);
        }

        return data;
    }

    /// <summary>
    /// The enthalpy (or entropy) of a gasless tp state, put to the solver as an hp (or sp) problem from the cold start, ends
    /// <c>NoGasPhase</c> at the tp temperature with the tp state's moles (1e-12 relative to the largest) and multipliers (1e-9),
    /// a state of the temperature and the pressure only. Red before the bracket: <c>NotConverged</c> or
    /// <c>TemperatureOutOfRange</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(GaslessStates))]
    public void TheStateOfAGaslessTpCaseSolvedAgainAsHpOrSpEndsNoGasPhaseWithTheSameMoles(
        string name, double temperature, double pressure, double epsilon, ProblemKind kind)
    {
        var system = GaslessSystem.Named(name);
        var tp = HostSolver.Solve(CpuFixture.Shared.Accelerator, system.Case(temperature, pressure, epsilon));
        Assert.Equal(CaseStatus.NoGasPhase, tp.Status);
        var target = GaslessTarget.Of(tp, kind);

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, system.Case(0.0, pressure, epsilon) with { Kind = kind, Target = target });

        var label = string.Create(CultureInfo.InvariantCulture, $"{name} {kind}, T0 {temperature} K, {pressure:G3} Pa, ε {epsilon:G3}");
        Assert.True(solution.Status == CaseStatus.NoGasPhase, $"{label}: status {solution.Status} after {solution.Iterations} iterations");
        Assert.Equal(temperature, solution.State.Temperature, 1.0e-9 * temperature);
        Assert.Equal(new MixtureState { Temperature = solution.State.Temperature, Pressure = pressure }, solution.State);
        var largest = tp.Moles.Max();
        Assert.All(Enumerable.Range(0, tp.Moles.Length), j => Assert.Equal(tp.Moles[j], solution.Moles[j], 1.0e-12 * largest));
        Assert.Empty(EquilibriumConditions.GaslessViolations(solution));
    }

    /// <summary>
    /// A gasless melting plateau: potassium superoxide at 1e7 Pa (K:O = 1:2), the enthalpy halfway between the solid just below
    /// its melting temperature and the liquid just above it, is <c>NoGasPhase</c> with both records in the solution, at the
    /// melting temperature, and the lever amounts of the two reproduce the enthalpy within 1e-9 relative. Red before the
    /// bracket: <c>TemperatureOutOfRange</c>.
    /// </summary>
    [Fact]
    public void AGaslessMeltingPlateauIsNoGasPhaseWithBothRecordsAndTheLeverAmounts()
    {
        const double pressure = 1.0e7;
        var system = GaslessSystem.Superoxide;
        var (melting, solid, liquid) = MeltingOf(system, pressure);
        var target = 0.5 * (GaslessTarget.Of(solid, ProblemKind.AssignedEnthalpyPressure) + GaslessTarget.Of(liquid, ProblemKind.AssignedEnthalpyPressure));

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, system.Case(0.0, pressure, 0.0) with { Kind = ProblemKind.AssignedEnthalpyPressure, Target = target });

        Assert.True(solution.Status == CaseStatus.NoGasPhase, $"status {solution.Status} after {solution.Iterations} iterations");
        Assert.Contains("KO2(a)", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
        Assert.Contains("KO2(L)", UnivariantRig.CondensedOf(solution), StringComparison.Ordinal);
        Assert.Equal(melting, solution.State.Temperature, 1.0e-6 * melting);
        Assert.Equal(target, GaslessTarget.Of(solution, ProblemKind.AssignedEnthalpyPressure), 1.0e-9 * Math.Abs(target));
        Assert.Empty(EquilibriumConditions.GaslessViolations(solution));
    }

    /// <summary>
    /// A target no state of the system reaches (an enthalpy beyond the window's top) ends <c>TemperatureOutOfRange</c> after
    /// probes that converged and wrote states: the state of the failed case is zero, whatever a probe wrote, and the iterations
    /// are those of every attempt, more than the first attempt's alone.
    /// </summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, 1.0e10)]
    [InlineData(ProblemKind.AssignedEntropyPressure, 1.0e6)]
    public void ACaseTheBracketGivesUpOnLeavesAZeroStateAndSumsEveryAttempt(ProblemKind kind, double target)
    {
        var problem = GaslessSystem.Water.Case(0.0, 1.0e5, 0.0) with { Kind = kind, Target = target };

        var solution = HostSolver.Solve(CpuFixture.Shared.Accelerator, problem);

        Assert.Equal(CaseStatus.TemperatureOutOfRange, solution.Status);
        Assert.Equal(default, solution.State);
        Assert.True(solution.Iterations > GasPhaseRig.FirstAttemptIterations(problem), $"{solution.Iterations} iterations in all");
    }

    /// <summary>The AP/HTPB/Al states of 0.2.1's limitations below the water band: cold hp and seeded hp and sp, at 7 MPa, are <c>Ok</c> at the tp temperatures.</summary>
    [Theory]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, 400.0, false)]
    [InlineData(ProblemKind.AssignedEnthalpyPressure, 390.0, true)]
    [InlineData(ProblemKind.AssignedEntropyPressure, 390.0, true)]
    public void AStateOfApHtpbAlBelowTheWaterBandEndsOkAtItsTpTemperature(ProblemKind kind, double temperature, bool seeded)
    {
        const double pressure = 7.0e6;
        var c = HostSolver.Load("tp", "ap-htpb-al_pc7MPa_T430");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var moles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var tp = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles));
        var seed = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, 430.0, 0.0, moles));
        Assert.Equal(CaseStatus.Ok, tp.Status);
        var target = kind == ProblemKind.AssignedEnthalpyPressure ? tp.State.Enthalpy : tp.State.Entropy;

        var solution = seeded
            ? HostSolver.Solve(accelerator, new EquilibriumCase(table, kind, pressure, 430.0, target, moles), (double[])seed.Moles.Clone())
            : HostSolver.Solve(accelerator, new EquilibriumCase(table, kind, pressure, 0.0, target, moles));

        Assert.True(solution.Status == CaseStatus.Ok, $"{kind} at {temperature} K: status {solution.Status} after {solution.Iterations} iterations");
        Assert.Equal(temperature, solution.State.Temperature, Tolerances.SelfConsistency * temperature);
        Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
    }

    /// <summary>The temperatures, K, of the AP/HTPB/Al tp states at 20 MPa below every dead-end floor of the table (273.15 K, 298.15 K, 300 K) whose enthalpy is asked for again.</summary>
    public static TheoryData<double, bool> VapourStatesBelowTheFloors()
    {
        var data = new TheoryData<double, bool>();
        foreach (var temperature in Enumerable.Range(0, 16).Select(i => 200.0 + 5.0 * i))
        {
            data.Add(temperature, false);
            data.Add(temperature, true);
        }

        return data;
    }

    /// <summary>
    /// The enthalpy of the AP/HTPB/Al tp state at 20 MPa and a temperature from 200 K to 275 K, the supercooled vapour below the
    /// floors of <c>H2O(L)</c>, <c>NH4CL(II)</c> and <c>C(gr)</c>, put to the solver as an hp problem from the cold start and
    /// from the 430 K state, ends <c>Ok</c> at the tp temperature, clear of the equilibrium conditions. The bracket converges
    /// onto the jump of <c>H2O(L)</c> at its upper bound, 600 K, where no state lies, and the search goes on below the floors
    /// (Recovery BOOT.md, "Dead-end gaps"). Red before the scan: <c>NotConverged</c> after the final attempt on the gap.
    /// </summary>
    [Theory]
    [MemberData(nameof(VapourStatesBelowTheFloors))]
    public void AnHpTargetOfASupercooledVapourStateAt20MPaEndsOkBelowTheFloors(double temperature, bool seeded)
    {
        const double pressure = 2.0e7;
        var c = HostSolver.Load("tp", "ap-htpb-al_pc7MPa_T430");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var moles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;
        var tp = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles));
        var seed = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedTemperaturePressure, pressure, 430.0, 0.0, moles));
        Assert.Equal(CaseStatus.Ok, tp.Status);

        var solution = seeded
            ? HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, pressure, 430.0, tp.State.Enthalpy, moles), (double[])seed.Moles.Clone())
            : HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, tp.State.Enthalpy, moles));

        Assert.True(solution.Status == CaseStatus.Ok, $"{temperature} K {(seeded ? "seeded" : "cold")}: status {solution.Status} after {solution.Iterations} iterations");
        Assert.Equal(temperature, solution.State.Temperature, Tolerances.SelfConsistency * temperature);
        Assert.Empty(EquilibriumConditions.Violations(solution, Tolerances.GasChemicalPotential));
    }

    /// <summary>
    /// An enthalpy a tenth or three tenths of the way across the jump of <c>H2O(L)</c> at its upper bound, 600 K (30 MPa,
    /// AP/HTPB/Al), lies below every state of the table at or above 160 K: the tp state at 160 K, the lowest of the window, has
    /// a higher enthalpy than the target. The bracket converges on the gap, the scan of the far sides of the floors below finds no
    /// probe above the target, and the case ends <c>TemperatureOutOfRange</c> with a zero state, never <c>NotConverged</c>.
    /// </summary>
    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    public void AnEnthalpyInsideTheJumpAtTheLiquidsUpperBoundWithNoStateBelowEndsTemperatureOutOfRange(double fraction)
    {
        const double pressure = 3.0e7;
        var c = HostSolver.Load("tp", "ap-htpb-al_pc7MPa_T430");
        var table = HostSolver.BuildTable(CpuFixture.Shared.Database, c);
        var moles = HostSolver.ElementMolesOf(c);
        var accelerator = CpuFixture.Shared.Accelerator;
        EquilibriumCase Tp(double temperature) => new(table, ProblemKind.AssignedTemperaturePressure, pressure, temperature, 0.0, moles);
        var below = HostSolver.Solve(accelerator, Tp(599.999));
        var above = HostSolver.Solve(accelerator, Tp(600.001));
        var lowest = HostSolver.Solve(accelerator, Tp(160.0));
        var target = below.State.Enthalpy + fraction * (above.State.Enthalpy - below.State.Enthalpy);
        Assert.True(above.State.Enthalpy - below.State.Enthalpy > 1.0e5, $"jump {above.State.Enthalpy - below.State.Enthalpy} J/kg");
        Assert.True(lowest.State.Enthalpy > target, $"the state at 160 K, {lowest.State.Enthalpy}, is not above the target {target}");

        var solution = HostSolver.Solve(accelerator, new EquilibriumCase(table, ProblemKind.AssignedEnthalpyPressure, pressure, 0.0, target, moles));

        Assert.True(solution.Status == CaseStatus.TemperatureOutOfRange, $"status {solution.Status} after {solution.Iterations} iterations");
        Assert.Equal(default, solution.State);
    }

    /// <summary>The melting temperature of a gasless system at a pressure by bisection on the condensed set, with the tp states a hair below and above it.</summary>
    private static (double Temperature, HostSolution Solid, HostSolution Liquid) MeltingOf(GaslessSystem system, double pressure)
    {
        const double hair = 1.0e-6;
        var accelerator = CpuFixture.Shared.Accelerator;
        HostSolution At(double temperature) => HostSolver.Solve(accelerator, system.Case(temperature, pressure, 0.0));
        var low = 600.0;
        var high = 1500.0;
        for (var step = 0; step < 60; step++)
        {
            var middle = 0.5 * (low + high);
            if (UnivariantRig.CondensedOf(At(middle)).Contains("KO2(L)", StringComparison.Ordinal))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        var melting = 0.5 * (low + high);
        return (melting, At(melting - hair), At(melting + hair));
    }
}
