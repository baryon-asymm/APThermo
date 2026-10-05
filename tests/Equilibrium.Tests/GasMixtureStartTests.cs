using System.Globalization;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The mixture column of the gas basis (TraceGas BOOT.md, "The starts", the fifth, 2026-10-05): CaCO3 with a deficit of oxygen is gasless,
/// CaCO3(cr) + CaO(cr) + C(gr), until the tangent-plane sum of its gas reaches one at T_t, and then CaCO3(cr) + CaO(cr) under a gas of CO and
/// CO2. Above T_t, until CO alone reaches unit fraction at the gasless vertex, the program with the gases as columns holds no gas (every gas
/// is below unit fraction, their mixture above it), the gas basis was skipped, and every state of that band ended <c>NotConverged</c>. The
/// mixture at the vertex enters as one column: the ratio test sends C(gr) out, and the states end <c>Ok</c>.
/// </summary>
[Collection(CpuFixture.CollectionName)]
public sealed class GasMixtureStartTests
{
    private static readonly string[] CalciteElements = ["CA", "C", "O"];

    /// <summary>
    /// States of the band, each <c>NotConverged</c> on c2aa1227: CaCO3 − 1e-7 O at 100 Pa, 1 kPa, 2 kPa and 5 kPa, − 3e-8 O at 100 kPa and
    /// 10 MPa (as (deficit, pressure, temperature)); NaCl − 1e-6 Cl at 100 kPa and 1 160 K, whose gas is a mixture of Na and NaCl, the one
    /// state of another system the transition scan found.
    /// </summary>
    public static TheoryData<string> BandStates() => TraceGasCases.Names(Band());

    /// <summary>The states of <see cref="BandStates"/> as cases, which <c>NoHiddenStateTests</c> also runs.</summary>
    internal static IEnumerable<TraceGasCase> Band()
    {
        var states = new List<TraceGasCase>();
        foreach (var (deficit, pressure, temperature) in new (double, double, double)[]
        {
            (1.0e-7, 1.0e3, 849.0), (1.0e-7, 1.0e3, 850.0), (1.0e-7, 1.0e3, 851.0), (1.0e-7, 1.0e3, 852.0), (1.0e-7, 1.0e3, 853.0), (1.0e-7, 1.0e3, 854.0),
            (1.0e-7, 1.0e2, 776.0), (1.0e-7, 1.0e2, 778.0), (1.0e-7, 1.0e2, 780.0), (1.0e-7, 2.0e3, 874.0), (1.0e-7, 2.0e3, 878.0),
            (1.0e-7, 5.0e3, 910.0), (3.0e-8, 1.0e5, 1050.0), (3.0e-8, 1.0e7, 1390.0),
        })
        {
            states.Add(Calcite(deficit, pressure, temperature));
        }

        states.Add(new TraceGasCase("NaCl|-1E-06|100000|1160", ["NA", "CL"], [1.0, 1.0 * (1.0 - 1.0e-6)], 1.0e5, 1160.0));
        return states;
    }

    /// <summary>Each state of the band ends <c>Ok</c> and clear; a calcite state holds CaCO3(cr) and CaO(cr) and no graphite.</summary>
    [Theory]
    [MemberData(nameof(BandStates))]
    public void AStateOfTheBandIsOkAndClearFromTheMixtureColumn(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var solution = TraceGasCases.Named(name).Solve();
        TraceGasChecks.AssertOkAndClear(solution, name);
        if (name.StartsWith("CaCO3", StringComparison.Ordinal))
        {
            Assert.Equal(["CaCO3(cr)", "CaO(cr)"], CondensedOf(solution));
        }
    }

    /// <summary>
    /// CaCO3 − 1e-7 O at 1 kPa from 2 K below T_t to 7 K above it, every 0.25 K, T_t computed here from the table (the temperature at which
    /// ln S of the gases at the multipliers of CaCO3(cr), CaO(cr) and C(gr) is zero): below T_t every state is <c>NoGasPhase</c> and clear of
    /// the gasless conditions, above it every state is <c>Ok</c> and clear. Red on c2aa1227: the states from T_t to 6.1 K above it end
    /// <c>NotConverged</c>.
    /// </summary>
    [Fact]
    public void TheGaslessAssemblageGivesWayToTheGasWhereItsTangentPlaneSumReachesOne()
    {
        var transition = TransitionTemperature(1.0e3);
        Assert.InRange(transition, 840.0, 860.0);
        var problems = new List<string>();
        for (var k = -8; k <= 28; k++)
        {
            var temperature = transition + 0.25 * k + 0.125;
            var state = Calcite(1.0e-7, 1.0e3, temperature);
            var solution = state.Solve();
            var expected = temperature < transition ? CaseStatus.NoGasPhase : CaseStatus.Ok;
            if (solution.Status != expected)
            {
                problems.Add($"{state.Name}: {solution.Status}, expected {expected}");
                continue;
            }

            problems.AddRange(ViolationsOf(solution).Select(violation => $"{state.Name}: {violation}"));
        }

        Assert.True(problems.Count == 0, $"T_t {transition:R} K; {problems.Count} problems:\n" + string.Join("\n", problems.Take(40)));
    }

