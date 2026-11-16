using Xunit.Abstractions;

namespace APThermo.Execution.Tests;

/// <summary>Actual-device cold/hit/miss provenance; throughput and difficult launch budgets remain separate original facts.</summary>
[Collection(EngineFixture.CollectionName)]
public sealed class CalibrationCudaTests(ITestOutputHelper output)
{
    /// <summary>Every run uses the real cached CUDA launcher and current telemetry; changed input restores cold sampling.</summary>
    [Fact]
    [Trait("Category", "Cuda")]
    [Trait("CudaEvidence", "hardware")]
    public void ActualCudaReportsColdHitAndChangedInputWithExactResults()
    {
        if (Engine.CudaForbidden)
        {
            _ = EngineFixture.Shared.RequireCuda(output);
            output.WriteLine("actualCuda=false; CUDA forbidden: no hardware calibration evidence");
            return;
        }

        using var cuda = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda, ChunkSize = 16 });
        Assert.Equal(AcceleratorKind.Cuda, cuda.Accelerator.Kind);
        CudaEvidence.Hardware(output, cuda.Accelerator.DeviceName);
        using var tables = cuda.Upload(FixtureBatches.RocketFamilies(EngineFixture.SharedDatabase)[0].Table);
        var batch = new SpeciesFunctionBatch(31);
        for (var index = 0; index < batch.Count; index++)
        {
            batch.Species[index] = index % tables.Species.SpeciesCount;
            batch.Temperature[index] = 1400 + index;
        }

        var cold = cuda.Run(tables, batch);
        var hit = cuda.Run(tables, batch);
        batch.Temperature[0] = Math.BitIncrement(batch.Temperature[0]);
        var changed = cuda.Run(tables, batch);
        if (!cuda.Budget.IsBounded)
        {
            Assert.All([cold, hit, changed], result => Assert.False(result.Timings.ReusedPrefix));
            output.WriteLine($"actualCuda=true; device={cuda.Accelerator.DeviceName}; unbounded device: calibration bypassed, no bounded-hit evidence");
            return;
        }

        Assert.False(cold.Timings.ReusedPrefix);
        Assert.Equal(1, cold.Timings.FirstLaunchCases);
        Assert.True(hit.Timings.ReusedPrefix);
        Assert.Equal(15, hit.Timings.FirstLaunchCases);
        Assert.False(changed.Timings.ReusedPrefix);
        Assert.Equal(1, changed.Timings.FirstLaunchCases);
        Assert.Empty(ExactComparison.Functions(cold, hit, index => $"case {index}"));
        foreach (var result in new[] { cold, hit, changed })
        {
            Assert.Equal(AcceleratorKind.Cuda, result.Accelerator.Kind);
            Assert.Null(LaunchBudgetObservation.Violation(batch.Count, result.Timings, TimeSpan.FromMilliseconds(500)));
        }

        output.WriteLine($"actualCuda=true; device={cuda.Accelerator.DeviceName}; ILGPU={cuda.Accelerator.IlgpuVersion}");
        output.WriteLine("cold: " + LaunchBudgetObservation.Describe(cold.Timings));
        output.WriteLine("hit: " + LaunchBudgetObservation.Describe(hit.Timings));
        output.WriteLine("changed input: " + LaunchBudgetObservation.Describe(changed.Timings));
    }
}