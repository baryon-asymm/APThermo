using APThermo.Thermo;

namespace APThermo.Performance.Tests;

/// <summary>L0: one test per invariant of the node (RocketInvariants) on every converged fixture case, and the statuses of invalid exits.</summary>
[Collection(CpuFixture.CollectionName)]
public sealed class InvariantTests
{
    /// <summary>
    /// Two paths through the same arithmetic (a station recomputed from a mutated case against its unmutated reference): the
    /// solver's own convergence threshold is 1e-11, so 1e-9 is two decades of head-room.
    /// </summary>
    private const double SelfConsistency = 1e-9;

    /// <summary>The rocket fixture files as theory data, delegating to <see cref="RocketHost.Cases()"/>.</summary>
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

    /// <summary>Every accepted area-ratio exit is supersonic (finding F3).</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryAcceptedAreaRatioExitIsSupersonic(string name)
    {
        var violations = RocketInvariants.SupersonicAreaRatioExits(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>Every Ok station's velocity equals its specific impulse, both finite (the guards audit's F5/F6).</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void VelocityEqualsSpecificImpulseAtEveryOkStation(string name)
    {
        var violations = RocketInvariants.VelocityEqualsSpecificImpulse(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>Every station's PressureRatio is p_c/p of the state actually solved there (finding F6).</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void PressureRatioMatchesTheSolvedPressure(string name)
    {
        var violations = RocketInvariants.PressureRatioMatchesTheSolvedPressure(Load(name));
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>The same holds at the throat family's chamber and throat.</summary>
    [Theory]
    [MemberData(nameof(ThroatCases))]
    public void PressureRatioMatchesTheSolvedPressureAtTheThroatFamily(string name)
    {
        var c = RocketHost.Load("throat", name);
        var solution = RocketHost.Solve(CpuFixture.Shared, RocketInputs.Of(c));
        Assert.Equal(CaseStatus.Ok, solution.Status);
        var violations = RocketInvariants.PressureRatioMatchesTheSolvedPressure(solution);
        Assert.True(violations.Count == 0, string.Join("; ", violations));
    }

    /// <summary>The throat fixture files as theory data.</summary>
    public static TheoryData<string> ThroatCases() => RocketHost.Cases("throat");

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

    /// <summary>An area ratio of exactly one is rejected (finding F2: the reference's rule is "greater than 1.0", not "at least 1.0").</summary>
    [Fact]
    public void AnAreaRatioOfExactlyOneIsInvalid()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-rp1_of3.2_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([1.0], [ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(CpuFixture.Shared, mutated);
        Assert.Equal(CaseStatus.AreaRatioInvalid, solution.Outcome.StationStatus[RocketLayout.FixedStations]);
    }

    /// <summary>An area ratio 1 + 1e-9 clears the F2 gate and converges: Ok and supersonic (Performance BOOT.md, 2026-09-26).</summary>
    [Fact]
    public void AnAreaRatioJustAboveOneIsOkAndSupersonic()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-rp1_of3.2_pc7MPa_shiftingEquilibrium"));
        var mutated = inputs with { Exits = new ExitPlan([1.0 + 1.0e-9], [ExitSpecification.AreaRatio]) };
        var solution = RocketHost.Solve(CpuFixture.Shared, mutated);
        var station = solution.Outcome.Stations[RocketLayout.FixedStations];
        Assert.Equal(CaseStatus.Ok, solution.Outcome.StationStatus[RocketLayout.FixedStations]);
        Assert.True(station.Mach >= 1.0, $"Mach {station.Mach:R}");
    }

    /// <summary>
    /// Finding F3, at the exact input the audit's scan found it at (BOOT.md, the ⚠ of 2026-09-26 under Constraints:
    /// "a subsonic final pass kept the verdict of the supersonic pass before it … all at ε = 1"). F2's gate now
    /// rejects ε = 1 before this stage ever runs it, so the station is driven directly, under the gate, the way
    /// <see cref="SubsonicStationTests"/> drives the never-supersonic path.
    /// </summary>
    [Fact]
    public void AnExitAtTheThroatsOwnAreaRatioIsNeverAcceptedOnAStaleSupersonicVerdict()
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of8_pc10MPa_shiftingEquilibrium"));
        var table = SpeciesTable.Build(CpuFixture.Shared.Database, inputs.System.Elements, inputs.System.Products);
        using var rocketCase = new RocketCase(CpuFixture.Shared.Accelerator, table, inputs);
        var context = rocketCase.Context;
        Assert.Equal(CaseStatus.Ok, ChamberSolve.At(in context, out var chamber));
        Assert.Equal(CaseStatus.Ok, ThroatSearch.At(in context, in chamber, out var throat));

        const int exitStation = RocketLayout.FixedStations;
        StationSolve.CopyComposition(in context, RocketSolver.Throat, exitStation);
        var estimate = new ExitEstimate { Temperature = context.Result.Stations[RocketSolver.Throat].Temperature, Derivative = 1.0 };
        _ = AreaRatioIteration.At(in context, in chamber, in throat, 1.0, exitStation, ref estimate);

        var state = context.Result.Stations[exitStation];
        var status = (CaseStatus)context.Result.StationStatus[exitStation];
        Assert.False(status == CaseStatus.Ok && state.Mach < 1.0, $"accepted at ε = 1 with Mach {state.Mach:R} (status {status})");
    }

    /// <summary>A flow model outside the three named values is invalid input for the whole case (finding F5).</summary>
    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void AnUndefinedFlowModelIsInvalidInput(int flow)
    {
        var inputs = RocketInputs.Of(RocketHost.Load("lox-lh2_of6_pc7MPa_shiftingEquilibrium"));
        var solution = RocketHost.Solve(CpuFixture.Shared, inputs with { Flow = (FlowModel)flow });
        Assert.Equal(CaseStatus.InvalidInput, solution.Status);
        Assert.All(solution.Outcome.StationStatus, s => Assert.Equal(CaseStatus.InvalidInput, s));
    }
}
