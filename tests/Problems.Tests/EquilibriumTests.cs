using APThermo.Equilibrium;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>L1 and L2: every tp, hp and sp fixture through the library, singly from its propellant and as state records in batches over unions of elements.</summary>
[Collection(SolverCollection.Name)]
public sealed class EquilibriumTests(SolverFixture fixture)
{
    public static IEnumerable<object[]> Cases(string kind) => FixtureCases.Names(kind);

    [Theory]
    [MemberData(nameof(Cases), "tp")]
    public void Assigned_temperature_cases_reproduce_the_reference(string name) => Check("tp", name);

    [Theory]
    [MemberData(nameof(Cases), "hp")]
    public void Assigned_enthalpy_cases_reproduce_the_reference(string name) => Check("hp", name);

    [Theory]
    [MemberData(nameof(Cases), "sp")]
    public void Assigned_entropy_cases_reproduce_the_reference(string name) => Check("sp", name);

    private void Check(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        Assert.True(c.Outputs.GetProperty("converged").GetBoolean(), "the reference case did not converge; it cannot serve as a reference");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problem = FixtureCases.EquilibriumProblemOf(c);
        var result = fixture.Solver.Solve(propellant, problem);
        Assert.True(result.Status == CaseStatus.Ok, $"status {result.Status}");
        Assert.Equal("state", result.State.Name);
        Assert.Null(result.State.Performance);
        Assert.Same(propellant, result.Propellant);
        var caveats = new StationCaveats(Transport: false, Frozen: false, SingularReference: ReferenceCaveats.SingularTp(c));
        var mismatches = ReferenceComparison.Compare(c.Outputs, result.State, SpeciesList.Of(result.Species), name, fixture.Tolerances, caveats).ToList();
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
    }

    [Fact]
    public void State_batches_over_the_union_of_elements_reproduce_the_reference()
    {
        // Every tp, hp and sp fixture as a state record, one batch per (omit, only) pair: the union of elements makes most records lack some.
        var cases = new[] { "tp", "hp", "sp" }.SelectMany(kind => FixtureFiles.Enumerate(kind).Select(CeaFixtures.Load)).ToList();
        var mismatches = new List<string>();
        var lackingRecords = 0;
        var solved = 0;
        foreach (var group in cases.GroupBy(c => string.Join(",", FixtureCases.OmitOf(c)) + "|" + string.Join(",", FixtureCases.OnlyOf(c) ?? ["*"]), StringComparer.Ordinal))
        {
            var members = group.ToList();
            var records = members.Select(FixtureCases.StateRecordOf).ToList();
            var results = fixture.Solver.SolveStates(records, new StateBatchOptions(Omit: FixtureCases.OmitOf(members[0]), Only: FixtureCases.OnlyOf(members[0])));
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
                mismatches.AddRange(ReferenceComparison.Compare(c.Outputs, results[k].State, SpeciesList.Of(results[k].Species), c.Name, fixture.Tolerances, caveats));
                solved++;
            }
        }

        Assert.True(solved > 100, $"only {solved} records solved");
        Assert.True(lackingRecords > 0, "no record lacked an element of its batch; the absent-element path was not exercised");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches.Take(40)));
    }

    [Fact]
    public void The_default_enthalpy_of_an_assigned_enthalpy_problem_is_the_propellants()
    {
        var c = FixtureCases.Load("rocket", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var chamber = FixtureCases.ReferenceStationsOf(c)[0];
        var result = fixture.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = c.Inputs.GetProperty("chamberPressure").GetDouble() });
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
            Assert.True(fixture.Tolerances.Matches(field, expected, actual), $"{field}: reference {expected:R}, tree {actual:R}");
        }
    }

    [Fact]
    public void Transport_figures_are_attached_to_an_equilibrium_state_when_requested()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var chamber = FixtureCases.ReferenceStationsOf(c)[0];
        var pressure = c.Inputs.GetProperty("chamberPressure").GetDouble();
        var result = fixture.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure, Transport = true });
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.Equal(CaseStatus.Ok, result.State.TransportStatus);
        var figures = Assert.NotNull(result.State.Transport);
        foreach (var (field, actual) in new[] { ("viscosity", figures.Viscosity), ("frozenConductivity", figures.FrozenConductivity), ("frozenPrandtl", figures.FrozenPrandtl) })
        {
            var expected = chamber.GetProperty(field).GetDouble();
            Assert.True(fixture.Tolerances.Matches(field, expected, actual), $"{field}: reference {expected:R}, tree {actual:R}");
        }

        var without = fixture.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure });
        Assert.Null(without.State.Transport);
        Assert.Null(without.State.TransportStatus);
        Assert.Empty(StationEquality.BitDifferences(without.State with { Transport = null, TransportStatus = null }, result.State with { Transport = null, TransportStatus = null }, "state"));
    }
}
