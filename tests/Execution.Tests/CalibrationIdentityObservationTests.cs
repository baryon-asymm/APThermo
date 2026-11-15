using System.Reflection;
using APThermo.Execution.Calibration;

namespace APThermo.Execution.Tests;

/// <summary>Reports the original identity comparison's actual first failing condition.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationIdentityObservationTests
{
    /// <summary>A controlled dead weak target does not require a sleep or a retained live owner.</summary>
    [Theory]
    [InlineData("_launcher", false, nameof(CalibrationReason.DeadLauncher))]
    [InlineData("_tables", false, nameof(CalibrationReason.DeadTables))]
    [InlineData("_launcher", true, nameof(CalibrationReason.IdentityMismatch))]
    public void DeadWeakTargetsAndDigestShortCircuitAreDistinguished(string field, bool changedInput, string reason)
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        var entry = typeof(LaunchCalibrationCache).GetField("_entry", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scenario.Cache)!;
        var key = entry.GetType().GetProperty("Key", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(entry)!;
        var weak = key.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(key)!;
        if (field == "_launcher")
        {
            ((WeakReference<Delegate>)weak).SetTarget(null!);
        }
        else
        {
            ((WeakReference<UploadedTables>)weak).SetTarget(null!);
        }

        using var run = scenario.Cache.Begin(scenario.Launcher, scenario.Tables, scenario.Plan,
            changedInput ? "different-input" : "input-A", scenario.Views);
        Assert.Equal(1, run.FirstChunkCases());
        Assert.Equal(Enum.Parse<CalibrationReason>(reason), scenario.Cache.Observation.MatchReason);
        Assert.Null(scenario.Cache.Observation.BeforeUpload.CandidateAge);
    }

    /// <summary>Changed live reference identities are different from a dead target or changed digest.</summary>
    [Fact]
    public void ChangedLiveLauncherAndTablesHaveTheirOwnReasons()
    {
        using var scenario = new CalibrationScenario();
        scenario.Train();
        Action<int> otherLauncher = cases => GC.KeepAlive(cases);
        using (var changed = scenario.Cache.Begin(otherLauncher, scenario.Tables, scenario.Plan, "input-A", scenario.Views))
        {
            Assert.Equal(1, changed.FirstChunkCases());
            Assert.Equal(CalibrationReason.LauncherMismatch, scenario.Cache.Observation.MatchReason);
        }

        scenario.Train();
        using var otherTables = scenario.Cpu.Upload(scenario.Tables.Species);
        using var other = scenario.Cache.Begin(scenario.Launcher, otherTables, scenario.Plan, "input-A", scenario.Views);
        Assert.Equal(1, other.FirstChunkCases());
        Assert.Equal(CalibrationReason.TablesMismatch, scenario.Cache.Observation.MatchReason);
    }
}
