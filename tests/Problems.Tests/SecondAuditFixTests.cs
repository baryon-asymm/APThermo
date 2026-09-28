using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>
/// L0: the second hidden-defect audit of 2026-09-28 (Data, Problems and Cli), the findings and observations this
/// node's own code answers: an element at zero abundance everywhere is masked, not refused (F1); an `Only` list
/// names its own cause and admits no ion or inert record (observation 1 and 2); one amount kind per unit of
/// normalization (F2); a record is named for its own elements, a batch for its options (F3); estimates and amount
/// sums must be finite (observations 5 and 7); a multi-record reactant's window is the lowest lower and highest
/// upper bound, bound by bound (observation 6, `Br2(cr)`); and the chemical-system cache key is unambiguous
/// (observation 4).
/// </summary>
[Collection("solver")]
public sealed class SecondAuditFixTests
{
    /// <summary>
    /// An element with no candidate species is refused only when some mixture of the batch gives it a nonzero
    /// abundance (finding F1, a regression of the first audit's fix): a record with <c>"E": 0.0</c>, as a
    /// plasma-capable code writes a neutral mixture, solves bit for bit like the record without it; the same
    /// element at a positive abundance is still refused.
    /// </summary>
    [Fact]
    public void AnElementAtZeroAbundanceEverywhereIsMaskedNotRefused()
    {
        var without = new StateRecord(RejectionTests.RecordPressure, RejectionTests.OneKilogram, Temperature: 3000.0);
        var withZeroElectron = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["E"] = 0.0 };
        var withZero = new StateRecord(RejectionTests.RecordPressure, withZeroElectron, Temperature: 3000.0);

        var resultsWithout = SolverFixture.Shared.Solver.SolveStates([without]);
        var resultsWithZero = SolverFixture.Shared.Solver.SolveStates([withZero]);
        Assert.Equal(CaseStatus.Ok, resultsWithout[0].Status);
        Assert.Equal(CaseStatus.Ok, resultsWithZero[0].Status);
        Assert.Empty(StationEquality.BitDifferences(resultsWithout[0].State, resultsWithZero[0].State, "state"));

