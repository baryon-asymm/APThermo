using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Known terminal outcomes survive normal consumption even when no reusable entry was published.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationPublicationObservationTests
{
    /// <summary>The state that prevented publication is explicit and belongs to the preceding transaction.</summary>
    [Theory]
    [InlineData("partial", nameof(CalibrationNonpublication.IncompleteCoverage))]
    [InlineData("over-target", nameof(CalibrationNonpublication.OverTarget))]
    [InlineData("invalid", nameof(CalibrationNonpublication.InvalidObservation))]
    [InlineData("one-case", nameof(CalibrationNonpublication.NoUsefulPrefix))]
    [InlineData("unsupported", nameof(CalibrationNonpublication.UnsupportedIdentity))]
    public void EachNonpublicationReasonIsCarriedIntoTheNextColdTransaction(string mode, string reason)
    {
        using var scenario = new CalibrationScenario();
        using (var run = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan,
            mode == "unsupported" ? null : "input-A", scenario.Views))
        {
            Assert.Equal(1, run.FirstChunkCases());
            run.ObserveSuccessfulLaunch(1, mode == "one-case" ? TimeSpan.FromMilliseconds(500) : TimeSpan.Zero);
            if (mode != "partial")
            {
                run.ObserveSuccessfulLaunch(2, mode == "one-case" ? TimeSpan.FromTicks(1) : TimeSpan.Zero);
                run.ObserveSuccessfulLaunch(4, TimeSpan.Zero);
                run.ObserveSuccessfulLaunch(8, TimeSpan.Zero);
                run.ObserveSuccessfulLaunch(16, mode == "over-target" ? TimeSpan.FromMilliseconds(501) : TimeSpan.Zero);
            }

            if (mode == "invalid")
            {
                run.ObserveSuccessfulLaunch(0, TimeSpan.Zero);
            }

            run.Complete();
        }

        var terminal = scenario.Cache.Observation;
        Assert.Equal(CalibrationOutcome.NotPublished, terminal.Outcome);
        Assert.Equal(Enum.Parse<CalibrationNonpublication>(reason), terminal.NonpublicationReason);
        using var next = scenario.Begin();
        Assert.Equal(1, next.FirstChunkCases());
        var current = scenario.Cache.Observation;
        Assert.False(current.PreviousEntryPresent);
        Assert.Equal(0, current.PreviousPublicationId);
        Assert.Equal(terminal.TransactionId, current.PreviousTransactionId);
        Assert.Equal(terminal.Outcome, current.PreviousOutcome);
        Assert.Equal(terminal.NonpublicationReason, current.PreviousNonpublicationReason);
        Assert.Equal(CalibrationReason.NoPreviousEntry, current.MatchReason);
    }

    /// <summary>Abandonment, rather than absence alone, explains the next cold run.</summary>
    [Fact]
    public void AbandonedResultAndUnsupportedShapeHaveDifferentPreviousOutcomes()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using (var abandoned = scenario.Begin())
        {
            Assert.Equal(15, abandoned.FirstChunkCases());
        }

        var terminal = scenario.Cache.Observation;
        Assert.Equal(CalibrationOutcome.Abandoned, terminal.Outcome);
        using var next = scenario.Begin();
        Assert.False(scenario.Cache.Observation.PreviousEntryPresent);
        Assert.Equal(CalibrationOutcome.Abandoned, scenario.Cache.Observation.PreviousOutcome);
        Assert.Equal(terminal.TransactionId, scenario.Cache.Observation.PreviousTransactionId);
    }
}
