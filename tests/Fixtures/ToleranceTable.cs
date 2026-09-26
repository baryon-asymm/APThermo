using System.Text.Json;

namespace APThermo.Fixtures;

/// <summary>An absolute and a relative tolerance; a value matches when |expected − actual| ≤ Absolute + Relative·|expected|.</summary>
public readonly record struct Tolerance(double Absolute, double Relative);

/// <summary>The single tolerance table of the tree, <c>tests/Fixtures/tolerances.json</c>, one entry per field with its derivation.</summary>
public sealed class ToleranceTable
{
    private readonly Dictionary<string, (Tolerance Tolerance, string Derivation)> _entries;

    private ToleranceTable(Dictionary<string, (Tolerance, string)> entries)
    {
        _entries = entries;
        Fields = [.. entries.Keys.OrderBy(k => k, StringComparer.Ordinal)];
    }

    /// <summary>The path of the table under the repository root.</summary>
    public static string Path => RepositoryPaths.Resolve("tests", "Fixtures", "tolerances.json");

    /// <summary>The field names of the table, sorted ordinally.</summary>
    public IReadOnlyList<string> Fields { get; }

    /// <summary>Loads the single tolerance table of the tree, <see cref="Path"/>.</summary>
    /// <returns>The loaded table.</returns>
    public static ToleranceTable Load() => Load(Path);

    /// <summary>Loads a tolerance table from an explicit path.</summary>
    /// <param name="path">The path of the table document.</param>
    /// <returns>The loaded table.</returns>
    public static ToleranceTable Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
        {
            throw new FixtureFormatException(path, "fields", "the table must be an object with a 'fields' object");
        }

        var entries = new Dictionary<string, (Tolerance, string)>(StringComparer.Ordinal);
        foreach (var property in fields.EnumerateObject())
        {
            var field = property.Name;
            var absolute = Number(path, property.Value, field, "absolute");
            var relative = Number(path, property.Value, field, "relative");
            if (!property.Value.TryGetProperty("derivation", out var derivation) || derivation.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(derivation.GetString()))
            {
                throw new FixtureFormatException(path, field + ".derivation", "every tolerance carries a non-empty derivation");
            }

            entries[field] = (new Tolerance(absolute, relative), derivation.GetString()!);
        }

        return new ToleranceTable(entries);
    }

    /// <summary>The absolute and relative tolerance recorded for a field.</summary>
    /// <param name="field">The field name, as it appears in a fixture's outputs.</param>
    /// <returns>The field's tolerance.</returns>
    public Tolerance For(string field) =>
        _entries.TryGetValue(field, out var entry) ? entry.Tolerance : throw new KeyNotFoundException($"no tolerance for '{field}'");

    /// <summary>The recorded derivation of a field's tolerance, for diagnostics.</summary>
    /// <param name="field">The field name, as it appears in a fixture's outputs.</param>
    /// <returns>The field's derivation text.</returns>
    public string Derivation(string field) =>
        _entries.TryGetValue(field, out var entry) ? entry.Derivation : throw new KeyNotFoundException($"no tolerance for '{field}'");

    /// <summary>Whether an actual value matches an expected value within the field's tolerance.</summary>
    /// <param name="field">The field name, as it appears in a fixture's outputs.</param>
    /// <param name="expected">The reference value.</param>
    /// <param name="actual">The tree's value.</param>
    /// <returns><see langword="true"/> when the two values agree within the field's tolerance.</returns>
    public bool Matches(string field, double expected, double actual)
    {
        var tolerance = For(field);
        return Math.Abs(expected - actual) <= tolerance.Absolute + tolerance.Relative * Math.Abs(expected);
    }

    /// <summary>
    /// "moleFraction" when <paramref name="referenceValue"/> is not below the reference's own print threshold
    /// (<c>moleFraction</c>'s absolute tolerance), else "moleFractionTrace": the rule that picks which entry compares a
    /// reference mole fraction, written once so the print threshold is not typed again at each of its call sites
    /// (BOOT.md, the tolerance-table invariant).
    /// </summary>
    /// <param name="referenceValue">The reference's mole fraction of the species at the station.</param>
    public string MoleFractionField(double referenceValue) =>
        referenceValue >= For("moleFraction").Absolute ? "moleFraction" : "moleFractionTrace";

    private static double Number(string path, JsonElement entry, string field, string property)
    {
        if (!entry.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new FixtureFormatException(path, $"{field}.{property}", "a number is required");
        }

        var number = value.GetDouble();
        return number >= 0.0
            ? number
            : throw new FixtureFormatException(path, $"{field}.{property}", "a tolerance is not negative");
    }
}