        var withPositiveElectron = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["E"] = 1.0e-6 };
        var stillRefused = Assert.Throws<StateRecordException>(
            () => SolverFixture.Shared.Solver.SolveStates([new StateRecord(RejectionTests.RecordPressure, withPositiveElectron, Temperature: 3000.0)]));
        Assert.Contains("'E'", stillRefused.Reason, StringComparison.Ordinal);
        Assert.Contains("no candidate species", stillRefused.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// When the candidates of an element were removed by an <c>Only</c> list, the refusal says so instead of the
    /// database's own wording (observation 1); an <c>Only</c> name that is ionized or an inert pseudo-element
    /// record is refused by name, since the candidate rule always excludes them (observation 2).
    /// </summary>
    [Fact]
    public void AnOnlyListNamesItsOwnCauseAndExcludesIonsAndInertRecords()
    {
        var propellant = Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("H2(L)")
            .OxidizerToFuelRatio(6.0)
            .Only("H2", "H")
            .Build();
        var e = Assert.Throws<ArgumentException>(
            () => SolverFixture.Shared.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] }));
        Assert.Contains("'O'", e.Message, StringComparison.Ordinal);
        Assert.Contains("Only list", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("only ionized or inert records carry it", e.Message, StringComparison.Ordinal);

        foreach (var ion in new[] { "e-", "H+", "InertH" })
        {
            var ex = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database)
                .Oxidizer("O2(L)")
                .Fuel("H2(L)")
                .OxidizerToFuelRatio(6.0)
                .Only("H2O", ion)
                .Build());
            Assert.Contains($"'{ion}'", ex.Message, StringComparison.Ordinal);
            Assert.Contains("ionized or an inert pseudo-element record", ex.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Without an oxidizer-to-fuel ratio the whole propellant is one kilogram, so every reactant of every group
    /// shares one amount kind (finding F2, incomplete in the first audit's own fix): a fuel in mass fractions
    /// beside a named reactant in moles is refused, and so is an oxidizer in moles beside a named reactant in mass
    /// fractions. With a ratio the per-group rule of 2026-09-26 is unchanged: an oxidizer in moles beside a fuel in
    /// mass fractions still builds.
    /// </summary>
    [Fact]
    public void OneAmountKindPerUnitOfNormalizationAppliesAcrossGroupsOnlyWithoutARatio()
    {
        var fuelMassNamedMoles = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database)
            .Fuel("H2(L)", amount: 0.5)
            .Add(Reactant.FromDatabase("CH4(L)", ReactantRole.Named, 0.5, amountKind: AmountKind.Moles))
            .Build());
        Assert.Contains("'H2(L)'", fuelMassNamedMoles.Message, StringComparison.Ordinal);
        Assert.Contains("'CH4(L)'", fuelMassNamedMoles.Message, StringComparison.Ordinal);

        var oxidizerMolesNamedMass = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database)
            .Add(Reactant.FromDatabase("O2(L)", ReactantRole.Oxidizer, 0.5, amountKind: AmountKind.Moles))
            .Named("RP-1", 0.5)
            .Build());
        Assert.Contains("'O2(L)'", oxidizerMolesNamedMass.Message, StringComparison.Ordinal);
        Assert.Contains("'RP-1'", oxidizerMolesNamedMass.Message, StringComparison.Ordinal);

        var withRatio = Propellant.From(SolverFixture.Shared.Database)
            .Add(Reactant.FromDatabase("O2(L)", ReactantRole.Oxidizer, 1.0, amountKind: AmountKind.Moles))
            .Fuel("H2(L)", amount: 1.0)
            .OxidizerToFuelRatio(6.0)
            .Build();
        Assert.Equal(2, withRatio.Resolved.Count);
    }

    /// <summary>
    /// <see cref="Solver.SolveStates"/> checks each record's own elements before the union of the batch is built
    /// (finding F3, incomplete in the first audit's own fix): an unknown element or a candidate-less positive
    /// abundance is a <see cref="StateRecordException"/> naming its own record, never the union's. A condition of
    /// the batch, not of a record — a transport request the database cannot honour, or an invalid
    /// <see cref="StateBatchOptions.MassTolerance"/> — is a plain <see cref="ArgumentException"/> naming the
    /// option instead.
    /// </summary>
    [Fact]
    public void ARecordIsNamedForItsOwnElementsABatchForItsOptions()
    {
        var good = new StateRecord(RejectionTests.RecordPressure, RejectionTests.OneKilogram, Temperature: 3000.0);
        var unknownElement = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["Xx"] = 0.0 };
        var withUnknown = new StateRecord(RejectionTests.RecordPressure, unknownElement, Temperature: 3000.0);
        var electron = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["e"] = 1.0e-6 };
        var withElectron = new StateRecord(RejectionTests.RecordPressure, electron, Temperature: 3000.0);

        var e1 = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([good, withUnknown, good]));
        Assert.Equal(1, e1.Index);
        Assert.Contains("'XX'", e1.Reason, StringComparison.Ordinal);

        var e2 = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([good, good, withElectron]));
        Assert.Equal(2, e2.Index);
        Assert.Contains("'E'", e2.Reason, StringComparison.Ordinal);

        using var thermoOnly = Solver.Create(SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp")), new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var transport = Assert.Throws<ArgumentException>(() => thermoOnly.SolveStates([good], new StateBatchOptions(Transport: true)));
        Assert.False(transport is StateRecordException);
        Assert.Contains("trans.inp", transport.Message, StringComparison.Ordinal);

        var badTolerance = Assert.Throws<ArgumentException>(
            () => SolverFixture.Shared.Solver.SolveStates([good], new StateBatchOptions(MassTolerance: double.NaN)));
        Assert.False(badTolerance is StateRecordException);
        Assert.Contains("massTolerance", badTolerance.ParamName, StringComparison.Ordinal);
    }

    /// <summary>A rocket's temperature estimate and an equilibrium's assigned temperature must be finite (observation 5): +Infinity used to pass and end SingularMatrix at the chamber.</summary>
    [Fact]
    public void EstimatesMustBeFinite()
    {
        var mixture = ElementalMixture.Create(RejectionTests.OneKilogram, -1.5e6);
        var rocket = Assert.Throws<ArgumentException>(
            () => SolverFixture.Shared.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 7.0e6, TemperatureEstimate = double.PositiveInfinity }));
        Assert.Contains("temperature estimate", rocket.Message, StringComparison.Ordinal);

        var equilibrium = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(
            mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 7.0e6, Temperature = double.PositiveInfinity }));
        Assert.Contains("temperature", equilibrium.Message, StringComparison.Ordinal);
    }

    /// <summary>A group whose amounts sum to a non-finite value is refused, naming the group (observation 7): two fuels of 1e308 used to be refused later, blaming the oxidizer's share alone.</summary>
    [Fact]
    public void AmountsMustSumToAFiniteValue()
    {
        var e = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("H2(L)", amount: 1.0e308)
            .Fuel("CH4(L)", amount: 1.0e308)
            .OxidizerToFuelRatio(2.6)
            .Build());
        Assert.Contains("fuel group", e.Message, StringComparison.Ordinal);
        Assert.Contains("non-finite", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>Br2(cr)</c>'s one interval is written 300 -> 265.9 K; the accepted window now takes the lowest lower and
    /// the highest upper bound over each interval's own two bounds, unordered (observation 6), so the reactant
    /// resolves at 298.15 K instead of nowhere. Its enthalpy equals the pinned <c>cea</c> package's own
    /// (<c>Mixture.calc_property</c>), read from the fixtures node's generated reference, not typed
    /// (`tests/Fixtures/cases/reactant/Br2(cr)_298.15K.json`, `propellants.py`'s `br2_reactant_anomaly`).
    /// </summary>
    [Fact]
    public void Br2ResolvesAt298Point15KAgainstTheGeneratedReference()
    {
        // The file name is the generator's safe_name rendering of the case name ("(" and ")" become "_"); the
        // case's own name, read back below, is the one the generator gave it, unmangled.
        var c = FixtureCases.Load("reactant", "Br2_cr__298.15K");
        Assert.Equal("Br2(cr)_298.15K", c.Name);
        var expected = c.Outputs.GetProperty("enthalpyPerKilogram").GetDouble();

        var propellant = Propellant.From(SolverFixture.Shared.Database).Fuel("Br2(cr)").Build();
        var resolved = Assert.Single(propellant.Resolved);
        Assert.Equal(298.15, resolved.Temperature);
        var mixture = SolverFixture.Shared.Solver.MixtureOf(propellant);

        Assert.True(SolverFixture.Shared.Tolerances.Matches("enthalpy", expected, mixture.Enthalpy!.Value),
            $"enthalpy: reference {expected:R}, tree {mixture.Enthalpy:R}");
    }

    /// <summary>
    /// The chemical-system cache key no longer joins names with a comma (observation 4): two omit lists that used
    /// to join to the same text, <c>["B2H4,db"]</c> and <c>["B2H4", "db"]</c>, now build two systems, each
    /// excluding only the species it actually named.
    /// </summary>
    [Fact]
    public void TwoOmitListsThatJoinToTheSameTextGiveTwoTables()
    {
        var elementMoles = new Dictionary<string, double>(StringComparer.Ordinal) { ["B"] = 5.0, ["H"] = 900.0 };
        var excludingDb = ElementalMixture.Create(elementMoles, -1.0e6, omit: ["B2H4,db"], massTolerance: 0.5);
        var excludingBoth = ElementalMixture.Create(elementMoles, -1.0e6, omit: ["B2H4", "db"], massTolerance: 0.5);

        var resultExcludingDb = SolverFixture.Shared.Solver.Solve(excludingDb, new EquilibriumProblem { Pressure = 1.0e6 });
        var resultExcludingBoth = SolverFixture.Shared.Solver.Solve(excludingBoth, new EquilibriumProblem { Pressure = 1.0e6 });

        Assert.Contains("B2H4", resultExcludingDb.Species);
        Assert.DoesNotContain("B2H4,db", resultExcludingDb.Species);
        Assert.Contains("B2H4,db", resultExcludingBoth.Species);
        Assert.DoesNotContain("B2H4", resultExcludingBoth.Species);
    }
}
