using APThermo.Data;

namespace APThermo.Problems.Tests;

/// <summary>L0: element moles, reactant enthalpy, mass normalization and candidate species against every fixture that carries reactants.</summary>
[Collection(SolverCollectionDefinition.Name)]
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

    /// <summary>MassOf and an independent sum over the atomic weights are the same arithmetic, scaled by the unit factor; two solves of the tree's own code agree to summation-order rounding.</summary>
    public const double MassOfSummationTolerance = 1e-14;

    /// <summary>MassOf of a mixture scaled by a known factor against that factor times the original MassOf: two solves of the tree's own code, agreeing to rounding.</summary>
    public const double ScaledMassSummationTolerance = 1e-12;

    /// <summary>The theory data of (kind, name) pairs over every fixture that carries reactants.</summary>
    public static TheoryData<string, string> Cases() => FixtureCases.NamesWithReactants();

    /// <summary>The recorded element moles of every fixture weigh one kilogram within the derivation figure.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheRecordedElementMolesOfEveryFixtureWeighOneKilogramWithinTheDerivationFigure(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var moles = FixtureCases.ElementMolesOf(c).ToDictionary(kv => kv.Key, kv => kv.Value * FixtureCases.KilomolesToMoles, StringComparer.Ordinal);
        var expected = moles.Sum(kv => kv.Value * 1.0e-3 * fixture.Database.AtomicWeight(kv.Key));
        var mass = fixture.Solver.MassOf(ElementalMixture.Create(moles));
        Assert.True(Math.Abs(mass - expected) <= MassOfSummationTolerance * expected, $"MassOf {mass:R} kg, the sum over the atomic weights {expected:R} kg");
        Assert.True(Math.Abs(mass - 1.0) <= FixtureMassDeviation, $"the recorded element moles weigh {mass:R} kg");
    }

    /// <summary>Results carry the mass of their mixture.</summary>
    [Fact]
    public void ResultsCarryTheMassOfTheirMixture()
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
        Assert.True(Math.Abs(expected - 1.005 * rocket.MixtureMass) <= ScaledMassSummationTolerance, $"{expected:R} kg");
        Assert.Equal(expected, fixture.Solver.Solve(heavy, FixtureCases.RocketProblemOf(c)).MixtureMass);
        Assert.Equal(expected, fixture.Solver.Solve(heavy, new EquilibriumProblem { Pressure = 7.0e6 }).MixtureMass);
    }

    /// <summary>Element moles and enthalpy equal the reference from its mass fractions.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ElementMolesAndEnthalpyEqualTheReferenceFromItsMassFractions(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var propellant = FixtureCases.PropellantOf(fixture.Database, c, byMassFractions: true);
        _ = Assert.IsType<MixtureSpecification.MassFractions>(propellant.Mixture);
        var fractions = MixtureRule.MassFractionsOf(propellant.Resolved, propellant.Mixture, null);
        var massFractions = FixtureCases.ReactantMassFractionsOf(c);
        for (var k = 0; k < propellant.Reactants.Count; k++)
        {
            var reference = massFractions[propellant.Reactants[k].Name];
            Assert.True(Math.Abs(fractions[k] - reference) <= MassFractionTolerance, $"{propellant.Reactants[k].Name}: mass fraction reference {reference:R}, tree {fractions[k]:R}");
        }

        var mixture = fixture.Solver.MixtureOf(propellant);
        var expected = FixtureCases.ElementMolesOf(c);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), mixture.Elements.Order(StringComparer.Ordinal));
        foreach (var (symbol, kilomoles) in expected)
        {
            var actual = mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles;
            Assert.True(Math.Abs(actual - kilomoles) <= MixtureTolerance * Math.Abs(kilomoles), $"{symbol}: reference {kilomoles:R}, tree {actual:R}");
        }

        // An hp fixture whose enthalpy was assigned by the generator (the latent-heat band cases) or derived from a
        // rocket station does not carry the propellant's own enthalpy.
        var assigned = c.Inputs.TryGetProperty("enthalpyAssigned", out var flag) && flag.GetBoolean();
        double? referenceEnthalpy = kind == "rocket" ? c.Inputs.GetProperty("reactantEnthalpy").GetDouble()
            : kind == "hp" && !assigned && !c.Inputs.TryGetProperty("derivedFrom", out _) ? c.Inputs.GetProperty("enthalpy").GetDouble()
            : null;
        if (referenceEnthalpy is { } h)
        {
            _ = Assert.NotNull(mixture.Enthalpy);
            Assert.True(Math.Abs(mixture.Enthalpy.Value - h) <= MixtureTolerance * Math.Abs(h), $"enthalpy: reference {h:R}, tree {mixture.Enthalpy:R}");
        }
    }

    /// <summary>A ratio split reproduces the reference mass fractions within its single precision.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ARatioSplitReproducesTheReferenceMassFractionsWithinItsSinglePrecision(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        if (FixtureCases.OxidizerToFuelRatioOf(c) is not { } ratio)
        {
            return;   // given by mass fractions; the other theory covers it
        }

        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        Assert.Equal(ratio, propellant.OxidizerToFuelRatio);
        var fractions = MixtureRule.MassFractionsOf(propellant.Resolved, propellant.Mixture, null);
        var massFractions = FixtureCases.ReactantMassFractionsOf(c);
        for (var k = 0; k < propellant.Reactants.Count; k++)
        {
            var reference = massFractions[propellant.Reactants[k].Name];
            Assert.True(Math.Abs(fractions[k] - reference) <= RatioMassFractionTolerance * reference, $"{propellant.Reactants[k].Name}: mass fraction reference {reference:R}, tree {fractions[k]:R}");
        }

        var mixture = fixture.Solver.MixtureOf(propellant);
        foreach (var (symbol, kilomoles) in FixtureCases.ElementMolesOf(c))
        {
            var actual = mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles;
            Assert.True(Math.Abs(actual - kilomoles) <= RatioMassFractionTolerance * Math.Abs(kilomoles), $"{symbol}: reference {kilomoles:R}, tree {actual:R}");
        }
    }

    /// <summary>Candidate species equal the reference product list.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void CandidateSpeciesEqualTheReferenceProductList(string kind, string name)
    {
        var c = FixtureCases.Load(kind, name);
        var products = FixtureCases.ProductsOf(c);
        var elements = FixtureCases.ElementMolesOf(c).Keys.ToList();
        var candidates = fixture.Solver.CandidateSpeciesFor(elements, FixtureCases.OmitOf(c), FixtureCases.OnlyOf(c));
        var missing = products.Except(candidates, StringComparer.Ordinal).ToList();
        var extra = candidates.Except(products, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
                    $"missing from the tree: [{string.Join(", ", missing)}]; not in the reference: [{string.Join(", ", extra)}]");
        Assert.Equal(products.Count, candidates.Count);

        var propellant = FixtureCases.PropellantOf(fixture.Database, c);
        Assert.Equal(candidates, fixture.Solver.CandidateSpeciesFor(propellant.Elements, propellant.Omit, propellant.Only));
    }

    /// <summary>Mole amounts are converted with the record molar mass.</summary>
    [Fact]
    public void MoleAmountsAreConvertedWithTheRecordMolarMass()
    {
        // RP-1311 example 14 is given in moles: the fixture records the moles and the mass fractions the reference derived from them.
        var c = FixtureCases.Load("tp", "rp1311-example14_T300");
        var fuelMoles = c.Inputs.GetProperty("fuelMoles").GetDouble();
        var oxidantMoles = c.Inputs.GetProperty("oxidantMoles").GetDouble();
        var propellant = Propellant.From(fixture.Database)
            .Add(Reactant.FromDatabase("H2(L)", ReactantRole.Named, fuelMoles, amountKind: AmountKind.Moles))
            .Add(Reactant.FromDatabase("O2(L)", ReactantRole.Named, oxidantMoles, amountKind: AmountKind.Moles))
            .Build();
        var fractions = MixtureRule.MassFractionsOf(propellant.Resolved, propellant.Mixture, null);
        var reference = FixtureCases.ReactantMassFractionsOf(c);
        Assert.True(Math.Abs(fractions[0] - reference["H2(L)"]) <= MassFractionTolerance, $"H2(L): {fractions[0]:R} vs {reference["H2(L)"]:R}");
        Assert.True(Math.Abs(fractions[1] - reference["O2(L)"]) <= MassFractionTolerance, $"O2(L): {fractions[1]:R} vs {reference["O2(L)"]:R}");
        Assert.Null(propellant.OxidizerToFuelRatio);
        var mixture = fixture.Solver.MixtureOf(propellant);
        foreach (var (symbol, kilomoles) in FixtureCases.ElementMolesOf(c))
        {
            Assert.True(Math.Abs(mixture.ElementMoles[symbol] / FixtureCases.KilomolesToMoles - kilomoles) <= MixtureTolerance * kilomoles, symbol);
        }
    }

    /// <summary>A custom reactant derives its molar mass from the formula and the atomic weights.</summary>
    [Fact]
    public void ACustomReactantDerivesItsMolarMassFromTheFormulaAndTheAtomicWeights()
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

    /// <summary>Candidates are gases then condensed species in database order.</summary>
    [Fact]
    public void CandidatesAreGasesThenCondensedSpeciesInDatabaseOrder()
    {
        var c = FixtureCases.Load("rocket", "ap-htpb-al_pc7MPa_shiftingEquilibrium");
        var candidates = fixture.Solver.CandidateSpeciesFor([.. FixtureCases.ElementMolesOf(c).Keys]);
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

    /// <summary>An elemental mixture normalizes symbols and keeps the order.</summary>
    [Fact]
    public void AnElementalMixtureNormalizesSymbolsAndKeepsTheOrder()
    {
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["h"] = 1.0, ["Al"] = 2.0, ["O"] = 0.0 }, 5.0);
        Assert.Equal(["H", "AL", "O"], mixture.Elements);
        Assert.Equal(2.0, mixture.ElementMoles["AL"]);
        Assert.Equal(5.0, mixture.Enthalpy);
        Assert.Equal([1.0e-3, 0.0, 2.0e-3], mixture.KilomolesPerKilogram(["H", "N", "AL"]));
    }
}
