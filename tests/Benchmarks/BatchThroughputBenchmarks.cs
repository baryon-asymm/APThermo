using System.Text.Json;
using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Performance;
using APThermo.Thermo;
using BenchmarkDotNet.Attributes;

namespace APThermo.Benchmarks;

/// Group 1 of `BOOT.md`, Constraints: the LOX/LH2 rocket sweep family at 1 000,
/// 10 000 and 100 000 cases, on the CPU accelerator and on CUDA, through the raw
/// `Execution.Engine` batch API — no `Problems.Solver` — so that the engine's own
/// `RunTimings` (kernel against upload and download) are visible. Every case of a
/// batch is the same rocket case, read from the reference's own LOX/LH2 fixture.
[MemoryDiagnoser]
public class BatchThroughputBenchmarks
{
    private const string FixtureFile = "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json";

    [Params(1000, 10000, 100000)]
    public int CaseCount { get; set; }

    [Params(AcceleratorKind.Cpu, AcceleratorKind.Cuda)]
    public AcceleratorKind Accelerator { get; set; }

    private Engine _engine = null!;
    private UploadedTables _tables = null!;
    private RocketBatch _batch = null!;

    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        var fixtureCase = CeaFixtures.Load(RepositoryPaths.Resolve("tests", "Fixtures", "cases", "rocket", FixtureFile));
        var inputs = fixtureCase.Inputs;

        var (elements, molesPerKilogram) = FixtureJson.ReadElementMoles(inputs);
        var species = FixtureJson.ReadStrings(inputs, "products");
        var table = SpeciesTable.Build(database, elements, species);

        _engine = Engine.Create(AcceleratorSelection.OptionsFor(Accelerator));
        _tables = _engine.Upload(table);
        _batch = BuildBatch(inputs, elements.Count, molesPerKilogram);

        RecordDiagnostics(_engine.Run(_tables, _batch));
    }

    [Benchmark]
    public RocketBatchResult SolveBatch() => _engine.Run(_tables, _batch);

    [GlobalCleanup]
    public void Cleanup()
    {
        _tables.Dispose();
        _engine.Dispose();
    }

    private RocketBatch BuildBatch(JsonElement inputs, int elementCount, IReadOnlyList<double> molesPerKilogram)
    {
        var pressure = inputs.GetProperty("chamberPressure").GetDouble();
        var enthalpy = inputs.GetProperty("reactantEnthalpy").GetDouble();
        var flow = FixtureStateRecords.ReadFlow(inputs);
        var areaRatios = FixtureJson.ReadDoubles(inputs, "areaRatios");
        var exitKinds = areaRatios.Select(_ => ExitSpecification.AreaRatio).ToArray();

        var batch = new RocketBatch(CaseCount, elementCount, exitKinds);
        for (var i = 0; i < CaseCount; i++)
        {
            batch.ChamberPressure[i] = pressure;
            batch.ReactantEnthalpy[i] = enthalpy;
            batch.Flow[i] = flow;
            for (var e = 0; e < elementCount; e++)
            {
                batch.ElementMoles[i * elementCount + e] = molesPerKilogram[e];
            }
            for (var x = 0; x < areaRatios.Count; x++)
            {
                batch.ExitValues[i * areaRatios.Count + x] = areaRatios[x];
            }
        }
        return batch;
    }

    private void RecordDiagnostics(RocketBatchResult result)
    {
        var hash = new BitHash();
        for (var i = 0; i < result.Stations.Length; i++)
        {
            hash.AddState(result.Stations[i]);
        }
        hash.Add(result.Moles);
        var okCount = result.Status.Count(status => status == CaseStatus.Ok);
        Console.WriteLine(
            $"[BatchThroughput] cases={CaseCount} accelerator={Accelerator} ok={okCount}/{result.Count} " +
            $"cudaSkippedBecause={result.Accelerator.CudaSkippedBecause ?? "-"} " +
            $"warmUp={result.Timings.WarmUp} kernel={result.Timings.Kernel} hash={hash.ToHex()}");
    }
}
