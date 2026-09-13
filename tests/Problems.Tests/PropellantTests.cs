using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>L0: element moles, reactant enthalpy, mass normalization and candidate species against every fixture that carries reactants.</summary>
[Collection(SolverCollection.Name)]
public sealed class PropellantTests(SolverFixture fixture)
{
    /// <summary>The BOOT criterion: the same sums as the reference in double precision, differing by rounding only.</summary>
    public const double MixtureTolerance = 1e-10;

    /// <summary>Mass fractions are normalized by one division on both sides.</summary>
    public const double MassFractionTolerance = 1e-12;

    /// <summary>
    /// The reference rounds an oxidizer-to-fuel ratio to single precision before splitting the kilogram (Fixtures BOOT.md: 2.6
    /// becomes 2.5999999046), so its mass fractions carry up to 6e-8 relative of that rounding; the tree splits in double precision.
    /// </summary>
    public const double RatioMassFractionTolerance = 1e-7;

    /// <summary>
    /// How far the fixtures' element moles lie from one kilogram with the database's atomic weights: the reference divides by each reactant
    /// record's molar mass, and the Air record's 28.9651159 kg/kmol against its formula's 28.96561 sets the maximum, 1.6502e-5 on
    /// 2026-09-13 (RP-1311 example 1). The figure the Problems BOOT.md's derivation of the default mass tolerance rests on.
    /// </summary>
    public const double FixtureMassDeviation = 1.7e-5;

    public static IEnumerable<object[]> Cases() => FixtureCases.NamesWithReactants();

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_recorded_element_moles_of_every_fixture_weigh_one_kilogram_within_the_derivation_figure(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var moles = FixtureCases.ElementMolesOf(c).ToDictionary(kv => kv.Key, kv => kv.Value * FixtureCases.KilomolesToMoles, StringComparer.Ordinal);
        var expected = moles.Sum(kv => kv.Value * 1.0e-3 * fixture.Database.AtomicWeight(kv.Key));
        var mass = fixture.Solver.MassOf(ElementalMixture.Create(moles));
        Assert.True(Math.Abs(mass - expected) <= 1e-14 * expected, $"MassOf {mass:R} kg, the sum over the atomic weights {expected:R} kg");
        Assert.True(Math.Abs(mass - 1.0) <= FixtureMassDeviation, $"the recorded element moles weigh {mass:R} kg");
    }

