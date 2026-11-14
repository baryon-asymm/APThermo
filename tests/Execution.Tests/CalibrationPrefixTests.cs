namespace APThermo.Execution.Tests;

/// <summary>Only a fully observed initial prefix may be probed, within its short empirical validity window.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationPrefixTests
{
    /// <summary>The largest qualifying boundary is 15, not rounded 16 or a fast tail launch of 16.</summary>
    [Fact]
    public void FullInitialBoundaryIsNeitherRoundedNorReplacedByTheFastTail()
    {
        using var scenario = new CalibrationScenario();
        using (var run = scenario.Begin())
        {
            run.ObserveSuccessfulLaunch(1, TimeSpan.FromMilliseconds(100));
            run.ObserveSuccessfulLaunch(2, TimeSpan.FromMilliseconds(100));
            run.ObserveSuccessfulLaunch(4, TimeSpan.FromMilliseconds(100));
            run.ObserveSuccessfulLaunch(8, TimeSpan.FromMilliseconds(200));
            run.ObserveSuccessfulLaunch(16, TimeSpan.Zero);
            run.Complete();
        }

        using var hit = scenario.Begin();
        Assert.Equal(15, hit.FirstChunkCases());
        Assert.True(hit.ReusedPrefix);
    }

    /// <summary>A cheap tail cannot erase cumulative prefix cost above the target.</summary>
    [Fact]
    public void CumulativePrefixCostStopsAtTheLastQualifyingBoundary()
    {
        using var scenario = new CalibrationScenario();
        using (var run = scenario.Begin())
        {
            run.ObserveSuccessfulLaunch(1, TimeSpan.FromMilliseconds(200));
            run.ObserveSuccessfulLaunch(2, TimeSpan.FromMilliseconds(200));
            run.ObserveSuccessfulLaunch(4, TimeSpan.FromMilliseconds(200));
            run.ObserveSuccessfulLaunch(8, TimeSpan.Zero);
            run.ObserveSuccessfulLaunch(16, TimeSpan.Zero);
            run.Complete();
        }

        using var hit = scenario.Begin();
        Assert.Equal(3, hit.FirstChunkCases());
    }

    /// <summary>A single over-target physical launch, even after a useful prefix, prevents any publication.</summary>
    [Fact]
    public void AnyOverTargetLaunchInvalidatesTheWholeObservation()
    {
        using var scenario = new CalibrationScenario();
        using (var run = scenario.Begin())
        {
            run.ObserveSuccessfulLaunch(1, TimeSpan.Zero);
            run.ObserveSuccessfulLaunch(2, TimeSpan.Zero);
            run.ObserveSuccessfulLaunch(4, TimeSpan.Zero);
            run.ObserveSuccessfulLaunch(8, TimeSpan.Zero);
            run.ObserveSuccessfulLaunch(16, TimeSpan.FromMilliseconds(500) + TimeSpan.FromTicks(1));
            run.Complete();
        }

        using var cold = scenario.Begin();
        Assert.Equal(1, cold.FirstChunkCases());
    }

    /// <summary>Partial work, abandoned result assembly, and a one-case-only useful prefix cannot seed reuse.</summary>
    [Theory]
    [InlineData("partial")]
    [InlineData("abandoned")]
    [InlineData("one-case")]
    public void IncompleteOrUselessObservationsRemainCold(string mode)
    {
        using var scenario = new CalibrationScenario();
        using (var run = scenario.Begin())
        {
            run.ObserveSuccessfulLaunch(1, TimeSpan.FromMilliseconds(500));
            run.ObserveSuccessfulLaunch(2, mode == "one-case" ? TimeSpan.FromTicks(1) : TimeSpan.Zero);
            if (mode != "partial")
            {
                run.ObserveSuccessfulLaunch(4, TimeSpan.Zero);
                run.ObserveSuccessfulLaunch(8, TimeSpan.Zero);
                run.ObserveSuccessfulLaunch(16, TimeSpan.Zero);
            }

            if (mode != "abandoned")
            {
                run.Complete();
            }
        }

        using var cold = scenario.Begin();
        Assert.Equal(1, cold.FirstChunkCases());
    }

    /// <summary>Completion starts the TTL; its exact target boundary is inclusive, one tick later is stale.</summary>
    [Fact]
    public void ExpiryIsMeasuredFromCompletionWithAnInclusiveBoundary()
    {
        using var scenario = new CalibrationScenario();
        scenario.Clock.Advance(TimeSpan.FromSeconds(1));
        scenario.Train();
        scenario.Clock.Advance(TimeSpan.FromMilliseconds(500));
        using (var boundary = scenario.Begin())
        {
            Assert.Equal(15, boundary.FirstChunkCases());
        }

        scenario.Train();
        scenario.Clock.Advance(TimeSpan.FromMilliseconds(500) + TimeSpan.FromTicks(1));
        using var expired = scenario.Begin();
        Assert.Equal(1, expired.FirstChunkCases());
        Assert.False(expired.ReusedPrefix);
    }

    /// <summary>Expiry between lookup and physical launch, or backward monotonic time, refuses the old probe.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreshnessIsRecheckedAtTheFirstLaunch(bool backward)
    {
        using var scenario = new CalibrationScenario();
        scenario.Clock.Timestamp = TimeSpan.TicksPerSecond;
        scenario.Train();
        using var pending = scenario.Begin();
        Assert.Equal(15, pending.FirstChunkCases());
        scenario.Clock.Advance(backward ? TimeSpan.FromTicks(-1) : TimeSpan.FromMilliseconds(501));
        Assert.Equal(1, pending.FirstChunkCases());
        Assert.False(pending.ReusedPrefix);
    }

    /// <summary>Clear invalidates pending probes and publication, not only the previous cached entry.</summary>
    [Fact]
    public void ClearAndANewerTransactionCancelAnOlderPendingRun()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        using var older = scenario.Begin();
        scenario.Cache.Clear();
        Assert.Equal(1, older.FirstChunkCases());
        using var newer = scenario.Begin();
        foreach (var cases in new[] { 1, 2, 4, 8, 16 })
        {
            older.ObserveSuccessfulLaunch(cases, TimeSpan.Zero);
        }

        older.Complete();
        using var stillCold = scenario.Begin();
        Assert.Equal(1, stillCold.FirstChunkCases());
    }
}