using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>L1 and L2: every rocket fixture through the library, sweeps against single cases, elemental mixtures against propellants.</summary>
[Collection(SolverCollection.Name)]
public sealed class RocketTests(SolverFixture fixture)
{
    public static IEnumerable<object[]> Cases() => FixtureCases.Names("rocket");

    /// <summary>
    /// When the union of a batch reorders the elements of a case, the linear solves pivot in another order and the iterates differ by
    /// rounding at every step; the converged state then agrees to what the polish threshold guarantees, as between the accelerators.
    /// </summary>
    public const double ReorderedElementsTolerance = 1e-9;

    /// <summary>Below this mole fraction the reordered comparison does not look (the floor of the GPU/CPU table).</summary>
    public const double MoleFractionFloor = 1e-8;

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_rocket_case_reproduces_the_reference_end_to_end(string name)
    {
        var c = FixtureCases.Load("rocket", name);
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problem = FixtureCases.RocketProblemOf(c);
        var result = fixture.Solver.Solve(propellant, problem);
        Assert.True(result.Status == CaseStatus.Ok, $"status {result.Status}; stations [{string.Join(", ", result.Stations.Select(s => s.Status))}]");
        var reference = FixtureCases.ReferenceStationsOf(c);
        Assert.Equal(reference.Count, result.Stations.Count);
        var transport = problem.Transport;
        var gasCount = Comparison.GasCountOf(result.Species);
        var defective = FixtureCases.DefectiveStationsOf(fixture, c, result.Species);
        var mismatches = new List<string>();
        for (var s = 0; s < reference.Count; s++)
        {
            var label = reference[s].GetProperty("station").GetString()!;
            var frozen = reference[s].GetProperty("frozen").GetBoolean();
            mismatches.AddRange(Comparison.Compare(reference[s], result.Stations[s], result.Species, gasCount, transport, frozen, label, fixture.Tolerances, defective.Contains(s)));
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
        Assert.All(result.Stations, s => Assert.Equal(transport, s.Transport.HasValue));
        Assert.Equal(["chamber", "throat"], result.Stations.Take(2).Select(s => s.Name));
        Assert.Equal(FixtureCases.OxidizerToFuelRatioOf(c), result.OxidizerToFuelRatio);
        Assert.Same(propellant, result.Propellant);
    }

    [Fact]
    public void A_sweep_equals_its_cases_solved_one_by_one()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        double[] ratios = [4.0, 5.5, 7.0];
        double[] pressures = [5.0e6, 8.0e6];
        double[] areas = [20.0, 77.5];
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var results = fixture.Solver.Solve(new RocketSweep(propellant, ratios, pressures, areas, FlowModel.ShiftingEquilibrium, Transport: true));
        Assert.Equal(ratios.Length * pressures.Length, results.Count);
        var i = 0;
        foreach (var ratio in ratios)
        {
            var withRatio = FixtureCases.PropellantOf(fixture.Database, c, ratio);
            foreach (var pressure in pressures)
            {
                var one = fixture.Solver.Solve(withRatio, new RocketProblem { ChamberPressure = pressure, AreaRatios = areas, Transport = true });
                Assert.Equal(CaseStatus.Ok, one.Status);
                Assert.Equal(one.Status, results[i].Status);
                Assert.Equal(ratio, results[i].OxidizerToFuelRatio);
                Assert.Equal(one.Mixture.ElementMoles, results[i].Mixture.ElementMoles);
                Assert.Equal(one.Mixture.Enthalpy, results[i].Mixture.Enthalpy);
                Assert.Equal(one.Stations.Count, results[i].Stations.Count);
                for (var s = 0; s < one.Stations.Count; s++)
                {
                    Assert.Empty(Comparison.BitDifferences(one.Stations[s], results[i].Stations[s], $"ratio {ratio} pressure {pressure} station {s}"));
                }

                i++;
            }
        }
    }

