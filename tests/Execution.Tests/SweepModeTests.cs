using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Known-answer controls for the measured sweep modes.</summary>
public sealed class SweepModeTests
{
    /// <summary>The intended cold followed by two reused prefixes is accepted.</summary>
    [Fact]
    public void ColdHitHitPasses() => Assert.Null(SweepMode.Violation(Timing(0)));

    /// <summary>A lost prefix identifies the timed sample and its observed expiry and age.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void AColdRepeatNamesItsSampleAndExpiry(int ordinal)
    {
        var violation = SweepMode.Violation(Timing(ordinal));
        Assert.NotNull(violation);
        Assert.Contains($"sample {ordinal}", violation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Expired", violation, StringComparison.Ordinal);
        Assert.Contains("750", violation, StringComparison.Ordinal);
    }

    /// <summary>A reused first launch is not a cold first sample.</summary>
    [Fact]
    public void AReusedFirstSampleIsRefused()
    {
        var timing = Timing(0);
        var launches = timing.LaunchRuns.ToArray();
        launches[0] = launches[0] with { ReusedPrefix = true, FirstLaunchCases = 64 };
        var violation = SweepMode.Violation(timing with { LaunchRuns = launches });
        Assert.NotNull(violation);
        Assert.Contains("sample 1", violation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Every required observation must have exactly three entries.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void IncompleteOrExtraObservationsAreRefused(int missing)
    {
        var timing = Timing(0);
        timing = missing switch
        {
            0 => timing with { KernelRuns = [.. timing.KernelRuns.Take(2)] },
            1 => timing with { LaunchRuns = [.. timing.LaunchRuns.Take(2)] },
            2 => timing with { CalibrationRuns = [.. timing.CalibrationRuns.Take(2)] },
            _ => timing with { CalibrationRuns = [.. timing.CalibrationRuns, timing.CalibrationRuns[0]] },
        };
        Assert.NotNull(SweepMode.Violation(timing));
    }

    /// <summary>Reported reuse must agree with the physical first launch decision.</summary>
    [Fact]
    public void AFalseHitIsRefused()
    {
        var timing = Timing(0);
        var observations = timing.CalibrationRuns.ToArray();
        observations[1] = observations[1] with { PhysicalFirstLaunch = new CalibrationChoice(1, CalibrationReason.Expired, TimeSpan.FromMilliseconds(750)) };
        var violation = SweepMode.Violation(timing with { CalibrationRuns = observations });
        Assert.NotNull(violation);
        Assert.Contains("sample 2", violation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Expired", violation, StringComparison.Ordinal);
    }

    private static CudaTiming Timing(int expiredOrdinal)
    {
        var zero = TimeSpan.Zero;
        var kernel = TimeSpan.FromSeconds(1);
        var launches = new RunTimings[3];
        var observations = new CalibrationObservation[3];
        for (var index = 0; index < 3; index++)
        {
            var hit = index > 0 && index + 1 != expiredOrdinal;
            var expired = index + 1 == expiredOrdinal;
            var reason = expired ? CalibrationReason.Expired : hit ? CalibrationReason.ReusedPrefix : CalibrationReason.IdentityMismatch;
            var age = expired ? TimeSpan.FromMilliseconds(750) : (TimeSpan?)null;
            launches[index] = new RunTimings(zero, zero, kernel, zero)
            {
                LaunchCount = 2,
                CoveredCases = 100_000,
                FirstLaunchCases = hit ? 64 : 1,
                ReusedPrefix = hit,
            };
            observations[index] = new CalibrationObservation
            {
                TransactionId = index + 1,
                PhysicalFirstLaunch = new CalibrationChoice(hit ? 64 : 1, reason, age),
            };
        }
        return CudaTiming.From([kernel, kernel, kernel], [kernel, kernel, kernel]) with
        {
            LaunchRuns = launches,
            CalibrationRuns = observations,
        };
    }
}
