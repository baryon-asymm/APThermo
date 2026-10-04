using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>L1 and L2: every tp, hp and sp fixture through the library, singly from its propellant and as state records in batches over unions of elements.</summary>
[Collection("solver")]
public sealed class EquilibriumTests
{
    /// <summary>The theory data of assigned-temperature, assigned-enthalpy and assigned-entropy fixture names for <paramref name="kind"/>.</summary>
    public static TheoryData<string> Cases(string kind) => FixtureCases.Names(kind);

    /// <summary>Assigned temperature cases reproduce the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "tp")]
    public void AssignedTemperatureCasesReproduceTheReference(string name) => Check("tp", name);

    /// <summary>Assigned enthalpy cases reproduce the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "hp")]
    public void AssignedEnthalpyCasesReproduceTheReference(string name) => Check("hp", name);

    /// <summary>Assigned entropy cases reproduce the reference.</summary>
    [Theory]
    [MemberData(nameof(Cases), "sp")]
    public void AssignedEntropyCasesReproduceTheReference(string name) => Check("sp", name);

    private static void Check(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        Assert.True(c.Outputs.GetProperty("converged").GetBoolean(), "the reference case did not converge; it cannot serve as a reference");
        var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
        var problem = FixtureCases.EquilibriumProblemOf(c);
        var result = SolverFixture.Shared.Solver.Solve(propellant, problem);
        Assert.True(result.Status == CaseStatus.Ok, $"status {result.Status}");
        Assert.Equal("state", result.State.Name);
        Assert.Null(result.State.Performance);
        Assert.Same(propellant, result.Propellant);
        var caveats = new StationCaveats(Transport: false, Frozen: false, SingularReference: ReferenceCaveats.SingularTp(c));
        var mismatches = ReferenceComparison.Compare(c.Outputs, result.State, SpeciesList.Of(result.Species), name, SolverFixture.Shared.Tolerances, caveats).ToList();
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }

    private static readonly string[] AssignedKinds = ["tp", "hp", "sp"];

