using APThermo.Equilibrium.Recovery;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// L0: the pure transitions of the temperature bracket on the host, no views (Recovery BOOT.md, "The bracket"): the ln 2
/// clamp, the bisection safeguard, the floor stop, the retreats, the give-up statuses, the lever clamp and the finals.
/// </summary>
public sealed class TemperatureBracketTests
{
    private const double LogTwo = 0.6931471805599453;

    /// <summary>A one-sided step is limited to a factor of two in T, whatever the Newton step.</summary>
    [Fact]
    public void AOneSidedStepIsClampedToLnTwo()
    {
        var bracket = Started(3800.0);
        var step = bracket.Record(target: 1.0e6, value: 0.0, slope: 1.0, EndKind.Gas);

        var move = bracket.Advance(step, floor: 0.0);

        Assert.Equal(1.0e6, step);
        Assert.Equal(BracketMove.Probe, move);
        Assert.Equal(Math.Log(3800.0) + LogTwo, bracket.ProbeX, 12);
        Assert.True(bracket.HaveLow && !bracket.HaveHigh);
    }

    /// <summary>A slope that is not positive (a plateau's zero, a NaN) gives a step of ln 2 toward the target.</summary>
    [Theory]
    [InlineData(0.0, 1.0, 1.0)]
    [InlineData(-1.0, 0.0, -1.0)]
    [InlineData(double.NaN, 1.0, 1.0)]
    public void ASlopeThatIsNotPositiveStepsLnTwoTowardTheTarget(double slope, double target, double sign)
    {
        var bracket = Started(1000.0);

        var step = bracket.Record(target: target, value: 0.5, slope: slope, EndKind.Gas);

        Assert.Equal(sign * LogTwo, step);
    }

    /// <summary>A probe becomes the lower end when its property lies below the target, else the upper.</summary>
    [Fact]
    public void AProbeBecomesTheEndOfItsSide()
    {
        var bracket = Started(1000.0);
        _ = bracket.Record(target: 10.0, value: 4.0, slope: 1.0, EndKind.Gas);
        bracket.ProbeX = Math.Log(2000.0);
        _ = bracket.Record(target: 10.0, value: 12.0, slope: 1.0, EndKind.Gasless);

        Assert.True(bracket.HaveLow && bracket.HaveHigh);
        Assert.Equal(Math.Log(1000.0), bracket.LowX, 12);
        Assert.Equal(Math.Log(2000.0), bracket.HighX, 12);
        Assert.Equal((4.0, 12.0), (bracket.LowP, bracket.HighP));
        Assert.Equal((EndKind.Gas, EndKind.Gasless), (bracket.LowKind, bracket.HighKind));
        Assert.False(bracket.LastBelow);
    }

    /// <summary>Inside a bracket the Newton point is taken when it lies inside and halves the step before last; else the midpoint.</summary>
    [Fact]
    public void InsideTheBracketTheBisectionSafeguardTakesTheMidpointWhenTheNewtonPointFails()
    {
        var inside = Bracketed(lowX: 5.0, highX: 7.0, probeX: 5.5);
        inside.OlderStep = 1.0;
        Assert.Equal(BracketMove.Probe, inside.Advance(step: 0.4, floor: 0.0));
        Assert.Equal(5.9, inside.ProbeX, 12);

        var outside = Bracketed(lowX: 5.0, highX: 7.0, probeX: 5.5);
        outside.OlderStep = 10.0;
        Assert.Equal(BracketMove.Probe, outside.Advance(step: 3.0, floor: 0.0));
        Assert.Equal(6.0, outside.ProbeX, 12);

        var slow = Bracketed(lowX: 5.0, highX: 7.0, probeX: 5.5);
        slow.OlderStep = 0.5;
        Assert.Equal(BracketMove.Probe, slow.Advance(step: 0.4, floor: 0.0));
        Assert.Equal(6.0, slow.ProbeX, 12);
    }

    /// <summary>Going down with one end known, a probe stops at the data floor of a condensed record rather than crossing it.</summary>
    [Fact]
    public void AStepDownStopsAtTheDataFloor()
    {
        var bracket = Started(600.0);
        bracket.HaveHigh = true;
        bracket.HighX = bracket.ProbeX;
        bracket.LastBelow = false;
        bracket.LastKind = EndKind.Gas;
        bracket.HighKind = EndKind.Gas;

        var move = bracket.Advance(step: -0.5, floor: 550.0);

        Assert.Equal(BracketMove.Probe, move);
        Assert.Equal(Math.Log(550.0), bracket.ProbeX, 12);
    }

    /// <summary>A failed probe retreats halfway toward the nearer end, eight times; then, or with no end known, the case gives up with its first failure.</summary>
    [Fact]
    public void AFailedProbeRetreatsEightTimesAndThenGivesUpWithTheFirstFailure()
    {
        var bracket = Started(1000.0);
        bracket.FirstFailure = CaseStatus.SingularMatrix;
        bracket.HaveHigh = true;
        bracket.HighX = 7.0;
        bracket.ProbeX = 5.0;

        Assert.Equal(BracketMove.Probe, bracket.Retreat());
        Assert.Equal(6.0, bracket.ProbeX, 12);
        for (var retreat = 1; retreat < 8; retreat++)
        {
            Assert.Equal(BracketMove.Probe, bracket.Retreat());
        }

        Assert.Equal(BracketMove.GiveUp, bracket.Retreat());
        Assert.Equal(CaseStatus.SingularMatrix, bracket.GiveUpStatus);

        var alone = Started(1000.0);
        alone.FirstFailure = CaseStatus.NotConverged;
        Assert.Equal(BracketMove.GiveUp, alone.Retreat());
        Assert.Equal(CaseStatus.NotConverged, alone.GiveUpStatus);
    }

