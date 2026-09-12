using System.Text.Json;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>A JSON object read strictly: every field is known, every value has the expected type, and the path names the offender.</summary>
internal sealed class StrictObject
{
    private readonly JsonElement _element;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public StrictObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected an object at {path}, not {Describe(element)}");
        }

        _element = element;
        Path = path;
    }

    public string Path { get; }

    public bool Has(string name) => _element.TryGetProperty(name, out _);

    public double Number(string name) => AsNumber(Required(name), PathOf(name));

    public double? OptionalNumber(string name) => Take(name) is { } value ? AsNumber(value, PathOf(name)) : null;

    public string String(string name) => AsString(Required(name), PathOf(name));

    public string? OptionalString(string name) => Take(name) is { } value ? AsString(value, PathOf(name)) : null;

    public bool OptionalBool(string name, bool fallback)
    {
        if (Take(name) is not { } value)
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InputException($"expected true or false at {PathOf(name)}, not {Describe(value)}"),
        };
    }

    public StrictObject Object(string name) => new(Required(name), PathOf(name));

    public StrictObject? OptionalObject(string name) => Take(name) is { } value ? new StrictObject(value, PathOf(name)) : null;

    public IReadOnlyList<StrictObject> ObjectList(string name)
    {
        var value = Required(name);
        var path = PathOf(name);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InputException($"expected an array at {path}, not {Describe(value)}");
        }

        return value.EnumerateArray().Select((item, i) => new StrictObject(item, $"{path}[{i}]")).ToList();
    }

    public IReadOnlyList<double>? OptionalNumberList(string name) => Take(name) is { } value ? AsNumberList(value, PathOf(name)) : null;

    public IReadOnlyList<string>? OptionalStringList(string name)
    {
        if (Take(name) is not { } value)
        {
            return null;
        }

        var path = PathOf(name);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InputException($"expected an array of strings at {path}, not {Describe(value)}");
        }

        return value.EnumerateArray().Select((item, i) => AsString(item, $"{path}[{i}]")).ToList();
    }

    public IReadOnlyDictionary<string, double> NumberMap(string name) => AsNumberMap(Required(name), PathOf(name));

    public IReadOnlyDictionary<string, double>? OptionalNumberMap(string name) => Take(name) is { } value ? AsNumberMap(value, PathOf(name)) : null;

    /// <summary>A field the caller interprets itself (a list or a range); consumed here so that it is not reported unknown.</summary>
    public JsonElement? OptionalAny(string name) => Take(name);

    /// <summary>Every field of the object must have been read: a field nobody asked for is a misspelling or a unit nobody agreed on.</summary>
    public void Finish()
    {
        foreach (var property in _element.EnumerateObject())
        {
            if (!_seen.Contains(property.Name))
            {
                throw new InputException($"unknown field '{property.Name}' at {Path}");
            }
        }
    }

    public static double AsNumber(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new InputException($"expected a number at {path}, not {Describe(value)}");
        }

        var number = value.GetDouble();
        if (!double.IsFinite(number))
        {
            throw new InputException($"the number at {path} is out of range");
        }

        return number;
    }

    public static string AsString(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InputException($"expected a string at {path}, not {Describe(value)}");

    public static IReadOnlyList<double> AsNumberList(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InputException($"expected an array of numbers at {path}, not {Describe(value)}");
        }

        return value.EnumerateArray().Select((item, i) => AsNumber(item, $"{path}[{i}]")).ToList();
    }

    public static IReadOnlyDictionary<string, double> AsNumberMap(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected an object of numbers at {path}, not {Describe(value)}");
        }

        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!map.TryAdd(property.Name, AsNumber(property.Value, $"{path}.{property.Name}")))
            {
                throw new InputException($"the key '{property.Name}' at {path} is given twice");
            }
        }

        return map;
    }

    public static string Describe(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "nothing",
    };

    private JsonElement Required(string name) => Take(name) ?? throw new InputException($"missing field '{name}' at {Path}");

    private JsonElement? Take(string name)
    {
        if (_element.TryGetProperty(name, out var value))
        {
            _seen.Add(name);
            return value;
        }

        return null;
    }

    private string PathOf(string name) => $"{Path}.{name}";
}

