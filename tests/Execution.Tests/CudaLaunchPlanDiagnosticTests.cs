using System.Diagnostics;
using System.Reflection;
using APThermo.Execution.Chunks;
using APThermo.Thermo;
using ILGPU.Runtime.Cuda;
using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>An isolated scheduling experiment on the frozen throughput input, never a replacement throughput gate.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CudaLaunchPlanDiagnosticTests(ITestOutputHelper output)
{
    private const int Runs = 3;
    private static readonly int[] ProbeCases = [1, 2, 4, 8192, 16384];
    private sealed record Outcome(string Cases, string Stations, int FailedCases, int FailedStations, long Steps);
    private sealed record Measurement(string Label, RocketBatchResult First, CudaTiming Timing, Outcome[] Outcomes);

    /// <summary>Compare default sampling with static capped launches on the identical batch; passing is not regression closure.</summary>
    [Fact]
    [Trait("Category", "CudaDiagnostic")]
    [Trait("Category", "LongRunning")]
    public void DiagnosticOnlyFrozenThroughputBoundedVersusStaticLaunches()
    {
        var cuda = EngineFixture.Shared.RequireCuda();
        if (cuda is null)
        {
            output.WriteLine("actualCuda=false; diagnostic not executed; no scheduling or throughput evidence");
            return;
        }

        Assert.Equal(AcceleratorKind.Cuda, cuda.Accelerator.Kind);
        Assert.Null(cuda.Accelerator.CudaSkippedBecause);
        var device = Assert.IsType<CudaAccelerator>(cuda.IlgpuAccelerator);
        Assert.True(cuda.Budget.IsBounded, "this comparison requires the default bounded device policy");
        var family = FixtureBatches.Family(EngineFixture.Shared.Database, SweepRun.FamilyName);
        var batch = FixtureBatches.Sweep(family, SweepRun.From, SweepRun.To, SweepRun.LongRunningCases, 5.0e6, 10.0e6);
        var warmUp = FixtureBatches.Sweep(family, SweepRun.From, SweepRun.To, 64, 5.0e6, 10.0e6);
        using var tables = cuda.Upload(family.Table);
        WarmUp(cuda, tables, warmUp);
        var bounded = Measure("default bounded full batch", cuda, tables, batch);
        var statics = WithBudget(cuda, LaunchBudget.None, () =>
        {
            WarmUp(cuda, tables, warmUp);
            var measured = new List<Measurement> { Measure("DIAGNOSTIC static full batch", cuda, tables, batch) };
            foreach (var count in ProbeCases)
            {
                measured.Add(Measure($"DIAGNOSTIC static one-launch prefix {count}", cuda, tables, Prefix(batch, count)));
            }

            return measured;
        });
        output.WriteLine($"DIAGNOSTIC ONLY; actualCuda=true; deviceId={device.DeviceId}; name={device.Name}; original policy restored; no production or acceptance change");
        Print(bounded);
        foreach (var measurement in statics)
        {
            Print(measurement);
        }

        Assert.Empty(ExactComparison.Rocket(bounded.First, statics[0].First, index => $"identical full batch case {index}"));
        for (var index = 0; index < ProbeCases.Length; index++)
        {
            var probe = statics[index + 1];
            Assert.All(probe.Timing.LaunchRuns, run => Assert.Equal(1, run.LaunchCount));
            Assert.Empty(ExactComparison.Rocket(Prefix(bounded.First, ProbeCases[index]), probe.First, item => $"static prefix case {item}"));
        }

        Assert.True(cuda.Budget.IsBounded, "the diagnostic must restore the original bounded policy");
    }

    /// <summary>A failure inside the test-only override restores the exact original session budget.</summary>
    [Fact]
    public void DiagnosticOverrideRestoresTheOriginalBudgetOnFailure()
    {
        using var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu });
        var original = cpu.Budget;
        var temporary = LaunchBudget.FromRunTimeLimit(LaunchBudget.DefaultRunTimeLimit);
        var primary = new InvalidOperationException("injected diagnostic failure");
        var failure = Assert.Throws<InvalidOperationException>((Action)(() => WithBudget<int>(cpu, temporary, () =>
        {
            Assert.Same(temporary, cpu.Budget);
            throw primary;
        })));
        Assert.Same(primary, failure);
        Assert.Same(original, cpu.Budget);
    }

    private static T WithBudget<T>(Engine engine, LaunchBudget budget, Func<T> action)
    {
        var field = typeof(Engine).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var session = Assert.IsType<AcceleratorSession>(field.GetValue(engine));
        var original = session.Budget;
        try
        {
            _ = session.Attach(budget);
            return action();
        }
        finally
        {
            _ = session.Attach(original);
        }
    }

    private static void WarmUp(Engine engine, UploadedTables tables, RocketBatch batch)
    {
        for (var run = 0; run < 5; run++)
        {
            _ = engine.Run(tables, batch);
        }
    }

    private static Measurement Measure(string label, Engine engine, UploadedTables tables, RocketBatch batch)
    {
        var results = new RocketBatchResult[Runs];
        var elapsed = new TimeSpan[Runs];
        var watch = new Stopwatch();
        for (var index = 0; index < Runs; index++)
        {
            watch.Restart();
            results[index] = engine.Run(tables, batch);
            elapsed[index] = watch.Elapsed;
        }

        var kernel = results.Select(result => result.Timings.Kernel).ToArray();
        var timing = CudaTiming.From(elapsed, kernel) with { LaunchRuns = [.. results.Select(result => result.Timings)] };
        var outcomes = results.Select(result => new Outcome(
            Cases: Statuses(result.Status), Stations: Statuses(result.StationStatus),
            FailedCases: result.Status.Count(status => status != CaseStatus.Ok), FailedStations: result.StationStatus.Count(status => status != CaseStatus.Ok),
            Steps: result.Iterations.Sum(steps => (long)steps))).ToArray();
        return new Measurement(Label: label, First: results[0], Timing: timing, Outcomes: outcomes);
    }

    private void Print(Measurement measurement)
    {
        output.WriteLine($"{measurement.Label}: cases={measurement.First.Count}; median elapsed={measurement.Timing.Elapsed.TotalSeconds:R} s; median cumulative Kernel={measurement.Timing.Kernel.TotalSeconds:R} s");
        for (var index = 0; index < Runs; index++)
        {
            var timing = measurement.Timing.LaunchRuns[index];
            var outcome = measurement.Outcomes[index];
            output.WriteLine($"  timed run {index + 1}: {LaunchBudgetObservation.Describe(timing)}; statuses={outcome.Cases}; stationStatuses={outcome.Stations}; NewtonSteps={outcome.Steps}");
            Assert.Equal(measurement.First.Count, timing.CoveredCases);
            Assert.True(timing.LaunchCount > 0);
            Assert.Equal(0, outcome.FailedCases);
            Assert.Equal(0, outcome.FailedStations);
        }
    }

    private static string Statuses(IEnumerable<CaseStatus> statuses) =>
        string.Join(",", statuses.GroupBy(status => status).OrderBy(group => group.Key).Select(group => $"{group.Key}={group.Count()}"));

    private static RocketBatch Prefix(RocketBatch source, int count)
    {
        var batch = new RocketBatch(count, source.ElementCount, source.ExitKinds);
        Array.Copy(source.ChamberPressure, batch.ChamberPressure, count);
        Array.Copy(source.ReactantEnthalpy, batch.ReactantEnthalpy, count);
        Array.Copy(source.TemperatureEstimate, batch.TemperatureEstimate, count);
        Array.Copy(source.Flow, batch.Flow, count);
        Array.Copy(source.ElementMoles, batch.ElementMoles, count * source.ElementCount);
        Array.Copy(source.ExitValues, batch.ExitValues, count * source.Exits);
        return batch;
    }

    private static RocketBatchResult Prefix(RocketBatchResult source, int count) =>
        new(speciesCount: source.SpeciesCount, stationCount: source.StationCount,
            stations: source.Stations.AsSpan(0, count * source.StationCount).ToArray(),
            moles: source.Moles.AsSpan(0, count * source.StationCount * source.SpeciesCount).ToArray(),
            figures: source.Figures.AsSpan(0, count * source.StationCount).ToArray(),
            stationStatus: source.StationStatus.AsSpan(0, count * source.StationCount).ToArray(),
            iterations: source.Iterations.AsSpan(0, count * source.StationCount).ToArray(),
            status: source.Status.AsSpan(0, count).ToArray(), timings: source.Timings, accelerator: source.Accelerator);
}