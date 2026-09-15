using APThermo.Data;

namespace APThermo.Problems;

/// <summary>The one rule that turns an element set into candidate product species (BOOT.md, invariants).</summary>
internal static class SpeciesSelection
{
    /// <summary>The database spelling of an element symbol: upper case, as the NASA files write formulas.</summary>
    public static string Spelling(string symbol) => symbol.Trim().ToUpperInvariant();

    /// <summary>
    /// Every gaseous product species of the database whose elements are all among the given ones, then every condensed one, each
    /// in database order, minus the omitted names (an omitted name that is no product is ignored, as the reference ignores it), or
    /// exactly the given list; ionized species and inert pseudo-element records are never candidates.
    /// </summary>
    public static IReadOnlyList<string> Candidates(SpeciesDatabase database, IReadOnlyList<string> elements, IReadOnlyList<string> omit, IReadOnlyList<string>? only)
    {
        if (only is not null)
        {
            ValidateOnly(database, elements, only);
            return only.ToList();
        }

        var present = new HashSet<string>(elements.Select(Spelling), StringComparer.Ordinal);
        var omitted = new HashSet<string>(omit, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var gases = new List<string>();
        var condensed = new List<string>();
        foreach (var species in database.Products)
        {
            // A condensed species may have several records of one name for its temperature ranges (Co(b)); the name is the candidate.
            if (species.IsInert || IsIonized(species) || omitted.Contains(species.Name) || !Fits(species, present) || !seen.Add(species.Name))
            {
                continue;
            }

            (species.Phase == SpeciesPhase.Gas ? gases : condensed).Add(species.Name);
        }

        gases.AddRange(condensed);
        return gases;
    }

    /// <summary>Every name of an Only list must be a product species whose elements lie within the mixture's.</summary>
    public static void ValidateOnly(SpeciesDatabase database, IReadOnlyList<string> elements, IReadOnlyList<string> only)
    {
        if (only.Count == 0)
        {
            throw new ArgumentException("the Only list is empty");
        }

        var present = new HashSet<string>(elements.Select(Spelling), StringComparer.Ordinal);
        foreach (var name in only)
        {
            if (!database.TryGet(name, out var species) || species.Section != SpeciesSection.Products)
            {
                throw new ArgumentException($"species '{name}' of the Only list is not a product species of the database");
            }

            if (!Fits(species, present))
            {
                throw new ArgumentException($"species '{name}' of the Only list contains an element the mixture does not have");
            }
        }
    }

    private static bool Fits(Species species, HashSet<string> present) => species.Formula.All(pair => present.Contains(Spelling(pair.Symbol)));

    /// <summary>An ion carries the electron pseudo-element in its formula (a trailing sign in the name is no criterion: "C3H4,cyclo-" is neutral).</summary>
    private static bool IsIonized(Species species) => species.Formula.Any(pair => Spelling(pair.Symbol) == "E");
}