    /// <summary>
    /// Why the gas basis needs the mixture: at 850 K and 1 kPa the program with the gases as columns holds CaCO3(cr), CaO(cr) and C(gr) and no
    /// gas, while ln S at the multipliers of those three is above zero (the gas phase lowers the Gibbs energy). A guard of the reason, green on
    /// c2aa1227 too.
    /// </summary>
    [Fact]
    public void InTheBandTheProgramWithTheGasesAsColumnsHoldsNoGasWhileTheirMixtureLowersTheGibbsEnergy()
    {
        var basis = GasPhaseRig.GasBasisOf(Calcite(1.0e-7, 1.0e3, 850.0).AsCase());
        Assert.NotNull(basis);
        Assert.Equal(["C(gr)", "CaCO3(cr)", "CaO(cr)"], basis.Keys.Order(StringComparer.Ordinal));
        Assert.True(LogTangentSumOfTheGaslessVertex(1.0e3, 850.0) > 0.0);
    }

    /// <summary>
    /// The hp and sp states of the band that ended <c>TemperatureOutOfRange</c> on c2aa1227 (the bracket's probes in the band failed) end
    /// <c>Ok</c> and clear at the tp temperature within 1e-9.
    /// </summary>
    [Theory]
    [InlineData(1.0e3, 848.2, "hp-warm5")]
    [InlineData(1.0e3, 848.2, "sp-warm5")]
    [InlineData(1.0e3, 849.2, "hp-warm5")]
    [InlineData(1.0e3, 849.2, "sp-warm5")]
    [InlineData(1.0e2, 775.2, "sp-cold")]
    [InlineData(1.0e2, 777.7, "sp-cold")]
    public void AnHpOrSpTargetInTheBandEndsOkAtItsTpTemperature(double pressure, double temperature, string mode)
    {
        var state = Calcite(1.0e-7, pressure, temperature);
        var tp = state.Solve();
        TraceGasChecks.AssertOkAndClear(tp, state.Name);
        var solution = state.SolveInMode(tp, mode);
        TraceGasChecks.AssertOkAndClear(solution, $"{state.Name} {mode}");
        Assert.True(
            Math.Abs(solution.State.Temperature / temperature - 1.0) <= 1.0e-9,
            $"{state.Name} {mode}: T {solution.State.Temperature:R}");
    }

    private static TraceGasCase Calcite(double deficit, double pressure, double temperature) =>
        new(
            string.Create(CultureInfo.InvariantCulture, $"CaCO3|{-deficit:E0}|{pressure:R}|{temperature:R}"), CalciteElements,
            [1.0, 1.0, 3.0 * (1.0 - deficit)], pressure, temperature);

    /// <summary>The gasless conditions of a <c>NoGasPhase</c> state, the equilibrium conditions with every gas of an <c>Ok</c> one.</summary>
    private static List<string> ViolationsOf(HostSolution solution)
    {
        if (solution.Status == CaseStatus.NoGasPhase)
        {
            return EquilibriumConditions.GaslessViolations(solution);
        }

        var violations = EquilibriumConditions.Violations(solution, Tolerances.EveryGasChemicalPotential);
        violations.AddRange(EquilibriumConditions.EveryGasViolations(solution, Tolerances.EveryGasChemicalPotential));
        return violations;
    }

    private static string[] CondensedOf(HostSolution solution)
    {
        var table = solution.Case.Table;
        return [.. Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount).Where(j => solution.Moles[j] > 0.0).Select(j => table.Species[j]).Order(StringComparer.Ordinal)];
    }

    /// <summary>T_t at <paramref name="pressure"/>: the zero of <see cref="LogTangentSumOfTheGaslessVertex"/> between 700 and 1 500 K, by bisection.</summary>
    private static double TransitionTemperature(double pressure)
    {
        var low = 700.0;
        var high = 1500.0;
        for (var k = 0; k < 100 && high - low > 1.0e-10; k++)
        {
            var middle = 0.5 * (low + high);
            if (LogTangentSumOfTheGaslessVertex(pressure, middle) > 0.0)
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        return 0.5 * (low + high);
    }

    /// <summary>
    /// ln S of every gas at the multipliers that put CaCO3(cr), CaO(cr) and C(gr) on their stationarities (π_Ca + π_C + 3π_O, π_Ca + π_O and
    /// π_C at their g/RT), at the temperature and pressure.
    /// </summary>
    private static double LogTangentSumOfTheGaslessVertex(double pressure, double temperature)
    {
        var table = TraceGasCases.TableOver(CalciteElements);
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;
        double G(string species) => SpeciesFunctions.GOverRT(view, table.IndexOf(species), temperature);
        var carbon = G("C(gr)");
        var oxygen = G("CaCO3(cr)") - G("CaO(cr)") - carbon;
        oxygen /= 2.0;
        var calcium = G("CaO(cr)") - oxygen;
        double[] multipliers = [calcium, carbon, oxygen];
        var logPressure = Math.Log(pressure / 1.0e5);
        var exponents = Enumerable.Range(0, table.GasCount).Select(j =>
            Enumerable.Range(0, table.ElementCount).Sum(i => table.Arrays.Stoichiometry[i * table.SpeciesCount + j] * multipliers[i])
            - SpeciesFunctions.GOverRT(view, j, temperature) - logPressure).ToList();
        var largest = exponents.Max();
        return largest + Math.Log(exponents.Sum(e => Math.Exp(e - largest)));
    }
}
