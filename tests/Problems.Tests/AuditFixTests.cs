using APThermo.Execution;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>
/// L0: the hidden-defect audit of 2026-09-26 (Data, Problems and Cli), the findings this node's own code answers:
/// a state record's rule breaks by its own index (finding 3), an element with no candidate species or no atomic
/// weight is refused before any kernel runs (finding 4), one role group takes one amount kind (finding 5), and a
/// reactant name with several database records resolves as CEA does (finding 6).
/// </summary>
[Collection("solver")]
public sealed class AuditFixTests
{
    /// <summary>
    /// A rule <see cref="ProblemValidation"/> applies (a non-positive pressure, here) is refused by the record's own
    /// index through <see cref="StateRecordException"/>, not the batch-local "problem k" an <see cref="ArgumentException"/>
    /// used to carry: a caller that groups records before solving, as the command line does, named the wrong record
    /// (the audit's finding 3, reproduced with <c>misname.jsonl</c>: a good record at index 0 and 1, the offending
    /// one — a zero pressure — at index 2).
    /// </summary>
    [Fact]
    public void ARuleProblemValidationAppliesIsRefusedByTheRecordsOwnIndexNotABatchLocalOne()
    {
        var good = new StateRecord(RejectionTests.RecordPressure, RejectionTests.OneKilogram, Temperature: 3000.0);
        var badPressure = new StateRecord(0.0, RejectionTests.OneKilogram, Temperature: 1659.18);
        var e = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([good, good, badPressure]));
        Assert.Equal(2, e.Index);
        Assert.Equal("the pressure must be positive and finite, not 0", e.Reason);
        Assert.Equal("state record 2: the pressure must be positive and finite, not 0", e.Message);

