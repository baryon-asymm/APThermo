using System.Text.Json;

namespace AerospacePropellantThermodynamics.Benchmarks;

/// The small JSON readers every group that builds its case from a fixture's
/// `case.inputs` shares (BOOT.md, Invariants: fixture-based benchmarks read
/// `tests/Fixtures` files through `RepositoryPaths`; no fixture value is typed into
/// code).
internal static class FixtureJson
{
    public static IReadOnlyList<double> ReadDoubles(JsonElement inputs, string property)
    {
        var values = new List<double>();
        foreach (var entry in inputs.GetProperty(property).EnumerateArray())
        {
            values.Add(entry.GetDouble());
        }
        return values;
    }

    public static IReadOnlyList<string> ReadStrings(JsonElement inputs, string property)
    {
        var values = new List<string>();
        foreach (var entry in inputs.GetProperty(property).EnumerateArray())
        {
            values.Add(entry.GetString()!);
        }
        return values;
    }

    /// The fixtures' own `elementMoles` (`tests/Fixtures/generate/common.py`) are
    /// kmol per kilogram, the numerical nodes' own unit: a case built straight from
    /// them (`ReadElementMoles`, for the raw `Execution` batches of
    /// `BatchThroughputBenchmarks` and `OneTimeCostBenchmarks`) needs no conversion.
    public static (IReadOnlyList<string> Elements, double[] MolesPerKilogram) ReadElementMoles(JsonElement inputs)
    {
        var elements = new List<string>();
        var moles = new List<double>();
        foreach (var element in inputs.GetProperty("elementMoles").EnumerateObject())
        {
            elements.Add(element.Name);
            moles.Add(element.Value.GetDouble());
        }
        return (elements, moles.ToArray());
    }

    /// `StateRecord.Composition` and `ElementalMixture.ElementMoles` (`Problems/API.md`)
    /// are mol per kilogram, one thousand times the fixtures' own kmol-per-kilogram
    /// scale (the comment on `ReadElementMoles`): scaled here, once, for every
    /// benchmark that builds a `Problems` case from a fixture.
    public static Dictionary<string, double> ReadComposition(JsonElement inputs)
    {
        var composition = new Dictionary<string, double>();
        foreach (var element in inputs.GetProperty("elementMoles").EnumerateObject())
        {
            composition[element.Name] = element.Value.GetDouble() * 1000.0;
        }
        return composition;
    }
}
