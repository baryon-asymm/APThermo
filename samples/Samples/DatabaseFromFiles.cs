// snippet-start: DatabaseFromFilesUsings
using System.Runtime.CompilerServices;
using APThermo.Data;
// snippet-end

namespace APThermo.Samples;

/// <summary>Loads the database from the committed <c>data/</c> files instead of the bundled copy; the one scenario allowed file I/O.</summary>
internal sealed class DatabaseFromFiles
{
    internal static void Run(TextWriter output)
    {
        // snippet-start: DatabaseFromFiles
        var directory = RepositoryDataDirectory();
        var database = SpeciesDatabase.Load(
            Path.Combine(directory, "thermo.inp"),
            Path.Combine(directory, "trans.inp"));

        output.WriteLine($"products = {database.Products.Count}, reactants = {database.Reactants.Count}");
        output.WriteLine($"thermoSha256 starts with: {database.Provenance.ThermoSha256[..12]}");
        // snippet-end
    }

    /// <summary>The repository's own <c>data/</c> directory, found from this source file (AGENTS.md §13), never from the current directory.</summary>
    private static string RepositoryDataDirectory([CallerFilePath] string sourceFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "..", "data"));
}
