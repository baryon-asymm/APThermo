using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>L0: one test per invariant of the node (RocketInvariants) on every converged fixture case, and the statuses of invalid exits.</summary>
public sealed class InvariantTests
{
    /// <summary>
    /// Two paths through the same arithmetic (a station recomputed from a mutated case against its unmutated reference): the
    /// solver's own convergence threshold is 1e-11, so 1e-9 is two decades of head-room.
    /// </summary>
    private const double SelfConsistency = 1e-9;

    /// <summary>The rocket fixture files as theory data, delegating to <see cref="RocketHost.Cases"/>.</summary>
    public static TheoryData<string> Cases() => RocketHost.Cases();

    private static RocketSolution Load(string name)
    {
        var solution = RocketHost.Solve(CpuFixture.Shared, RocketHost.Load(name));
        Assert.Equal(CaseStatus.Ok, solution.Status);
        return solution;
    }

    /// <summary>The throat is sonic.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheThroatIsSonic(string name)
    {
        var violations = RocketInvariants.SonicThroat(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>Entropy is constant along the nozzle.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EntropyIsConstantAlongTheNozzle(string name)
    {
        var violations = RocketInvariants.ConstantEntropy(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>Velocity follows the energy equation.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void VelocityFollowsTheEnergyEquation(string name)
    {
        var violations = RocketInvariants.EnergyEquation(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>Assigned area and pressure ratios are met.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AssignedAreaAndPressureRatiosAreMet(string name)
    {
        var violations = RocketInvariants.AssignedExit(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>The composition is frozen after the freezing station.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCompositionIsFrozenAfterTheFreezingStation(string name)
    {
        var violations = RocketInvariants.FrozenComposition(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>An area ratio below one fails its station only.</summary>
    [Fact]
    public void AnAreaRatioBelowOneFailsItsStationOnly()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([0.5, inputs.Exits.Values[0]], [ExitSpecification.AreaRatio, ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(CpuFixture.Shared, mutated);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Status);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[0]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[1]);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Outcome.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[3]);

        // The station after the failed one starts from the last converged station, so it reaches the same state to rounding level.
        var reference = RocketHost.Solve(CpuFixture.Shared, inputs);
        Assert.Equal(reference.Outcome.Stations[2].Temperature, solution.Outcome.Stations[3].Temperature, reference.Outcome.Stations[2].Temperature * SelfConsistency);
        Assert.Equal(reference.Outcome.Figures[2].SpecificImpulse, solution.Outcome.Figures[3].SpecificImpulse, reference.Outcome.Figures[2].SpecificImpulse * SelfConsistency);
    }

    /// <summary>A pressure ratio not above one fails its station only.</summary>
    [Fact]
    public void APressureRatioNotAboveOneFailsItsStationOnly()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([1.0, inputs.Exits.Values[0]], [ExitSpecification.PressureRatio, ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(CpuFixture.Shared, mutated);
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.Equal(CaseStatus.InvalidInput, solution.Outcome.StationStatus[2]);
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[3]);
    }

    /// <summary>A case without exits gives the chamber and the throat.</summary>
    [Fact]
    public void ACaseWithoutExitsGivesTheChamberAndTheThroat()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(CpuFixture.Shared, inputs with { Exits = new ExitPlan([], []) });
        Assert.Equal(CaseStatus.Ok, solution.Status);
        Assert.Equal(2, solution.StationCount);
        Assert.True(solution.Outcome.Figures[1].CharacteristicVelocity > 0.0);
    }

    /// <summary>A non-positive chamber pressure is invalid input.</summary>
    [Fact]
    public void ANonPositiveChamberPressureIsInvalidInput()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(CpuFixture.Shared, inputs with { ChamberPressure = 0.0 });
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.All(solution.Outcome.StationStatus, s => Assert.Equal(CaseStatus.InvalidInput, s));
    }
}
