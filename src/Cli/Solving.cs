using System.Diagnostics;
using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Problems;
using ProblemKind = AerospacePropellantThermodynamics.Equilibrium.ProblemKind;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>Dispatch of the commands.</summary>
internal static class Commands
{
    public static ExitCode Execute(Invocation invocation, TextWriter output) => invocation.Command switch
    {
        "rocket" or "equilibrium" => Solving.SolveDocument(invocation, output),
        "states" => Solving.SolveStates(invocation, output),
        "species" => Listings.Species(invocation, output),
        "devices" => Listings.Devices(invocation, output),
        var other => throw new InputException($"unknown command '{other}'"),
    };

    public static string ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new InputException($"input file not found: {path}");
        }

        return File.ReadAllText(path);
    }
}

/// <summary>One combination of the sweep; null where the document's own value stands.</summary>
internal sealed record Combination(double? OxidizerToFuel, double? ChamberPressure, double? Pressure, double? Temperature);

/// <summary>The Cartesian product of a sweep, ratio-major, then pressure, then temperature.</summary>
internal static class Sweeps
{
    public static IReadOnlyList<Combination> Expand(SweepDocument? sweep)
    {
        var ratios = sweep?.OxidizerToFuel?.Select(v => (double?)v).ToList() ?? [null];
        var chamberPressures = sweep?.ChamberPressure?.Select(v => (double?)v).ToList() ?? [null];
        var pressures = sweep?.Pressure?.Select(v => (double?)v).ToList() ?? [null];
        var temperatures = sweep?.Temperature?.Select(v => (double?)v).ToList() ?? [null];
        var combinations = new List<Combination>();
        foreach (var ratio in ratios)
        {
            foreach (var chamberPressure in chamberPressures)
            {
                foreach (var pressure in pressures)
                {
                    foreach (var temperature in temperatures)
                    {
                        combinations.Add(new Combination(ratio, chamberPressure, pressure, temperature));
                    }
                }
            }
        }

        return combinations;
    }
}

/// <summary>Propellant documents into the library's definitions.</summary>
internal static class Propellants
{
    public static Propellant Build(SpeciesDatabase database, ReactantPropellant document)
    {
        var builder = Propellant.From(database);
        foreach (var r in document.Reactants)
        {
            if (r.Formula is not null)
            {
                var formula = r.Formula.Select(pair => new ElementCount(pair.Key, pair.Value)).ToList();
                builder.Custom(Reactant.Custom(r.Name, formula, r.Enthalpy!.Value, r.Temperature!.Value, r.Role, r.Amount, r.MolarMass, r.AmountKind));
            }
            else
            {
                builder.Add(Reactant.FromDatabase(r.Name, r.Role, r.Amount, r.Temperature, r.AmountKind));
            }
        }

        if (document.OxidizerToFuel is { } ratio)
        {
            builder.OxidizerToFuelRatio(ratio);
        }

        if (document.Omit.Count > 0)
        {
            builder.Omit([.. document.Omit]);
        }

        if (document.Only is not null)
        {
            builder.Only([.. document.Only]);
        }

        return builder.Build();
    }

    public static ElementalMixture Build(ElementalPropellant document) =>
        ElementalMixture.Create(document.ElementMoles, document.Enthalpy, document.Omit, document.Only);
}

