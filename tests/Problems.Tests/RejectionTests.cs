using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Performance;
using APThermo.Thermo;

namespace APThermo.Problems.Tests;

/// <summary>L0: invalid inputs are rejected by name, before any kernel runs.</summary>
[Collection("solver")]
public sealed partial class RejectionTests
{
    /// <summary>The record another simulation handed over on 2026-09-13 (C, H, O, N, Cl, Al in mol/kg): 1000.015 g with the database's atomic weights.</summary>
    internal static readonly IReadOnlyDictionary<string, double> OneKilogram = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["C"] = 9.505849129331365,
        ["H"] = 35.214695099119155,
        ["O"] = 15.704786718374072,
        ["N"] = 6.007718569653603,
        ["Cl"] = 3.3293409533388547,
        ["Al"] = 14.709996403305084,
    };

    private const double RecordEnthalpy = -1527829.408385985;   // J/kg
    internal const double RecordPressure = 6.5e6;                 // Pa

    /// <summary>e.Mass is the unit-factor scaling of an independently-summed composition; the two solves of the tree's own code agree to rounding.</summary>
    private const double MassBitRoundingTolerance = 1.0e-15;

    /// <summary>The exception's message prints the mass to seven significant digits (G7); parsing it back loses precision beyond that.</summary>
    private const double ReportedMassPrintTolerance = 1.0e-6;

    /// <summary>A reactant record's implied mass ratio (its formula's atomic-weight sum over its molar mass) against the exception's own Mass: two solves of the tree's own code, agreeing to rounding.</summary>
    private const double FormulaMassRoundingTolerance = 1.0e-12;

    /// <summary>One kilogram of water as element moles (55.508 mol of H2O), for the facts that need a composition and not a mixture in particular.</summary>
    private static readonly IReadOnlyDictionary<string, double> Water = new Dictionary<string, double>(StringComparer.Ordinal) { ["H"] = 111.0168, ["O"] = 55.5084 };

    private static PropellantBuilder LoxLh2() => Propellant.From(SolverFixture.Shared.Database).Oxidizer("O2(L)").Fuel("H2(L)");

    private static Dictionary<string, double> Scaled(double factor) => OneKilogram.ToDictionary(kv => kv.Key, kv => kv.Value * factor, StringComparer.Ordinal);

    /// <summary>Σ n_i A_i in grams with the database's atomic weights: the number the message must report.</summary>
    private static double GramsOf(IReadOnlyDictionary<string, double> composition) => composition.Sum(kv => kv.Value * SolverFixture.Shared.Database.AtomicWeight(kv.Key));

    private static EquilibriumProblem AssignedTemperature() => new() { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = RecordPressure, Temperature = 3000.0 };

    /// <summary>A composition that does not weigh one kilogram is rejected with its mass and the tolerance.</summary>
    [Fact]
    public void ACompositionThatDoesNotWeighOneKilogramIsRejectedWithItsMassAndTheTolerance()
    {
        // The record as handed over passes: it is 1.5e-5 off one kilogram, and it solves.
        var solved = Assert.Single(SolverFixture.Shared.Solver.SolveStates([new StateRecord(RecordPressure, OneKilogram, Enthalpy: RecordEnthalpy)]));
        Assert.Equal(CaseStatus.Ok, solved.Status);

        // Doubled (two kilograms), in mol/g or kmol/kg (a thousandth) and in mmol/kg (a thousandfold): refused before any kernel runs,
        // naming the record, the mass found in grams and the tolerance.
        foreach (var factor in new[] { 2.0, 1.0e-3, 1.0e3 })
        {
            var composition = Scaled(factor);
            var e = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(RecordPressure, composition, Enthalpy: RecordEnthalpy)]));
            Assert.Equal(0, e.Index);
            Assert.Equal(ElementalMixture.DefaultMassTolerance, e.Tolerance);
            Assert.StartsWith("state record 0: the composition weighs ", e.Message, StringComparison.Ordinal);
            Assert.Equal("state record 0: " + e.Reason, e.Message);
            var grams = GramsOf(composition);
            Assert.Equal(grams * 1.0e-3, e.Mass, grams * MassBitRoundingTolerance);
            var reported = double.Parse(MyRegex().Match(e.Message).Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.Equal(grams, reported, grams * ReportedMassPrintTolerance);
            Assert.Contains("element moles are per kilogram of mixture", e.Message, StringComparison.Ordinal);
            Assert.Contains("must weigh 1000 g within 1 %", e.Message, StringComparison.Ordinal);
        }

        // The other front doors of the elemental form name the mixture by its index in the batch.
        var doubled = ElementalMixture.Create(Scaled(2.0), RecordEnthalpy);
        var rocket = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve(doubled, new RocketProblem { ChamberPressure = RecordPressure, AreaRatios = [10.0] }));
        Assert.StartsWith("mixture 0: the composition weighs ", rocket.Message, StringComparison.Ordinal);
        var equilibrium = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve(doubled, AssignedTemperature()));
        Assert.StartsWith("mixture 0: the composition weighs ", equilibrium.Message, StringComparison.Ordinal);
        var good = ElementalMixture.Create(OneKilogram, RecordEnthalpy);
        var problem = new EquilibriumProblem { Pressure = RecordPressure };
        var batch = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve([good, doubled], [problem, problem]));
        Assert.Equal(1, batch.Index);
        Assert.StartsWith("mixture 1: ", batch.Message, StringComparison.Ordinal);

        // The tolerance is the one the message names: 0.9 % heavy solves, 1.1 % heavy is refused.
        Assert.Equal(CaseStatus.Ok, SolverFixture.Shared.Solver.Solve(ElementalMixture.Create(Scaled(1.009)), AssignedTemperature()).Status);
        _ = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve(ElementalMixture.Create(Scaled(1.011)), AssignedTemperature()));
    }

    /// <summary>The tolerance a mixture declares is the one applied.</summary>
    [Fact]
    public void TheToleranceAMixtureDeclaresIsTheOneApplied()
    {
        // The record made 2 % and 2.5 % heavy: refused at the default through the state batch, solved when the batch declares 3 %, every
        // record of it. Heavy, not light: the same record made 1 % to 10 % light does not converge as an hp state at 6.5 MPa (the
        // equilibrium node's open defect at variable temperature, found 2026-09-13 with this test), and a record that fails numerically
        // would not show that the check let it through.
        var heavy = new StateRecord(RecordPressure, Scaled(1.02), Enthalpy: RecordEnthalpy);
        var heavier = new StateRecord(RecordPressure, Scaled(1.025), Enthalpy: RecordEnthalpy);
        var refused = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.SolveStates([heavy, heavier]));
        Assert.Equal(ElementalMixture.DefaultMassTolerance, refused.Tolerance);
        var results = SolverFixture.Shared.Solver.SolveStates([heavy, heavier], new StateBatchOptions(MassTolerance: 0.03));
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(CaseStatus.Ok, r.Status));
        Assert.All(results, r => Assert.Equal(0.03, r.Mixture.MassTolerance));

        // Through Create, for the direct overloads; the exception and its message name the tolerance in force.
        var declared = ElementalMixture.Create(Scaled(1.02), RecordEnthalpy, massTolerance: 0.03);
        Assert.Equal(0.03, declared.MassTolerance);
        Assert.Equal(CaseStatus.Ok, SolverFixture.Shared.Solver.Solve(declared, AssignedTemperature()).Status);
        Assert.Equal(CaseStatus.Ok, SolverFixture.Shared.Solver.Solve(declared, new RocketProblem { ChamberPressure = RecordPressure, Flow = FlowModel.FrozenAtChamber }).Status);
        var beyond = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve(ElementalMixture.Create(Scaled(1.05), RecordEnthalpy, massTolerance: 0.03), AssignedTemperature()));
        Assert.Equal(0.03, beyond.Tolerance);
        Assert.EndsWith("must weigh 1000 g within 3 %", beyond.Message, StringComparison.Ordinal);

        // The propellant path and a mixture that names no tolerance declare the default; a tolerance that is no tolerance is refused by name.
        Assert.Equal(ElementalMixture.DefaultMassTolerance, SolverFixture.Shared.Solver.MixtureOf(LoxLh2().OxidizerToFuelRatio(6.0).Build()).MassTolerance);
        Assert.Equal(ElementalMixture.DefaultMassTolerance, ElementalMixture.Create(OneKilogram).MassTolerance);
        foreach (var invalid in new[] { -0.01, double.NaN, double.PositiveInfinity })
        {
            var e = Assert.Throws<ArgumentException>(() => ElementalMixture.Create(OneKilogram, massTolerance: invalid));
            Assert.Equal("massTolerance", e.ParamName);
        }
    }

    /// <summary><see cref="ElementalMixture.IsValidMassTolerance"/> is the one statement of the rule (BOOT.md, the review's open question 2): false exactly where Create refuses.</summary>
    [Fact]
    public void TheToleranceRuleIsTheOneCreateApplies()
    {
        double[] tolerances = [-0.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity, 0.0, ElementalMixture.DefaultMassTolerance, 0.03, 1.0];
        foreach (var tolerance in tolerances)
        {
            var valid = ElementalMixture.IsValidMassTolerance(tolerance);
            if (valid)
            {
                Assert.Equal(tolerance, ElementalMixture.Create(OneKilogram, massTolerance: tolerance).MassTolerance);
            }
            else
            {
                var e = Assert.Throws<ArgumentException>(() => ElementalMixture.Create(OneKilogram, massTolerance: tolerance));
                Assert.Equal("massTolerance", e.ParamName);
            }
        }
    }

    /// <summary>A reactant record whose molar mass contradicts its formula is caught at the solve.</summary>
    [Fact]
    public void AReactantRecordWhoseMolarMassContradictsItsFormulaIsCaughtAtTheSolve()
    {
        // The committed file's ADN reactant record carries 630.0 kg/kmol against its formula H4N4O4 (124.06 with the file's own atomic
        // weights), so the element moles per kilogram the record implies weigh a fifth of a kilogram: the propellant path is checked too,
        // and the solve says so instead of computing with them. When the record is corrected upstream, this fact goes with it.
        var adn = SolverFixture.Shared.Database["ADN"];
        var propellant = Propellant.From(SolverFixture.Shared.Database).Named("ADN", 1.0).Build();
        var e = Assert.Throws<MixtureMassException>(() => SolverFixture.Shared.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6, Temperature = 2000.0 }));
        Assert.StartsWith("the propellant's mixture (case 0): the composition weighs ", e.Message, StringComparison.Ordinal);
        var formulaMass = adn.Formula.Sum(pair => pair.Count * SolverFixture.Shared.Database.AtomicWeight(pair.Symbol));
        Assert.Equal(formulaMass / adn.MolarMass, e.Mass, FormulaMassRoundingTolerance);
        Assert.True(e.Mass < 0.25, $"the ADN mixture weighs {e.Mass} kg");
    }

    /// <summary>An unknown reactant is rejected by name.</summary>
    [Fact]
    public void AnUnknownReactantIsRejectedByName()
    {
        var missing = Assert.Throws<KeyNotFoundException>(() => Propellant.From(SolverFixture.Shared.Database).Fuel("Unobtainium").Build());
        Assert.Contains("Unobtainium", missing.Message, StringComparison.Ordinal);
    }

    /// <summary>A temperature outside the record range is rejected by name.</summary>
    [Fact]
    public void ATemperatureOutsideTheRecordRangeIsRejectedByName()
    {
        var outside = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Named("AL(cr)", 1.0, 1000.0).Build());
        Assert.Contains("AL(cr)", outside.Message, StringComparison.Ordinal);
        var assigned = Assert.Throws<ArgumentException>(() => LoxLh2().Oxidizer("O2(L)", 150.0).OxidizerToFuelRatio(6.0).Build());
        Assert.Contains("O2(L)", assigned.Message, StringComparison.Ordinal);

        // Within the margin the reference itself extrapolates: AL(cr) at 298.15 K below its first fit, and an assigned temperature within 10 K.
        Assert.Equal(298.15, Propellant.From(SolverFixture.Shared.Database).Named("AL(cr)", 1.0).Build().Resolved[0].Temperature);
        Assert.Equal(95.0, Propellant.From(SolverFixture.Shared.Database).Oxidizer("O2(L)", 95.0).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build().Resolved[0].Temperature);
        Assert.Equal(90.17, LoxLh2().OxidizerToFuelRatio(6.0).Build().Resolved[0].Temperature);
    }

    /// <summary>Mixture rules that leave a group empty or ambiguous are rejected.</summary>
    [Fact]
    public void MixtureRulesThatLeaveAGroupEmptyOrAmbiguousAreRejected()
    {
        var zero = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Oxidizer("O2(L)", amount: 0.0).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build());
        Assert.Contains("oxidizer group", zero.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(0.0).Build());
        _ = Assert.Throws<ArgumentException>(() => LoxLh2().Build());
        _ = Assert.Throws<ArgumentException>(() => LoxLh2().Named("H2O(L)", 0.1).OxidizerToFuelRatio(6.0).Build());
        _ = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build());
        _ = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Named("H2(L)", 0.0).Build());
        _ = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Build());
        _ = Assert.Throws<ArgumentException>(() => Reactant.FromDatabase("H2(L)", ReactantRole.Fuel, -1.0));

        var monopropellant = Propellant.From(SolverFixture.Shared.Database).Fuel("C2H8N2(L),UDMH").Build();
        _ = Assert.IsType<MixtureSpecification.MassFractions>(monopropellant.Mixture);
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.MixtureOf(monopropellant, 2.0));
    }

    /// <summary>A custom reactant with an unknown element is rejected by name.</summary>
    [Fact]
    public void ACustomReactantWithAnUnknownElementIsRejectedByName()
    {
        var binder = Reactant.Custom("Binder", new CustomReactantDefinition([new ElementCount("C", 1.0), new ElementCount("Xx", 0.5)], -1000.0, 298.15), ReactantRole.Fuel, 1.0);
        var unknown = Assert.Throws<ArgumentException>(() => Propellant.From(SolverFixture.Shared.Database).Custom(binder).Build());
        Assert.Contains("Binder", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("XX", unknown.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", new CustomReactantDefinition([], -1000.0, 298.15), ReactantRole.Fuel, 1.0));
        _ = Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", new CustomReactantDefinition([new ElementCount("C", 0.0)], -1000.0, 298.15), ReactantRole.Fuel, 1.0));
    }

    /// <summary>An only list beyond the elements is rejected and a valid one is used as given.</summary>
    [Fact]
    public void AnOnlyListBeyondTheElementsIsRejectedAndAValidOneIsUsedAsGiven()
    {
        var beyond = Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(6.0).Only("H2O", "CO2").Build());
        Assert.Contains("CO2", beyond.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(6.0).Only().Build());
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Only("H2O", "H2", "O2", "OH").Build();
        var result = SolverFixture.Shared.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] });
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.Equal(["H2O", "H2", "O2", "OH"], result.Species);
    }

    /// <summary>Invalid state records are rejected by index or element.</summary>
    [Fact]
    public void InvalidStateRecordsAreRejectedByIndexOrElement()
    {
        var composition = Water;
        var two = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(1.0e6, composition, Enthalpy: 0.0, Temperature: 3000.0)]));
        Assert.Contains("record 0", two.Message, StringComparison.Ordinal);
        Assert.Equal(0, two.Index);
        var none = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(1.0e6, composition, Temperature: 3000.0), new StateRecord(1.0e6, composition)]));
        Assert.Contains("record 1", none.Message, StringComparison.Ordinal);
        Assert.Equal(1, none.Index);
        var negative = Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(1.0e6, new Dictionary<string, double> { ["H"] = -1.0 }, Temperature: 3000.0)]));
        Assert.Contains("'H'", negative.Message, StringComparison.Ordinal);
        var unknown = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(1.0e6, new Dictionary<string, double> { ["XX"] = 1.0 }, Temperature: 3000.0)]));
        Assert.Contains("XX", unknown.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.SolveStates([]));
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.SolveStates([new StateRecord(0.0, composition, Temperature: 3000.0)]));
        _ = Assert.Throws<ArgumentException>(() => ElementalMixture.Create(new Dictionary<string, double>()));
        _ = Assert.Throws<ArgumentException>(() => ElementalMixture.Create(new Dictionary<string, double> { ["h"] = 1.0, ["H"] = 2.0 }));
    }

    /// <summary>The records <see cref="ShapeViolations"/> names by index: a <see cref="StateRecord"/> is not serializable (xUnit1044), so the theory data carries the index instead.</summary>
    private static readonly IReadOnlyList<(StateRecord Record, bool Rocket, string ReasonContains)> ShapeViolationRecords =
    [
        (new StateRecord(1.0e6, Water), false, "exactly one of enthalpy, temperature and entropy must be given, not 0"),
        (new StateRecord(1.0e6, Water, Enthalpy: 0.0, Temperature: 3000.0), false, "exactly one of enthalpy, temperature and entropy must be given, not 2"),
        (new StateRecord(1.0e6, Water, Temperature: 3000.0) { AreaRatios = [20.0] }, true, "a record with exits needs an enthalpy"),
        (new StateRecord(1.0e6, Water, Enthalpy: -1.0e6) { Flow = FlowModel.FrozenAtThroat }, false, "a flow model needs exits"),
        (new StateRecord(1.0e6, Water, Enthalpy: -1.0e6) { AreaRatios = [20.0] }, false, "the record has exits; call SolveRocketStates"),
        (new StateRecord(1.0e6, Water, Enthalpy: -1.0e6), true, "the record has no exits; call SolveStates"),
        (new StateRecord(1.0e6, new Dictionary<string, double> { ["H"] = -1.0 }, Temperature: 3000.0), false, "abundance"),
        (new StateRecord(1.0e6, new Dictionary<string, double> { ["h"] = 1.0, ["H"] = 2.0 }, Temperature: 3000.0), false, "given twice"),
    ];

    /// <summary>Shape violations.</summary>
    public static TheoryData<int> ShapeViolations() => [.. Enumerable.Range(0, ShapeViolationRecords.Count)];

    /// <summary>Every rule of a state record's shape (BOOT.md, StateRecords) refuses by index, naming the rule in Reason, through the batch method that owns it.</summary>
    [Theory]
    [MemberData(nameof(ShapeViolations))]
    public void AStateRecordThatBreaksARuleOfItsShapeIsRefusedWithItsIndex(int violation)
    {
        var (record, rocket, reasonContains) = ShapeViolationRecords[violation];
        var e = rocket
            ? Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveRocketStates([record]))
            : Assert.Throws<StateRecordException>(() => SolverFixture.Shared.Solver.SolveStates([record]));
        Assert.Equal(0, e.Index);
        Assert.Contains(reasonContains, e.Reason, StringComparison.Ordinal);
        Assert.Equal($"state record 0: {e.Reason}", e.Message);
    }

    /// <summary>Problems without the data they need are rejected.</summary>
    [Fact]
    public void ProblemsWithoutTheDataTheyNeedAreRejected()
    {
        var mixture = ElementalMixture.Create(Water);
        var rocket = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 7.0e6 }));
        Assert.Contains("enthalpy", rocket.Message, StringComparison.Ordinal);
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = 1.0e6 }));
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6 }));
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 0.0, Temperature = 3000.0 }));
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [-1.0] }));
        var fine = SolverFixture.Shared.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6, Temperature = 3000.0 });
        Assert.Equal(CaseStatus.Ok, fine.Status);
        Assert.Null(fine.Mixture.Enthalpy);

        using var thermoOnly = Solver.Create(SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp")), new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var propellant = Propellant.From(thermoOnly.Database).Oxidizer("O2(L)").Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build();
        var transport = Assert.Throws<ArgumentException>(() => thermoOnly.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, Transport = true }));
        Assert.Contains("trans.inp", transport.Message, StringComparison.Ordinal);
        Assert.Equal(CaseStatus.Ok, thermoOnly.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, Flow = FlowModel.FrozenAtThroat }).Status);
    }

    /// <summary>Several mixtures with one problem each are one batch only when their counts and their species lists agree (Problems BOOT.md, Solving); moved here from the union fact it used to close (BOOT.md, the union test becomes three facts).</summary>
    [Fact]
    public void ABatchOverMismatchedMixturesOrProblemCountsIsRejected()
    {
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Build();
        var mixture = SolverFixture.Shared.Solver.MixtureOf(propellant);
        var problem = new EquilibriumProblem { Pressure = 7.0e6 };
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve([mixture, mixture], [problem]));
        var restricted = ElementalMixture.Create(mixture.ElementMoles, mixture.Enthalpy, omit: ["HO2"]);
        _ = Assert.Throws<ArgumentException>(() => SolverFixture.Shared.Solver.Solve([mixture, restricted], [problem, problem]));
    }

    /// <summary>Every public instance method of Solver but Dispose, which is idempotent, and the properties that stay readable.</summary>
    private static IEnumerable<MethodInfo> DisposalGuardedMethods() =>
        typeof(Solver).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                      .Where(m => !m.IsSpecialName && m.Name != nameof(Solver.Dispose));

    /// <summary>One invocation per overload of every method reflection finds, so that a call reaching a disposed solver is observed for each.</summary>
    private List<(string Name, Action<Solver> Invoke)> DisposedInvocations()
    {
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Build();
        var mixture = ElementalMixture.Create(Water, RecordEnthalpy);
        var mixtures = new List<ElementalMixture> { mixture };
        var rocketProblem = new RocketProblem { ChamberPressure = RecordPressure };
        var rocketProblems = new List<RocketProblem> { rocketProblem };
        var equilibriumProblem = new EquilibriumProblem { Pressure = RecordPressure };
        var equilibriumProblems = new List<EquilibriumProblem> { equilibriumProblem };
        var stateRecord = new StateRecord(RecordPressure, Water, Temperature: 3000.0);
        var rocketStateRecord = new StateRecord(RecordPressure, Water, Enthalpy: RecordEnthalpy) { AreaRatios = [20.0] };

        return
        [
            (nameof(Solver.MixtureOf), s => s.MixtureOf(propellant)),
            (nameof(Solver.CandidateSpeciesFor), s => s.CandidateSpeciesFor(["H", "O"])),
            (nameof(Solver.MassOf), s => s.MassOf(mixture)),
            (nameof(Solver.Solve), s => s.Solve(propellant, rocketProblem)),
            (nameof(Solver.Solve), s => s.Solve(propellant, rocketProblems)),
            (nameof(Solver.Solve), s => s.Solve(propellant, equilibriumProblem)),
            (nameof(Solver.Solve), s => s.Solve(propellant, equilibriumProblems)),
            (nameof(Solver.Solve), s => s.Solve(mixture, rocketProblem)),
            (nameof(Solver.Solve), s => s.Solve(mixture, rocketProblems)),
            (nameof(Solver.Solve), s => s.Solve(mixture, equilibriumProblem)),
            (nameof(Solver.Solve), s => s.Solve(mixture, equilibriumProblems)),
            (nameof(Solver.Solve), s => s.Solve(mixtures, rocketProblems)),
            (nameof(Solver.Solve), s => s.Solve(mixtures, equilibriumProblems)),
            (nameof(Solver.SolveStates), s => s.SolveStates([stateRecord])),
            (nameof(Solver.SolveRocketStates), s => s.SolveRocketStates([rocketStateRecord])),
        ];
    }

    /// <summary>The hand-written list above names every overload reflection finds, once each, so that a new method cannot be missed (BOOT.md, F-PR-09).</summary>
    [Fact]
    public void TheDisposalFactsCoverEveryPublicMethodOfTheSolver()
    {
        var reflected = DisposalGuardedMethods().Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var covered = DisposedInvocations().Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(reflected, covered);
    }

    /// <summary>Every public method of a disposed solver throws (BOOT.md, F-PR-09); Database and Accelerator name no device resource and stay readable.</summary>
    [Fact]
    public void EveryPublicMethodOfADisposedSolverThrows()
    {
        var solver = Solver.Create(SolverFixture.Shared.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        solver.Dispose();
        foreach (var (name, invoke) in DisposedInvocations())
        {
            var action = invoke;
            _ = Assert.Throws<ObjectDisposedException>(() => action(solver));
        }

        _ = solver.Database;
        _ = solver.Accelerator;
    }

    /// <summary>A disposed solver refuses work.</summary>
    [Fact]
    public void ADisposedSolverRefusesWork()
    {
        var solver = Solver.Create(SolverFixture.Shared.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Build();
        solver.Dispose();
        solver.Dispose();
        _ = Assert.Throws<ObjectDisposedException>(() => solver.MixtureOf(propellant));
        _ = Assert.Throws<ObjectDisposedException>(() => solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6 }));
    }

    [GeneratedRegex(@"weighs ([0-9.E+-]+) g")]
    private static partial Regex MyRegex();
}
