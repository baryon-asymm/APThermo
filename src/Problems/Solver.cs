using APThermo.Data;
using APThermo.Execution;

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
    /// <param name="database">The species database problems are solved against.</param>
    /// <param name="options">The engine options to bind with, or <see langword="null"/> for the default
    /// options.</param>
    /// <returns>A solver bound to the accelerator the options select.</returns>
    /// <exception cref="AcceleratorUnavailableException">No accelerator of the requested kind could be
    /// bound.</exception>
    public static Solver Create(SpeciesDatabase database, EngineOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new Solver(database, Engine.Create(options));
    }

    /// <value>The database this solver was created against. Readable after <see cref="Dispose"/>: it names no
    /// device resource.</value>
    public SpeciesDatabase Database { get; }

    /// <value>A description of the bound accelerator. Readable after <see cref="Dispose"/>: it names no device
    /// resource.</value>
    public AcceleratorInfo Accelerator => _engine.Accelerator;

    /// <summary>The element moles and the enthalpy per kilogram a propellant implies, for its own ratio or the one given.</summary>
    /// <param name="propellant">The propellant to convert.</param>
    /// <param name="oxidizerToFuelRatio">An oxidizer-to-fuel ratio to use instead of the propellant's own, or
    /// <see langword="null"/> to use the propellant's mixture rule as given.</param>
    /// <returns>The element moles (mol/kg) and the enthalpy (J/kg) of one kilogram of the propellant.</returns>
    /// <exception cref="ArgumentException"><paramref name="oxidizerToFuelRatio"/> is given for a propellant
    /// given by total mass fractions, or is not positive and finite.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public ElementalMixture MixtureOf(Propellant propellant, double? oxidizerToFuelRatio = null)
    {
        ArgumentNullException.ThrowIfNull(propellant);
        ThrowIfDisposed();
        return _mixtures.Of(propellant, oxidizerToFuelRatio);
    }

    /// <summary>The candidate product species of an element set under the selection rule (BOOT.md).</summary>
    /// <param name="elements">The element symbols the candidate species may be built from.</param>
    /// <param name="omit">Product species never to consider, or <see langword="null"/> for none.</param>
    /// <param name="only">When given, exactly the product species to consider.</param>
    /// <returns>The candidate product species, in the selection rule's order.</returns>
    /// <exception cref="ArgumentException">An <paramref name="only"/> name is no product species or lies
    /// outside <paramref name="elements"/>.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<string> CandidateSpeciesFor(IReadOnlyList<string> elements, IReadOnlyList<string>? omit = null, IReadOnlyList<string>? only = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ThrowIfDisposed();
        return SpeciesSelection.Candidates(Database, elements, omit ?? [], only);
    }

    /// <summary>Σ n_i A_i in kilograms with the database's atomic weights: the mass the element moles describe, the number the mass check compares with one kilogram.</summary>
    /// <param name="mixture">The mixture whose element moles are weighed.</param>
    /// <returns>The mass, in kilograms, the mixture's element moles describe.</returns>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public double MassOf(ElementalMixture mixture)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ThrowIfDisposed();
        return MixtureMass.Of(Database, mixture);
    }

    /// <summary>Solves one rocket problem for a propellant.</summary>
    /// <param name="propellant">The propellant to solve for.</param>
    /// <param name="problem">The rocket problem.</param>
    /// <returns>The rocket result.</returns>
    /// <exception cref="ArgumentException">An element of the propellant has no database record; the chamber
    /// pressure or an exit value is not positive; or transport is requested on a database loaded without
    /// <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The propellant's element moles do not weigh one kilogram within
    /// its mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public RocketResult Solve(Propellant propellant, RocketProblem problem) => Solve(propellant, [problem])[0];

    /// <summary>Solves rocket problems for a propellant, one case per problem.</summary>
    /// <param name="propellant">The propellant to solve for.</param>
    /// <param name="problems">The rocket problems, one case each.</param>
    /// <returns>The rocket results, in the order of <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">An element of the propellant has no database record; a chamber
    /// pressure or an exit value is not positive; or transport is requested on a database loaded without
    /// <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The propellant's element moles do not weigh one kilogram within
    /// its mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<RocketResult> Solve(Propellant propellant, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var mixture = MixtureOf(propellant);
        return _rocketRunner.Solve(_systems.Get(mixture), [.. problems.Select(p => new RocketCase(mixture, p, propellant, propellant.OxidizerToFuelRatio))]);
    }

    /// <summary>Solves one equilibrium problem for a propellant.</summary>
    /// <param name="propellant">The propellant to solve for.</param>
    /// <param name="problem">The equilibrium problem.</param>
    /// <returns>The equilibrium result.</returns>
    /// <exception cref="ArgumentException">An element of the propellant has no database record; the pressure is
    /// not positive; the problem has no enthalpy for an assigned-enthalpy kind and the mixture has none either;
    /// or transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The propellant's element moles do not weigh one kilogram within
    /// its mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem) => Solve(propellant, [problem])[0];

    /// <summary>Solves equilibrium problems for a propellant, one case per problem.</summary>
    /// <param name="propellant">The propellant to solve for.</param>
    /// <param name="problems">The equilibrium problems, one case each.</param>
    /// <returns>The equilibrium results, in the order of <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">An element of the propellant has no database record; a pressure is
    /// not positive; a problem has no enthalpy for an assigned-enthalpy kind and the mixture has none either; or
    /// transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The propellant's element moles do not weigh one kilogram within
    /// its mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var mixture = MixtureOf(propellant);
        return _equilibriumRunner.Solve(_systems.Get(mixture), [.. problems.Select(p => new EquilibriumCase(mixture, p, propellant))]);
    }

    /// <summary>Solves one rocket problem for an elemental mixture.</summary>
    /// <param name="mixture">The mixture to solve for.</param>
    /// <param name="problem">The rocket problem.</param>
    /// <returns>The rocket result.</returns>
    /// <exception cref="ArgumentException">An element of the mixture has no database record; the chamber
    /// pressure or an exit value is not positive; the mixture has no enthalpy; or transport is requested on a
    /// database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem) => Solve(mixture, [problem])[0];

    /// <summary>Solves rocket problems for an elemental mixture, one case per problem.</summary>
    /// <param name="mixture">The mixture to solve for.</param>
    /// <param name="problems">The rocket problems, one case each.</param>
    /// <returns>The rocket results, in the order of <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">An element of the mixture has no database record; a chamber pressure
    /// or an exit value is not positive; the mixture has no enthalpy; or transport is requested on a database
    /// loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _rocketRunner.Solve(_systems.Get(mixture), [.. problems.Select(p => new RocketCase(mixture, p, null, null))]);
    }

    /// <summary>Solves one equilibrium problem for an elemental mixture.</summary>
    /// <param name="mixture">The mixture to solve for.</param>
    /// <param name="problem">The equilibrium problem.</param>
    /// <returns>The equilibrium result.</returns>
    /// <exception cref="ArgumentException">An element of the mixture has no database record; the pressure is
    /// not positive; the problem has no enthalpy for an assigned-enthalpy kind and the mixture has none either;
    /// or transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem) => Solve(mixture, [problem])[0];

    /// <summary>Solves equilibrium problems for an elemental mixture, one case per problem.</summary>
    /// <param name="mixture">The mixture to solve for.</param>
    /// <param name="problems">The equilibrium problems, one case each.</param>
    /// <returns>The equilibrium results, in the order of <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">An element of the mixture has no database record; a pressure is not
    /// positive; a problem has no enthalpy for an assigned-enthalpy kind and the mixture has none either; or
    /// transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">The mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return _equilibriumRunner.Solve(_systems.Get(mixture), [.. problems.Select(p => new EquilibriumCase(mixture, p, null))]);
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    /// <param name="mixtures">The mixtures to solve, one per case.</param>
    /// <param name="problems">The rocket problems, one per case, matched to <paramref name="mixtures"/> by index.</param>
    /// <returns>The rocket results, one per index of <paramref name="mixtures"/> and <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">The batch is empty; the mixtures do not share the same
    /// <see cref="ElementalMixture.Omit"/> and <see cref="ElementalMixture.Only"/> lists; an element of a
    /// mixture has no database record; a chamber pressure or an exit value is not positive; a mixture has no
    /// enthalpy; or transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">A mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<RocketResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveRocket(mixtures, problems, "mixture");
    }

    /// <summary>One equilibrium case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    /// <param name="mixtures">The mixtures to solve, one per case.</param>
    /// <param name="problems">The equilibrium problems, one per case, matched to <paramref name="mixtures"/> by
    /// index.</param>
    /// <returns>The equilibrium results, one per index of <paramref name="mixtures"/> and <paramref name="problems"/>.</returns>
    /// <exception cref="ArgumentException">The batch is empty; the mixtures do not share the same
    /// <see cref="ElementalMixture.Omit"/> and <see cref="ElementalMixture.Only"/> lists; an element of a
    /// mixture has no database record; a pressure is not positive; a mixture has no enthalpy where its problem
    /// needs one; or transport is requested on a database loaded without <c>trans.inp</c>.</exception>
    /// <exception cref="MixtureMassException">A mixture's element moles do not weigh one kilogram within its
    /// mass tolerance.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<EquilibriumResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveEquilibrium(mixtures, problems, "mixture");
    }

    /// <summary>One batch of state records without exits over the union of their elements; a record lacking an element runs with the species containing it inactive.</summary>
    /// <param name="states">The state records to solve, none of them naming an exit.</param>
    /// <param name="options">The batch's options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The equilibrium results, one per index of <paramref name="states"/>.</returns>
    /// <exception cref="StateRecordException">A record breaks a shape rule: none or several of enthalpy,
    /// temperature and entropy given; the record names an exit; or a composition rule is broken (a negative
    /// abundance, an empty or duplicated symbol).</exception>
    /// <exception cref="MixtureMassException">A record's element moles do not weigh one kilogram within the
    /// batch's mass tolerance.</exception>
    /// <exception cref="ArgumentException">The batch is empty, or an element of a record has no database
    /// record.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
    public IReadOnlyList<EquilibriumResult> SolveStates(IReadOnlyList<StateRecord> states, StateBatchOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(states);
        ThrowIfDisposed();
        var (mixtures, problems) = StateRecords.ToEquilibriumProblems(states, Validated(states, options));
        return SolveEquilibrium(mixtures, problems, "state record");
    }

    /// <summary>One batch of state records with exits (BOOT.md, F-AR-02): each is a rocket case whose pressure is the chamber pressure.</summary>
    /// <param name="states">The state records to solve, each naming at least one exit.</param>
    /// <param name="options">The batch's options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The rocket results, one per index of <paramref name="states"/>.</returns>
    /// <exception cref="StateRecordException">A record breaks a shape rule: none or several of enthalpy,
    /// temperature and entropy given; the record does not name an exit; exits without an enthalpy; or a
    /// composition rule is broken (a negative abundance, an empty or duplicated symbol).</exception>
    /// <exception cref="MixtureMassException">A record's element moles do not weigh one kilogram within the
    /// batch's mass tolerance.</exception>
    /// <exception cref="ArgumentException">The batch is empty, or an element of a record has no database
    /// record.</exception>
    /// <exception cref="ObjectDisposedException">The solver has been disposed.</exception>
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
        return states.Count == 0
            ? throw new ArgumentException("the state batch is empty", nameof(states))
            : options ?? new StateBatchOptions();
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<RocketResult> SolveRocket(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems, string noun)
    {
        var system = _systems.Union(mixtures, problems.Count, "rocket");
        return _rocketRunner.Solve(system, [.. mixtures.Select((mixture, i) => new RocketCase(mixture, problems[i], null, null))], noun);
    }

    /// <summary>One case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<EquilibriumResult> SolveEquilibrium(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems, string noun)
    {
        var system = _systems.Union(mixtures, problems.Count, "equilibrium");
        return _equilibriumRunner.Solve(system, [.. mixtures.Select((mixture, i) => new EquilibriumCase(mixture, problems[i], null))], noun);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
