using System.Diagnostics;
using APThermo.Data;

namespace APThermo.Cli;

/// <summary>Finding and loading the database files: a given directory, or the database embedded in the library.</summary>
internal static class DatabaseFiles
{
    public const string ThermoFile = "thermo.inp";
    public const string TransFile = "trans.inp";

    /// <summary>The path-like marker <see cref="DatabaseInfo"/> carries in place of a file path when the embedded database was used.</summary>
    public const string EmbeddedThermoMarker = "embedded:thermo.inp";

    /// <summary>The path-like marker <see cref="DatabaseInfo"/> carries in place of a file path when the embedded database was used.</summary>
    public const string EmbeddedTransMarker = "embedded:trans.inp";

    public static (SpeciesDatabase Database, DatabaseInfo Info, double Seconds) Load(string? directory)
    {
        var watch = Stopwatch.StartNew();
        var (database, info) = directory is null ? LoadEmbedded() : LoadFromDirectory(directory);
        watch.Stop();
        return (database, info, watch.Elapsed.TotalSeconds);
    }

    private static (SpeciesDatabase, DatabaseInfo) LoadEmbedded()
    {
        var database = ReadDatabase(SpeciesDatabase.LoadBundled);
        var info = new DatabaseInfo(EmbeddedThermoMarker, EmbeddedTransMarker, database.Provenance.ThermoSha256, database.Provenance.TransSha256);
        return (database, info);
    }

    private static (SpeciesDatabase, DatabaseInfo) LoadFromDirectory(string directory)
    {
        var thermo = Path.Combine(directory, ThermoFile);
        if (!File.Exists(thermo))
        {
            throw new InputException($"no {ThermoFile} in the database directory '{directory}'");
        }

        var trans = Path.Combine(directory, TransFile);
        var transPath = File.Exists(trans) ? trans : null;
        var database = ReadDatabase(() => SpeciesDatabase.Load(thermo, transPath));
        var info = new DatabaseInfo(thermo, transPath, database.Provenance.ThermoSha256, database.Provenance.TransSha256);
        return (database, info);
    }

    /// <summary>The library's file and format refusals, translated to this node's InputException where the library is called (F-CL-13).</summary>
    private static SpeciesDatabase ReadDatabase(Func<SpeciesDatabase> read)
    {
        try
        {
            return read();
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
}
