using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Problems;
using APThermo.Thermo;
using BenchmarkDotNet.Attributes;

namespace APThermo.Benchmarks;

/// Which states of `data/user-states.json` a run of `UserStatesBenchmarks` solves:
/// every one of the 48 together, or one record's 12 alone (BOOT.md, Constraints,
/// group 6).
public enum UserStateSelection
{
    /// <summary>All four records' 48 states together.</summary>
    All,

    /// <summary>Record 1 (17.5 % Al by mass) alone, 12 states.</summary>
    Record1,

    /// <summary>Record 2 (20.7 % Al by mass) alone, 12 states.</summary>
    Record2,

    /// <summary>Record 3 (0 % Al by mass) alone, 12 states.</summary>
    Record3,

    /// <summary>Record 4 (39.7 % Al by mass) alone, 12 states; reaches the 2700 K
    /// `ALN(L)` enthalpy-gap region at 6.5 MPa (BOOT.md, Constraints, group 6).</summary>
    Record4,
}

/// Group 6 of `BOOT.md`, Constraints: the user's four AP/HTPB/Al records of
/// `data/user-states.json`, through `Solver.SolveStates`, on the CPU accelerator and
/// on CUDA — all 48 states together, and each record's 12 alone.
[MemoryDiagnoser]
public class UserStatesBenchmarks
{
    /// <summary>Which of the four user-state records this run solves.</summary>
    [ParamsAllValues]
    public UserStateSelection Selection { get; set; }

    /// <summary>Which accelerator this run solves the selected states on.</summary>
    [Params(AcceleratorKind.Cpu, AcceleratorKind.Cuda)]
    public AcceleratorKind Accelerator { get; set; }

    private Solver _solver = null!;
    private IReadOnlyList<StateRecord> _states = null!;

    /// <summary>Loads the user-states fixture, selects the states this run solves, creates the solver on the chosen
    /// accelerator, and runs one warm-up solve to record its diagnostics.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        _solver = Solver.Create(database, AcceleratorSelection.OptionsFor(Accelerator));

        var byRecord = UserStates.ReadByRecord(RepositoryPaths.Resolve("tests", "Benchmarks", "data", "user-states.json"));
        _states = Selection switch
        {
            UserStateSelection.All => [.. byRecord.SelectMany(record => record)],
            UserStateSelection.Record1 => byRecord[0],
            UserStateSelection.Record2 => byRecord[1],
            UserStateSelection.Record3 => byRecord[2],
            UserStateSelection.Record4 => byRecord[3],
            _ => throw new ArgumentOutOfRangeException(nameof(Selection), Selection, "unknown selection"),
        };

        RecordDiagnostics();
    }

    /// <summary>Solves the selected user states on the selected accelerator.</summary>
    [Benchmark]
    public IReadOnlyList<EquilibriumResult> SolveStates() => _solver.SolveStates(_states);

    /// <summary>Disposes the solver after every benchmark of this class has run.</summary>
    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();

    private void RecordDiagnostics()
    {
        var results = _solver.SolveStates(_states);
        var hash = new BitHash();
        var okCount = 0;
        foreach (var result in results)
        {
            _ = hash.AddState(result.State.State).AddMoles(result.State.MoleFractions).AddStatus(result.State.Status);
            okCount += result.Status == CaseStatus.Ok ? 1 : 0;
        }
        Console.WriteLine($"[UserStates] selection={Selection} accelerator={Accelerator} ok={okCount}/{results.Count} hash={hash.ToHex()}");
    }
}
