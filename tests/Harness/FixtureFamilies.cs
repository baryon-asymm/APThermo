using APThermo.Fixtures;
using Xunit;

namespace APThermo.Harness;

/// <summary>
/// Fixture cases grouped into families that can share one batch. Nothing here runs a solver, builds a table or reads a
/// tolerance (BOOT.md, "no formula and no tolerance"): the key function is the only place that knows what makes two
/// cases share a batch, and it is the caller's.
/// </summary>
public static class FixtureFamilies
{
    /// <summary>
    /// Theory data: the key and case count of every family of <paramref name="kinds"/> (directories under
    /// <c>tests/Fixtures/cases</c>) grouped by <paramref name="key"/>, the largest family first, ties broken
    /// ordinally by the key. Carries no <see cref="CeaCase"/>: a caller's theory reads the cases back inside the
    /// test body through <see cref="CasesOf"/>, so that the theory data stays the string and int xUnit already
    /// knows how to serialize (xUnit1045) rather than a collection of this node's own type.
    /// </summary>
    public static TheoryData<string, int> Keys(IEnumerable<string> kinds, Func<CeaCase, string> key)
    {
        var data = new TheoryData<string, int>();
        foreach (var (familyKey, cases) in Families(kinds, key))
        {
            data.Add(familyKey, cases.Count);
        }

        return data;
    }

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
