using System.Globalization;
using System.Text.RegularExpressions;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Problems.Tests;

/// <summary>L0: invalid inputs are rejected by name, before any kernel runs.</summary>
[Collection(SolverCollection.Name)]
public sealed class RejectionTests(SolverFixture fixture)
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

    /// <summary>One kilogram of water as element moles (55.508 mol of H2O), for the facts that need a composition and not a mixture in particular.</summary>
    private static readonly IReadOnlyDictionary<string, double> Water = new Dictionary<string, double>(StringComparer.Ordinal) { ["H"] = 111.0168, ["O"] = 55.5084 };

    private PropellantBuilder LoxLh2() => Propellant.From(fixture.Database).Oxidizer("O2(L)").Fuel("H2(L)");

    private static Dictionary<string, double> Scaled(double factor) => OneKilogram.ToDictionary(kv => kv.Key, kv => kv.Value * factor, StringComparer.Ordinal);

    /// <summary>Σ n_i A_i in grams with the database's atomic weights: the number the message must report.</summary>
    private double GramsOf(IReadOnlyDictionary<string, double> composition) => composition.Sum(kv => kv.Value * fixture.Database.AtomicWeight(kv.Key));

    private static EquilibriumProblem AssignedTemperature() => new() { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = RecordPressure, Temperature = 3000.0 };

    [Fact]
    public void A_composition_that_does_not_weigh_one_kilogram_is_rejected_with_its_mass_and_the_tolerance()
    {
        // The record as handed over passes: it is 1.5e-5 off one kilogram, and it solves.
        var solved = Assert.Single(fixture.Solver.SolveStates([new StateRecord(RecordPressure, OneKilogram, Enthalpy: RecordEnthalpy)]));
        Assert.Equal(CaseStatus.Ok, solved.Status);

        // Doubled (two kilograms), in mol/g or kmol/kg (a thousandth) and in mmol/kg (a thousandfold): refused before any kernel runs,
        // naming the record, the mass found in grams and the tolerance.
        foreach (var factor in new[] { 2.0, 1.0e-3, 1.0e3 })
        {
            var composition = Scaled(factor);
            var e = Assert.Throws<MixtureMassException>(() => fixture.Solver.SolveStates([new StateRecord(RecordPressure, composition, Enthalpy: RecordEnthalpy)]));
            Assert.Equal(0, e.Index);
            Assert.Equal(ElementalMixture.DefaultMassTolerance, e.Tolerance);
            Assert.StartsWith("state record 0: the composition weighs ", e.Message, StringComparison.Ordinal);
            Assert.Equal("state record 0: " + e.Reason, e.Message);
            var grams = GramsOf(composition);
            Assert.Equal(grams * 1.0e-3, e.Mass, grams * 1.0e-15);
            var reported = double.Parse(Regex.Match(e.Message, @"weighs ([0-9.E+-]+) g").Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.Equal(grams, reported, grams * 1.0e-6);
            Assert.Contains("element moles are per kilogram of mixture", e.Message, StringComparison.Ordinal);
            Assert.Contains("must weigh 1000 g within 1 %", e.Message, StringComparison.Ordinal);
        }

        // The other front doors of the elemental form name the mixture by its index in the batch.
        var doubled = ElementalMixture.Create(Scaled(2.0), RecordEnthalpy);
        var rocket = Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve(doubled, new RocketProblem { ChamberPressure = RecordPressure, AreaRatios = [10.0] }));
        Assert.StartsWith("mixture 0: the composition weighs ", rocket.Message, StringComparison.Ordinal);
        var equilibrium = Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve(doubled, AssignedTemperature()));
        Assert.StartsWith("mixture 0: the composition weighs ", equilibrium.Message, StringComparison.Ordinal);
        var good = ElementalMixture.Create(OneKilogram, RecordEnthalpy);
        var problem = new EquilibriumProblem { Pressure = RecordPressure };
        var batch = Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve([good, doubled], [problem, problem]));
        Assert.Equal(1, batch.Index);
        Assert.StartsWith("mixture 1: ", batch.Message, StringComparison.Ordinal);

        // The tolerance is the one the message names: 0.9 % heavy solves, 1.1 % heavy is refused.
        Assert.Equal(CaseStatus.Ok, fixture.Solver.Solve(ElementalMixture.Create(Scaled(1.009)), AssignedTemperature()).Status);
        Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve(ElementalMixture.Create(Scaled(1.011)), AssignedTemperature()));
    }

    [Fact]
    public void The_tolerance_a_mixture_declares_is_the_one_applied()
    {
        // The record made 2 % and 2.5 % heavy: refused at the default through the state batch, solved when the batch declares 3 %, every
        // record of it. Heavy, not light: the same record made 1 % to 10 % light does not converge as an hp state at 6.5 MPa (the
        // equilibrium node's open defect at variable temperature, found 2026-09-13 with this test), and a record that fails numerically
        // would not show that the check let it through.
        var heavy = new StateRecord(RecordPressure, Scaled(1.02), Enthalpy: RecordEnthalpy);
        var heavier = new StateRecord(RecordPressure, Scaled(1.025), Enthalpy: RecordEnthalpy);
        var refused = Assert.Throws<MixtureMassException>(() => fixture.Solver.SolveStates([heavy, heavier]));
        Assert.Equal(ElementalMixture.DefaultMassTolerance, refused.Tolerance);
        var results = fixture.Solver.SolveStates([heavy, heavier], new StateBatchOptions(MassTolerance: 0.03));
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(CaseStatus.Ok, r.Status));
        Assert.All(results, r => Assert.Equal(0.03, r.Mixture.MassTolerance));

        // Through Create, for the direct overloads; the exception and its message name the tolerance in force.
        var declared = ElementalMixture.Create(Scaled(1.02), RecordEnthalpy, massTolerance: 0.03);
        Assert.Equal(0.03, declared.MassTolerance);
        Assert.Equal(CaseStatus.Ok, fixture.Solver.Solve(declared, AssignedTemperature()).Status);
        Assert.Equal(CaseStatus.Ok, fixture.Solver.Solve(declared, new RocketProblem { ChamberPressure = RecordPressure, Flow = FlowModel.FrozenAtChamber }).Status);
        var beyond = Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve(ElementalMixture.Create(Scaled(1.05), RecordEnthalpy, massTolerance: 0.03), AssignedTemperature()));
        Assert.Equal(0.03, beyond.Tolerance);
        Assert.EndsWith("must weigh 1000 g within 3 %", beyond.Message, StringComparison.Ordinal);

        // The propellant path and a mixture that names no tolerance declare the default; a tolerance that is no tolerance is refused by name.
        Assert.Equal(ElementalMixture.DefaultMassTolerance, fixture.Solver.MixtureOf(LoxLh2().OxidizerToFuelRatio(6.0).Build()).MassTolerance);
        Assert.Equal(ElementalMixture.DefaultMassTolerance, ElementalMixture.Create(OneKilogram).MassTolerance);
        foreach (var invalid in new[] { -0.01, double.NaN, double.PositiveInfinity })
        {
            var e = Assert.Throws<ArgumentException>(() => ElementalMixture.Create(OneKilogram, massTolerance: invalid));
            Assert.Equal("massTolerance", e.ParamName);
        }
    }

    [Fact]
    public void A_reactant_record_whose_molar_mass_contradicts_its_formula_is_caught_at_the_solve()
    {
        // The committed file's ADN reactant record carries 630.0 kg/kmol against its formula H4N4O4 (124.06 with the file's own atomic
        // weights), so the element moles per kilogram the record implies weigh a fifth of a kilogram: the propellant path is checked too,
        // and the solve says so instead of computing with them. When the record is corrected upstream, this fact goes with it.
        var adn = fixture.Database["ADN"];
        var propellant = Propellant.From(fixture.Database).Named("ADN", 1.0).Build();
        var e = Assert.Throws<MixtureMassException>(() => fixture.Solver.Solve(propellant, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6, Temperature = 2000.0 }));
        Assert.StartsWith("the propellant's mixture (case 0): the composition weighs ", e.Message, StringComparison.Ordinal);
        var formulaMass = adn.Formula.Sum(pair => pair.Count * fixture.Database.AtomicWeight(pair.Symbol));
        Assert.Equal(formulaMass / adn.MolarMass, e.Mass, 1.0e-12);
        Assert.True(e.Mass < 0.25, $"the ADN mixture weighs {e.Mass} kg");
    }

    [Fact]
    public void An_unknown_reactant_is_rejected_by_name()
    {
        var missing = Assert.Throws<KeyNotFoundException>(() => Propellant.From(fixture.Database).Fuel("Unobtainium").Build());
        Assert.Contains("Unobtainium", missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_temperature_outside_the_record_range_is_rejected_by_name()
    {
        var outside = Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Named("AL(cr)", 1.0, 1000.0).Build());
        Assert.Contains("AL(cr)", outside.Message, StringComparison.Ordinal);
        var assigned = Assert.Throws<ArgumentException>(() => LoxLh2().Oxidizer("O2(L)", 150.0).OxidizerToFuelRatio(6.0).Build());
        Assert.Contains("O2(L)", assigned.Message, StringComparison.Ordinal);

        // Within the margin the reference itself extrapolates: AL(cr) at 298.15 K below its first fit, and an assigned temperature within 10 K.
        Assert.Equal(298.15, Propellant.From(fixture.Database).Named("AL(cr)", 1.0).Build().Resolved[0].Temperature);
        Assert.Equal(95.0, Propellant.From(fixture.Database).Oxidizer("O2(L)", 95.0).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build().Resolved[0].Temperature);
        Assert.Equal(90.17, LoxLh2().OxidizerToFuelRatio(6.0).Build().Resolved[0].Temperature);
    }

    [Fact]
    public void Mixture_rules_that_leave_a_group_empty_or_ambiguous_are_rejected()
    {
        var zero = Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Oxidizer("O2(L)", amount: 0.0).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build());
        Assert.Contains("oxidizer group", zero.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(0.0).Build());
        Assert.Throws<ArgumentException>(() => LoxLh2().Build());
        Assert.Throws<ArgumentException>(() => LoxLh2().Named("H2O(L)", 0.1).OxidizerToFuelRatio(6.0).Build());
        Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build());
        Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Named("H2(L)", 0.0).Build());
        Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Build());
        Assert.Throws<ArgumentException>(() => Reactant.FromDatabase("H2(L)", ReactantRole.Fuel, -1.0));

        var monopropellant = Propellant.From(fixture.Database).Fuel("C2H8N2(L),UDMH").Build();
        Assert.IsType<MixtureSpecification.MassFractions>(monopropellant.Mixture);
        Assert.Throws<ArgumentException>(() => fixture.Solver.MixtureOf(monopropellant, 2.0));
    }

    [Fact]
    public void A_custom_reactant_with_an_unknown_element_is_rejected_by_name()
    {
        var binder = Reactant.Custom("Binder", new CustomReactantDefinition([new ElementCount("C", 1.0), new ElementCount("Xx", 0.5)], -1000.0, 298.15), ReactantRole.Fuel, 1.0);
        var unknown = Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Custom(binder).Build());
        Assert.Contains("Binder", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("XX", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", new CustomReactantDefinition([], -1000.0, 298.15), ReactantRole.Fuel, 1.0));
        Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", new CustomReactantDefinition([new ElementCount("C", 0.0)], -1000.0, 298.15), ReactantRole.Fuel, 1.0));
    }

    [Fact]
    public void An_only_list_beyond_the_elements_is_rejected_and_a_valid_one_is_used_as_given()
    {
        var beyond = Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(6.0).Only("H2O", "CO2").Build());
        Assert.Contains("CO2", beyond.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => LoxLh2().OxidizerToFuelRatio(6.0).Only().Build());
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Only("H2O", "H2", "O2", "OH").Build();
        var result = fixture.Solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [20.0] });
        Assert.Equal(CaseStatus.Ok, result.Status);
        Assert.Equal(["H2O", "H2", "O2", "OH"], result.Species);
    }

    [Fact]
    public void Invalid_state_records_are_rejected_by_index_or_element()
    {
        var composition = Water;
        var two = Assert.Throws<StateRecordException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, composition, Enthalpy: 0.0, Temperature: 3000.0)]));
        Assert.Contains("record 0", two.Message, StringComparison.Ordinal);
        Assert.Equal(0, two.Index);
        var none = Assert.Throws<StateRecordException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, composition, Temperature: 3000.0), new StateRecord(1.0e6, composition)]));
        Assert.Contains("record 1", none.Message, StringComparison.Ordinal);
        Assert.Equal(1, none.Index);
        var negative = Assert.Throws<StateRecordException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, new Dictionary<string, double> { ["H"] = -1.0 }, Temperature: 3000.0)]));
        Assert.Contains("'H'", negative.Message, StringComparison.Ordinal);
        var unknown = Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, new Dictionary<string, double> { ["XX"] = 1.0 }, Temperature: 3000.0)]));
        Assert.Contains("XX", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([]));
        Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([new StateRecord(0.0, composition, Temperature: 3000.0)]));
        Assert.Throws<ArgumentException>(() => ElementalMixture.Create(new Dictionary<string, double>()));
        Assert.Throws<ArgumentException>(() => ElementalMixture.Create(new Dictionary<string, double> { ["h"] = 1.0, ["H"] = 2.0 }));
    }

    [Fact]
    public void Problems_without_the_data_they_need_are_rejected()
    {
        var mixture = ElementalMixture.Create(Water);
        var rocket = Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 7.0e6 }));
        Assert.Contains("enthalpy", rocket.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = 1.0e6 }));
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6 }));
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 0.0, Temperature = 3000.0 }));
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(mixture, new RocketProblem { ChamberPressure = 7.0e6, AreaRatios = [-1.0] }));
        var fine = fixture.Solver.Solve(mixture, new EquilibriumProblem { Kind = ProblemKind.AssignedTemperaturePressure, Pressure = 1.0e6, Temperature = 3000.0 });
        Assert.Equal(CaseStatus.Ok, fine.Status);
        Assert.Null(fine.Mixture.Enthalpy);

        using var thermoOnly = Solver.Create(SpeciesDatabase.Load(Path.Combine(RepositoryPaths.Data, "thermo.inp")), new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var propellant = Propellant.From(thermoOnly.Database).Oxidizer("O2(L)").Fuel("H2(L)").OxidizerToFuelRatio(6.0).Build();
        var transport = Assert.Throws<ArgumentException>(() => thermoOnly.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, Transport = true }));
        Assert.Contains("trans.inp", transport.Message, StringComparison.Ordinal);
        Assert.Equal(CaseStatus.Ok, thermoOnly.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6, Flow = FlowModel.FrozenAtThroat }).Status);
    }

    [Fact]
    public void A_disposed_solver_refuses_work()
    {
        var solver = Solver.Create(fixture.Database, new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var propellant = LoxLh2().OxidizerToFuelRatio(6.0).Build();
        solver.Dispose();
        solver.Dispose();
        Assert.Throws<ObjectDisposedException>(() => solver.MixtureOf(propellant));
        Assert.Throws<ObjectDisposedException>(() => solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6 }));
    }
}
