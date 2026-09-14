using System.Text;
using System.Text.Json;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The species command: the database's entries, filtered by name, flattened once and rendered in JSON or CSV.</summary>
internal static class SpeciesListing
{
    public static ExitCode Execute(Invocation invocation, TextWriter output)
    {
        var options = invocation.Options;
        var (database, info, databaseSeconds) = DatabaseFiles.Load(options.Database);
        var rows = database.Products.Concat(database.Reactants)
            .Where(s => options.Find is null || s.Name.Contains(options.Find, StringComparison.OrdinalIgnoreCase))
            .Select(s => SpeciesRow.From(s, database))
            .ToList();
        var run = new RunInfo("species", [], info, null, new Timings(databaseSeconds, 0.0), new RunLimits(options.Threshold, options.MassTolerance));
        var text = options.Format == OutputFormat.Csv ? Csv(rows) : Json(run, rows);
        DocumentWriter.Deliver(text, options.Output, output);
        return ExitCode.Ok;
    }

    private static string Json(RunInfo run, IReadOnlyList<SpeciesRow> rows) => DocumentWriter.Render(writer =>
    {
        writer.WriteStartObject();
        RunSection.Write(writer, run);
        writer.WriteStartArray("species");
        foreach (var row in rows)
        {
            WriteRow(writer, row);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static void WriteRow(Utf8JsonWriter writer, SpeciesRow row)
    {
        writer.WriteStartObject();
        writer.WriteString("name", row.Name);
        writer.WriteString("section", row.Section);
        writer.WriteString("phase", row.Phase);
        writer.WriteStartObject("formula");
        foreach (var pair in row.Formula)
        {
            writer.WriteNumber(pair.Symbol, pair.Count);
        }

        writer.WriteEndObject();
        writer.WriteNumber("molarMass", row.MolarMass);
        writer.WriteNumber("formationEnthalpy", row.FormationEnthalpy);
        if (row.TemperatureLow is { } low && row.TemperatureHigh is { } high)
        {
            writer.WriteStartArray("temperatureRange");
            writer.WriteNumberValue(low);
            writer.WriteNumberValue(high);
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteNull("temperatureRange");
            writer.WriteNumber("assignedTemperature", row.AssignedTemperature!.Value);
        }

        writer.WriteBoolean("transportData", row.TransportData);
        writer.WriteEndObject();
    }

    private static string Csv(IReadOnlyList<SpeciesRow> rows)
    {
        var text = new StringBuilder("name,section,phase,formula,molarMass,formationEnthalpy,temperatureLow,temperatureHigh,transportData\n");
        foreach (var row in rows)
        {
            var formula = string.Join(";", row.Formula.Select(pair => $"{pair.Symbol}:{CsvOutput.Number(pair.Count)}"));
            var low = row.TemperatureLow is { } l ? CsvOutput.Number(l) : "";
            var high = row.TemperatureHigh is { } h ? CsvOutput.Number(h) : "";
            string[] cells =
            [
                row.Name, row.Section, row.Phase, formula,
                CsvOutput.Number(row.MolarMass), CsvOutput.Number(row.FormationEnthalpy), low, high,
                row.TransportData ? "true" : "false",
            ];
            text.Append(string.Join(",", cells.Select(CsvOutput.Escape))).Append('\n');
        }

        return text.ToString();
    }
}