    [Fact]
    public void Results_carry_the_mass_of_their_mixture()
    {
        var c = FixtureCases.Load("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var rocket = fixture.Solver.Solve(propellant, FixtureCases.RocketProblemOf(c));
        Assert.Equal(fixture.Solver.MassOf(rocket.Mixture), rocket.MixtureMass);
        Assert.True(Math.Abs(rocket.MixtureMass - 1.0) <= FixtureMassDeviation, $"{rocket.MixtureMass:R} kg");
        var equilibrium = fixture.Solver.Solve(propellant, new EquilibriumProblem { Pressure = 7.0e6 });
        Assert.Equal(rocket.MixtureMass, equilibrium.MixtureMass);
        var mixture = ElementalMixture.Create(rocket.Mixture.ElementMoles, rocket.Mixture.Enthalpy);
        Assert.Equal(rocket.MixtureMass, fixture.Solver.Solve(mixture, new EquilibriumProblem { Pressure = 7.0e6 }).MixtureMass);
        Assert.Equal(rocket.MixtureMass, fixture.Solver.Solve(mixture, FixtureCases.RocketProblemOf(c)).MixtureMass);
        Assert.Equal(rocket.MixtureMass, fixture.Solver.SolveStates([new StateRecord(7.0e6, mixture.ElementMoles, Enthalpy: mixture.Enthalpy)])[0].MixtureMass);

        // The figure is the measured one, not one kilogram: the same mixture made 0.5 % heavy, within the default tolerance, reports 1.005.
        var heavy = ElementalMixture.Create(rocket.Mixture.ElementMoles.ToDictionary(kv => kv.Key, kv => kv.Value * 1.005, StringComparer.Ordinal), rocket.Mixture.Enthalpy);
        var expected = fixture.Solver.MassOf(heavy);
        Assert.True(Math.Abs(expected - 1.005 * rocket.MixtureMass) <= 1e-12, $"{expected:R} kg");
        Assert.Equal(expected, fixture.Solver.Solve(heavy, FixtureCases.RocketProblemOf(c)).MixtureMass);
        Assert.Equal(expected, fixture.Solver.Solve(heavy, new EquilibriumProblem { Pressure = 7.0e6 }).MixtureMass);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Element_moles_and_enthalpy_equal_the_reference_from_its_mass_fractions(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var propellant = FixtureCases.PropellantOf(fixture.Database, c, byMassFractions: true);
        Assert.IsType<MixtureSpecification.MassFractions>(propellant.Mixture);
        var fractions = propellant.MassFractionsFor(null);
        var massFractions = FixtureCases.ReactantMassFractionsOf(c);
        for (var k = 0; k < propellant.Reactants.Count; k++)
        {
            var reference = massFractions[propellant.Reactants[k].Name];
            Assert.True(Math.Abs(fractions[k] - reference) <= MassFractionTolerance, $"{propellant.Reactants[k].Name}: mass fraction reference {reference:R}, tree {fractions[k]:R}");
        }

        var mixture = fixture.Solver.Mixture(propellant);
        var expected = FixtureCases.ElementMolesOf(c);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), mixture.Elements.Order(StringComparer.Ordinal));
        foreach (var (symbol, kilomoles) in expected)
        {
            var actual = mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles;
            Assert.True(Math.Abs(actual - kilomoles) <= MixtureTolerance * Math.Abs(kilomoles), $"{symbol}: reference {kilomoles:R}, tree {actual:R}");
        }

        double? referenceEnthalpy = kind == "rocket" ? c.Inputs.GetProperty("reactantEnthalpy").GetDouble()
            : kind == "hp" && !c.Inputs.TryGetProperty("derivedFrom", out _) ? c.Inputs.GetProperty("enthalpy").GetDouble()
            : null;
        if (referenceEnthalpy is { } h)
        {
            Assert.NotNull(mixture.Enthalpy);
            Assert.True(Math.Abs(mixture.Enthalpy.Value - h) <= MixtureTolerance * Math.Abs(h), $"enthalpy: reference {h:R}, tree {mixture.Enthalpy:R}");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void A_ratio_split_reproduces_the_reference_mass_fractions_within_its_single_precision(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        if (FixtureCases.OxidizerToFuelRatioOf(c) is not { } ratio)
        {
            return;   // given by mass fractions; the other theory covers it
        }

        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        Assert.Equal(ratio, propellant.OxidizerToFuelRatio);
        var fractions = propellant.MassFractionsFor(null);
        var massFractions = FixtureCases.ReactantMassFractionsOf(c);
        for (var k = 0; k < propellant.Reactants.Count; k++)
        {
            var reference = massFractions[propellant.Reactants[k].Name];
            Assert.True(Math.Abs(fractions[k] - reference) <= RatioMassFractionTolerance * reference, $"{propellant.Reactants[k].Name}: mass fraction reference {reference:R}, tree {fractions[k]:R}");
        }

        var mixture = fixture.Solver.Mixture(propellant);
        foreach (var (symbol, kilomoles) in FixtureCases.ElementMolesOf(c))
        {
            var actual = mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles;
            Assert.True(Math.Abs(actual - kilomoles) <= RatioMassFractionTolerance * Math.Abs(kilomoles), $"{symbol}: reference {kilomoles:R}, tree {actual:R}");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Candidate_species_equal_the_reference_product_list(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var products = FixtureCases.ProductsOf(c);
        var elements = FixtureCases.ElementMolesOf(c).Keys.ToList();
        var candidates = fixture.Solver.CandidateSpecies(elements, FixtureCases.OmitOf(c), FixtureCases.OnlyOf(c));
        var missing = products.Except(candidates, StringComparer.Ordinal).ToList();
        var extra = candidates.Except(products, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
                    $"missing from the tree: [{string.Join(", ", missing)}]; not in the reference: [{string.Join(", ", extra)}]");
        Assert.Equal(products.Count, candidates.Count);

        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        Assert.Equal(candidates, fixture.Solver.CandidateSpecies(propellant.Elements, propellant.Omit, propellant.Only));
    }

    [Fact]
    public void Mole_amounts_are_converted_with_the_record_molar_mass()
    {
        // RP-1311 example 14 is given in moles: the fixture records the moles and the mass fractions the reference derived from them.
        var c = FixtureCases.Load("tp", "rp1311-example14_T300");
        var fuelMoles = c.Inputs.GetProperty("fuelMoles").GetDouble();
        var oxidantMoles = c.Inputs.GetProperty("oxidantMoles").GetDouble();
        var propellant = Propellant.From(fixture.Database)
            .Add(Reactant.FromDatabase("H2(L)", ReactantRole.Named, fuelMoles, amountKind: AmountKind.Moles))
            .Add(Reactant.FromDatabase("O2(L)", ReactantRole.Named, oxidantMoles, amountKind: AmountKind.Moles))
            .Build();
        var fractions = propellant.MassFractionsFor(null);
        var reference = FixtureCases.ReactantMassFractionsOf(c);
        Assert.True(Math.Abs(fractions[0] - reference["H2(L)"]) <= MassFractionTolerance, $"H2(L): {fractions[0]:R} vs {reference["H2(L)"]:R}");
        Assert.True(Math.Abs(fractions[1] - reference["O2(L)"]) <= MassFractionTolerance, $"O2(L): {fractions[1]:R} vs {reference["O2(L)"]:R}");
        Assert.Null(propellant.OxidizerToFuelRatio);
        var mixture = fixture.Solver.Mixture(propellant);
        foreach (var (symbol, kilomoles) in FixtureCases.ElementMolesOf(c))
        {
            Assert.True(Math.Abs(mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles - kilomoles) <= MixtureTolerance * kilomoles, symbol);
        }
    }

    [Fact]
    public void A_custom_reactant_derives_its_molar_mass_from_the_formula_and_the_atomic_weights()
    {
        var c = FixtureCases.Load("rocket", "ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        var binder = c.Inputs.GetProperty("reactants").EnumerateArray().Single(r => r.GetProperty("name").GetString() == "HTPB");
        var expected = binder.GetProperty("molarMass").GetDouble();
        var resolved = propellant.Resolved.Single(r => r.Reactant.Name == "HTPB");
        Assert.True(resolved.Reactant.IsCustom);
        Assert.True(Math.Abs(resolved.MolarMass - expected) <= MassFractionTolerance * expected, $"molar mass {resolved.MolarMass:R} vs {expected:R}");
        Assert.Contains("HTPB", propellant.Reactants.Select(r => r.Name));
    }

    [Fact]
    public void Candidates_are_gases_then_condensed_species_in_database_order()
    {
        var c = FixtureCases.Load("rocket", "ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var candidates = fixture.Solver.CandidateSpecies(FixtureCases.ElementMolesOf(c).Keys.ToList());
        var records = candidates.Select(name => fixture.Database[name]).ToList();
        var firstCondensed = records.FindIndex(r => r.Phase == SpeciesPhase.Condensed);
        Assert.True(firstCondensed > 0);
        Assert.All(records.Take(firstCondensed), r => Assert.Equal(SpeciesPhase.Gas, r.Phase));
        Assert.All(records.Skip(firstCondensed), r => Assert.Equal(SpeciesPhase.Condensed, r.Phase));
        var order = fixture.Database.Products.Select((s, i) => (s.Name, i)).GroupBy(p => p.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().i, StringComparer.Ordinal);
        Assert.Equal(candidates.Take(firstCondensed).Select(n => order[n]).Order(), candidates.Take(firstCondensed).Select(n => order[n]));
        Assert.Equal(candidates.Skip(firstCondensed).Select(n => order[n]).Order(), candidates.Skip(firstCondensed).Select(n => order[n]));
        Assert.DoesNotContain(records, r => r.Formula.Any(pair => pair.Symbol.Equals("E", StringComparison.OrdinalIgnoreCase)));
        Assert.Contains(candidates, n => n.EndsWith('-'));   // "C3H4,cyclo-" and its kind are neutral species with a truncated name
    }

    [Fact]
    public void An_elemental_mixture_normalizes_symbols_and_keeps_the_order()
    {
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["h"] = 1.0, ["Al"] = 2.0, ["O"] = 0.0 }, 5.0);
        Assert.Equal(["H", "AL", "O"], mixture.Elements);
        Assert.Equal(2.0, mixture.ElementMoles["AL"]);
        Assert.Equal(5.0, mixture.Enthalpy);
        Assert.Equal([1.0e-3, 0.0, 2.0e-3], mixture.KilomolesPerKilogram(["H", "N", "AL"]));
    }
}