    /// <summary>The domain's edge in the target's direction gives <c>TemperatureOutOfRange</c>; the probe budget gives the first failure, or the lever final when both ends are known.</summary>
    [Fact]
    public void TheGiveUpStatusesAreTheEdgeAndTheBudget()
    {
        var edge = Started(20000.0);
        var step = edge.Record(target: 1.0e6, value: 0.0, slope: 1.0, EndKind.Gas);
        edge.ProbeX = Math.Log(EquilibriumSolver.MaxTemperature);
        Assert.Equal(BracketMove.GiveUp, edge.Advance(step, floor: 0.0));
        Assert.Equal(CaseStatus.TemperatureOutOfRange, edge.GiveUpStatus);

        var spent = Started(1000.0);
        spent.FirstFailure = CaseStatus.NotConverged;
        _ = spent.Record(target: 1.0e6, value: 0.0, slope: 1.0, EndKind.Gas);
        spent.Probes = 40;
        Assert.Equal(BracketMove.GiveUp, spent.Advance(step: 0.5, floor: 0.0));
        Assert.Equal(CaseStatus.NotConverged, spent.GiveUpStatus);

        var both = Bracketed(lowX: 5.0, highX: 7.0, probeX: 5.5);
        both.Probes = 40;
        Assert.Equal(BracketMove.AttemptFromLever, both.Advance(step: 0.4, floor: 0.0));
    }

    /// <summary>The lever fraction is clamped to [0, 1], 0.5 where the difference of the ends is not positive; the temperature follows it.</summary>
    [Fact]
    public void TheLeverRuleIsClampedAndNeverLeavesTheEnds()
    {
        var bracket = Bracketed(lowX: Math.Log(1000.0), highX: Math.Log(2000.0), probeX: Math.Log(1500.0));
        bracket.LowP = 10.0;
        bracket.HighP = 30.0;

        Assert.Equal(0.0, bracket.LeverFraction(5.0));
        Assert.Equal(1.0, bracket.LeverFraction(35.0));
        Assert.Equal(0.25, bracket.LeverFraction(15.0), 12);
        Assert.Equal(1250.0, bracket.LeverTemperature(15.0), 9);
        Assert.Equal(1000.0, bracket.LeverTemperature(5.0), 9);
        bracket.HighP = 10.0;
        Assert.Equal(0.5, bracket.LeverFraction(10.0));
    }

    /// <summary>
    /// What ends the search is chosen by what the ends found: two gasless ends within 1e-9 end in the verdict alone, any other
    /// narrow pair in the case itself from the lever; a gas probe's converged step seeds the final from the probe, a gasless
    /// probe's tiny step ends in the verdict at the temperature it converged on.
    /// </summary>
    [Fact]
    public void TheFinalIsChosenByWhatTheEndsFound()
    {
        var gasless = Bracketed(lowX: 6.0, highX: 6.0 + 5.0e-10, probeX: 6.0, EndKind.Gasless);
        Assert.Equal(BracketMove.GaslessFromLever, gasless.Advance(step: 1.0, floor: 0.0));

        var mixed = Bracketed(lowX: 6.0, highX: 6.0 + 5.0e-8, probeX: 6.0);
        mixed.HighKind = EndKind.Gasless;
        Assert.Equal(BracketMove.AttemptFromLever, mixed.Advance(step: 1.0, floor: 0.0));

        var wideGasless = Bracketed(lowX: 6.0, highX: 6.0 + 5.0e-8, probeX: 6.0, EndKind.Gasless);
        Assert.Equal(BracketMove.Probe, wideGasless.Advance(step: 1.0e-8, floor: 0.0));

        var converged = Bracketed(lowX: 5.0, highX: 7.0, probeX: 6.0);
        Assert.Equal(BracketMove.AttemptFromProbe, converged.Advance(step: 1.0e-8, floor: 0.0));

        var tiny = Bracketed(lowX: 5.0, highX: 7.0, probeX: 6.0, EndKind.Gasless);
        Assert.Equal(BracketMove.GaslessAtProbe, tiny.Advance(step: 1.0e-13, floor: 0.0));
        Assert.Equal(6.0 + 1.0e-13, tiny.FinalX);
    }

    private static TemperatureBracket Started(double estimate)
    {
        var bracket = default(TemperatureBracket);
        bracket.Start(CaseStatus.NotConverged, estimate);
        return bracket;
    }

    private static TemperatureBracket Bracketed(double lowX, double highX, double probeX, EndKind kind = EndKind.Gas)
    {
        var bracket = default(TemperatureBracket);
        bracket.Start(CaseStatus.NotConverged, 1000.0);
        bracket.HaveLow = true;
        bracket.HaveHigh = true;
        bracket.LowX = lowX;
        bracket.HighX = highX;
        bracket.ProbeX = probeX;
        bracket.LowKind = kind;
        bracket.HighKind = kind;
        bracket.LastKind = kind;
        bracket.LastBelow = true;
        bracket.Probes = 2;
        return bracket;
    }
}
