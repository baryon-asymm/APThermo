using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>L1: frozen mode against the frozen stations of the reference rocket cases and against the equilibrium solve itself.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class FrozenModeTests
{
    /// <summary>
    /// Station outputs not compared: the reference computes no Cv at a frozen station (it prints zero or the previous station's
    /// value; see the fixtures node), its equilibrium Cp is the frozen one there (compared separately), and the flow speed is
    /// the performance node's.
    /// </summary>
    private static readonly HashSet<string> NotFrozenFields = ["cpEquilibrium", "cvEquilibrium", "cvFrozen", "mach"];

    /// <summary>The rocket fixtures whose flow is frozen somewhere.</summary>
    public static TheoryData<string> FrozenRocketCases()
    {
        var data = new TheoryData<string>();
        foreach (var path in FixtureFiles.Enumerate("rocket"))
        {
            var c = CeaFixtures.Load(path);
            if (c.Outputs.GetProperty("stations").EnumerateArray().Any(s => s.GetProperty("frozen").GetBoolean()))
            {
                data.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        return data;
    }

    /// <summary>The three equilibrium cases the self-consistency tests below are run over, one of each problem kind.</summary>
    public static TheoryData<string, string> SelfConsistencyCases() => new()
    {
        { "tp", "rp1311-example1_r1.0_p1.0atm_T3000" },
        { "hp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber" },
        { "sp", "nto-udmh_of2.2_pc2MPa_shiftingEquilibrium_exit2" },
    };

    /// <summary>Frozen stations of the reference are reproduced from the frozen composition.</summary>
    [Theory]
    [MemberData(nameof(FrozenRocketCases))]
    public void FrozenStationsOfTheReferenceAreReproducedFromTheFrozenComposition(string name)
    {
        var c = CeaFixtures.Load(Path.Combine(FixtureFiles.Root, "rocket", name + ".json"));
        var stations = c.Outputs.GetProperty("stations").EnumerateArray().ToList();
        var source = stations.Last(s => !s.GetProperty("frozen").GetBoolean());
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, HostSolver.ElementsOf(c), HostSolver.ProductsOf(c));

        // The composition is frozen at the last equilibrium station: moles per kilogram from its mole fractions and M = 1/n.
        var totalMoles = 1.0 / source.GetProperty("molarMass").GetDouble();
        var moles = new double[table.SpeciesCount];
        foreach (var species in source.GetProperty("moleFractions").EnumerateObject())
        {
            var indices = table.IndicesOf(species.Name);
            Assert.True(indices.Count > 0, $"{species.Name} is not in the table");
            Assert.True(indices[0] < table.GasCount || species.Value.GetDouble() == 0.0, "a condensed species in the frozen composition is outside this test");
            moles[indices[0]] = species.Value.GetDouble() * totalMoles;
        }

        var entropy = source.GetProperty("entropy").GetDouble();
        var elementMoles = HostSolver.ElementMolesOf(c);
        var mismatches = new List<string>();
        var compared = 0;
        foreach (var station in stations.Where(s => s.GetProperty("frozen").GetBoolean()))
        {
            var label = station.GetProperty("station").GetString();
            var expansion = new EquilibriumCase(table, ProblemKind.AssignedEntropyPressure,
                                                Pressure: station.GetProperty("pressure").GetDouble(),
                                                Temperature: 0.0, Target: entropy, ElementMoles: elementMoles);
            var solution = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, expansion, moles);
            Assert.True(solution.Status == CaseStatus.Ok, $"{label}: status {solution.Status}");
            compared += CompareStation(station, solution, label, mismatches);
        }

        Assert.True(compared > 0, "no frozen station field was compared");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }

    /// <summary>Frozen mode at the equilibrium composition recovers the equilibrium state.</summary>
    [Theory]
    [MemberData(nameof(SelfConsistencyCases))]
    public void FrozenModeAtTheEquilibriumCompositionRecoversTheEquilibriumState(string kind, string name)
    {
        var (equilibrium, frozen) = FrozenAtEquilibrium(kind, name);
        foreach (var state in frozen)
        {
            Assert.Equal(equilibrium.Temperature, state.Temperature, equilibrium.Temperature * Tolerances.SelfConsistency);
            Assert.Equal(equilibrium.Enthalpy, state.Enthalpy, Math.Abs(equilibrium.Enthalpy) * Tolerances.SelfConsistency + Tolerances.EnthalpyFloor);
            Assert.Equal(equilibrium.Entropy, state.Entropy, equilibrium.Entropy * Tolerances.SelfConsistency);
            Assert.Equal(equilibrium.CpFrozen, state.CpFrozen, equilibrium.CpFrozen * Tolerances.SelfConsistency);
            Assert.Equal(equilibrium.CvFrozen, state.CvFrozen, equilibrium.CvFrozen * Tolerances.SelfConsistency);
            Assert.Equal(equilibrium.MolarMass, state.MolarMass, equilibrium.MolarMass * Tolerances.Exact);
        }
    }

    /// <summary>A frozen state reports the frozen heat capacities as the equilibrium ones.</summary>
    [Theory]
    [MemberData(nameof(SelfConsistencyCases))]
    public void AFrozenStateReportsTheFrozenHeatCapacitiesAsTheEquilibriumOnes(string kind, string name)
    {
        var (_, frozen) = FrozenAtEquilibrium(kind, name);
        foreach (var state in frozen)
        {
            Assert.Equal(state.CpFrozen, state.CpEquilibrium);
            Assert.Equal(state.CvFrozen, state.CvEquilibrium);
        }
    }

    /// <summary>A frozen state carries the ideal gas derivatives.</summary>
    [Theory]
    [MemberData(nameof(SelfConsistencyCases))]
    public void AFrozenStateCarriesTheIdealGasDerivatives(string kind, string name)
    {
        var (_, frozen) = FrozenAtEquilibrium(kind, name);
        foreach (var state in frozen)
        {
            Assert.Equal(1.0, state.DlnVdlnT);
            Assert.Equal(-1.0, state.DlnVdlnP);
            Assert.Equal(state.CpFrozen / state.CvFrozen, state.GammaS, Tolerances.Exact);
        }
    }

    /// <summary>Every field of one frozen station the reference settles, against the tolerance table; returns how many were compared.</summary>
    private static int CompareStation(System.Text.Json.JsonElement station, HostSolution solution, string? label, List<string> mismatches)
    {
        var compared = 0;
        foreach (var (field, expected, info) in StateComparison.StateFields(station, strict: false))
        {
            if (NotFrozenFields.Contains(field))
            {
                continue;
            }

            var actual = (double)info.GetValue(solution.State)!;
            if (!CpuFixture.Shared.Tolerances.Matches(field, expected, actual))
            {
                mismatches.Add($"{label} {field}: reference {expected:R}, tree {actual:R}");
            }

            compared++;
        }

        // In frozen flow the reference's equilibrium heat capacity is the frozen one.
        var cpEquilibrium = station.GetProperty("cpEquilibrium").GetDouble();
        if (!CpuFixture.Shared.Tolerances.Matches("cpEquilibrium", cpEquilibrium, solution.State.CpEquilibrium))
        {
            mismatches.Add($"{label} cpEquilibrium: reference {cpEquilibrium:R}, tree {solution.State.CpEquilibrium:R}");
        }

        return compared;
    }

    /// <summary>
    /// One equilibrium solve of the fixture case, then the same composition held frozen and solved for its temperature three
    /// ways: from the enthalpy, from the entropy, and at the temperature itself. All four states describe one point.
    /// </summary>
    private static (MixtureState Equilibrium, MixtureState[] Frozen) FrozenAtEquilibrium(string kind, string name)
    {
        var c = HostSolver.Load(kind, name);
        var equilibrium = HostSolver.Solve(CpuFixture.Shared, c);
        Assert.Equal(CaseStatus.Ok, equilibrium.Status);
        var held = HostSolver.Of(equilibrium.Case.Table, c);
        var state = equilibrium.State;
        var byEnthalpy = held with { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = state.Pressure, Temperature = 0.0, Target = state.Enthalpy };
        var byEntropy = held with { Kind = ProblemKind.AssignedEntropyPressure, Pressure = state.Pressure, Temperature = 0.0, Target = state.Entropy };
        var atTemperature = held with { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = state.Pressure, Temperature = state.Temperature, Target = 0.0 };

        var frozen = new MixtureState[3];
        var cases = new[] { byEnthalpy, byEntropy, atTemperature };
        for (var i = 0; i < cases.Length; i++)
        {
            var solution = HostSolver.SolveFrozen(CpuFixture.Shared.Accelerator, cases[i], equilibrium.Moles);
            Assert.Equal(CaseStatus.Ok, solution.Status);
            frozen[i] = solution.State;
        }

        return (state, frozen);
    }
}