    [Fact]
    public void An_elemental_mixture_reproduces_its_propellant_bit_for_bit()
    {
        var c = FixtureCases.Load("rocket", "ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problem = FixtureCases.RocketProblemOf(c);
        var viaPropellant = fixture.Solver.Solve(propellant, problem);
        var mixture = viaPropellant.Mixture;
        var elemental = ElementalMixture.Create(mixture.ElementMoles, mixture.Enthalpy, propellant.Omit, propellant.Only);
        var viaMixture = fixture.Solver.Solve(elemental, problem);
        Assert.Null(viaMixture.Propellant);
        Assert.Null(viaMixture.OxidizerToFuelRatio);
        Assert.Equal(viaPropellant.Species, viaMixture.Species);
        Assert.Equal(viaPropellant.Status, viaMixture.Status);
        for (var s = 0; s < viaPropellant.Stations.Count; s++)
        {
            Assert.Empty(Comparison.BitDifferences(viaPropellant.Stations[s], viaMixture.Stations[s], $"station {s}"));
        }
    }

    [Fact]
    public void Identical_problems_give_identical_results_alone_and_in_one_call()
    {
        var c = FixtureCases.Load("rocket", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problem = FixtureCases.RocketProblemOf(c);
        var first = fixture.Solver.Solve(propellant, problem);
        var second = fixture.Solver.Solve(propellant, problem);
        var both = fixture.Solver.Solve(propellant, [problem, problem]);
        Assert.Equal(2, both.Count);
        foreach (var other in new[] { second, both[0], both[1] })
        {
            Assert.Equal(first.Status, other.Status);
            for (var s = 0; s < first.Stations.Count; s++)
            {
                Assert.Empty(Comparison.BitDifferences(first.Stations[s], other.Stations[s], $"station {s}"));
            }
        }
    }

    [Fact]
    public void Problems_with_different_exit_layouts_are_solved_in_one_call_in_order()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problems = new List<RocketProblem>
        {
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0] },
            new() { ChamberPressure = 7.0e6, PressureRatios = [100.0], AreaRatios = [20.0, 77.5], Flow = FlowModel.FrozenAtThroat },
            new() { ChamberPressure = 5.0e6, AreaRatios = [20.0], Transport = true },
            new() { ChamberPressure = 6.0e6 },
        };
        var results = fixture.Solver.Solve(propellant, problems);
        Assert.Equal([3, 5, 3, 2], results.Select(r => r.Stations.Count));
        Assert.Equal(["chamber", "throat", "exit1", "exit2", "exit3"], results[1].Stations.Select(s => s.Name));
        for (var k = 0; k < problems.Count; k++)
        {
            var single = fixture.Solver.Solve(propellant, problems[k]);
            Assert.Same(problems[k], results[k].Problem);
            Assert.Equal(CaseStatus.Ok, results[k].Status);
            for (var s = 0; s < single.Stations.Count; s++)
            {
                Assert.Empty(Comparison.BitDifferences(single.Stations[s], results[k].Stations[s], $"problem {k} station {s}"));
            }
        }

