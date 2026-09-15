using System.Text.Json;
using APThermo.Execution;
using APThermo.Performance;

namespace APThermo.Benchmarks;

/// Builds the raw `Execution.RocketBatch` of one fixture case replicated `caseCount`
/// times (BOOT.md, Invariants: fixture-based benchmarks read `tests/Fixtures` files
/// through `RepositoryPaths`; no fixture value is typed into code). Shared by
/// `BatchThroughputBenchmarks` (group 1, the engine path) and `SolverBatchBenchmarks`
/// (group 7, whose `[GlobalSetup]` also runs the engine path once over the identical
/// batch to compare against the consumer path), so that "the same cases" the two
/// groups compare are built by the one piece of code, not two that could drift apart.
internal static class RocketBatches
{
    public static RocketBatch Build(JsonElement inputs, int elementCount, IReadOnlyList<double> molesPerKilogram, int caseCount)
    {
        var rocketInputs = FixtureStateRecords.RocketBatchInputs(inputs);
        var exitKinds = rocketInputs.AreaRatios.Select(_ => ExitSpecification.AreaRatio).ToArray();

        var batch = new RocketBatch(caseCount, elementCount, exitKinds);
        for (var i = 0; i < caseCount; i++)
        {
            FillCase(batch, i, elementCount, molesPerKilogram, rocketInputs);
        }
        return batch;
    }

    private static void FillCase(RocketBatch batch, int caseIndex, int elementCount, IReadOnlyList<double> molesPerKilogram, RocketFixtureInputs inputs)
    {
        batch.ChamberPressure[caseIndex] = inputs.Pressure;
        batch.ReactantEnthalpy[caseIndex] = inputs.Enthalpy;
        batch.Flow[caseIndex] = inputs.Flow;
        for (var e = 0; e < elementCount; e++)
        {
            batch.ElementMoles[caseIndex * elementCount + e] = molesPerKilogram[e];
        }
        for (var x = 0; x < inputs.AreaRatios.Count; x++)
        {
            batch.ExitValues[caseIndex * inputs.AreaRatios.Count + x] = inputs.AreaRatios[x];
        }
    }
}