/// <summary>The solving commands: document in, cases out.</summary>
internal static class Solving
{
    public static ExitCode SolveDocument(Invocation invocation, TextWriter output)
    {
        var path = invocation.Arguments[0];
        var document = InputDocuments.ReadProblem(Commands.ReadFile(path), path);
        var isRocket = document.Problem is RocketDocument;
        if (isRocket != (invocation.Command == "rocket"))
        {
            var type = isRocket ? "rocket" : "equilibrium";
            throw new InputException($"{path}: the problem type is '{type}'; run apthermo {type}");
        }

        var options = invocation.Options;
        var (database, info, databaseSeconds) = DatabaseFiles.Load(options.Database);
        var accelerator = options.Accelerator ?? document.Accelerator ?? AcceleratorKind.Auto;
        var watch = Stopwatch.StartNew();
        using var solver = Solver.Create(database, new EngineOptions { Accelerator = accelerator });
        var combinations = Sweeps.Expand(document.Sweep);
        double? ownRatio = null;
        IReadOnlyList<ElementalMixture> mixtures;
        if (document.Propellant is ReactantPropellant reactants)
        {
            var propellant = Propellants.Build(database, reactants);
            ownRatio = propellant.OxidizerToFuelRatio;
            mixtures = combinations.Select(c => solver.Mixture(propellant, c.OxidizerToFuel)).ToList();
        }
        else
        {
            var mixture = Propellants.Build((ElementalPropellant)document.Propellant);
            mixtures = combinations.Select(_ => mixture).ToList();
        }

        var cases = document.Problem switch
        {
            RocketDocument rocket => Rocket(solver, mixtures, combinations, rocket, ownRatio),
            EquilibriumDocument equilibrium => Equilibrium(solver, mixtures, combinations, equilibrium, ownRatio),
            var other => throw new InvalidOperationException($"unknown problem document {other.GetType().Name}"),
        };
        watch.Stop();
        var run = new RunInfo(invocation.Command, [path], info, solver.Accelerator, databaseSeconds, watch.Elapsed.TotalSeconds, options.Threshold);
        return Outputs.Write(run, cases, options, output);
    }

    public static ExitCode SolveStates(Invocation invocation, TextWriter output)
    {
        var files = invocation.Arguments.Select(path => (path, Commands.ReadFile(path))).ToList();
        var records = InputDocuments.ReadStates(files);
        var options = invocation.Options;
        var (database, info, databaseSeconds) = DatabaseFiles.Load(options.Database);
        var watch = Stopwatch.StartNew();
        using var solver = Solver.Create(database, new EngineOptions { Accelerator = options.Accelerator ?? AcceleratorKind.Auto });
        var equilibrium = records.Where(r => !r.IsRocket).ToList();
        var rockets = records.Where(r => r.IsRocket).ToList();
        var cases = new CaseOutput[records.Count];
        if (equilibrium.Count > 0)
        {
            var mixtures = equilibrium.Select(r => MixtureOf(r)).ToList();
            var problems = equilibrium.Select(r => new EquilibriumProblem
            {
                Kind = r.Temperature is not null ? ProblemKind.AssignedTemperaturePressure
                     : r.Entropy is not null ? ProblemKind.AssignedEntropyPressure
                     : ProblemKind.AssignedEnthalpyPressure,
                Pressure = r.Pressure,
                Temperature = r.Temperature ?? 0.0,
                Enthalpy = r.Enthalpy,
                Entropy = r.Entropy ?? 0.0,
                Transport = options.Transport,
            }).ToList();
            var results = solver.Solve(mixtures, problems);
            for (var k = 0; k < equilibrium.Count; k++)
            {
                var record = equilibrium[k];
                var result = results[k];
                cases[record.Index] = new CaseOutput(record.Index, JsonNode.Parse(record.Record.GetRawText())!, result.Status, result.Mixture, result.Species, [result.State]);
            }
        }

        if (rockets.Count > 0)
        {
            var mixtures = rockets.Select(r => MixtureOf(r)).ToList();
            var problems = rockets.Select(r => new RocketProblem
            {
                ChamberPressure = r.Pressure,
                Flow = r.Flow,
                AreaRatios = r.AreaRatios,
                PressureRatios = r.PressureRatios,
                Transport = options.Transport,
            }).ToList();
            var results = solver.Solve(mixtures, problems);
            for (var k = 0; k < rockets.Count; k++)
            {
                var record = rockets[k];
                var result = results[k];
                cases[record.Index] = new CaseOutput(record.Index, JsonNode.Parse(record.Record.GetRawText())!, result.Status, result.Mixture, result.Species, result.Stations);
            }
        }

        watch.Stop();
        var run = new RunInfo("states", invocation.Arguments, info, solver.Accelerator, databaseSeconds, watch.Elapsed.TotalSeconds, options.Threshold);
        return Outputs.Write(run, cases, options, output);
    }

    private static ElementalMixture MixtureOf(StateDocument record)
    {
        try
        {
            return ElementalMixture.Create(record.Composition, record.Enthalpy);
        }
        catch (ArgumentException e)
        {
            throw new InputException($"{record.Source}: {e.Message}");
        }
    }

