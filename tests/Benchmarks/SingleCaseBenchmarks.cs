using APThermo.Data;
using APThermo.Execution;
using APThermo.Fixtures;
using APThermo.Harness;
using APThermo.Problems;
using BenchmarkDotNet.Attributes;
using EquilibriumProblem = APThermo.Problems.EquilibriumProblem;
using EquilibriumResult = APThermo.Problems.EquilibriumResult;
using ProblemKind = APThermo.Equilibrium.ProblemKind;

namespace APThermo.Benchmarks;

/// Group 3 of `BOOT.md`, Constraints: one case through `Problems.Solver`'s single-case
/// overload, the latency a .NET caller sees, on the CPU accelerator. Reads the
/// reference's own LOX/LH2 hp fixture.
[MemoryDiagnoser]
public class SingleCaseBenchmarks
{
    private Solver _solver = null!;
    private ElementalMixture _mixture = null!;
    private EquilibriumProblem _problem = null!;

    /// <summary>Loads the database, creates the solver on the CPU accelerator, builds the LOX/LH2 hp fixture's mixture and
    /// problem, and runs one warm-up solve to record its status and hash.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var database = SpeciesDatabase.Load(RepositoryPaths.Resolve("data", "thermo.inp"), RepositoryPaths.Resolve("data", "trans.inp"));
        _solver = Solver.Create(database, AcceleratorSelection.OptionsFor(AcceleratorKind.Cpu));

        var fixtureCase = CeaFixtures.Load(RepositoryPaths.Resolve(
            "tests", "Fixtures", "cases", "hp", "lox-lh2_of6_pc7MPa_shiftingEquilibrium_chamber.json"));
        var inputs = fixtureCase.Inputs;
        var composition = FixtureJson.ReadComposition(inputs);
        var enthalpy = inputs.GetProperty("enthalpy").GetDouble();
        var pressure = inputs.GetProperty("pressure").GetDouble();

        _mixture = ElementalMixture.Create(composition, enthalpy);
        _problem = new EquilibriumProblem { Kind = ProblemKind.AssignedEnthalpyPressure, Pressure = pressure };

        var result = _solver.Solve(_mixture, _problem);
        var hash = new BitHash().AddState(result.State.State).AddMoles(result.State.MoleFractions).AddStatus(result.State.Status);
        Console.WriteLine($"[SingleCase] status={result.Status} hash={hash.ToHex()}");
    }

    /// <summary>Solves the fixture's hp problem once.</summary>
    [Benchmark]
    public EquilibriumResult Solve() => _solver.Solve(_mixture, _problem);

    /// <summary>Disposes the solver after every benchmark of this class has run.</summary>
    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();
}
