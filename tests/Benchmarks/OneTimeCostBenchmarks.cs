using System.Globalization;
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
    private SpeciesTable? _assembledTable;
    private EquilibriumBatch _compileBatch = null!;
    private EquilibriumBatchResult? _compiledResult;

    private Engine _uploadEngine = null!;
    private UploadedTables? _uploaded;

    private Engine _cpuCompileEngine = null!;
    private UploadedTables? _cpuCompileTables;

    private Engine _cudaCompileEngine = null!;
    private UploadedTables? _cudaCompileTables;

    /// <summary>Loads the database and the fixture once, builds the species table and a trivial one-case batch the
    /// compilation benchmarks reuse, and creates the engine the upload benchmark uploads into.</summary>
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

    /// <summary>Loads the NASA database from files, cold.</summary>
    [Benchmark]
    public SpeciesDatabase LoadDatabase() => SpeciesDatabase.Load(_thermoPath, _transPath);

    /// <summary>Assembles the chemical system (candidate species, table) for the fixture's elements. `SpeciesTable` is
    /// internal to the tree contract (root BOOT.md, Delivery: Tree contracts), so the benchmark keeps it in a field rather
    /// than returning it from this public method; <see cref="Cleanup"/> reads that field so the store is not dead.</summary>
    [Benchmark]
    public void AssembleChemicalSystem() => _assembledTable = SpeciesTable.Build(_database, _elements, _solver.CandidateSpeciesFor(_elements));

    /// <summary>Disposes the previous iteration's upload before the next `UploadSpeciesTable` iteration, so every measured
    /// upload starts from a clean engine.</summary>
    [IterationSetup(Target = nameof(UploadSpeciesTable))]
    public void SetupUpload() => _uploaded?.Dispose();

    /// <summary>Uploads the species table to the engine once.</summary>
    [Benchmark]
    public void UploadSpeciesTable() => _uploaded = _uploadEngine.Upload(_table);

    /// <summary>Creates a fresh CPU engine and uploads the species table into it, so the next `CompileCpuKernel` iteration
    /// compiles cold rather than reusing an already-JIT-compiled kernel.</summary>
    [IterationSetup(Target = nameof(CompileCpuKernel))]
    public void SetupCpuCompile()
    {
        _cpuCompileEngine = Engine.Create(AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));
        _cpuCompileTables = _cpuCompileEngine.Upload(_table);
    }

    /// <summary>Runs the trivial one-case batch on the fresh CPU engine, forcing its kernel to compile cold.
    /// `EquilibriumBatchResult` is internal to the tree contract (root BOOT.md, Delivery: Tree contracts), so the benchmark
    /// keeps it in a field rather than returning it from this public method; <see cref="Cleanup"/> reads that field so the
    /// store is not dead.</summary>
    [Benchmark]
    public void CompileCpuKernel() => _compiledResult = _cpuCompileEngine.Run(_cpuCompileTables!, _compileBatch);

    /// <summary>Disposes the CPU engine and its uploaded tables after each `CompileCpuKernel` iteration.</summary>
    [IterationCleanup(Target = nameof(CompileCpuKernel))]
    public void CleanupCpuCompile()
    {
        _cpuCompileTables!.Dispose();
        _cpuCompileEngine.Dispose();
    }

    /// <summary>Creates a fresh CUDA engine and uploads the species table into it, so the next `CompileCudaKernel` iteration
    /// compiles cold, the libdevice post-link included.</summary>
    [IterationSetup(Target = nameof(CompileCudaKernel))]
    public void SetupCudaCompile()
    {
        _cudaCompileEngine = Engine.Create(AcceleratorSelection.OptionsFor(AcceleratorKind.Cuda));
        _cudaCompileTables = _cudaCompileEngine.Upload(_table);
    }

    /// <summary>Runs the trivial one-case batch on the fresh CUDA engine, forcing its kernel to compile cold.</summary>
    [Benchmark]
    public void CompileCudaKernel() => _compiledResult = _cudaCompileEngine.Run(_cudaCompileTables!, _compileBatch);

    /// <summary>Disposes the CUDA engine and its uploaded tables after each `CompileCudaKernel` iteration.</summary>
    [IterationCleanup(Target = nameof(CompileCudaKernel))]
    public void CleanupCudaCompile()
    {
        _cudaCompileTables!.Dispose();
        _cudaCompileEngine.Dispose();
    }

    /// <summary>Logs the last assembled table's and the last compiled batch's diagnostics, then disposes the upload
    /// engine's last upload, the upload engine and the solver after every benchmark of this class has run.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        Console.WriteLine(
            $"[OneTimeCost] assembledSpecies={_assembledTable?.Species.Count.ToString(CultureInfo.InvariantCulture) ?? "-"} " +
            $"compiledStatus={_compiledResult?.Status[0].ToString() ?? "-"}");

        _uploaded?.Dispose();
        _uploadEngine.Dispose();
        _solver.Dispose();
    }

    private static EquilibriumBatch BuildTrivialBatch(int elementCount, double[] molesPerKilogram, JsonElement inputs)
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