    private static IReadOnlyList<CaseOutput> Rocket(Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
                                                    RocketDocument document, double? ownRatio)
    {
        var problems = combinations.Select(c => new RocketProblem
        {
            ChamberPressure = c.ChamberPressure ?? document.ChamberPressure,
            Flow = document.Flow,
            AreaRatios = document.AreaRatios,
            PressureRatios = document.PressureRatios,
            Transport = document.Transport,
            TemperatureEstimate = document.TemperatureEstimate,
        }).ToList();
        var results = solver.Solve(mixtures, problems);
        var cases = new List<CaseOutput>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var inputs = new JsonObject();
            if ((combinations[i].OxidizerToFuel ?? ownRatio) is { } ratio)
            {
                inputs["oxidizerToFuel"] = ratio;
            }

            inputs["chamberPressure"] = problems[i].ChamberPressure;
            cases.Add(new CaseOutput(i, inputs, results[i].Status, results[i].Mixture, results[i].Species, results[i].Stations));
        }

        return cases;
    }

    private static IReadOnlyList<CaseOutput> Equilibrium(Solver solver, IReadOnlyList<ElementalMixture> mixtures, IReadOnlyList<Combination> combinations,
                                                         EquilibriumDocument document, double? ownRatio)
    {
        var problems = combinations.Select(c => new EquilibriumProblem
        {
            Kind = document.Kind,
            Pressure = c.Pressure ?? document.Pressure,
            Temperature = c.Temperature ?? document.Temperature ?? 0.0,
            Enthalpy = document.Enthalpy,
            Entropy = document.Entropy ?? 0.0,
            Transport = document.Transport,
        }).ToList();
        var results = solver.Solve(mixtures, problems);
        var cases = new List<CaseOutput>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            var inputs = new JsonObject();
            if ((combinations[i].OxidizerToFuel ?? ownRatio) is { } ratio)
            {
                inputs["oxidizerToFuel"] = ratio;
            }

            inputs["kind"] = Names.Kind(document.Kind);
            inputs["pressure"] = problems[i].Pressure;
            switch (document.Kind)
            {
                case ProblemKind.AssignedTemperaturePressure:
                    inputs["temperature"] = problems[i].Temperature;
                    break;
                case ProblemKind.AssignedEnthalpyPressure:
                    if ((problems[i].Enthalpy ?? mixtures[i].Enthalpy) is { } enthalpy)
                    {
                        inputs["enthalpy"] = enthalpy;
                    }

                    break;
                default:
                    inputs["entropy"] = problems[i].Entropy;
                    break;
            }

            cases.Add(new CaseOutput(i, inputs, results[i].Status, results[i].Mixture, results[i].Species, [results[i].State]));
        }

        return cases;
    }
}

/// <summary>Finding and loading the database files.</summary>
internal static class DatabaseFiles
{
    public const string ThermoFile = "thermo.inp";
    public const string TransFile = "trans.inp";

    public static (SpeciesDatabase Database, DatabaseInfo Info, double Seconds) Load(string? directory)
    {
        var resolved = Resolve(directory);
        var thermo = Path.Combine(resolved, ThermoFile);
        var trans = Path.Combine(resolved, TransFile);
        string? transPath = File.Exists(trans) ? trans : null;
        var watch = Stopwatch.StartNew();
        var database = SpeciesDatabase.Load(thermo, transPath);
        watch.Stop();
        var info = new DatabaseInfo(thermo, transPath, database.Provenance.ThermoSha256, database.Provenance.TransSha256);
        return (database, info, watch.Elapsed.TotalSeconds);
    }

    /// <summary>The given directory, else data/ next to the executable, then data/ under the current directory, then the current directory.</summary>
    public static string Resolve(string? directory)
    {
        if (directory is not null)
        {
            if (!File.Exists(Path.Combine(directory, ThermoFile)))
            {
                throw new InputException($"no {ThermoFile} in the database directory '{directory}'");
            }

            return directory;
        }

        var current = Directory.GetCurrentDirectory();
        string[] candidates = [Path.Combine(AppContext.BaseDirectory, "data"), Path.Combine(current, "data"), current];
        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, ThermoFile)))
            {
                return candidate;
            }
        }

        throw new InputException($"no {ThermoFile} found; looked in {string.Join(", ", candidates)}; give --database DIR");
    }
}
