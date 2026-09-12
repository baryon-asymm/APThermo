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
    private PropellantBuilder LoxLh2() => Propellant.From(fixture.Database).Oxidizer("O2(L)").Fuel("H2(L)");

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
        Assert.Throws<ArgumentException>(() => fixture.Solver.Mixture(monopropellant, 2.0));
        Assert.Throws<ArgumentException>(() => fixture.Solver.Solve(new RocketSweep(monopropellant, [2.0], [1.0e6], [10.0])));
    }

    [Fact]
    public void A_custom_reactant_with_an_unknown_element_is_rejected_by_name()
    {
        var binder = Reactant.Custom("Binder", [new ElementCount("C", 1.0), new ElementCount("Xx", 0.5)], -1000.0, 298.15, ReactantRole.Fuel, 1.0);
        var unknown = Assert.Throws<ArgumentException>(() => Propellant.From(fixture.Database).Custom(binder).Build());
        Assert.Contains("Binder", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("XX", unknown.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", [], -1000.0, 298.15, ReactantRole.Fuel, 1.0));
        Assert.Throws<ArgumentException>(() => Reactant.Custom("Binder", [new ElementCount("C", 0.0)], -1000.0, 298.15, ReactantRole.Fuel, 1.0));
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
        var composition = new Dictionary<string, double> { ["H"] = 100.0, ["O"] = 50.0 };
        var two = Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, composition, Enthalpy: 0.0, Temperature: 3000.0)]));
        Assert.Contains("record 0", two.Message, StringComparison.Ordinal);
        var none = Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, composition, Temperature: 3000.0), new StateRecord(1.0e6, composition)]));
        Assert.Contains("record 1", none.Message, StringComparison.Ordinal);
        var negative = Assert.Throws<ArgumentException>(() => fixture.Solver.SolveStates([new StateRecord(1.0e6, new Dictionary<string, double> { ["H"] = -1.0 }, Temperature: 3000.0)]));
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
        var mixture = ElementalMixture.Create(new Dictionary<string, double> { ["H"] = 100.0, ["O"] = 50.0 });
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
        Assert.Throws<ObjectDisposedException>(() => solver.Mixture(propellant));
        Assert.Throws<ObjectDisposedException>(() => solver.Solve(propellant, new RocketProblem { ChamberPressure = 7.0e6 }));
    }
}
