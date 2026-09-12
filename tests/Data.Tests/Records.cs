using System.Text.Json;
using System.Text.Json.Serialization;
using AerospacePropellantThermodynamics.Fixtures;

namespace AerospacePropellantThermodynamics.Data.Tests;

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
        JsonSerializer.Deserialize<SpeciesRecord>(File.ReadAllText(path), Options) ?? throw new InvalidDataException(path);

    public static TransportRecord LoadTransport(string path) =>
        JsonSerializer.Deserialize<TransportRecord>(File.ReadAllText(path), Options) ?? throw new InvalidDataException(path);

    public sealed record SpeciesRecord(
        string Name, int Line, string Comment, string DateCode, List<FormulaPair> Formula, SpeciesPhase Phase,
        double MolarMass, double FormationEnthalpy, double AssignedTemperature, List<IntervalRecord> Intervals,
        SpeciesSection Section, bool IsInert);

    public sealed record FormulaPair(string Symbol, double Count);

    public sealed record IntervalRecord(
        double TLow, double THigh, List<double> Exponents, List<double> Coefficients, double B1, double B2, double EnthalpyOffset);

    public sealed record TransportRecord(
        string Species, string? Partner, int Line, string Reference, List<FitRecord> Viscosity, List<FitRecord> Conductivity);

    public sealed record FitRecord(double TLow, double THigh, double A, double B, double C, double D);
}
