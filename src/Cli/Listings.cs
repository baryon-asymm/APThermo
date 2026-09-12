using System.Globalization;
using System.Text;
using System.Text.Json;
using AerospacePropellantThermodynamics.Data;
using AerospacePropellantThermodynamics.Execution;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The species and devices commands.</summary>
internal static class Listings
{
    public static ExitCode Species(Invocation invocation, TextWriter output)
    {
        var options = invocation.Options;
        var (database, info, databaseSeconds) = DatabaseFiles.Load(options.Database);
        var entries = database.Products.Concat(database.Reactants)
            .Where(s => options.Find is null || s.Name.Contains(options.Find, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var run = new RunInfo("species", [], info, null, databaseSeconds, 0.0, options.Threshold);
        var text = options.Format == OutputFormat.Csv ? SpeciesCsv(entries, database) : SpeciesJson(run, entries, database);
        Outputs.Deliver(text, options.Output, output);
        return ExitCode.Ok;
    }

    public static ExitCode Devices(Invocation invocation, TextWriter output)
    {
        var options = invocation.Options;
        var text = Outputs.Render(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartObject("run");
            writer.WriteString("tool", Program.ToolName);
            writer.WriteString("version", Program.Version);
            writer.WriteString("command", "devices");
            writer.WriteEndObject();
            writer.WriteBoolean("cudaForbidden", Engine.CudaForbidden);
            writer.WritePropertyName("cpu");
            using (var cpu = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cpu }))
            {
                Outputs.WriteAccelerator(writer, cpu.Accelerator);
            }

            writer.WritePropertyName("cuda");
            try
            {
                using var cuda = Engine.Create(new EngineOptions { Accelerator = AcceleratorKind.Cuda });
                Outputs.WriteAccelerator(writer, cuda.Accelerator);
            }
            catch (AcceleratorUnavailableException e)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("available", false);
                writer.WriteString("message", e.Message);
                writer.WriteStartArray("pathsTried");
                foreach (var path in e.PathsTried)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            catch (InvalidOperationException e)
            {
                writer.WriteStartObject();
                writer.WriteBoolean("available", false);
                writer.WriteString("message", e.Message);
                writer.WriteStartArray("pathsTried");
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
        Outputs.Deliver(text, options.Output, output);
        return ExitCode.Ok;
    }

    private static string SpeciesJson(RunInfo run, IReadOnlyList<Species> entries, SpeciesDatabase database) => Outputs.Render(writer =>
    {
        writer.WriteStartObject();
        Outputs.WriteRun(writer, run);
        writer.WriteStartArray("species");
        foreach (var species in entries)
        {
            writer.WriteStartObject();
            writer.WriteString("name", species.Name);
            writer.WriteString("section", Names.Camel(species.Section.ToString()));
            writer.WriteString("phase", Names.Camel(species.Phase.ToString()));
            writer.WriteStartObject("formula");
            foreach (var pair in species.Formula)
            {
                writer.WriteNumber(pair.Symbol, pair.Count);
            }

            writer.WriteEndObject();
            writer.WriteNumber("molarMass", species.MolarMass);
            writer.WriteNumber("formationEnthalpy", species.FormationEnthalpy);
            if (species.Intervals.Count > 0)
            {
                writer.WriteStartArray("temperatureRange");
                writer.WriteNumberValue(species.Intervals.Min(i => i.TLow));
                writer.WriteNumberValue(species.Intervals.Max(i => i.THigh));
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("temperatureRange");
                writer.WriteNumber("assignedTemperature", species.AssignedTemperature);
            }

            writer.WriteBoolean("transportData", database.Transport?.Find(species.Name) is not null);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });

    private static string SpeciesCsv(IReadOnlyList<Species> entries, SpeciesDatabase database)
    {
        var text = new StringBuilder("name,section,phase,formula,molarMass,formationEnthalpy,temperatureLow,temperatureHigh,transportData\n");
        foreach (var species in entries)
        {
            var formula = string.Join(";", species.Formula.Select(pair => $"{pair.Symbol}:{CsvOutput.Number(pair.Count)}"));
            var low = species.Intervals.Count > 0 ? CsvOutput.Number(species.Intervals.Min(i => i.TLow)) : "";
            var high = species.Intervals.Count > 0 ? CsvOutput.Number(species.Intervals.Max(i => i.THigh)) : "";
            string[] cells =
            [
                species.Name, Names.Camel(species.Section.ToString()), Names.Camel(species.Phase.ToString()), formula,
                CsvOutput.Number(species.MolarMass), CsvOutput.Number(species.FormationEnthalpy), low, high,
                database.Transport?.Find(species.Name) is not null ? "true" : "false",
            ];
            text.Append(string.Join(",", cells.Select(CsvOutput.Escape))).Append('\n');
        }

        return text.ToString();
    }
}