internal sealed record ReactantDocument(
    string Name, ReactantRole Role, double Amount, AmountKind AmountKind, double? Temperature,
    IReadOnlyDictionary<string, double>? Formula, double? Enthalpy, double? MolarMass);

internal abstract record PropellantDocument(IReadOnlyList<string> Omit, IReadOnlyList<string>? Only);

internal sealed record ReactantPropellant(IReadOnlyList<ReactantDocument> Reactants, double? OxidizerToFuel, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

internal sealed record ElementalPropellant(IReadOnlyDictionary<string, double> ElementMoles, double? Enthalpy, IReadOnlyList<string> Omit, IReadOnlyList<string>? Only)
    : PropellantDocument(Omit, Only);

internal abstract record ProblemDocument;

internal sealed record RocketDocument(double ChamberPressure, FlowModel Flow, IReadOnlyList<double> AreaRatios, IReadOnlyList<double> PressureRatios,
                                      bool Transport, double TemperatureEstimate) : ProblemDocument;

internal sealed record EquilibriumDocument(ProblemKind Kind, double Pressure, double? Temperature, double? Enthalpy, double? Entropy, bool Transport) : ProblemDocument;

/// <summary>The lists of a sweep, expanded from lists or ranges; null where the document sweeps nothing.</summary>
internal sealed record SweepDocument(IReadOnlyList<double>? OxidizerToFuel, IReadOnlyList<double>? ChamberPressure, IReadOnlyList<double>? Pressure, IReadOnlyList<double>? Temperature);

internal sealed record InputDocument(PropellantDocument Propellant, ProblemDocument Problem, SweepDocument? Sweep, AcceleratorKind? Accelerator);

/// <summary>One record of the states command; <see cref="Record"/> is the record as given, echoed into the output.</summary>
internal sealed record StateDocument(
    int Index, string Source, JsonElement Record, double Pressure, IReadOnlyDictionary<string, double> Composition,
    double? Enthalpy, double? Temperature, double? Entropy, IReadOnlyList<double> AreaRatios, IReadOnlyList<double> PressureRatios, FlowModel Flow)
{
    public bool IsRocket => AreaRatios.Count + PressureRatios.Count > 0;
}

/// <summary>The readers of the input documents (API.md): strict, with the JSON path in every message.</summary>
internal static class InputDocuments
{
    public const string FlowShifting = "shifting-equilibrium";
    public const string FlowFrozenAtChamber = "frozen-at-chamber";
    public const string FlowFrozenAtThroat = "frozen-at-throat";

    public static InputDocument ReadProblem(string text, string source)
    {
        using var document = ParseJson(text, source);
        try
        {
            var root = new StrictObject(document.RootElement, "$");
            var propellant = ReadPropellant(root.Object("propellant"));
            var problem = ReadProblemPart(root.Object("problem"));
            var sweep = root.OptionalObject("sweep") is { } s ? ReadSweep(s, problem, propellant) : null;
            AcceleratorKind? accelerator = null;
            if (root.OptionalObject("engine") is { } engine)
            {
                accelerator = ParseAccelerator(engine.String("accelerator"), engine.Path + ".accelerator");
                engine.Finish();
            }

            root.Finish();
            return new InputDocument(propellant, problem, sweep, accelerator);
        }
        catch (InputException e)
        {
            throw new InputException($"{source}: {e.Message}");
        }
    }

    /// <summary>The records of one or more files: a JSON array, a single object, or JSON Lines.</summary>
    public static IReadOnlyList<StateDocument> ReadStates(IReadOnlyList<(string Source, string Text)> files)
    {
        var records = new List<StateDocument>();
        foreach (var (source, text) in files)
        {
            foreach (var (element, label) in RecordElements(text, source))
            {
                records.Add(ReadState(element, label, records.Count));
            }
        }

        if (records.Count == 0)
        {
            throw new InputException("no state record was given");
        }

        return records;
    }

    public static IReadOnlyList<double> ReadValues(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            var list = StrictObject.AsNumberList(value, path);
            if (list.Count == 0)
            {
                throw new InputException($"the list at {path} is empty");
            }

            return list;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InputException($"expected a list of numbers or a range {{from, to, step}} at {path}, not {StrictObject.Describe(value)}");
        }

        var range = new StrictObject(value, path);
        var from = range.Number("from");
        var to = range.Number("to");
        var step = range.Number("step");
        range.Finish();
        if (!(step > 0.0))
        {
            throw new InputException($"the step at {path} must be positive");
        }

        if (to < from)
        {
            throw new InputException($"the range at {path} ends before it starts");
        }

        var steps = (to - from) / step;
        var count = (int)Math.Round(steps);
        if (Math.Abs(steps - count) > 1e-9 * Math.Max(1.0, Math.Abs(steps)))
        {
            throw new InputException($"the range at {path} does not end on a step: ({to} - {from}) / {step} is not an integer");
        }

        var values = new double[count + 1];
        for (var k = 0; k <= count; k++)
        {
            values[k] = k == count ? to : from + k * step;
        }

        return values;
    }

    public static FlowModel ParseFlow(string value, string path) => value switch
    {
        FlowShifting => FlowModel.ShiftingEquilibrium,
        FlowFrozenAtChamber => FlowModel.FrozenAtChamber,
        FlowFrozenAtThroat => FlowModel.FrozenAtThroat,
        _ => throw new InputException($"unknown flow '{value}' at {path}; {FlowShifting}, {FlowFrozenAtChamber} or {FlowFrozenAtThroat}"),
    };

    public static AcceleratorKind ParseAccelerator(string value, string path) => value switch
    {
        "auto" => AcceleratorKind.Auto,
        "cpu" => AcceleratorKind.Cpu,
        "cuda" => AcceleratorKind.Cuda,
        _ => throw new InputException($"unknown accelerator '{value}' at {path}; auto, cpu or cuda"),
    };

    private static JsonDocument ParseJson(string text, string source)
    {
        try
        {
            return JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
        }
        catch (JsonException e)
        {
            throw new InputException($"{source}: malformed JSON: {e.Message}");
        }
    }

    private static PropellantDocument ReadPropellant(StrictObject propellant)
    {
        var omit = propellant.OptionalStringList("omit") ?? [];
        var only = propellant.OptionalStringList("only");
        if (only is { Count: 0 })
        {
            throw new InputException($"the field 'only' at {propellant.Path} is empty; leave it out to select the species from the elements");
        }

        if (propellant.Has("elementMoles"))
        {
            var moles = propellant.NumberMap("elementMoles");
            var enthalpy = propellant.OptionalNumber("enthalpy");
            propellant.Finish();
            return new ElementalPropellant(moles, enthalpy, omit, only);
        }

        var reactants = propellant.ObjectList("reactants").Select(ReadReactant).ToList();
        if (reactants.Count == 0)
        {
            throw new InputException($"the list at {propellant.Path}.reactants is empty");
        }

        double? ratio = null;
        if (propellant.OptionalObject("mixture") is { } mixture)
        {
            ratio = mixture.Number("oxidizerToFuel");
            mixture.Finish();
        }

        propellant.Finish();
        return new ReactantPropellant(reactants, ratio, omit, only);
    }

    private static ReactantDocument ReadReactant(StrictObject reactant)
    {
        var name = reactant.String("name");
        var role = reactant.String("role") switch
        {
            "oxidizer" => ReactantRole.Oxidizer,
            "fuel" => ReactantRole.Fuel,
            "named" => ReactantRole.Named,
            var other => throw new InputException($"unknown role '{other}' at {reactant.Path}.role; oxidizer, fuel or named"),
        };
        var amount = reactant.Number("amount");
        var amountKind = reactant.OptionalString("amountKind") switch
        {
            null or "mass-fraction" => AmountKind.MassFraction,
            "moles" => AmountKind.Moles,
            var other => throw new InputException($"unknown amountKind '{other}' at {reactant.Path}.amountKind; mass-fraction or moles"),
        };
        var temperature = reactant.OptionalNumber("temperature");
        var formula = reactant.OptionalNumberMap("formula");
        var enthalpy = reactant.OptionalNumber("enthalpy");
        var molarMass = reactant.OptionalNumber("molarMass");
        if (formula is not null)
        {
            if (enthalpy is null)
            {
                throw new InputException($"missing field 'enthalpy' at {reactant.Path}: a custom reactant needs its enthalpy (J/mol) at its temperature");
            }

            if (temperature is null)
            {
                throw new InputException($"missing field 'temperature' at {reactant.Path}: a custom reactant needs the temperature of its enthalpy");
            }
        }
        else if (enthalpy is not null || molarMass is not null)
        {
            throw new InputException($"'enthalpy' and 'molarMass' at {reactant.Path} belong to a custom reactant, which needs a 'formula'");
        }

        reactant.Finish();
        return new ReactantDocument(name, role, amount, amountKind, temperature, formula, enthalpy, molarMass);
    }

    private static ProblemDocument ReadProblemPart(StrictObject problem)
    {
        var type = problem.String("type");
        switch (type)
        {
            case "rocket":
            {
                var chamberPressure = problem.Number("chamberPressure");
                var flow = ParseFlow(problem.OptionalString("flow") ?? FlowShifting, problem.Path + ".flow");
                var areaRatios = problem.OptionalNumberList("areaRatios") ?? [];
                var pressureRatios = problem.OptionalNumberList("pressureRatios") ?? [];
                var transport = problem.OptionalBool("transport", false);
                var estimate = problem.OptionalNumber("temperatureEstimate") ?? 0.0;
                problem.Finish();
                return new RocketDocument(chamberPressure, flow, areaRatios, pressureRatios, transport, estimate);
            }

            case "equilibrium":
            {
                var kind = problem.String("kind") switch
                {
                    "tp" => ProblemKind.AssignedTemperaturePressure,
                    "hp" => ProblemKind.AssignedEnthalpyPressure,
                    "sp" => ProblemKind.AssignedEntropyPressure,
                    var other => throw new InputException($"unknown kind '{other}' at {problem.Path}.kind; tp, hp or sp"),
                };
                var pressure = problem.Number("pressure");
                var temperature = problem.OptionalNumber("temperature");
                var enthalpy = problem.OptionalNumber("enthalpy");
                var entropy = problem.OptionalNumber("entropy");
                var transport = problem.OptionalBool("transport", false);
                problem.Finish();
                switch (kind)
                {
                    case ProblemKind.AssignedTemperaturePressure:
                        Forbid(enthalpy, "enthalpy", "tp", problem.Path);
                        Forbid(entropy, "entropy", "tp", problem.Path);
                        if (temperature is null)
                        {
                            throw new InputException($"missing field 'temperature' at {problem.Path}: a tp problem assigns the temperature");
                        }

                        break;
                    case ProblemKind.AssignedEnthalpyPressure:
                        Forbid(entropy, "entropy", "hp", problem.Path);
                        break;
                    default:
                        Forbid(enthalpy, "enthalpy", "sp", problem.Path);
                        if (entropy is null)
                        {
                            throw new InputException($"missing field 'entropy' at {problem.Path}: an sp problem assigns the entropy");
                        }

                        break;
                }

                return new EquilibriumDocument(kind, pressure, temperature, enthalpy, entropy, transport);
            }

            default:
                throw new InputException($"unknown problem type '{type}' at {problem.Path}.type; rocket or equilibrium");
        }
    }

    private static void Forbid(double? value, string name, string kind, string path)
    {
        if (value is not null)
        {
            throw new InputException($"the field '{name}' at {path} does not belong to a {kind} problem");
        }
    }

    private static SweepDocument ReadSweep(StrictObject sweep, ProblemDocument problem, PropellantDocument propellant)
    {
        IReadOnlyList<double>? Values(string name) => sweep.OptionalAny(name) is { } value ? ReadValues(value, $"{sweep.Path}.{name}") : null;

        var ratios = Values("oxidizerToFuel");
        IReadOnlyList<double>? chamberPressures = null, pressures = null, temperatures = null;
        if (problem is RocketDocument)
        {
            chamberPressures = Values("chamberPressure");
        }
        else
        {
            pressures = Values("pressure");
            if (((EquilibriumDocument)problem).Kind == ProblemKind.AssignedTemperaturePressure)
            {
                temperatures = Values("temperature");
            }
        }

        sweep.Finish();
        if (ratios is not null && propellant is not ReactantPropellant { OxidizerToFuel: not null })
        {
            throw new InputException($"a sweep over oxidizerToFuel at {sweep.Path} needs a propellant given with mixture.oxidizerToFuel");
        }

        return new SweepDocument(ratios, chamberPressures, pressures, temperatures);
    }

    private static IEnumerable<(JsonElement Element, string Label)> RecordElements(string text, string source)
    {
        JsonDocument? whole = null;
        try
        {
            whole = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            // Not one JSON value: JSON Lines, one record per non-blank line.
        }

        if (whole is not null)
        {
            using (whole)
            {
                var root = whole.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    return root.EnumerateArray().Select((item, i) => (item.Clone(), $"{source}: record {i}")).ToList();
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    return [(root.Clone(), $"{source}: record 0")];
                }

                throw new InputException($"{source}: expected an array of records, one record or JSON Lines, not {StrictObject.Describe(root)}");
            }
        }

        var records = new List<(JsonElement, string)>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                records.Add((document.RootElement.Clone(), $"{source}:{i + 1}"));
            }
            catch (JsonException e)
            {
                throw new InputException($"{source}:{i + 1}: malformed JSON: {e.Message}");
            }
        }

        return records;
    }

    private static StateDocument ReadState(JsonElement element, string label, int index)
    {
        try
        {
            var record = new StrictObject(element, "$");
            var pressure = record.Number("pressure");
            var composition = record.NumberMap("composition");
            var enthalpy = record.OptionalNumber("enthalpy");
            var temperature = record.OptionalNumber("temperature");
            var entropy = record.OptionalNumber("entropy");
            var areaRatios = record.OptionalNumberList("areaRatios") ?? [];
            var pressureRatios = record.OptionalNumberList("pressureRatios") ?? [];
            var flowName = record.OptionalString("flow");
            record.Finish();
            var targets = (enthalpy is null ? 0 : 1) + (temperature is null ? 0 : 1) + (entropy is null ? 0 : 1);
            if (targets != 1)
            {
                throw new InputException($"exactly one of enthalpy, temperature and entropy must be given, not {targets}");
            }

            var isRocket = areaRatios.Count + pressureRatios.Count > 0;
            if (isRocket && enthalpy is null)
            {
                throw new InputException("a record with exits is a rocket case and needs 'enthalpy'");
            }

            if (flowName is not null && !isRocket)
            {
                throw new InputException("'flow' belongs to a record with exits");
            }

            var flow = ParseFlow(flowName ?? FlowShifting, "$.flow");
            return new StateDocument(index, label, element, pressure, composition, enthalpy, temperature, entropy, areaRatios, pressureRatios, flow);
        }
        catch (InputException e)
        {
            throw new InputException($"{label}: {e.Message}");
        }
    }
}
