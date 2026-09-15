using System.Text.Json;
using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Problems;
using APThermo.Thermo;
using BenchmarkDotNet.Attributes;

namespace APThermo.Benchmarks;

/// Group 4 of `BOOT.md`, Constraints: the one-time costs a .NET caller pays once per
/// process (database load, chemical-system assembly, kernel compilation on the CPU
/// accelerator and on CUDA with the libdevice post-link, species-table upload).
/// `[IterationSetup]`/`[IterationCleanup]` rebuild the cold state the compilation and
/// upload benchmarks measure, so that every measured call is genuinely first.
[MemoryDiagnoser]
public class OneTimeCostBenchmarks
{
    private string _thermoPath = null!;
    private string _transPath = null!;
    private SpeciesDatabase _database = null!;
    private Solver _solver = null!;
    private IReadOnlyList<string> _elements = null!;
    private SpeciesTable _table = null!;
    private EquilibriumBatch _compileBatch = null!;

    private Engine _uploadEngine = null!;
    private UploadedTables? _uploaded;

    private Engine _cpuCompileEngine = null!;
    private UploadedTables? _cpuCompileTables;

    private Engine _cudaCompileEngine = null!;
    private UploadedTables? _cudaCompileTables;

    [GlobalSetup]
    public void Setup()
    {
        _thermoPath = RepositoryPaths.Resolve("data", "thermo.inp");
        _transPath = RepositoryPaths.Resolve("data", "trans.inp");
        _database = SpeciesDatabase.Load(_thermoPath, _transPath);
        _solver = Solver.Create(_database, AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));

        var fixtureCase = CeaFixtures.Load(RepositoryPaths.Resolve(
            "tests", "Fixtures", "cases", "hp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber.json"));
        var inputs = fixtureCase.Inputs;
        var (elements, molesPerKilogram) = FixtureJson.ReadElementMoles(inputs);
        _elements = elements;
        _table = SpeciesTable.Build(_database, _elements, _solver.CandidateSpeciesFor(_elements));
        _compileBatch = BuildTrivialBatch(elements.Count, molesPerKilogram, inputs);

        _uploadEngine = Engine.Create(AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));
    }

    [Benchmark]
    public SpeciesDatabase LoadDatabase() => SpeciesDatabase.Load(_thermoPath, _transPath);

    [Benchmark]
    public SpeciesTable AssembleChemicalSystem() =>
        SpeciesTable.Build(_database, _elements, _solver.CandidateSpeciesFor(_elements));

    [IterationSetup(Target = nameof(UploadSpeciesTable))]
    public void SetupUpload() => _uploaded?.Dispose();

    [Benchmark]
    public UploadedTables UploadSpeciesTable() => _uploaded = _uploadEngine.Upload(_table);

    [IterationSetup(Target = nameof(CompileCpuKernel))]
    public void SetupCpuCompile()
    {
        _cpuCompileEngine = Engine.Create(AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));
        _cpuCompileTables = _cpuCompileEngine.Upload(_table);
    }

    [Benchmark]
    public EquilibriumBatchResult CompileCpuKernel() => _cpuCompileEngine.Run(_cpuCompileTables!, _compileBatch);

    [IterationCleanup(Target = nameof(CompileCpuKernel))]
    public void CleanupCpuCompile()
    {
        _cpuCompileTables!.Dispose();
        _cpuCompileEngine.Dispose();
    }

    [IterationSetup(Target = nameof(CompileCudaKernel))]
    public void SetupCudaCompile()
    {
        _cudaCompileEngine = Engine.Create(AcceleratorSelection.OptionsFor(AcceleratorKind.Cuda));
        _cudaCompileTables = _cudaCompileEngine.Upload(_table);
    }

    [Benchmark]
    public EquilibriumBatchResult CompileCudaKernel() => _cudaCompileEngine.Run(_cudaCompileTables!, _compileBatch);

    [IterationCleanup(Target = nameof(CompileCudaKernel))]
    public void CleanupCudaCompile()
    {
        _cudaCompileTables!.Dispose();
        _cudaCompileEngine.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _uploaded?.Dispose();
        _uploadEngine.Dispose();
        _solver.Dispose();
    }

    private static EquilibriumBatch BuildTrivialBatch(int elementCount, IReadOnlyList<double> molesPerKilogram, JsonElement inputs)
    {
        var batch = new EquilibriumBatch(1, elementCount);
        batch.Kind[0] = ProblemKind.AssignedEnthalpyPressure;
        batch.Pressure[0] = inputs.GetProperty("pressure").GetDouble();
        batch.Target[0] = inputs.GetProperty("enthalpy").GetDouble();
        for (var e = 0; e < elementCount; e++)
        {
            batch.ElementMoles[e] = molesPerKilogram[e];
        }
        return batch;
    }
}
