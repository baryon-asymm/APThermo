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
    private readonly Dictionary<string, ChemicalSystem> _systems = new(StringComparer.Ordinal);
    private readonly Dictionary<Propellant, double[]> _reactantEnthalpies = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    private Solver(SpeciesDatabase database, Engine engine)
    {
        Database = database;
        _engine = engine;
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
            byName[elements[i]] = moles[i] * 1.0e3;
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
        var mass = 0.0;
        foreach (var (symbol, moles) in mixture.ElementMoles)
        {
            try
            {
                mass += moles * 1.0e-3 * Database.AtomicWeight(symbol);
            }
            catch (KeyNotFoundException inner)
            {
                throw new ArgumentException($"element '{symbol}' has no record in the database", inner);
            }
        }

        return mass;
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

        var mixtures = new List<ElementalMixture>(states.Count);
        var problems = new List<EquilibriumProblem>(states.Count);
        for (var i = 0; i < states.Count; i++)
        {
            var record = states[i] ?? throw new ArgumentException($"state record {i} is null", nameof(states));
            var targets = (record.Enthalpy is not null ? 1 : 0) + (record.Temperature is not null ? 1 : 0) + (record.Entropy is not null ? 1 : 0);
            if (targets != 1)
            {
                throw new ArgumentException($"state record {i}: exactly one of enthalpy, temperature and entropy must be given, not {targets}", nameof(states));
            }

            try
            {
                mixtures.Add(ElementalMixture.Create(record.Composition, record.Enthalpy, options.Omit, options.Only, options.MassTolerance));
            }
            catch (ArgumentException inner)
            {
                throw new ArgumentException($"state record {i}: {inner.Message}", nameof(states), inner);
            }

            problems.Add(new EquilibriumProblem
            {
                Kind = record.Temperature is not null ? ProblemKind.AssignedTemperaturePressure
                     : record.Entropy is not null ? ProblemKind.AssignedEntropyPressure
                     : ProblemKind.AssignedEnthalpyPressure,
                Pressure = record.Pressure,
                Temperature = record.Temperature ?? 0.0,
                Enthalpy = record.Enthalpy,
                Entropy = record.Entropy ?? 0.0,
                Transport = options.Transport,
            });
        }

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
        foreach (var system in _systems.Values)
        {
            system.Dispose();
        }

        _systems.Clear();
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
            ValidateRocket(mixture, problem, k);
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
        var speciesNames = ResultSpecies(table);
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
                    var name = s == 0 ? "chamber" : s == 1 ? "throat" : $"exit{s - 1}";
                    var wantTransport = problem.Transport && run.StationStatus[index] == CaseStatus.Ok;
                    var transportStatus = wantTransport ? transport!.Status[index] : (CaseStatus?)null;
                    var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[index] : (TransportFigures?)null;
                    stations[s] = MakeStation(name, table, run.Stations[index], run.Figures[index], run.Moles, (long)index * table.SpeciesCount,
                                              figures, transportStatus, run.StationStatus[index]);
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
            var target = ValidateEquilibrium(mixture, problem, k);
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
        var speciesNames = ResultSpecies(table);
        for (var k = 0; k < cases.Count; k++)
        {
            var (mixture, problem, propellant) = cases[k];
            var wantTransport = problem.Transport && run.Status[k] == CaseStatus.Ok;
            var transportStatus = wantTransport ? transport!.Status[k] : (CaseStatus?)null;
            var figures = transportStatus == CaseStatus.Ok ? transport!.Figures[k] : (TransportFigures?)null;
            var state = MakeStation("state", table, run.State[k], null, run.Moles, (long)k * table.SpeciesCount, figures, transportStatus, run.Status[k]);
            results[k] = new EquilibriumResult(propellant, mixture, masses[k], problem, speciesNames, state, run.Status[k], run.Accelerator);
        }

        return results;
    }

    private void ValidateRocket(ElementalMixture mixture, RocketProblem problem, int index)
    {
        if (mixture.Enthalpy is null)
        {
            throw new ArgumentException($"rocket problem {index}: the mixture has no enthalpy; a rocket problem needs the reactant enthalpy");
        }

        if (!(problem.ChamberPressure > 0.0) || double.IsInfinity(problem.ChamberPressure))
        {
            throw new ArgumentException($"rocket problem {index}: the chamber pressure must be positive and finite, not {problem.ChamberPressure}");
        }

        if (problem.TemperatureEstimate < 0.0 || double.IsNaN(problem.TemperatureEstimate))
        {
            throw new ArgumentException($"rocket problem {index}: the temperature estimate must not be negative");
        }

        foreach (var value in problem.PressureRatios.Concat(problem.AreaRatios))
        {
            if (!(value > 0.0) || double.IsInfinity(value))
            {
                throw new ArgumentException($"rocket problem {index}: an exit value must be positive and finite, not {value}");
            }
        }

        if (problem.Transport && Database.Transport is null)
        {
            throw new ArgumentException($"rocket problem {index}: transport properties were requested, but the database was loaded without a trans.inp file");
        }
    }

    /// <summary>
    /// Element moles are per kilogram: their mass with the database's atomic weights must be one kilogram within the tolerance the mixture
    /// declares (BOOT.md), whichever front door it came through. A propellant fails this only when a reactant record's molar mass contradicts
    /// its formula. Returns the mass, which the result reports.
    /// </summary>
    private double CheckMass(ElementalMixture mixture, Propellant? propellant, string noun, int index)
    {
        var mass = MassOf(mixture);
        if (Math.Abs(mass - 1.0) > mixture.MassTolerance)
        {
            throw new MixtureMassException(propellant is null ? $"{noun} {index}" : $"the propellant's mixture (case {index})", index, mass, mixture.MassTolerance);
        }

        return mass;
    }

    private double ValidateEquilibrium(ElementalMixture mixture, EquilibriumProblem problem, int index)
    {
        if (!(problem.Pressure > 0.0) || double.IsInfinity(problem.Pressure))
        {
            throw new ArgumentException($"equilibrium problem {index}: the pressure must be positive and finite, not {problem.Pressure}");
        }

        if (problem.Temperature < 0.0 || double.IsNaN(problem.Temperature))
        {
            throw new ArgumentException($"equilibrium problem {index}: the temperature must not be negative");
        }

        if (problem.Transport && Database.Transport is null)
        {
            throw new ArgumentException($"equilibrium problem {index}: transport properties were requested, but the database was loaded without a trans.inp file");
        }

        switch (problem.Kind)
        {
            case ProblemKind.AssignedTemperaturePressure:
                if (!(problem.Temperature > 0.0))
                {
                    throw new ArgumentException($"equilibrium problem {index}: an assigned-temperature problem needs a positive temperature");
                }

                return 0.0;
            case ProblemKind.AssignedEnthalpyPressure:
                var enthalpy = problem.Enthalpy ?? mixture.Enthalpy
                               ?? throw new ArgumentException($"equilibrium problem {index}: neither the problem nor the mixture gives an enthalpy");
                if (!double.IsFinite(enthalpy))
                {
                    throw new ArgumentException($"equilibrium problem {index}: the enthalpy must be finite");
                }

                return enthalpy;
            case ProblemKind.AssignedEntropyPressure:
                if (!double.IsFinite(problem.Entropy))
                {
                    throw new ArgumentException($"equilibrium problem {index}: the entropy must be finite");
                }

                return problem.Entropy;
            default:
                throw new ArgumentException($"equilibrium problem {index}: unknown problem kind {problem.Kind}");
        }
    }

    private static Station MakeStation(string name, SpeciesTable table, MixtureState state, PerformanceFigures? figures, double[] moles, long offset,
                                       TransportFigures? transport, CaseStatus? transportStatus, CaseStatus status)
    {
        var speciesCount = table.SpeciesCount;
        var total = 0.0;
        for (var j = 0; j < speciesCount; j++)
        {
            total += moles[offset + j];
        }

        var fractions = new Dictionary<string, double>(speciesCount, StringComparer.Ordinal);
        var condensed = new Dictionary<string, double>(table.CondensedCount, StringComparer.Ordinal);
        for (var j = 0; j < speciesCount; j++)
        {
            // A condensed record cut at a fit discontinuity reports the record's name, its pieces summed (BOOT.md, results).
            var species = table.Records[j].Name;
            var n = moles[offset + j];
            var fraction = total > 0.0 ? n / total : 0.0;
            fractions[species] = fractions.TryGetValue(species, out var f) ? f + fraction : fraction;
            if (j >= table.GasCount)
            {
                var massFraction = n * table.Arrays.MolarMass[j];
                condensed[species] = condensed.TryGetValue(species, out var w) ? w + massFraction : massFraction;
            }
        }

        return new Station(name, state, figures, fractions, condensed, transport, transportStatus, status);
    }

    /// <summary>The species names a result reports: the table's, with the pieces of a cut condensed record collapsed to the record's name (BOOT.md, results).</summary>
    private static IReadOnlyList<string> ResultSpecies(SpeciesTable table)
    {
        var names = new List<string>(table.SpeciesCount);
        for (var i = 0; i < table.SpeciesCount; i++)
        {
            var name = table.Records[i].Name;
            if (names.Count == 0 || !string.Equals(names[^1], name, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

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
                perKilogram[k] = r.AssignedEnthalpy * 1.0e3 / r.MolarMass;
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

    private ChemicalSystem GetSystem(ElementalMixture mixture) => GetSystem(mixture.Elements, mixture.Omit, mixture.Only);

    /// <summary>The system over the union of the mixtures' elements, in order of first appearance, under the species lists they all share.</summary>
    private ChemicalSystem UnionSystem(IReadOnlyList<ElementalMixture> mixtures, int problemCount, string kind)
    {
        if (mixtures.Count != problemCount)
        {
            throw new ArgumentException($"{mixtures.Count} mixtures were given for {problemCount} {kind} problems; a batch over mixtures takes one mixture per problem");
        }

        if (mixtures.Count == 0)
        {
            throw new ArgumentException($"no {kind} problems were given");
        }

        var first = mixtures[0] ?? throw new ArgumentException("mixture 0 is null");
        var elements = new List<string>();
        for (var i = 0; i < mixtures.Count; i++)
        {
            var mixture = mixtures[i] ?? throw new ArgumentException($"mixture {i} is null");
            if (!SameNames(mixture.Omit, first.Omit) || !SameNames(mixture.Only, first.Only))
            {
                throw new ArgumentException($"mixture {i}: its Omit or Only list differs from mixture 0's; a batch has one species selection");
            }

            foreach (var symbol in mixture.Elements)
            {
                if (!elements.Contains(symbol, StringComparer.Ordinal))
                {
                    elements.Add(symbol);
                }
            }
        }

        return GetSystem(elements, first.Omit, first.Only);
    }

    private static bool SameNames(IReadOnlyList<string>? a, IReadOnlyList<string>? b) =>
        a is null ? b is null : b is not null && a.Count == b.Count && a.ToHashSet(StringComparer.Ordinal).SetEquals(b);

    private ChemicalSystem GetSystem(IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only)
    {
        ThrowIfDisposed();
        foreach (var element in elements)
        {
            try
            {
                Database.AtomicWeight(element);
            }
            catch (KeyNotFoundException inner)
            {
                throw new ArgumentException($"element '{element}' has no record in the database", inner);
            }
        }

        var key = string.Join(",", elements) + "|" + string.Join(",", omit.Order(StringComparer.Ordinal)) + "|" + (only is null ? "*" : string.Join(",", only));
        if (_systems.TryGetValue(key, out var system))
        {
            return system;
        }

        var candidates = SpeciesSelection.Candidates(Database, elements, omit, only);
        if (candidates.Count == 0)
        {
            throw new ArgumentException($"no product species of the database consists of the elements {string.Join(", ", elements)} alone");
        }

        var table = SpeciesTable.Build(Database, elements.ToList(), candidates);
        var transport = Database.Transport is null ? null : TransportTable.Build(Database.Transport, table);
        system = new ChemicalSystem(elements.ToList(), table, transport, _engine.Upload(table, transport));
        _systems[key] = system;
        return system;
    }

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

    /// <summary>A table over one element set with its species selection, uploaded to the engine once.</summary>
    private sealed class ChemicalSystem(IReadOnlyList<string> elements, SpeciesTable table, TransportTable? transport, UploadedTables tables) : IDisposable
    {
        public IReadOnlyList<string> Elements { get; } = elements;

        public SpeciesTable Table { get; } = table;

        public TransportTable? Transport { get; } = transport;

        public UploadedTables Tables { get; } = tables;

        public void Dispose() => Tables.Dispose();
    }
}
