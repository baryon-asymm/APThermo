using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>L1 and L2: every rocket fixture through the library, batches against single cases, elemental mixtures against propellants.</summary>
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

    /// <summary>A result's mole fractions are a sum over its own species table; two solves of the tree's own code agree to summation-order rounding.</summary>
    public const double MoleFractionSumTolerance = 1e-12;

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
        var species = SpeciesList.Of(result.Species);
        var defective = FixtureCases.DefectiveStationsOf(fixture, c, result.Species);
        var mismatches = new List<string>();
        for (var s = 0; s < reference.Count; s++)
        {
            var label = reference[s].GetProperty("station").GetString()!;
            var frozen = reference[s].GetProperty("frozen").GetBoolean();
            var caveats = new StationCaveats(Transport: transport, Frozen: frozen, ReferenceDefective: defective.Contains(s));
            mismatches.AddRange(ReferenceComparison.Compare(reference[s], result.Stations[s], species, label, fixture.Tolerances, caveats));
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} mismatches: " + string.Join("; ", mismatches));
        Assert.All(result.Stations, s => Assert.Equal(transport, s.Transport.HasValue));
        Assert.Equal(["chamber", "throat"], result.Stations.Take(2).Select(s => s.Name));
        Assert.Equal(FixtureCases.OxidizerToFuelRatioOf(c), result.OxidizerToFuelRatio);
        Assert.Same(propellant, result.Propellant);
    }

    /// <summary>
    /// The library retired <c>RocketSweep</c> (BOOT.md, F-PR-06): a ratio × chamber-pressure product is a list of mixtures
    /// (one per ratio, built through <see cref="Solver.MixtureOf"/>) with a list of problems (one per pressure), the same
    /// batch mechanism as any other call over a union of mixtures, and it equals its cases solved one by one bit for bit.
    /// </summary>
    [Fact]
    public void A_ratio_and_pressure_product_as_one_batch_equals_its_cases_solved_one_by_one()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        double[] ratios = [4.0, 5.5, 7.0];
        double[] pressures = [5.0e6, 8.0e6];
        double[] areas = [20.0, 77.5];
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var mixtures = new List<ElementalMixture>();
        var problems = new List<RocketProblem>();
        foreach (var ratio in ratios)
        {
            var mixture = fixture.Solver.MixtureOf(propellant, ratio);
            foreach (var pressure in pressures)
            {
                mixtures.Add(mixture);
                problems.Add(new RocketProblem { ChamberPressure = pressure, AreaRatios = areas, Flow = FlowModel.ShiftingEquilibrium, Transport = true });
            }
        }

        var results = fixture.Solver.Solve(mixtures, problems);
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
                Assert.Equal(one.Mixture.ElementMoles, results[i].Mixture.ElementMoles);
                Assert.Equal(one.Mixture.Enthalpy, results[i].Mixture.Enthalpy);
                Assert.Equal(one.Stations.Count, results[i].Stations.Count);
                for (var s = 0; s < one.Stations.Count; s++)
                {
                    Assert.Empty(StationEquality.BitDifferences(one.Stations[s], results[i].Stations[s], $"ratio {ratio} pressure {pressure} station {s}"));
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
            Assert.Empty(StationEquality.BitDifferences(viaPropellant.Stations[s], viaMixture.Stations[s], $"station {s}"));
        }
    }

    /// <summary>A record with exits is a rocket case whose Pressure is the chamber pressure (BOOT.md, F-AR-02): it equals the same mixture and problem solved through the batch over mixtures, bit for bit, transport included.</summary>
    [Fact]
    public void A_state_record_with_exits_equals_its_case_through_the_batch_over_mixtures()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var mixture = fixture.Solver.MixtureOf(propellant);
        var problem = new RocketProblem { ChamberPressure = 7.0e6, PressureRatios = [50.0], AreaRatios = [20.0, 77.5], Flow = FlowModel.FrozenAtThroat, Transport = true };
        var viaMixture = fixture.Solver.Solve(mixture, problem);
        Assert.Equal(CaseStatus.Ok, viaMixture.Status);

        var record = new StateRecord(problem.ChamberPressure, mixture.ElementMoles, Enthalpy: mixture.Enthalpy)
        {
            PressureRatios = problem.PressureRatios,
            AreaRatios = problem.AreaRatios,
            Flow = problem.Flow,
        };
        Assert.True(record.HasExits);
        var viaState = Assert.Single(fixture.Solver.SolveRocketStates([record], new StateBatchOptions(Transport: true)));
        Assert.Equal(CaseStatus.Ok, viaState.Status);
        Assert.Null(viaState.Propellant);
        Assert.Equal(viaMixture.Stations.Count, viaState.Stations.Count);
        for (var s = 0; s < viaMixture.Stations.Count; s++)
        {
            Assert.Empty(StationEquality.BitDifferences(viaMixture.Stations[s], viaState.Stations[s], $"station {s}"));
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
                Assert.Empty(StationEquality.BitDifferences(first.Stations[s], other.Stations[s], $"station {s}"));
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
                Assert.Empty(StationEquality.BitDifferences(single.Stations[s], results[k].Stations[s], $"problem {k} station {s}"));
            }
        }

        Assert.All(results[2].Stations, s => Assert.NotNull(s.Transport));
        Assert.All(results[0].Stations, s => Assert.Null(s.Transport));
    }

    /// <summary>The transport pass is a second pass over the stations of the cases that asked (BOOT.md, F-PR-08): a batch mixing the flag equals every case solved alone, bit for bit, and reports no figures for the cases that did not ask.</summary>
    [Fact]
    public void A_batch_mixing_transport_and_none_equals_each_problem_solved_alone()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problems = new List<RocketProblem>
        {
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0, 77.5], Transport = true },
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0, 77.5], Transport = false },
        };
        var results = fixture.Solver.Solve(propellant, problems);
        for (var k = 0; k < problems.Count; k++)
        {
            var single = fixture.Solver.Solve(propellant, problems[k]);
            Assert.Equal(CaseStatus.Ok, results[k].Status);
            for (var s = 0; s < single.Stations.Count; s++)
            {
                Assert.Empty(StationEquality.BitDifferences(single.Stations[s], results[k].Stations[s], $"problem {k} station {s}"));
            }
        }

        Assert.All(results[0].Stations, s => Assert.NotNull(s.Transport));
        Assert.All(results[0].Stations, s => Assert.Equal(CaseStatus.Ok, s.TransportStatus));
        Assert.All(results[1].Stations, s => Assert.Null(s.Transport));
        Assert.All(results[1].Stations, s => Assert.Null(s.TransportStatus));
    }

    /// <summary>
    /// Rocket cases are grouped by exit layout and, within a layout, by the transport flag (BOOT.md, F-PR-08): a batch whose
    /// four cases interleave two layouts with two transport flags still recombines each case's own station count and own
    /// transport figures, bit for bit against it solved alone — a single-axis grouping would merge cases of different station
    /// counts or run (and then discard) the pass for a case that never asked.
    /// </summary>
    [Fact]
    public void Cases_are_grouped_by_exit_layout_and_transport_flag()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var problems = new List<RocketProblem>
        {
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0], Transport = true },
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0, 77.5], Transport = false },
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0], Transport = false },
            new() { ChamberPressure = 7.0e6, AreaRatios = [20.0, 77.5], Transport = true },
        };
        var results = fixture.Solver.Solve(propellant, problems);
        Assert.Equal(problems.Count, results.Count);
        for (var k = 0; k < problems.Count; k++)
        {
            Assert.Same(problems[k], results[k].Problem);
            Assert.Equal(problems[k].AreaRatios.Count + 2, results[k].Stations.Count);
            Assert.All(results[k].Stations, s => Assert.Equal(problems[k].Transport, s.Transport.HasValue));
            var single = fixture.Solver.Solve(propellant, problems[k]);
            for (var s = 0; s < single.Stations.Count; s++)
            {
                Assert.Empty(StationEquality.BitDifferences(single.Stations[s], results[k].Stations[s], $"problem {k} station {s}"));
            }
        }
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
        Assert.True(Math.Abs(chamber.MoleFractions.Values.Sum() - 1.0) < MoleFractionSumTolerance);
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
    public void Rocket_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements()
    {
        // Three propellants with different elements, one rocket problem each, in one call: the single solves bit for bit where
        // the union keeps the relative order of the case's elements, to rounding where it reorders them (Problems BOOT.md).
        var (names, propellants, problems, mixtures, union) = ThreeMixturesOverTheUnion();
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
                var differences = kept
                    ? StationEquality.BitDifferences(single.Stations[s], batch[i].Stations[s], label).ToList()
                    : StationEquality.RelativeDifferences(single.Stations[s], batch[i].Stations[s], ReorderedElementsTolerance, MoleFractionFloor, label).ToList();
                Assert.True(differences.Count == 0, string.Join("; ", differences));
            }
        }
    }

    [Fact]
    public void Equilibrium_problems_over_several_mixtures_are_one_batch_over_the_union_of_elements()
    {
        var (names, propellants, _, mixtures, _) = ThreeMixturesOverTheUnion();
        var hp = new EquilibriumProblem { Kind = Equilibrium.ProblemKind.AssignedEnthalpyPressure, Pressure = 1.0e6 };
        var states = fixture.Solver.Solve(mixtures, Enumerable.Repeat(hp, names.Length).ToList());
        for (var i = 0; i < names.Length; i++)
        {
            var differences = StationEquality.RelativeDifferences(fixture.Solver.Solve(propellants[i], hp).State, states[i].State, ReorderedElementsTolerance, MoleFractionFloor, $"{names[i]} state").ToList();
            Assert.True(differences.Count == 0, string.Join("; ", differences));
        }
    }

    /// <summary>Three fixture propellants with different elements, one rocket problem each, and their mixtures over the union (asserting the union both keeps and reorders some case's elements, so both comparison paths of the two facts above are exercised).</summary>
    private (string[] Names, List<Propellant> Propellants, List<RocketProblem> Problems, List<ElementalMixture> Mixtures, List<string> Union) ThreeMixturesOverTheUnion()
    {
        string[] names = ["lox-lh2_of6_pc7MPa_shiftingEquilibrium", "lox-rp1_of2.6_pc10MPa_shiftingEquilibrium", "ap-htpb-al_pc7MPa_shiftingEquilibrium"];
        var cases = names.Select(n => FixtureCases.Load("rocket", n)).ToList();
        var propellants = cases.Select(c => FixtureCases.PropellantOf(fixture.Database, c)).ToList();
        var problems = cases.Select(FixtureCases.RocketProblemOf).ToList();
        var mixtures = propellants.Select(p => fixture.Solver.MixtureOf(p)).ToList();
        var union = new List<string>();
        foreach (var symbol in mixtures.SelectMany(m => m.Elements))
        {
            if (!union.Contains(symbol, StringComparer.Ordinal))
            {
                union.Add(symbol);
            }
        }

        Assert.Contains(mixtures, m => m.Elements.Count < union.Count);
        Assert.Contains(mixtures, m => OrderKept(m.Elements, union));
        Assert.Contains(mixtures, m => !OrderKept(m.Elements, union));
        return (names, propellants, problems, mixtures, union);
    }

    private static bool OrderKept(IReadOnlyList<string> elements, List<string> union)
    {
        var positions = elements.Select(e => union.IndexOf(e)).ToList();
        return positions.Zip(positions.Skip(1)).All(pair => pair.First < pair.Second);
    }
}
