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
    private readonly Dictionary<Propellant, double[]> _reactantEnthalpies = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    private Solver(SpeciesDatabase database, Engine engine)
    {
        Database = database;
        _engine = engine;
        _systems = new ChemicalSystemCache(database, engine);
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
        var fractions = propellant.MassFractionsFor(oxidizerToFuelRatio);
        var perKilogram = ReactantEnthalpies(propellant);
        var elements = propellant.Elements;
        var moles = new double[elements.Count];
        var enthalpy = 0.0;
        for (var k = 0; k < propellant.Resolved.Count; k++)
        {
            var r = propellant.Resolved[k];
            foreach (var (symbol, count) in r.Formula)
            {
                moles[IndexOf(elements, symbol)] += fractions[k] * count / r.MolarMass;
            }

            enthalpy += fractions[k] * perKilogram[k];
        }

        var byName = new Dictionary<string, double>(elements.Count, StringComparer.Ordinal);
        for (var i = 0; i < elements.Count; i++)
        {
            byName[elements[i]] = moles[i] * UnitFactors.MolesPerKilomole;
        }

        return ElementalMixture.Create(byName, enthalpy, propellant.Omit, propellant.Only);
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
        return SolveRocket(GetSystem(mixture), problems.Select(p => new RocketCase(mixture, p, propellant, propellant.OxidizerToFuelRatio)).ToList());
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

        return SolveRocket(GetSystem(cases[0].Mixture), cases);
    }

    public EquilibriumResult Solve(Propellant propellant, EquilibriumProblem problem) => Solve(propellant, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(Propellant propellant, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        var mixture = Mixture(propellant);
        return SolveEquilibrium(GetSystem(mixture), problems.Select(p => new EquilibriumCase(mixture, p, propellant)).ToList());
    }

    public RocketResult Solve(ElementalMixture mixture, RocketProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<RocketResult> Solve(ElementalMixture mixture, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveRocket(GetSystem(mixture), problems.Select(p => new RocketCase(mixture, p, null, null)).ToList());
    }

    public EquilibriumResult Solve(ElementalMixture mixture, EquilibriumProblem problem) => Solve(mixture, [problem])[0];

    public IReadOnlyList<EquilibriumResult> Solve(ElementalMixture mixture, IReadOnlyList<EquilibriumProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixture);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        return SolveEquilibrium(GetSystem(mixture), problems.Select(p => new EquilibriumCase(mixture, p, null)).ToList());
    }

    /// <summary>One rocket case per index over the union of the mixtures' elements; every mixture carries the same species lists.</summary>
    public IReadOnlyList<RocketResult> Solve(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<RocketProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        ArgumentNullException.ThrowIfNull(problems);
        ThrowIfDisposed();
        var system = UnionSystem(mixtures, problems.Count, "rocket");
        return SolveRocket(system, mixtures.Select((mixture, i) => new RocketCase(mixture, problems[i], null, null)).ToList());
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

    private IReadOnlyList<RocketResult> SolveRocket(ChemicalSystem system, IReadOnlyList<RocketCase> cases)
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no rocket problems were given");
        }

        var groups = new Dictionary<(int Pressures, int Areas), List<int>>();
        var order = new List<(int Pressures, int Areas)>();
        var masses = new double[cases.Count];
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant, _) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            ProblemValidation.Rocket(Database, mixture, problem, k);
            masses[k] = CheckMass(mixture, propellant, "mixture", k);
            var key = (problem.PressureRatios.Count, problem.AreaRatios.Count);
            if (!groups.TryGetValue(key, out var members))
            {
                groups[key] = members = [];
                order.Add(key);
            }

            members.Add(k);
        }

        var table = system.Table;
        var speciesNames = StationFactory.SpeciesNames(table);
        var results = new RocketResult[cases.Count];
        foreach (var key in order)
        {
            var members = groups[key];
            var kinds = Enumerable.Repeat(ExitSpecification.PressureRatio, key.Pressures).Concat(Enumerable.Repeat(ExitSpecification.AreaRatio, key.Areas)).ToArray();
            var batch = new RocketBatch(members.Count, table.ElementCount, kinds);
            var anyTransport = false;
            for (var m = 0; m < members.Count; m++)
            {
                var (mixture, problem, _, _) = cases[members[m]];
                batch.ChamberPressure[m] = problem.ChamberPressure;
                batch.ReactantEnthalpy[m] = mixture.Enthalpy!.Value;
                batch.TemperatureEstimate[m] = problem.TemperatureEstimate;
                batch.Flow[m] = problem.Flow;
                Array.Copy(mixture.KilomolesPerKilogram(system.Elements), 0, batch.ElementMoles, m * table.ElementCount, table.ElementCount);
                var exits = problem.PressureRatios.Concat(problem.AreaRatios).ToArray();
                Array.Copy(exits, 0, batch.ExitValues, m * batch.Exits, batch.Exits);
                anyTransport |= problem.Transport;
            }

            var run = _engine.Run(system.Tables, batch);
            var transport = anyTransport ? _engine.Run(system.Tables, TransportBatch.FromRocket(run)) : null;
            var stationCount = run.StationCount;
            for (var m = 0; m < members.Count; m++)
            {
                var (mixture, problem, propellant, ratio) = cases[members[m]];
                var stations = new Station[stationCount];
                for (var s = 0; s < stationCount; s++)
                {
                    var index = m * stationCount + s;
                    var wantTransport = problem.Transport && run.StationStatus[index] == CaseStatus.Ok;
                    var transportStatus = wantTransport ? transport!.Status[index] : (CaseStatus?)null;
                    var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[index] : (TransportFigures?)null;
                    var slice = new StationSlice
                    {
                        Table = table,
                        State = run.Stations[index],
                        Performance = run.Figures[index],
                        Moles = run.Moles,
                        Offset = (long)index * table.SpeciesCount,
                        Transport = figures,
                        TransportStatus = transportStatus,
                        Status = run.StationStatus[index],
                    };
                    stations[s] = StationFactory.Create(StationFactory.NameOf(s), slice);
                }

                results[members[m]] = new RocketResult(propellant, mixture, masses[members[m]], problem, ratio, speciesNames, stations, run.Status[m], run.Accelerator);
            }
        }

        return results;
    }

    /// <summary>One case per index over the union of the mixtures' elements; <paramref name="noun"/> names a rejected mixture ("mixture", "state record").</summary>
    private IReadOnlyList<EquilibriumResult> SolveEquilibrium(IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<EquilibriumProblem> problems, string noun)
    {
        var system = UnionSystem(mixtures, problems.Count, "equilibrium");
        return SolveEquilibrium(system, mixtures.Select((mixture, i) => new EquilibriumCase(mixture, problems[i], null)).ToList(), noun);
    }

    private IReadOnlyList<EquilibriumResult> SolveEquilibrium(ChemicalSystem system, IReadOnlyList<EquilibriumCase> cases, string noun = "mixture")
    {
        if (cases.Count == 0)
        {
            throw new ArgumentException("no equilibrium problems were given");
        }

        var table = system.Table;
        var batch = new EquilibriumBatch(cases.Count, table.ElementCount);
        var anyTransport = false;
        var masses = new double[cases.Count];
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant) = cases[k];
            ArgumentNullException.ThrowIfNull(problem);
            var target = ProblemValidation.Equilibrium(Database, mixture, problem, k);
            masses[k] = CheckMass(mixture, propellant, noun, k);
            batch.Kind[k] = problem.Kind;
            batch.Pressure[k] = problem.Pressure;
            batch.Temperature[k] = problem.Temperature;
            batch.Target[k] = target;
            Array.Copy(mixture.KilomolesPerKilogram(system.Elements), 0, batch.ElementMoles, k * table.ElementCount, table.ElementCount);
            anyTransport |= problem.Transport;
        }

        var run = _engine.Run(system.Tables, batch);
        var transport = anyTransport ? _engine.Run(system.Tables, TransportBatch.FromEquilibrium(run)) : null;
        var results = new EquilibriumResult[cases.Count];
        var speciesNames = StationFactory.SpeciesNames(table);
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant) = cases[k];
            var wantTransport = problem.Transport && run.Status[k] == CaseStatus.Ok;
            var transportStatus = wantTransport ? transport!.Status[k] : (CaseStatus?)null;
            var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[k] : (TransportFigures?)null;
            var slice = new StationSlice
            {
                Table = table,
                State = run.State[k],
                Moles = run.Moles,
                Offset = (long)k * table.SpeciesCount,
                Transport = figures,
                TransportStatus = transportStatus,
                Status = run.Status[k],
            };
            var state = StationFactory.Create("state", slice);
            results[k] = new EquilibriumResult(propellant, mixture, masses[k], problem, speciesNames, state, run.Status[k], run.Accelerator);
        }

        return results;
    }

    /// <summary>
    /// Element moles are per kilogram: their mass with the database's atomic weights must be one kilogram within the tolerance the mixture
    /// declares (BOOT.md), whichever front door it came through. A propellant fails this only when a reactant record's molar mass contradicts
    /// its formula. Returns the mass, which the result reports.
    /// </summary>
    private double CheckMass(ElementalMixture mixture, Propellant? propellant, string noun, int index) =>
        MixtureMass.Check(Database, mixture, propellant is null ? $"{noun} {index}" : $"the propellant's mixture (case {index})", index);

    /// <summary>J per kilogram of every reactant at its temperature: the record's polynomial through the engine, or the assigned enthalpy.</summary>
    private double[] ReactantEnthalpies(Propellant propellant)
    {
        if (_reactantEnthalpies.TryGetValue(propellant, out var cached))
        {
            return cached;
        }

        var resolved = propellant.Resolved;
        var perKilogram = new double[resolved.Count];
        var fitted = new List<int>();
        for (var k = 0; k < resolved.Count; k++)
        {
            var r = resolved[k];
            if (r.HasFits)
            {
                fitted.Add(k);
            }
            else
            {
                perKilogram[k] = r.AssignedEnthalpy * UnitFactors.MolesPerKilomole / r.MolarMass;
            }
        }

        if (fitted.Count > 0)
        {
            var names = fitted.Select(k => resolved[k].Record!.Name).Distinct(StringComparer.Ordinal).ToList();
            var elements = fitted.SelectMany(k => resolved[k].Formula.Select(pair => pair.Symbol)).Distinct(StringComparer.Ordinal).ToList();
            var table = SpeciesTable.Build(Database, elements, names);
            using var tables = _engine.Upload(table);
            var batch = new SpeciesFunctionBatch(fitted.Count);
            for (var i = 0; i < fitted.Count; i++)
            {
                var r = resolved[fitted[i]];
                batch.Species[i] = PieceAt(table, r.Record!.Name, r.Temperature);
                batch.Temperature[i] = r.Temperature;
            }

            var functions = _engine.Run(tables, batch);
            for (var i = 0; i < fitted.Count; i++)
            {
                var r = resolved[fitted[i]];
                perKilogram[fitted[i]] = functions.HOverRT[i] * PhysicalConstants.R * r.Temperature / r.MolarMass;
            }
        }

        _reactantEnthalpies[propellant] = perKilogram;
        return perKilogram;
    }

    private ChemicalSystem GetSystem(ElementalMixture mixture) => _systems.Get(mixture);

    /// <summary>The system over the union of the mixtures' elements, in order of first appearance, under the species lists they all share.</summary>
    private ChemicalSystem UnionSystem(IReadOnlyList<ElementalMixture> mixtures, int problemCount, string kind) => _systems.Union(mixtures, problemCount, kind);

    /// <summary>
    /// The table entry of a record name at a temperature: the first piece of a species cut at a fit discontinuity
    /// (the Thermo API's join-and-cut) whose last bound is not below it, else the last piece.
    /// </summary>
    private static int PieceAt(SpeciesTable table, string name, double temperature)
    {
        var indices = table.IndicesOf(name);
        for (var k = 0; k < indices.Count - 1; k++)
        {
            var last = table.Arrays.IntervalStart[indices[k]] + table.Arrays.IntervalCount[indices[k]] - 1;
            if (temperature <= table.Arrays.IntervalBounds[last * 2 + 1])
            {
                return indices[k];
            }
        }

        return indices[^1];
    }

    private static int IndexOf(IReadOnlyList<string> elements, string symbol)
    {
        for (var i = 0; i < elements.Count; i++)
        {
            if (string.Equals(elements[i], symbol, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"element {symbol} is not in the propellant's element list");
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed record RocketCase(ElementalMixture Mixture, RocketProblem Problem, Propellant? Propellant, double? Ratio);

    private sealed record EquilibriumCase(ElementalMixture Mixture, EquilibriumProblem Problem, Propellant? Propellant);
}
