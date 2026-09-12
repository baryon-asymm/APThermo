using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AerospacePropellantThermodynamics.Equilibrium;
using AerospacePropellantThermodynamics.Execution;
using AerospacePropellantThermodynamics.Performance;
using AerospacePropellantThermodynamics.Problems;
using AerospacePropellantThermodynamics.Thermo;
using AerospacePropellantThermodynamics.Transport;

namespace AerospacePropellantThermodynamics.Cli;

internal sealed record DatabaseInfo(string ThermoPath, string? TransPath, string ThermoSha256, string? TransSha256);

/// <summary>The run section of an output document.</summary>
internal sealed record RunInfo(string Command, IReadOnlyList<string> Inputs, DatabaseInfo? Database, AcceleratorInfo? Accelerator,
                               double DatabaseSeconds, double SolveSeconds, double Threshold);

/// <summary>One case of an output document: what it was given, and what the library returned.</summary>
internal sealed record CaseOutput(int Index, JsonNode Inputs, CaseStatus Status, ElementalMixture Mixture, IReadOnlyList<string> Species, IReadOnlyList<Station> Stations);

/// <summary>The public fields of the library's result structs, in declaration order, with their document names.</summary>
internal static class Fields
{
    public static readonly IReadOnlyList<(string Name, FieldInfo Field)> State = Of<MixtureState>();
    public static readonly IReadOnlyList<(string Name, FieldInfo Field)> Performance = Of<PerformanceFigures>();
    public static readonly IReadOnlyList<(string Name, FieldInfo Field)> Transport = Of<TransportFigures>();

    private static IReadOnlyList<(string, FieldInfo)> Of<T>() where T : struct =>
        typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => (Names.Camel(f.Name), f)).ToList();
}

/// <summary>Names in documents: camel case of the library's names.</summary>
internal static class Names
{
    public static string Camel(string name) => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    public static string Status(CaseStatus status) => Camel(status.ToString());

    public static string Accelerator(AcceleratorKind kind) => Camel(kind.ToString());

    public static string Kind(ProblemKind kind) => kind switch
    {
        ProblemKind.AssignedTemperaturePressure => "tp",
        ProblemKind.AssignedEnthalpyPressure => "hp",
        ProblemKind.AssignedEntropyPressure => "sp",
        _ => Camel(kind.ToString()),
    };
}

/// <summary>Writes the documents and decides the exit code.</summary>
internal static class Outputs
{
    /// <summary>g0, m/s²: the one conversion of this node, to specific impulse in seconds.</summary>
    public const double StandardGravity = 9.80665;

    public static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static ExitCode Write(RunInfo run, IReadOnlyList<CaseOutput> cases, CommandOptions options, TextWriter output)
    {
        var text = options.Format == OutputFormat.Csv ? CsvOutput.Render(cases, run.Threshold) : JsonOutput.Render(run, cases);
        Deliver(text, options.Output, output);
        var failed = cases.Any(c => c.Status != CaseStatus.Ok
                                    || c.Stations.Any(s => s.Status != CaseStatus.Ok || (s.TransportStatus is { } t && t != CaseStatus.Ok)));
        return failed ? ExitCode.CaseFailed : ExitCode.Ok;
    }

