using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Fixtures;
using AerospacePropellantThermodynamics.Harness;
using AerospacePropellantThermodynamics.Problems;
using BenchmarkDotNet.Attributes;
using EquilibriumProblem = AerospacePropellantThermodynamics.Problems.EquilibriumProblem;
using EquilibriumResult = AerospacePropellantThermodynamics.Problems.EquilibriumResult;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Benchmarks;

/// Group 3 of `BOOT.md`, Constraints: one case through `Problems.Solver`'s single-case
/// overload, the latency a .NET caller sees, on the CPU accelerator. Reads the
/// reference's own LOX/LH2 hp fixture.
[MemoryDiagnoser]
public class SingleCaseBenchmarks
{
    private Solver _solver = null!;
    private ElementalMixture _mixture = null!;
    private EquilibriumProblem _problem = null!;

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

    [Benchmark]
    public EquilibriumResult Solve() => _solver.Solve(_mixture, _problem);

    [GlobalCleanup]
    public void Cleanup() => _solver.Dispose();
}
