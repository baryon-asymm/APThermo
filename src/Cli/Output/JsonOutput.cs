using System.Text.Json;
using APThermo.Cli.Cases;
using APThermo.Problems;

namespace APThermo.Cli.Output;

/// <summary>The JSON output document (API.md): the cells of <see cref="StationFields"/> as nested objects, compositions above the threshold.</summary>
internal static class JsonOutput
{
    public static string Render(RunInfo run, IReadOnlyList<CaseOutput> cases) => DocumentWriter.Render(writer =>
    {
        writer.WriteStartObject();
        RunSection.Write(writer, run);
        writer.WriteStartArray("cases");
        foreach (var c in cases)
        {
            WriteCase(writer, c, run.Limits.Threshold);
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
        WriteMixture(writer, c);
        writer.WriteStartArray("stations");
        foreach (var station in c.Stations)
        {
            WriteStation(writer, station, c.Species, threshold);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteMixture(Utf8JsonWriter writer, CaseOutput c)
    {
        writer.WriteStartObject("mixture");
        writer.WriteStartObject("elementMoles");
        foreach (var element in c.Mixture.Elements)
        {
            DocumentWriter.WriteNumber(writer, element, c.Mixture.ElementMoles[element]);
        }

        writer.WriteEndObject();
        if (c.Mixture.Enthalpy is { } enthalpy)
        {
            DocumentWriter.WriteNumber(writer, "enthalpy", enthalpy);
        }
        else
        {
            writer.WriteNull("enthalpy");
        }

        DocumentWriter.WriteNumber(writer, "mass", c.MixtureMass);
        writer.WriteEndObject();
    }

    private static void WriteStation(Utf8JsonWriter writer, Station station, IReadOnlyList<string> species, double threshold)
    {
        writer.WriteStartObject();
        writer.WriteString("name", station.Name);
        writer.WriteString("status", Names.Status(station.Status));
        foreach (var cell in StationFields.State(station.State))
        {
            DocumentWriter.WriteNumber(writer, cell.Name, cell.Number!.Value);
        }

        if (station.Performance is { } figures)
        {
            writer.WriteStartObject("performance");
            foreach (var cell in StationFields.Performance(figures))
            {
                DocumentWriter.WriteNumber(writer, cell.Name, cell.Number!.Value);
            }

            writer.WriteEndObject();
        }

        WriteComposition(writer, "moleFractions", species, station.MoleFractions, station.MoleFractions, threshold);
        WriteComposition(writer, "condensedMassFractions", species, station.CondensedMassFractions, station.MoleFractions, threshold);
        WriteTransport(writer, station);
        writer.WriteEndObject();
    }

    private static void WriteComposition(Utf8JsonWriter writer, string name, IReadOnlyList<string> species,
        IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, double> moleFractions, double threshold)
    {
        writer.WriteStartObject(name);
        foreach (var s in species)
        {
            if (values.TryGetValue(s, out var value) && moleFractions.TryGetValue(s, out var fraction) && fraction >= threshold)
            {
                DocumentWriter.WriteNumber(writer, s, value);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteTransport(Utf8JsonWriter writer, Station station)
    {
        if (station.TransportStatus is not { } transportStatus)
        {
            return;
        }

        writer.WriteStartObject("transport");
        writer.WriteString("status", Names.Status(transportStatus));
        if (station.Transport is { } transport)
        {
            foreach (var cell in StationFields.Transport(transport))
            {
                if (cell.Integer is { } i)
                {
                    writer.WriteNumber(cell.Name, i);
                }
                else
                {
                    DocumentWriter.WriteNumber(writer, cell.Name, cell.Number!.Value);
                }
            }
        }

        writer.WriteEndObject();
    }
}
