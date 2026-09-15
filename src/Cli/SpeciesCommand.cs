using AerospacePropellantThermodynamics.Cli.Output;
using AerospacePropellantThermodynamics.Cli.Syntax;

namespace AerospacePropellantThermodynamics.Cli;

/// <summary>The species command: the database, the name filter, the rows, the run and the delivery; the rendering is <see cref="SpeciesListing"/>.</summary>
internal static class SpeciesCommand
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
        var text = options.Format == OutputFormat.Csv ? SpeciesListing.Csv(rows) : SpeciesListing.Json(run, rows);
        DocumentWriter.Deliver(text, options.Output, output);
        return ExitCode.Ok;
    }
}
