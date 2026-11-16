using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Known-answer controls for the separate cold-sample throughput guard.</summary>
public sealed class ColdThroughputTests
{
    private const long Steps = 10_000_000;
    private const int Cases = 100_000;
    private static readonly Dictionary<string, string> ColdRecord = ThroughputRecord.Parse([$"{ThroughputRecord.PerIterationKey}: 2e-7"]);
    private static readonly Dictionary<string, string> WarmRecord = ThroughputRecord.Parse([$"{ThroughputRecord.PerIterationKey}: 1e-7"]);

    /// <summary>The actual first cold sample passes its own record and leaves the warm median comparison intact.</summary>
    [Fact]
    public void ColdSampleWithinItsRecordPasses()
    {
        var timing = Timing(2.0);
        Assert.Null(ColdThroughput.Violation(timing, Cases, Steps, ColdRecord, "cold"));
        Assert.Null(ThroughputRecord.Violation(WarmRecord, "warm", timing.Kernel.TotalSeconds / Steps));
    }

    /// <summary>Slowing only the cold sample breaches its record while the original median remains green.</summary>
    [Fact]
    public void SlowColdSampleFailsWhileWarmMedianPasses()
    {
        var timing = Timing(2.4);
        Assert.Null(ThroughputRecord.Violation(WarmRecord, "warm", timing.Kernel.TotalSeconds / Steps));
        Assert.NotNull(ColdThroughput.Violation(timing, Cases, Steps, ColdRecord, "cold"));
    }

    /// <summary>Evidence of a reused prefix, a non-cold first choice or incomplete coverage is refused.</summary>
    [Theory]
    [InlineData(true, 1, 100000, 1)]
    [InlineData(false, 2, 100000, 1)]
    [InlineData(false, 1, 99999, 1)]
    [InlineData(false, 1, 100000, 2)]
    public void AFalseColdOrIncompleteObservationFails(bool reused, int firstCases, int coveredCases, int physicalCases)
    {
        var timing = Timing(2.0, reused, firstCases, coveredCases, physicalCases);
        Assert.NotNull(ColdThroughput.Violation(timing, Cases, Steps, ColdRecord, "cold"));
    }

    /// <summary>Missing observation arrays or a missing baseline fail closed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void MissingEvidenceFails(int missing)
    {
        var timing = Timing(2.0);
        var record = ColdRecord;
        switch (missing)
        {
            case 0: timing = timing with { LaunchRuns = [] }; break;
            case 1: timing = timing with { CalibrationRuns = [] }; break;
            case 2: timing = timing with { KernelRuns = [] }; break;
            case 3: record = []; break;
            default: throw new ArgumentOutOfRangeException(nameof(missing));
        }

        Assert.NotNull(ColdThroughput.Violation(timing, Cases, Steps, record, "cold"));
    }

    private static CudaTiming Timing(double coldSeconds, bool reused = false, int firstCases = 1, int coveredCases = Cases, int physicalCases = 1)
    {
        var zero = TimeSpan.Zero;
        var cold = TimeSpan.FromSeconds(coldSeconds);
        var warm = TimeSpan.FromSeconds(1);
        var launch = new RunTimings(zero, zero, cold, zero)
        {
            LaunchCount = 20,
            CoveredCases = coveredCases,
            FirstLaunchCases = firstCases,
            ReusedPrefix = reused,
        };
        var observation = new CalibrationObservation
        {
            TransactionId = 1,
            PhysicalFirstLaunch = new CalibrationChoice(physicalCases, CalibrationReason.IdentityMismatch, null),
        };
        return CudaTiming.From([cold, warm, warm], [cold, warm, warm]) with
        {
            LaunchRuns = [launch],
            CalibrationRuns = [observation],
        };
    }
}