    /// <summary>State batches over the union of elements reproduce the reference.</summary>
    [Fact]
    public void StateBatchesOverTheUnionOfElementsReproduceTheReference()
    {
        // Every tp, hp and sp SolverFixture.Shared as a state record, one batch per (omit, only) pair: the union of elements makes most records lack some.
        var cases = AssignedKinds.SelectMany(kind => FixtureFiles.Enumerate(kind).Select(CeaFixtures.Load)).ToList();
        var mismatches = new List<string>();
        var lackingRecords = 0;
        var solved = 0;
        foreach (var group in cases.GroupBy(c => string.Join(",", FixtureCases.OmitOf(c)) + "|" + string.Join(",", FixtureCases.OnlyOf(c) ?? ["*"]), StringComparer.Ordinal))
        {
            var members = group.ToList();
            var records = members.Select(FixtureCases.StateRecordOf).ToList();
            var results = SolverFixture.Shared.Solver.SolveStates(records, new StateBatchOptions(Omit: FixtureCases.OmitOf(members[0]), Only: FixtureCases.OnlyOf(members[0])));
            Assert.Equal(records.Count, results.Count);
            var union = members.SelectMany(c => FixtureCases.ElementMolesOf(c).Keys).Distinct(StringComparer.Ordinal).Count();
            for (var k = 0; k < members.Count; k++)
            {
                var c = members[k];
                if (results[k].Mixture.Elements.Count < union)
                {
                    lackingRecords++;
                }

                if (results[k].Status != CaseStatus.Ok)
                {
                    mismatches.Add($"{c.Name}: status {results[k].Status}");
                    continue;
                }

                Assert.Null(results[k].Propellant);
                var caveats = new StationCaveats(Transport: false, Frozen: false, SingularReference: ReferenceCaveats.SingularTp(c));
                mismatches.AddRange(ReferenceComparison.Compare(c.Outputs, results[k].State, SpeciesList.Of(results[k].Species), c.Name, SolverFixture.Shared.Tolerances, caveats));
                solved++;
            }
        }

        Assert.True(solved > 100, $"only {solved} records solved");
        Assert.True(lackingRecords > 0, "no record lacked an element of its batch; the absent-element path was not exercised");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches.Take(40)));
    }

    /// <summary>The default enthalpy of an assigned enthalpy problem is the propellants.</summary>
    [Fact]
    public void TheDefaultEnthalpyOfAnAssignedEnthalpyProblemIsThePropellants()
    {
        var c = FixtureCases.Load("rocket", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
        var chamber = FixtureCases.ReferenceStationsOf(c)[0];
        var result = SolverFixture.Shared.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = c.Inputs.GetProperty("chamberPressure").GetDouble() });
        Assert.Equal(CaseStatus.Ok, result.Status);
        foreach (var field in new[] { "temperature", "enthalpy", "molarMass", "entropy" })
        {
            var expected = chamber.GetProperty(field).GetDouble();
            var actual = field switch
            {
                "temperature" => result.State.State.Temperature,
                "enthalpy" => result.State.State.Enthalpy,
                "molarMass" => result.State.State.MolarMass,
                _ => result.State.State.Entropy,
            };
            Assert.True(SolverFixture.Shared.Tolerances.Matches(field, expected, actual), $"{field}: reference {expected:R}, tree {actual:R}");
        }
    }

    /// <summary>Transport figures are attached to an equilibrium state when requested.</summary>
    [Fact]
    public void TransportFiguresAreAttachedToAnEquilibriumStateWhenRequested()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(SolverFixture.Shared.Database, c);
        var chamber = FixtureCases.ReferenceStationsOf(c)[0];
        var pressure = c.Inputs.GetProperty("chamberPressure").GetDouble();
        var result = SolverFixture.Shared.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure, Transport = true });
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.Equal(CaseStatus.Ok, result.State.TransportStatus);
        var figures = Assert.NotNull(result.State.Transport);
        foreach (var (field, actual) in new[] { ("viscosity", figures.Viscosity), ("frozenConductivity", figures.FrozenConductivity), ("frozenPrandtl", figures.FrozenPrandtl) })
        {
            var expected = chamber.GetProperty(field).GetDouble();
            Assert.True(SolverFixture.Shared.Tolerances.Matches(field, expected, actual), $"{field}: reference {expected:R}, tree {actual:R}");
        }

        var without = SolverFixture.Shared.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure });
        Assert.Null(without.State.Transport);
        Assert.Null(without.State.TransportStatus);
        Assert.Empty(StationEquality.BitDifferences(without.State with { Transport = null, TransportStatus = null }, result.State with { Transport = null, TransportStatus = null }, "state"));
    }

    /// <summary>
    /// Potassium superoxide at 500 K and 1 bar (K:O = 1:2, one kilogram): the equilibrium holds no gas phase, which the
    /// front door reports as the status <c>NoGasPhase</c> and not as an exception. The station's state holds the temperature
    /// and the pressure only, every other field zero; the mole fractions and the condensed mass fractions carry the
    /// condensed composition (the mole fractions sum to one, every gas zero, the condensed masses to the kilogram), and no
    /// transport is attached. Red on the code before the verdict: <c>NotConverged</c>.
    /// </summary>
    [Fact]
    public void ACompositionWithNoGasPhaseIsAStatusWithATemperatureAndAPressureOnly()
    {
        var database = SolverFixture.Shared.Database;
        var perKilogram = 1000.0 / (database.AtomicWeight("K") + 2.0 * database.AtomicWeight("O"));
        var composition = new Dictionary<string, double>(StringComparer.Ordinal) { ["K"] = perKilogram, ["O"] = 2.0 * perKilogram };
        const double pressure = 1.0e5;
        const double temperature = 500.0;

        var result = Assert.Single(SolverFixture.Shared.Solver.SolveStates(
            [new StateRecord(pressure, composition, Temperature: temperature)], new StateBatchOptions(Transport: true)));

        Assert.Equal(CaseStatus.NoGasPhase, result.Status);
        var station = result.State;
        Assert.Equal(CaseStatus.NoGasPhase, station.Status);
        Assert.Equal(new MixtureState { Temperature = temperature, Pressure = pressure }, station.State);
        Assert.Null(station.Transport);
        Assert.Null(station.TransportStatus);
        Assert.Equal(1.0, station.MoleFractions.Values.Sum(), 12);
        var gases = result.Species.Where(name => !station.CondensedMassFractions.ContainsKey(name));
        Assert.All(gases, name => Assert.Equal(0.0, station.MoleFractions[name]));
        Assert.Equal(1.0, station.CondensedMassFractions.Values.Sum(), 3);
    }
}
