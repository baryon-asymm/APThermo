using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Performance;
using APThermo.Problems;
using APThermo.Thermo;
using BenchmarkDotNet.Attributes;

namespace APThermo.Benchmarks;

/// The six problem kinds of `BOOT.md`, Constraints, group 2: tp, hp, sp, and rocket in
/// each of its three flow models, each with and without transport.
public enum BenchmarkProblemKind
{
    /// <summary>Assigned temperature and pressure, one AP/HTPB/Al chamber case.</summary>
    Tp,

    /// <summary>Assigned enthalpy and pressure, one LOX/LH2 chamber case.</summary>
    Hp,

    /// <summary>Assigned entropy and pressure, one LOX/LH2 chamber case.</summary>
    Sp,

    /// <summary>A rocket case in shifting-equilibrium flow.</summary>
    RocketShiftingEquilibrium,

    /// <summary>A rocket case frozen at the chamber.</summary>
    RocketFrozenAtChamber,

    /// <summary>A rocket case frozen at the throat.</summary>
    RocketFrozenAtThroat,
}

/// Group 2 of `BOOT.md`, Constraints: one case of every problem kind, through
/// `Problems.Solver` on the CPU accelerator, so that a regression points at a node.
/// Every case is read from the reference's own tp, hp, sp or rocket fixture.
[MemoryDiagnoser]
public class ProblemKindBenchmarks
{
    /// <summary>Which problem kind this run solves.</summary>
    [ParamsAllValues]
    public BenchmarkProblemKind Kind { get; set; }

    /// <summary>Whether this run also solves the transport properties.</summary>
    [Params(false, true)]
    public bool Transport { get; set; }

    private Solver _solver = null!;
    private IReadOnlyList<StateRecord>? _rocketRecords;
    private IReadOnlyList<StateRecord>? _equilibriumRecords;
    private StateBatchOptions _options = null!;

    /// <summary>Loads the database, creates the solver on the CPU accelerator, loads the fixture record for the chosen
    /// problem kind, and runs one warm-up solve to record its diagnostics.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        _solver = Solver.Create(database, AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));
        _options = new StateBatchOptions(Transport: Transport);

        var record = LoadRecord();
        if (record.HasExits)
        {
            _rocketRecords = [record];
        }
        else
        {
            _equilibriumRecords = [record];
        }

        RecordDiagnostics();
    }

    /// <summary>Solves the chosen problem kind's one state, rocket or equilibrium.</summary>
    [Benchmark]
    public object Solve() => _rocketRecords is not null
        ? _solver.SolveRocketStates(_rocketRecords, _options)
        : _solver.SolveStates(_equilibriumRecords!, _options);

    /// <summary>Disposes the solver after every benchmark of this class has run.</summary>
    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();

    private StateRecord LoadRecord() => Kind switch
    {
        BenchmarkProblemKind.Tp => FixtureStateRecords.EquilibriumRecord(
            LoadFixture("tp", "ap-htpb-al_pc7MPa_shiftingEquilibrium_chamber.json")),
        BenchmarkProblemKind.Hp => FixtureStateRecords.EquilibriumRecord(
            LoadFixture("hp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber.json")),
        BenchmarkProblemKind.Sp => FixtureStateRecords.EquilibriumRecord(
            LoadFixture("sp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber.json")),
        BenchmarkProblemKind.RocketShiftingEquilibrium or BenchmarkProblemKind.RocketFrozenAtChamber or BenchmarkProblemKind.RocketFrozenAtThroat =>
            WithFlow(FixtureStateRecords.RocketRecord(LoadFixture("rocket", "lox-lh2_of6_pc7MPa_shiftingEquilibrium.json"))),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "unknown problem kind"),
    };

    private StateRecord WithFlow(StateRecord record) => record with
    {
        Flow = Kind switch
        {
            BenchmarkProblemKind.RocketShiftingEquilibrium => FlowModel.ShiftingEquilibrium,
            BenchmarkProblemKind.RocketFrozenAtChamber => FlowModel.FrozenAtChamber,
            BenchmarkProblemKind.RocketFrozenAtThroat => FlowModel.FrozenAtThroat,
            BenchmarkProblemKind.Tp => throw new NotImplementedException(),
            BenchmarkProblemKind.Hp => throw new NotImplementedException(),
            BenchmarkProblemKind.Sp => throw new NotImplementedException(),
            _ => throw new InvalidOperationException($"{Kind} is not a rocket kind"),
        },
    };

    private static CeaCase LoadFixture(string kind, string name) =>
        CeaFixtures.Load(RepositoryPaths.Resolve("tests", "Fixtures", "cases", kind, name));

    private void RecordDiagnostics()
    {
        var hash = new BitHash();
        var status = _rocketRecords is not null ? RecordRocketDiagnostics(hash) : RecordEquilibriumDiagnostics(hash);
        Console.WriteLine($"[ProblemKind] kind={Kind} transport={Transport} status={status} hash={hash.ToHex()}");
    }

    private CaseStatus RecordRocketDiagnostics(BitHash hash)
    {
        var result = _solver.SolveRocketStates(_rocketRecords!, _options)[0];
        foreach (var station in result.Stations)
        {
            _ = hash.AddState(station.State).AddMoles(station.MoleFractions).AddStatus(station.Status);
        }
        return result.Status;
    }

    private CaseStatus RecordEquilibriumDiagnostics(BitHash hash)
    {
        var result = _solver.SolveStates(_equilibriumRecords!, _options)[0];
        _ = hash.AddState(result.State.State).AddMoles(result.State.MoleFractions).AddStatus(result.State.Status);
        return result.Status;
    }
}