        Assert.All(results[2].Stations, s => Assert.NotNull(s.Transport));
        Assert.All(results[0].Stations, s => Assert.Null(s.Transport));
    }

    [Fact]
    public void A_failing_station_is_a_status_and_not_an_exception()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var result = fixture.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [0.5], Transport = true });
        Assert.NotEqual(CaseStatus.Ok, result.Status);
        Assert.Equal(CaseStatus.Ok, result.Stations[0].Status);
        Assert.Equal(CaseStatus.Ok, result.Stations[1].Status);
        Assert.Equal(CaseStatus.AreaRatioInvalid, result.Stations[2].Status);
        Assert.NotNull(result.Stations[0].Transport);
        Assert.Null(result.Stations[2].Transport);
        Assert.Null(result.Stations[2].TransportStatus);
    }

    [Fact]
    public void Compositions_are_reported_by_name_over_all_species()
    {
        var c = FixtureCases.Load("rocket", "ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var result = fixture.Solver.Solve(propellant, FixtureCases.RocketProblemOf(c));
        var chamber = result.Stations[0];
        Assert.Equal(result.Species, chamber.MoleFractions.Keys);
        Assert.True(Math.Abs(chamber.MoleFractions.Values.Sum() - 1.0) < 1e-12);
        var condensed = result.Species.Where(s => s.EndsWith(')') && s.Contains('(')).ToList();
        Assert.Equal(condensed, chamber.CondensedMassFractions.Keys);

        // The mass fraction of a condensed species follows from the reference's mole fraction, the record's molar mass and the reference's MW.
        var reference = FixtureCases.ReferenceStationsOf(c)[0];
        var mixtureMolarMass = reference.GetProperty("mixtureMolarMass").GetDouble();
        var present = reference.GetProperty("moleFractions").EnumerateObject().Where(p => condensed.Contains(p.Name) && p.Value.GetDouble() > 0.0).ToList();
        Assert.NotEmpty(present);
        foreach (var species in present)
        {
            var molarMass = fixture.Database[species.Name].MolarMass;
            var expected = species.Value.GetDouble() * molarMass / mixtureMolarMass;
            var actual = chamber.CondensedMassFractions[species.Name];
            var allowed = fixture.Tolerances.For("moleFraction").Absolute * molarMass / mixtureMolarMass + fixture.Tolerances.For("mixtureMolarMass").Relative * expected;
            Assert.True(Math.Abs(actual - expected) <= allowed, $"{species.Name}: mass fraction {actual:R}, from the reference {expected:R}");
        }
    }

    [Fact]
    public void Rocket_and_equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements()
    {
        // Three propellants with different elements, one problem each, in one call: the single solves bit for bit where the union
        // keeps the relative order of the case's elements, to rounding where it reorders them (Problems BOOT.md).
        string[] names = ["lox-lh2_of6_pc7MPa_shiftingEquilibrium", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium", "ap-htpb-al_pc7MPa_shiftingEquilibrium"];
        var cases = names.Select(n => FixtureCases.Load("rocket", n)).ToList();
        var propellants = cases.Select(c => FixtureCases.PropellantOf(fixture.Database, c)).ToList();
        var problems = cases.Select(FixtureCases.RocketProblemOf).ToList();
        var mixtures = propellants.Select(p => fixture.Solver.Mixture(p)).ToList();
        var union = new List<string>();
        foreach (var symbol in mixtures.SelectMany(m => m.Elements))
        {
            if (!union.Contains(symbol, StringComparer.Ordinal))
            {
                union.Add(symbol);
            }
        }

        Assert.Contains(mixtures, m => m.Elements.Count < union.Count);
        static bool OrderKept(IReadOnlyList<string> elements, List<string> union)
        {
            var positions = elements.Select(e => union.IndexOf(e)).ToList();
            return positions.Zip(positions.Skip(1)).All(pair => pair.First < pair.Second);
        }

        Assert.Contains(mixtures, m => OrderKept(m.Elements, union));
        Assert.Contains(mixtures, m => !OrderKept(m.Elements, union));
        var batch = fixture.Solver.Solve(mixtures, problems);
        Assert.Equal(names.Length, batch.Count);
        for (var i = 0; i < names.Length; i++)
        {
            var single = fixture.Solver.Solve(propellants[i], problems[i]);
            Assert.Null(batch[i].Propellant);
            Assert.Equal(single.Status, batch[i].Status);
            Assert.True(batch[i].Species.Count >= single.Species.Count);
            Assert.Equal(single.Stations.Count, batch[i].Stations.Count);
            var kept = OrderKept(mixtures[i].Elements, union);
            for (var s = 0; s < single.Stations.Count; s++)
            {
                var label = $"{names[i]} station {s}";
                List<string> differences;
                if (kept)
                {
                    differences = Comparison.BitDifferences(single.Stations[s], batch[i].Stations[s], label).ToList();
                }
                else
                {
                    differences = Comparison.RelativeDifferences(single.Stations[s], batch[i].Stations[s], ReorderedElementsTolerance, MoleFractionFloor, label).ToList();
                }

                Assert.True(differences.Count == 0, string.Join("; ", differences));
            }
        }

        var hp = new EquilibriumProblem { Kind = Equilibrium.ProblemKind.AssignedEnthalpyPressure, Pressure = 1.0e6 };
        var states = fixture.Solver.Solve(mixtures, Enumerable.Repeat(hp, names.Length).ToList());
        for (var i = 0; i < names.Length; i++)
        {
            var differences = Comparison.RelativeDifferences(fixture.Solver.Solve(propellants[i], hp).State, states[i].State, ReorderedElementsTolerance, MoleFractionFloor, $"{names[i]} state").ToList();
            Assert.True(differences.Count == 0, string.Join("; ", differences));
        }

        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixtures, problems.Take(2).ToList()));
        var otherSelection = ElementalMixture.Create(mixtures[0].ElementMoles, mixtures[0].Enthalpy, ["HO2"]);
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve([mixtures[0], otherSelection], [problems[0], problems[0]]));
    }
}
