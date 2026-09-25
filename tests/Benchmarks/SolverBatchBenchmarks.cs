using System.Text.Json;
using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Problems;
using APThermo.Thermo;
using BenchmarkDotNet.Attributes;

namespace APThermo.Benchmarks;

/// Group 7 of `BOOT.md`, Constraints: the same LOX/LH2 rocket sweep family as group 1
/// (`BatchThroughputBenchmarks`), at the same case counts and on the same accelerators,
/// through the consumer path `Problems.Solver` — the batch entry the package surface
/// keeps once the engine leaves it (root `BOOT.md`, `## Delivery`, Tree contracts) —
/// instead of the raw `Execution.Engine` batch API. Measures what a .NET caller pays
/// end to end: building the inputs, the solve, and materialising the
/// `RocketResult`/`Station` records. `[GlobalSetup]` also runs the engine path over the
/// identical batch once, so that `EngineSolverComparison` can prove the two paths agree
/// before the timed comparison between this group and group 1 is read.
[MemoryDiagnoser]
public class SolverBatchBenchmarks
{
    private const string FixtureFile = "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json";

    /// <summary>How many identical rocket problems this run's batch solves.</summary>
    [Params(1000, 10000, 100000)]
    public int CaseCount { get; set; }

    /// <summary>Which accelerator this run's batch executes on.</summary>
    [Params(AcceleratorKind.Cpu, AcceleratorKind.Cuda)]
    public AcceleratorKind Accelerator { get; set; }

    private Solver _solver = null!;
    private ElementalMixture _mixture = null!;
    private IReadOnlyList<RocketProblem> _problems = null!;

    /// <summary>Builds the batch of `CaseCount` identical rocket problems, creates the solver on the chosen accelerator, runs
    /// one warm-up solve to record its diagnostics, and compares that solve against the raw engine path on the identical
    /// batch (`EngineSolverComparison`).</summary>
    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        var fixtureCase = CeaFixtures.Load(RepositoryPaths.Resolve("tests", "Fixtures", "cases", "rocket", FixtureFile));
        var inputs = fixtureCase.Inputs;

        var (elements, molesPerKilogram) = FixtureJson.ReadElementMoles(inputs);
        var species = FixtureJson.ReadStrings(inputs, "products");
        var rocketInputs = FixtureStateRecords.RocketBatchInputs(inputs);

        _mixture = ElementalMixture.Create(FixtureJson.ReadComposition(inputs), rocketInputs.Enthalpy, only: species);
        var problem = new RocketProblem { ChamberPressure = rocketInputs.Pressure, Flow = rocketInputs.Flow, AreaRatios = rocketInputs.AreaRatios };
        _problems = [.. Enumerable.Repeat(problem, CaseCount)];

        _solver = Solver.Create(database, AcceleratorSelection.OptionsFor(Accelerator));
        var results = _solver.Solve(_mixture, _problems);

        RecordDiagnostics(results);
        CompareWithEngine(database, inputs, elements, molesPerKilogram, species, results);
    }

    /// <summary>Runs the batch through the consumer path `Problems.Solver` once.</summary>
    [Benchmark]
    public IReadOnlyList<RocketResult> SolveBatch() => _solver.Solve(_mixture, _problems);

    /// <summary>Disposes the solver after every benchmark of this class has run.</summary>
    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();

    private void RecordDiagnostics(IReadOnlyList<RocketResult> results)
    {
        var hash = new BitHash();
        var okCount = 0;
        foreach (var result in results)
        {
            foreach (var station in result.Stations)
            {
                _ = hash.AddState(station.State).AddMoles(station.MoleFractions).AddStatus(station.Status);
            }
            okCount += result.Status == CaseStatus.Ok ? 1 : 0;
        }
        Console.WriteLine(
            $"[SolverBatch] cases={CaseCount} accelerator={Accelerator} ok={okCount}/{results.Count} hash={hash.ToHex()}");
    }

    private void CompareWithEngine(SpeciesDatabase database, JsonElement inputs, IReadOnlyList<string> elements,
        IReadOnlyList<double> molesPerKilogram, IReadOnlyList<string> species, IReadOnlyList<RocketResult> results)
    {
        var table = SpeciesTable.Build(database, elements, species);
        using var engine = Engine.Create(AcceleratorSelection.OptionsFor(Accelerator));
        using var tables = engine.Upload(table);
        var batch = RocketBatches.Build(inputs, elements.Count, molesPerKilogram, CaseCount);
        var engineResult = engine.Run(tables, batch);

        var comparison = new EngineSolverComparison(ToleranceTable.Load());
        comparison.Compare(engineResult, table.Species, results);
        LogComparison(comparison);
    }

    private void LogComparison(EngineSolverComparison comparison)
    {
        var verdict = comparison.ExactlyBitEqual
            ? "bitwise (same kernels, same accelerator)"
            : comparison.Passed
                ? $"tolerance (GPU/CPU tiers on state/performance fields, fixtures ToleranceTable moleFractionFloor/polishThresholdRelative on mole fractions; " +
                  $"worstFieldRelative={comparison.WorstFieldRelative:e3} worstMoleFractionRelative={comparison.WorstMoleFractionRelative:e3})"
                : $"FAILED ({comparison.Mismatches.Count} mismatches; worstFieldRelative={comparison.WorstFieldRelative:e3} worstMoleFractionRelative={comparison.WorstMoleFractionRelative:e3})";
        Console.WriteLine($"[SolverBatch] cases={CaseCount} accelerator={Accelerator} equalsEngine={verdict}");
        foreach (var mismatch in comparison.Mismatches.Take(5))
        {
            Console.WriteLine($"[SolverBatch] {mismatch}");
        }
    }
}
