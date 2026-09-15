using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;
using BenchmarkDotNet.Attributes;

namespace AerospacePropellantThermodynamics.Benchmarks;

/// Which states of `data/user-states.json` a run of `UserStatesBenchmarks` solves:
/// every one of the 48 together, or one record's 12 alone (BOOT.md, Constraints,
/// group 6).
public enum UserStateSelection { All, Record1, Record2, Record3, Record4 }

/// Group 6 of `BOOT.md`, Constraints: the user's four AP/HTPB/Al records of
/// `data/user-states.json`, through `Solver.SolveStates`, on the CPU accelerator and
/// on CUDA — all 48 states together, and each record's 12 alone.
[MemoryDiagnoser]
public class UserStatesBenchmarks
{
    [ParamsAllValues]
    public UserStateSelection Selection { get; set; }

    [Params(AcceleratorKind.Cpu, AcceleratorKind.Cuda)]
    public AcceleratorKind Accelerator { get; set; }

    private Solver _solver = null!;
    private IReadOnlyList<StateRecord> _states = null!;

    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        _solver = Solver.Create(database, AcceleratorSelection.OptionsFor(Accelerator));

        var byRecord = UserStates.ReadByRecord(RepositoryPaths.Resolve("tests", "Benchmarks", "data", "user-states.json"));
        _states = Selection switch
        {
            UserStateSelection.All => byRecord.SelectMany(record => record).ToList(),
            UserStateSelection.Record1 => byRecord[0],
            UserStateSelection.Record2 => byRecord[1],
            UserStateSelection.Record3 => byRecord[2],
            UserStateSelection.Record4 => byRecord[3],
            _ => throw new ArgumentOutOfRangeException(nameof(Selection), Selection, "unknown selection"),
        };

        RecordDiagnostics();
    }

    [Benchmark]
    public IReadOnlyList<EquilibriumResult> SolveStates() => _solver.SolveStates(_states);

    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();

    private void RecordDiagnostics()
    {
        var results = _solver.SolveStates(_states);
        var hash = new BitHash();
        var okCount = 0;
        foreach (var result in results)
        {
            hash.AddState(result.State.State).AddMoles(result.State.MoleFractions).AddStatus(result.State.Status);
            okCount += result.Status == CaseStatus.Ok ? 1 : 0;
        }
        Console.WriteLine($"[UserStates] selection={Selection} accelerator={Accelerator} ok={okCount}/{results.Count} hash={hash.ToHex()}");
    }
}
