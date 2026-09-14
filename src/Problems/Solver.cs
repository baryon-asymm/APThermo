using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Problems;

/// <summary>The library's entry point: turns propellants and mixtures into chemical systems and runs the problems on one engine.</summary>
public sealed class Solver : IDisposable
{
    private readonly Engine _engine;
    private readonly ChemicalSystemCache _systems;
    private readonly RocketRunner _rocketRunner;
    private readonly EquilibriumRunner _equilibriumRunner;
    private readonly PropellantMixtures _mixtures;
    private bool _disposed;

    private Solver(SpeciesDatabase database, Engine engine)
    {
        Database = database;
        _engine = engine;
        _systems = new ChemicalSystemCache(database, engine);
        _rocketRunner = new RocketRunner(database, engine);
        _equilibriumRunner = new EquilibriumRunner(database, engine);
        _mixtures = new PropellantMixtures(database, engine);
    }

    /// <summary>A solver over the database, bound to the accelerator the options select (Execution).</summary>
    public static Solver Create(SpeciesDatabase database, EngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new Solver(database, Engine.Create(options));
    }

    public SpeciesDatabase Database { get; }

    public AcceleratorInfo Accelerator => _engine.Accelerator;

    /// <summary>The element moles and the enthalpy per kilogram a propellant implies, for its own ratio or the one given.</summary>
    public ElementalMixture Mixture(Propellant propellant, double? oxidizerToFuelRatio = null)
    {
        ArgumentNullException.ThrowIfNull(propellant);
        ThrowIfDisposed();
        return _mixtures.Of(propellant, oxidizerToFuelRatio);
    }

    /// <summary>The candidate product species of an element set under the selection rule (BOOT.md).</summary>
    public IReadOnlyList<string> CandidateSpecies(IReadOnlyList<string> elements, IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return SpeciesSelection.Candidates(Database, elements, omit ?? [], only);
    }

    /// <summary>Σ n_i A_i in kilograms with the database's atomic weights: the mass the element moles describe, the number the mass check compares with one kilogram.</summary>
    public double MassOf(ElementalMixture mixture)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        return MixtureMass.Of(Database, mixture);
    }

    public RocketResult Solve(Propellant propellant, RocketProblem problem) => Solve(propellant, [problem])[0];

    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var mixture = Mixture(propellant);
        return _rocketRunner.Solve(GetSystem(mixture), problems.Select(p => new RocketCase(mixture, p, propellant, propellant.OxidizerToFuelRatio)).ToList());
    }

    /// <summary>Every (ratio, chamber pressure) of the sweep as one batch; results ratio-major, then by pressure, with all area ratios as exits.</summary>
    public IReadOnlyList<RocketResult> Solve(RocketSweep sweep)
    {
        ArgumentNullException.ThrowIfNull(sweep);
        if (sweep.OxidizerToFuelRatios.Count == 0 || sweep.ChamberPressures.Count == 0)
        {
            throw new ArgumentException("a sweep needs at least one ratio and one chamber pressure", nameof(sweep));
        }

        var cases = new List<RocketCase>();
        foreach (var ratio in sweep.OxidizerToFuelRatios)
        {
            var mixture = Mixture(sweep.Propellant, ratio);
            foreach (var pressure in sweep.ChamberPressures)
            {
                var problem = new RocketProblem { ChamberPressure = pressure, AreaRatios = sweep.AreaRatios, Flow = sweep.Flow, Transport = sweep.Transport };
                cases.Add(new RocketCase(mixture, problem, sweep.Propellant, ratio));
            }
        }

        return _rocketRunner.Solve(GetSystem(cases[0].Mixture), cases);
    }

    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem) => Solve(propellant, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var mixture = Mixture(propellant);
        return _equilibriumRunner.Solve(GetSystem(mixture), problems.Select(p => new EquilibriumCase(mixture, p, propellant)).ToList());
    }

    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _rocketRunner.Solve(GetSystem(mixture), problems.Select(p => new RocketCase(mixture, p, null, null)).ToList());
    }

    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _equilibriumRunner.Solve(GetSystem(mixture), problems.Select(p => new EquilibriumCase(mixture, p, null)).ToList());
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    public IReadOnlyList<RocketResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var system = UnionSystem(mixtures, problems.Count, "rocket");
        return _rocketRunner.Solve(system, mixtures.Select((mixture, i) => new RocketCase(mixture, problems[i], null, null)).ToList());
    }

    /// <summary>One equilibrium case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    public IReadOnlyList<EquilibriumResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveEquilibrium(mixtures, problems, "mixture");
    }

    /// <summary>One batch of state records over the union of their elements; a record lacking an element runs with the species containing it inactive.</summary>
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ThrowIfDisposed();
        options ??= new StateBatchOptions();
        if (states.Count == 0)
        {
            throw new ArgumentException("the state batch is empty", nameof(states));
        }

        var (mixtures, problems) = StateRecords.ToProblems(states, options);
        return SolveEquilibrium(mixtures, problems, "state record");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _systems.Dispose();
        _engine.Dispose();
    }

    /// <summary>One case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<EquilibriumResult> SolveEquilibrium(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems, string noun)
    {
        var system = UnionSystem(mixtures, problems.Count, "equilibrium");
        return _equilibriumRunner.Solve(system, mixtures.Select((mixture, i) => new EquilibriumCase(mixture, problems[i], null)).ToList(), noun);
    }

    private ChemicalSystem GetSystem(ElementalMixture mixture) => _systems.Get(mixture);

    /// <summary>The system over the union of the mixtures' elements, in order of first appearance, under the species lists they all share.</summary>
    private ChemicalSystem UnionSystem(IReadOnlyList<ElementalMixture> mixtures, int problemCount, string kind) => _systems.Union(mixtures, problemCount, kind);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
