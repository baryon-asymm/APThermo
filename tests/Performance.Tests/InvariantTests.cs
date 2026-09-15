using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>L0: one test per invariant of the node (RocketInvariants) on every converged fixture case, and the statuses of invalid exits.</summary>
[Collection(CpuCollection.Name)]
public sealed class InvariantTests(CpuFixture fixture)
{
    /// <summary>
    /// Two paths through the same arithmetic (a station recomputed from a mutated case against its unmutated reference): the
    /// solver's own convergence threshold is 1e-11, so 1e-9 is two decades of head-room.
    /// </summary>
    private const double SelfConsistency = 1e-9;

    public static IEnumerable<object[]> Cases() => RocketHost.Cases();

    private RocketSolution Load(string name)
    {
        var solution = RocketHost.Solve(fixture, RocketHost.Load(name));
        Assert.Equal(CaseStatus.Ok, solution.Status);
        return solution;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_throat_is_sonic(string name)
    {
        var violations = RocketInvariants.SonicThroat(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Entropy_is_constant_along_the_nozzle(string name)
    {
        var violations = RocketInvariants.ConstantEntropy(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Velocity_follows_the_energy_equation(string name)
    {
        var violations = RocketInvariants.EnergyEquation(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Assigned_area_and_pressure_ratios_are_met(string name)
    {
        var violations = RocketInvariants.AssignedExit(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void The_composition_is_frozen_after_the_freezing_station(string name)
    {
        var violations = RocketInvariants.FrozenComposition(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    [Fact]
    public void An_area_ratio_below_one_fails_its_station_only()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([0.5, inputs.Exits.Values[0]], [ExitSpecification.AreaRatio, ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(fixture, mutated);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Status);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[0]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[1]);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Outcome.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[3]);

        // The station after the failed one starts from the last converged station, so it reaches the same state to rounding level.
        var reference = RocketHost.Solve(fixture, inputs);
        Assert.Equal(reference.Outcome.Stations[2].Temperature, solution.Outcome.Stations[3].Temperature, reference.Outcome.Stations[2].Temperature * SelfConsistency);
        Assert.Equal(reference.Outcome.Figures[2].SpecificImpulse, solution.Outcome.Figures[3].SpecificImpulse, reference.Outcome.Figures[2].SpecificImpulse * SelfConsistency);
    }

    [Fact]
    public void A_pressure_ratio_not_above_one_fails_its_station_only()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([1.0, inputs.Exits.Values[0]], [ExitSpecification.PressureRatio, ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(fixture, mutated);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(CaseStatus.InvalidInput, solution.Outcome.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[3]);
    }

    [Fact]
    public void A_case_without_exits_gives_the_chamber_and_the_throat()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(fixture, inputs with { Exits = new ExitPlan([], []) });
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Equal(2, solution.StationCount);
        Assert.True(solution.Outcome.Figures[1].CharacteristicVelocity > 0.0);
    }

    [Fact]
    public void A_non_positive_chamber_pressure_is_invalid_input()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(fixture, inputs with { ChamberPressure = 0.0 });
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.All(solution.Outcome.StationStatus, s => Assert.Equal(CaseStatus.InvalidInput, s));
    }
}
