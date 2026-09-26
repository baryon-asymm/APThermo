using System.Text.Json;
using System.Text.Json.Serialization;
using APThermo.Fixtures;

namespace APThermo.Data.Tests;

/// <summary>The JSON fixture records written by <c>transcribe.py</c> of this node into <c>records/</c>.</summary>
internal static class Records
{
    public static string Directory => RepositoryPaths.Resolve("tests", "Data.Tests", "records");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IEnumerable<string> SpeciesFixtures() =>
        System.IO.Directory.GetFiles(Path.Combine(Directory, "species"), "*.json").OrderBy(p => p, StringComparer.Ordinal);

    public static IEnumerable<string> TransportFixtures() =>
        System.IO.Directory.GetFiles(Path.Combine(Directory, "transport"), "*.json").OrderBy(p => p, StringComparer.Ordinal);

    public static SpeciesRecord LoadSpecies(string path) =>
        JsonSerializer.Deserialize<SpeciesRecord>(File.ReadAllText(path), Options);

    public static TransportRecord LoadTransport(string path) =>
        JsonSerializer.Deserialize<TransportRecord>(File.ReadAllText(path), Options);

    /// <summary>
    /// A value type, not a class (CA1812: a class deserialized only through <see cref="JsonSerializer"/>'s
    /// reflection carries no call site the analyzer can see, and reads as dead code): the fixture record for one species.
    /// </summary>
    internal readonly record struct SpeciesRecord(
        string Name, int Line, string Comment, string DateCode, List<FormulaPair> Formula, SpeciesPhase Phase,
        double MolarMass, double FormationEnthalpy, double AssignedTemperature, List<IntervalRecord> Intervals,
        SpeciesSection Section, bool IsInert);

    /// <summary>One element symbol and its count in a species' formula.</summary>
    internal readonly record struct FormulaPair(string Symbol, double Count);

    /// <summary>One temperature interval of a species' thermodynamic polynomial.</summary>
    internal readonly record struct IntervalRecord(
        double TLow, double THigh, List<double> Exponents, List<double> Coefficients, double B1, double B2, double EnthalpyOffset);

    /// <summary>The fixture record for one transport entry (a pure species or a pair).</summary>
    internal readonly record struct TransportRecord(
        string Species, string? Partner, int Line, string Reference, List<FitRecord> Viscosity, List<FitRecord> Conductivity);

    /// <summary>One viscosity or conductivity fit over one temperature range.</summary>
    internal readonly record struct FitRecord(double TLow, double THigh, double A, double B, double C, double D);
}