    public static void Deliver(string text, string? path, TextWriter output)
    {
        if (path is null)
        {
            output.Write(text);
            return;
        }

        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    public static string Render(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    public static void WriteNumber(Utf8JsonWriter writer, string name, double value)
    {
        if (double.IsFinite(value))
        {
            writer.WriteNumber(name, value);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    public static void WriteRun(Utf8JsonWriter writer, RunInfo run)
    {
        writer.WriteStartObject("run");
        writer.WriteString("tool", Program.ToolName);
        writer.WriteString("version", Program.Version);
        writer.WriteString("command", run.Command);
        writer.WriteStartArray("inputs");
        foreach (var input in run.Inputs)
        {
            writer.WriteStringValue(input);
        }

        writer.WriteEndArray();
        if (run.Database is { } database)
        {
            writer.WriteStartObject("database");
            writer.WriteString("thermoPath", database.ThermoPath);
            writer.WriteString("transPath", database.TransPath);
            writer.WriteString("thermoSha256", database.ThermoSha256);
            writer.WriteString("transSha256", database.TransSha256);
            writer.WriteEndObject();
        }

        if (run.Accelerator is { } accelerator)
        {
            writer.WritePropertyName("accelerator");
            WriteAccelerator(writer, accelerator);
        }

        writer.WriteStartObject("timings");
        writer.WriteNumber("database", run.DatabaseSeconds);
        writer.WriteNumber("solve", run.SolveSeconds);
        writer.WriteEndObject();
        writer.WriteNumber("threshold", run.Threshold);
        writer.WriteEndObject();
    }

    public static void WriteAccelerator(Utf8JsonWriter writer, AcceleratorInfo accelerator)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", Names.Accelerator(accelerator.Kind));
        writer.WriteString("deviceName", accelerator.DeviceName);
        writer.WriteString("ilgpuVersion", accelerator.IlgpuVersion);
        writer.WriteString("libNvvmPath", accelerator.LibNvvmPath);
        writer.WriteString("libDevicePath", accelerator.LibDevicePath);
        writer.WriteNumber("threadsOrMultiprocessors", accelerator.ThreadsOrMultiprocessors);
        writer.WriteEndObject();
    }
}

/// <summary>The JSON output document (API.md).</summary>
internal static class JsonOutput
{
    public static string Render(RunInfo run, IReadOnlyList<CaseOutput> cases) => Outputs.Render(writer =>
    {
        writer.WriteStartObject();
        Outputs.WriteRun(writer, run);
        writer.WriteStartArray("cases");
        foreach (var c in cases)
        {
            WriteCase(writer, c, run.Threshold);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static void WriteCase(Utf8JsonWriter writer, CaseOutput c, double threshold)
    {
        writer.WriteStartObject();
        writer.WriteNumber("index", c.Index);
        writer.WritePropertyName("inputs");
        c.Inputs.WriteTo(writer);
        writer.WriteString("status", Names.Status(c.Status));
        writer.WriteStartObject("mixture");
        writer.WriteStartObject("elementMoles");
        foreach (var element in c.Mixture.Elements)
        {
            Outputs.WriteNumber(writer, element, c.Mixture.ElementMoles[element]);
        }

        writer.WriteEndObject();
        if (c.Mixture.Enthalpy is { } enthalpy)
        {
            Outputs.WriteNumber(writer, "enthalpy", enthalpy);
        }
        else
        {
            writer.WriteNull("enthalpy");
        }

        writer.WriteEndObject();
        writer.WriteStartArray("stations");
        foreach (var station in c.Stations)
        {
            WriteStation(writer, station, c.Species, threshold);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteStation(Utf8JsonWriter writer, Station station, IReadOnlyList<string> species, double threshold)
    {
        writer.WriteStartObject();
        writer.WriteString("name", station.Name);
        writer.WriteString("status", Names.Status(station.Status));
        foreach (var (name, field) in Fields.State)
        {
            Outputs.WriteNumber(writer, name, (double)field.GetValue(station.State)!);
        }

        if (station.Performance is { } figures)
        {
            writer.WriteStartObject("performance");
            foreach (var (name, field) in Fields.Performance)
            {
                Outputs.WriteNumber(writer, name, (double)field.GetValue(figures)!);
            }

            Outputs.WriteNumber(writer, "specificImpulseSeconds", figures.SpecificImpulse / Outputs.StandardGravity);
            Outputs.WriteNumber(writer, "vacuumSpecificImpulseSeconds", figures.VacuumSpecificImpulse / Outputs.StandardGravity);
            writer.WriteEndObject();
        }

        writer.WriteStartObject("moleFractions");
        foreach (var name in species)
        {
            if (station.MoleFractions.TryGetValue(name, out var fraction) && fraction >= threshold)
            {
                Outputs.WriteNumber(writer, name, fraction);
            }
        }

        writer.WriteEndObject();
        writer.WriteStartObject("condensedMassFractions");
        foreach (var name in species)
        {
            if (station.CondensedMassFractions.TryGetValue(name, out var mass) && station.MoleFractions.TryGetValue(name, out var fraction) && fraction >= threshold)
            {
                Outputs.WriteNumber(writer, name, mass);
            }
        }

        writer.WriteEndObject();
        if (station.TransportStatus is { } transportStatus)
        {
            writer.WriteStartObject("transport");
            writer.WriteString("status", Names.Status(transportStatus));
            if (station.Transport is { } transport)
            {
                foreach (var (name, field) in Fields.Transport)
                {
                    var value = field.GetValue(transport)!;
                    if (value is double number)
                    {
                        Outputs.WriteNumber(writer, name, number);
                    }
                    else
                    {
                        writer.WriteNumber(name, (int)value);
                    }
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}

/// <summary>The CSV form: one row per case and station, the scalar inputs, the state, the figures and the transport figures; no compositions.</summary>
internal static class CsvOutput
{
    public static string Render(IReadOnlyList<CaseOutput> cases, double threshold)
    {
        _ = threshold;   // compositions are not in the CSV form
        var inputColumns = new List<string>();
        foreach (var c in cases)
        {
            foreach (var (name, _) in ScalarInputs(c.Inputs))
            {
                if (!inputColumns.Contains(name, StringComparer.Ordinal))
                {
                    inputColumns.Add(name);
                }
            }
        }

        var header = new List<string> { "case" };
        header.AddRange(inputColumns);
        header.AddRange(["station", "status"]);
        header.AddRange(Fields.State.Select(f => f.Name));
        header.AddRange(Fields.Performance.Select(f => f.Name));
        header.AddRange(["specificImpulseSeconds", "vacuumSpecificImpulseSeconds", "transportStatus"]);
        header.AddRange(Fields.Transport.Select(f => f.Name));
        var text = new StringBuilder();
        text.Append(string.Join(",", header.Select(Escape))).Append('\n');
        foreach (var c in cases)
        {
            var inputs = ScalarInputs(c.Inputs).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);
            foreach (var station in c.Stations)
            {
                var cells = new List<string> { c.Index.ToString(CultureInfo.InvariantCulture) };
                cells.AddRange(inputColumns.Select(name => inputs.TryGetValue(name, out var value) ? value : ""));
                cells.Add(station.Name);
                cells.Add(Names.Status(station.Status));
                cells.AddRange(Fields.State.Select(f => Number((double)f.Field.GetValue(station.State)!)));
                if (station.Performance is { } figures)
                {
                    cells.AddRange(Fields.Performance.Select(f => Number((double)f.Field.GetValue(figures)!)));
                    cells.Add(Number(figures.SpecificImpulse / Outputs.StandardGravity));
                    cells.Add(Number(figures.VacuumSpecificImpulse / Outputs.StandardGravity));
                }
                else
                {
                    cells.AddRange(Enumerable.Repeat("", Fields.Performance.Count + 2));
                }

                cells.Add(station.TransportStatus is { } transportStatus ? Names.Status(transportStatus) : "");
                if (station.Transport is { } transport)
                {
                    cells.AddRange(Fields.Transport.Select(f => f.Field.GetValue(transport) is double d ? Number(d) : ((int)f.Field.GetValue(transport)!).ToString(CultureInfo.InvariantCulture)));
                }
                else
                {
                    cells.AddRange(Enumerable.Repeat("", Fields.Transport.Count));
                }

                text.Append(string.Join(",", cells.Select(Escape))).Append('\n');
            }
        }

        return text.ToString();
    }

    private static IEnumerable<(string Name, string Value)> ScalarInputs(JsonNode inputs)
    {
        if (inputs is not JsonObject obj)
        {
            yield break;
        }

        foreach (var (name, node) in obj)
        {
            if (node is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<double>(out var number))
            {
                yield return (name, Number(number));
            }
            else if (value.TryGetValue<string>(out var text))
            {
                yield return (name, text);
            }
            else if (value.TryGetValue<bool>(out var flag))
            {
                yield return (name, flag ? "true" : "false");
            }
        }
    }

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Escape(string cell) =>
        cell.Contains(',') || cell.Contains('"') || cell.Contains('\n') ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
}
