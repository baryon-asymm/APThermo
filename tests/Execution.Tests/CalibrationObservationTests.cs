using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Controlled histories for decisions that a post-return diagnostic must report without changing policy.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationObservationTests
{
    /// <summary>Absence, a completed publication and ordinary consumption remain distinct transactions.</summary>
    [Fact]
    public void ColdPublicationAndRepeatPreserveAssociationAndValueCopies()
    {
        using var scenario = new CalibrationScenario();
        using (var cold = scenario.Begin())
        {
            Assert.Equal(1, cold.FirstChunkCases());
            Assert.Equal(1, cold.FirstChunkCases());
            var pending = scenario.Cache.Observation;
            Assert.False(pending.PreviousEntryPresent);
            Assert.Equal(CalibrationReason.NoPreviousEntry, pending.MatchReason);
            Assert.Null(pending.BeforeUpload.CandidateAge);
            Assert.Equal(CalibrationOutcome.Pending, pending.Outcome);
            Complete(cold);
            Assert.Equal(CalibrationOutcome.Pending, pending.Outcome);
        }

        var published = scenario.Cache.Observation;
        Assert.Equal(CalibrationOutcome.Published, published.Outcome);
        Assert.Equal(published.TransactionId, published.PublicationId);
        scenario.Clock.Advance(TimeSpan.FromMilliseconds(200));
        using var hit = scenario.Begin();
        Assert.Equal(15, hit.FirstChunkCases());
        scenario.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal(15, hit.FirstChunkCases());
        hit.ObserveSuccessfulLaunch(15, TimeSpan.Zero);
        var observation = scenario.Cache.Observation;
        Assert.True(observation.PreviousEntryPresent);
        Assert.Equal(published.PublicationId, observation.PreviousPublicationId);
        Assert.NotEqual(published.TransactionId, observation.TransactionId);
        Assert.Equal(CalibrationReason.Matched, observation.MatchReason);
        Assert.Equal(15, observation.PriorPrefixCases);
        Assert.Equal(observation.BeginGeneration, observation.CurrentGeneration);
        Assert.NotEqual(observation.PreviousGeneration, observation.BeginGeneration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), observation.BeforeUpload.CandidateAge);
        Assert.Equal(TimeSpan.FromMilliseconds(300), observation.AfterUpload.CandidateAge);
        Assert.Equal(CalibrationReason.ReusedPrefix, observation.PhysicalFirstLaunch.Reason);
        Assert.Equal(15, observation.PhysicalFirstLaunch.Cases);
        hit.ObserveSuccessfulLaunch(16, TimeSpan.Zero);
        hit.Complete();
        Assert.Equal(observation.PhysicalFirstLaunch, scenario.Cache.Observation.PhysicalFirstLaunch);
        Assert.Equal(CalibrationOutcome.Published, scenario.Cache.Observation.Outcome);
    }

    /// <summary>Exact-boundary age is inclusive; expiry at either stage and reversed time retain the actual cause.</summary>
    [Theory]
    [InlineData(5000000L, 0L, 15, 15, CalibrationReason.ReusedPrefix)]
    [InlineData(5000001L, 0L, 1, 1, CalibrationReason.Expired)]
    [InlineData(5000000L, 1L, 15, 1, CalibrationReason.Expired)]
    [InlineData(-1L, 0L, 1, 1, CalibrationReason.BackwardClock)]
    public void StagedFreshnessKeepsItsCauseAndNullableAge(long beforeTicks, long uploadTicks,
        int beforeCases, int afterCases, CalibrationReason reason)
    {
        using var scenario = new CalibrationScenario();
        scenario.Clock.Timestamp = TimeSpan.TicksPerSecond;
        scenario.Train();
        scenario.Clock.Advance(TimeSpan.FromTicks(beforeTicks));
        using var run = scenario.Begin();
        Assert.Equal(beforeCases, run.FirstChunkCases());
        var before = scenario.Cache.Observation;
        scenario.Clock.Advance(TimeSpan.FromTicks(uploadTicks));
        Assert.Equal(afterCases, run.FirstChunkCases());
        run.ObserveSuccessfulLaunch(afterCases, TimeSpan.Zero);
        var after = scenario.Cache.Observation;
        Assert.Equal(TimeSpan.FromTicks(beforeTicks), before.BeforeUpload.CandidateAge);
        Assert.Equal(reason, after.AfterUpload.Reason);
        Assert.Equal(after.AfterUpload, after.PhysicalFirstLaunch);
        Assert.Equal(TimeSpan.FromTicks(beforeTicks + uploadTicks), after.AfterUpload.CandidateAge);
    }

    /// <summary>Failed identity short circuits are reported once, with no invented candidate age.</summary>
    [Theory]
    [InlineData(false, CalibrationReason.IdentityMismatch)]
    [InlineData(true, CalibrationReason.UnsupportedIdentity)]
    public void IdentityMissesDoNotPretendToExpire(bool unsupported, CalibrationReason reason)
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var run = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan,
            unsupported ? null : "different-input", scenario.Views);
        Assert.Equal(1, run.FirstChunkCases());
        Assert.Equal(1, run.FirstChunkCases());
        var observation = scenario.Cache.Observation;
        Assert.True(observation.PreviousEntryPresent);
        Assert.Equal(reason, observation.MatchReason);
        Assert.Equal(reason, observation.BeforeUpload.Reason);
        Assert.Equal(reason, observation.AfterUpload.Reason);
        Assert.Null(observation.BeforeUpload.CandidateAge);
        Assert.Null(observation.AfterUpload.CandidateAge);
    }

    /// <summary>A clear changes generation; a newer Begin prevents an old transaction from replacing its snapshot.</summary>
    [Fact]
    public void ExplicitInvalidationAndSupersessionAreNotNormalConsumption()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var older = scenario.Begin();
        Assert.Equal(15, older.FirstChunkCases());
        scenario.Cache.Clear();
        Assert.Equal(CalibrationOutcome.Invalidated, scenario.Cache.Observation.Outcome);
        Assert.Equal(1, older.FirstChunkCases());
        Assert.Equal(CalibrationReason.Superseded, scenario.Cache.Observation.AfterUpload.Reason);
        Complete(older);
        Assert.Equal(CalibrationOutcome.Superseded, scenario.Cache.Observation.Outcome);
        Assert.NotEqual(scenario.Cache.Observation.BeginGeneration, scenario.Cache.Observation.CurrentGeneration);
        using var newer = scenario.Begin();
        var current = scenario.Cache.Observation;
        older.Dispose();
        older.Complete();
        Assert.Equal(current, scenario.Cache.Observation);
    }

    /// <summary>Partial, abandoned and over-target work cannot be labeled a completed publication.</summary>
    [Theory]
    [InlineData("partial", CalibrationOutcome.NotPublished)]
    [InlineData("over-target", CalibrationOutcome.NotPublished)]
    [InlineData("abandoned", CalibrationOutcome.Abandoned)]
    public void NonpublicationIsIndependentOfTheFirstPhysicalChoice(string mode, CalibrationOutcome outcome)
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var run = scenario.Begin();
        Assert.Equal(15, run.FirstChunkCases());
        Assert.Equal(15, run.FirstChunkCases());
        run.ObserveSuccessfulLaunch(15, TimeSpan.Zero);
        var physical = scenario.Cache.Observation.PhysicalFirstLaunch;
        if (mode != "partial")
        {
            run.ObserveSuccessfulLaunch(16, mode == "over-target" ? TimeSpan.FromMilliseconds(501) : TimeSpan.Zero);
        }

        if (mode == "abandoned")
        {
            run.Dispose();
        }
        else
        {
            run.Complete();
        }

        Assert.Equal(outcome, scenario.Cache.Observation.Outcome);
        Assert.Equal(0, scenario.Cache.Observation.PublicationId);
        Assert.Equal(physical, scenario.Cache.Observation.PhysicalFirstLaunch);
    }

    /// <summary>An ordinary CPU Run bypasses hashing and cannot expose an earlier synthetic transaction as fresh.</summary>
    [Fact]
    public void CpuRunDoesNotMasqueradeAsACudaTransaction()
    {
        using var scenario = new CalibrationScenario();
        var batch = new SpeciesFunctionBatch(1);
        batch.Temperature[0] = 1000;
        _ = scenario.Cpu.Run(scenario.Tables, batch);
        Assert.Equal(CalibrationOutcome.NotApplicable, scenario.Cpu.CalibrationObservation.Outcome);
        Assert.Equal(0, scenario.Cpu.CalibrationObservation.TransactionId);
    }

    private static void Complete(CalibrationRun run)
    {
        foreach (var cases in new[] { 1, 2, 4, 8, 16 })
        {
            run.ObserveSuccessfulLaunch(cases, TimeSpan.FromMilliseconds(5));
        }

        run.Complete();
    }
}
