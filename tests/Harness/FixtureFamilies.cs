using APThermo.Fixtures;

namespace APThermo.Harness;

/// <summary>One family's key and the number of cases it carries, as <see cref="FixtureFamilies.Keys"/> reports it.</summary>
public sealed record FixtureFamilyKey(string Key, int Count);

/// <summary>
/// Fixture cases grouped into families that can share one batch. Nothing here runs a solver, builds a table or reads a
/// tolerance (BOOT.md, "no formula and no tolerance"): the key function is the only place that knows what makes two
/// cases share a batch, and it is the caller's. This node names no test framework (BOOT.md, Constraints): a caller
/// builds its own <c>TheoryData</c> from <see cref="Keys"/> in a static member of its own test class.
/// </summary>
public static class FixtureFamilies
{
    /// <summary>
    /// The key and case count of every family of <paramref name="kinds"/> (directories under
    /// <c>tests/Fixtures/cases</c>) grouped by <paramref name="key"/>, the largest family first, ties broken
    /// ordinally by the key. Carries no <see cref="CeaCase"/>: a caller reads the cases back inside the test body
    /// through <see cref="CasesOf"/>.
    /// </summary>
    public static IReadOnlyList<FixtureFamilyKey> Keys(IEnumerable<string> kinds, Func<CeaCase, string> key) =>
        [.. Families(kinds, key).Select(family => new FixtureFamilyKey(family.Key, family.Cases.Count))];

    /// <summary>The cases of the family named <paramref name="familyKey"/>, grouped the same way <see cref="Keys"/> did.</summary>
    public static IReadOnlyList<CeaCase> CasesOf(IEnumerable<string> kinds, Func<CeaCase, string> key, string familyKey) =>
        Families(kinds, key).First(family => string.Equals(family.Key, familyKey, StringComparison.Ordinal)).Cases;

    private static List<(string Key, List<CeaCase> Cases)> Families(IEnumerable<string> kinds, Func<CeaCase, string> key)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(key);

        var families = new Dictionary<string, List<CeaCase>>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            foreach (var c in CeaFixtures.LoadAll(kind))
            {
                var familyKey = key(c);
                if (!families.TryGetValue(familyKey, out var members))
                {
                    families[familyKey] = members = [];
                }

                members.Add(c);
            }
        }

        return [.. families.OrderByDescending(f => f.Value.Count).ThenBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => (f.Key, f.Value))];
    }
}