        var rocketGood = new StateRecord(RejectionTests.RecordPressure, RejectionTests.OneKilogram, Enthalpy: -1.5e6) { AreaRatios = [20.0] };
        var rocketBad = new StateRecord(1.0e6, RejectionTests.OneKilogram, Enthalpy: -1.0e6) { AreaRatios = [-1.0] };
        var re = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveRocketStates([rocketGood, rocketBad]));
        Assert.Equal(1, re.Index);
        Assert.Equal("an exit value must be positive and finite, not -1", re.Reason);

        // The wording a direct, non-record call keeps: unchanged, batch-local, no StateRecordException.
        var mixture = ElementalMixture.Create(RejectionTests.OneKilogram, -1.5e6);
        var direct = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 0.0 }));
        Assert.False(direct is StateRecordException);
        Assert.StartsWith("rocket problem 0: ", direct.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An element that survives only in ionized or inert records has no candidate species and is refused by name
    /// before any kernel runs, instead of entering the table as a row with no species and failing later as a
    /// numerical <c>SingularMatrix</c> (the audit's finding 4). <c>InertH2(L)</c> carries the pseudo-element
    /// <c>IH</c>, which <c>InertH</c> gives an atomic weight but no product species ever carries; the electron
    /// pseudo-element <c>E</c> (from <c>e-</c>) is the same story on the elemental front door.
    /// </summary>
    [Fact]
    public void AnElementThatSurvivesOnlyInIonizedOrInertRecordsIsRefusedByName()
    {
        var propellant = Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("H2(L)", amount: 0.9)
            .Fuel("InertH2(L)", amount: 0.1)
            .OxidizerToFuelRatio(6.0)
            .Build();
        var e = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] }));
        Assert.Contains("'IH'", e.Message, StringComparison.Ordinal);
        Assert.Contains("no candidate species", e.Message, StringComparison.Ordinal);

        // A candidate-less element with a positive abundance is the record's own StateRecordException since the
        // second audit's fix F3: SolveStates checks each record's own elements before the union is built.
        var composition = new Dictionary<string, double>(RejectionTests.OneKilogram, StringComparer.Ordinal) { ["e"] = 1.0e-6 };
        var withElectron = Assert.Throws<StateRecordException>(
            () => SolverFixture.Shared.Solver.SolveStates([new StateRecord(RejectionTests.RecordPressure, composition, Temperature: 3000.0)]));
        Assert.Contains("'E'", withElectron.Message, StringComparison.Ordinal);
        Assert.Contains("no candidate species", withElectron.Message, StringComparison.Ordinal);
        Assert.Equal(0, withElectron.Index);
    }

    /// <summary>
    /// An element with no monatomic record to take its atomic weight from is refused with a message that says
    /// exactly that, not "has no record in the database" (the committed file's <c>InertRP-1</c>, symbol <c>IC</c>,
    /// carries eleven other records but no monatomic one of its own; the audit's finding 4).
    /// </summary>
    [Fact]
    public void AnElementWithNoMonatomicRecordIsRefusedNamingWhatIsMissing()
    {
        // Build() succeeds (a real oxidizer and fuel satisfy the ratio guard); the atomic weight is only asked for
        // when the chemical system is assembled, at Solve.
        var propellant = Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("InertRP-1")
            .OxidizerToFuelRatio(6.0)
            .Build();
        var e = Assert.Throws<ArgumentException>(
            () => SolverFixture.Shared.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] }));
        Assert.Contains("'IC'", e.Message, StringComparison.Ordinal);
        Assert.Contains("no monatomic record", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("has no record in the database", e.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// One role group must use one amount kind (the audit's finding 5): a mass fraction has no unit and a mole
    /// amount becomes amount x M in g/mol, so mixing the two within a group summed a mass fraction as if it were
    /// grams (0.5 mass fraction of H2(L) beside 0.5 mol of CH4(L) made the fuel 5.87 % H2(L) instead of 50 %).
    /// </summary>
    [Fact]
    public void MixedAmountKindsInOneRoleGroupAreRejected()
    {
        var e = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("H2(L)", amount: 0.5)
            .Fuel("CH4(L)", amount: 0.5, amountKind: AmountKind.Moles)
            .OxidizerToFuelRatio(3.0)
            .Build());
        Assert.Contains("fuel group", e.Message, StringComparison.Ordinal);
        Assert.Contains("'H2(L)'", e.Message, StringComparison.Ordinal);
        Assert.Contains("'CH4(L)'", e.Message, StringComparison.Ordinal);

        // One kind per group is unaffected: an all-mole and an all-mass-fraction group both build.
        var allMoles = Propellant.From(SolverFixture.Shared.Database)
            .Oxidizer("O2(L)")
            .Fuel("H2(L)", amount: 0.5, amountKind: AmountKind.Moles)
            .Fuel("CH4(L)", amount: 0.5, amountKind: AmountKind.Moles)
            .OxidizerToFuelRatio(3.0)
            .Build();
        Assert.Equal(2, allMoles.Resolved.Count(r => r.Reactant.Role == ReactantRole.Fuel));
    }

    /// <summary>
    /// A reactant whose database name carries several records over different temperature ranges (a condensed
    /// product species Thermo's join-and-cut joins into one continuous curve) is accepted over the union of every
    /// record's range, not the first record's own (the audit's finding 6): <c>Fe2O3(cr)</c> at 1000 K used to be
    /// rejected against "298.15-960 K", its first record's range, although the joined table species — what
    /// <see cref="PropellantMixtures"/> actually evaluates — covers 298.15-6000 K. The reactant's enthalpy at that
    /// temperature equals a direct evaluation of the same joined table species, proving the accepted temperature
    /// is not merely let through but evaluated where the front door says it is.
    /// </summary>
    [Fact]
    public void AMultiRecordProductNamesRangeIsTheUnionOfItsRecordsAndItsEnthalpyEqualsTheJoinedTable()
    {
        var records = SolverFixture.Shared.Database.Records("Fe2O3(cr)");
        Assert.True(records.Count > 1, "the committed file no longer carries several Fe2O3(cr) records");
        Assert.True(records.Min(r => r.Intervals.Min(i => i.TLow)) < 960.0);
        Assert.True(records.Max(r => r.Intervals.Max(i => i.THigh)) > 960.0);

        var propellant = Propellant.From(SolverFixture.Shared.Database).Fuel("Fe2O3(cr)", temperature: 1000.0).Build();
        var resolved = Assert.Single(propellant.Resolved);
        Assert.Equal(1000.0, resolved.Temperature);
        var mixture = SolverFixture.Shared.Solver.MixtureOf(propellant);

        var table = SpeciesTable.Build(SolverFixture.Shared.Database, ["FE", "O"], ["Fe2O3(cr)"]);
        using var uploaded = SolverFixture.Shared.Engine.Upload(table);
        var batch = new SpeciesFunctionBatch(1);
        batch.Species[0] = table.PieceOf("Fe2O3(cr)", 1000.0);
        batch.Temperature[0] = 1000.0;
        var functions = SolverFixture.Shared.Engine.Run(uploaded, batch);
        var expected = functions.HOverRT[0] * PhysicalConstants.R * 1000.0 / resolved.MolarMass;
        Assert.Equal(expected, mixture.Enthalpy!.Value, Math.Abs(expected) * 1.0e-12);
    }

    /// <summary>
    /// Several reactant-only records of one name resolve to the last, as cea 3.3.4 does (the audit's finding 6):
    /// the committed file's <c>n-Butanol</c> is a gas record followed by a liquid one, and the resolver always took
    /// the gas, 369 kJ/kg above the reference's own value for the liquid.
    /// </summary>
    [Fact]
    public void SeveralReactantOnlyRecordsOfOneNameResolveToTheLast()
    {
        var records = SolverFixture.Shared.Database.Records("n-Butanol");
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Empty(r.Intervals));
        var last = records[^1];

        var propellant = Propellant.From(SolverFixture.Shared.Database).Fuel("n-Butanol").Build();
        var resolved = Assert.Single(propellant.Resolved);
        Assert.Same(last, resolved.Record);
        Assert.Equal(last.FormationEnthalpy, resolved.AssignedEnthalpy);
        Assert.NotEqual(records[0].FormationEnthalpy, resolved.AssignedEnthalpy);
    }
}
