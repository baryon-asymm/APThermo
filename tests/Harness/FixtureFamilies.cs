using APThermo.Fixtures;

namespace APThermo.Harness;

/// <summary>
/// Fixture cases grouped into families that can share one batch. Nothing here runs a solver, builds a table or reads a
/// tolerance (BOOT.md, "no formula and no tolerance"): the key function is the only place that knows what makes two
/// cases share a batch, and it is the caller's.
/// </summary>
public static class FixtureFamilies
{
    /// <summary>
    /// Theory data: the cases of <paramref name="kinds"/> (directories under <c>tests/Fixtures/cases</c>) grouped by
    /// <paramref name="key"/>, the largest family first, ties broken ordinally by the key. Each row is
    /// <c>[key, count, cases]</c>.
    /// </summary>
    public static IEnumerable<object[]> Of(IEnumerable<string> kinds, Func<CeaCase, string> key)
    {
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

        foreach (var family in families.OrderByDescending(f => f.Value.Count).ThenBy(f => f.Key, StringComparer.Ordinal))
        {
            yield return [family.Key, family.Value.Count, (IReadOnlyList<CeaCase>)family.Value];
        }
    }
}
