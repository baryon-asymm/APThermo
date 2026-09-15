using APThermo.Data;
using APThermo.Equilibrium;
using APThermo.Execution;
using APThermo.Performance;
using APThermo.Thermo;
using APThermo.Transport;

namespace APThermo.Problems;

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

    /// <summary>Readable after <see cref="Dispose"/>: it names no device resource.</summary>
    public SpeciesDatabase Database { get; }

    /// <summary>Readable after <see cref="Dispose"/>: it names no device resource.</summary>
    public AcceleratorInfo Accelerator => _engine.Accelerator;

    /// <summary>The element moles and the enthalpy per kilogram a propellant implies, for its own ratio or the one given.</summary>
    public ElementalMixture MixtureOf(Propellant propellant, double? oxidizerToFuelRatio = null)
    {
        ArgumentNullException.ThrowIfNull(propellant);
        ThrowIfDisposed();
        return _mixtures.Of(propellant, oxidizerToFuelRatio);
    }

    /// <summary>The candidate product species of an element set under the selection rule (BOOT.md).</summary>
    public IReadOnlyList<string> CandidateSpeciesFor(IReadOnlyList<string> elements, IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ThrowIfDisposed();
        return SpeciesSelection.Candidates(Database, elements, omit ?? [], only);
    }

    /// <summary>Σ n_i A_i in kilograms with the database's atomic weights: the mass the element moles describe, the number the mass check compares with one kilogram.</summary>
    public double MassOf(ElementalMixture mixture)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ThrowIfDisposed();
        return MixtureMass.Of(Database, mixture);
    }

    public RocketResult Solve(Propellant propellant, RocketProblem problem) => Solve(propellant, [problem])[0];

    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var mixture = MixtureOf(propellant);
        return _rocketRunner.Solve(_systems.Get(mixture), problems.Select(p => new RocketCase(mixture, p, propellant, propellant.OxidizerToFuelRatio)).ToList());
    }

    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem) => Solve(propellant, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var mixture = MixtureOf(propellant);
        return _equilibriumRunner.Solve(_systems.Get(mixture), problems.Select(p => new EquilibriumCase(mixture, p, propellant)).ToList());
    }

    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _rocketRunner.Solve(_systems.Get(mixture), problems.Select(p => new RocketCase(mixture, p, null, null)).ToList());
    }

    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _equilibriumRunner.Solve(_systems.Get(mixture), problems.Select(p => new EquilibriumCase(mixture, p, null)).ToList());
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    public IReadOnlyList<RocketResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveRocket(mixtures, problems, "mixture");
    }

    /// <summary>One equilibrium case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    public IReadOnlyList<EquilibriumResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveEquilibrium(mixtures, problems, "mixture");
    }

    /// <summary>One batch of state records without exits over the union of their elements; a record lacking an element runs with the species containing it inactive.</summary>
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ThrowIfDisposed();
        var (mixtures, problems) = StateRecords.ToEquilibriumProblems(states, Validated(states, options));
        return SolveEquilibrium(mixtures, problems, "state record");
    }

    /// <summary>One batch of state records with exits (BOOT.md, F-AR-02): each is a rocket case whose pressure is the chamber pressure.</summary>
    public IReadOnlyList<RocketResult> SolveRocketStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ThrowIfDisposed();
        var (mixtures, problems) = StateRecords.ToRocketProblems(states, Validated(states, options));
        return SolveRocket(mixtures, problems, "state record");
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

    /// <summary>The batch's options, defaulted, once the batch itself is confirmed non-empty.</summary>
    private static StateBatchOptions Validated(IReadOnlyList<StateRecord> states, StateBatchOptions? options)
    {
        if (states.Count == 0)
        {
            throw new ArgumentException("the state batch is empty", nameof(states));
        }

        return options ?? new StateBatchOptions();
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<RocketResult> SolveRocket(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems, string noun)
    {
        var system = _systems.Union(mixtures, problems.Count, "rocket");
        return _rocketRunner.Solve(system, mixtures.Select((mixture, i) => new RocketCase(mixture, problems[i], null, null)).ToList(), noun);
    }

    /// <summary>One case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<EquilibriumResult> SolveEquilibrium(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems, string noun)
    {
        var system = _systems.Union(mixtures, problems.Count, "equilibrium");
        return _equilibriumRunner.Solve(system, mixtures.Select((mixture, i) => new EquilibriumCase(mixture, problems[i], null)).ToList(), noun);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
