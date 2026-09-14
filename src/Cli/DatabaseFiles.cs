using System.Diagnostics;
using AerospacePropellantThermodynamics.Data;

namespace AerospacePropellantThermodynamics.Cli;

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
        var database = ReadDatabase(thermo, transPath);
        watch.Stop();
        var info = new DatabaseInfo(thermo, transPath, database.Provenance.ThermoSha256, database.Provenance.TransSha256);
        return (database, info, watch.Elapsed.TotalSeconds);
    }

    /// <summary>The library's file and format refusals, translated to this node's InputException where the library is called (F-CL-13).</summary>
    private static SpeciesDatabase ReadDatabase(string thermo, string? transPath)
    {
        try
        {
            return SpeciesDatabase.Load(thermo, transPath);
        }
        catch (DatabaseFormatException e)
        {
            throw new InputException($"{e.FileName ?? "database"}:{e.LineNumber}: {e.Message}");
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new InputException(e.Message);
        }
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
