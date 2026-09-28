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
            return [.. only];
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

    /// <summary>
    /// Every element that carries a nonzero abundance somewhere must appear in the formula of at least one candidate;
    /// an element that survives only in ionized or inert records (<c>E</c>, <c>IH</c>, <c>IO</c> of the committed
    /// file) has none, and the mixture is unsupported (BOOT.md, the audit fixes of 2026-09-26: it used to enter the
    /// table as a row with no species and fail later as a numerical <c>singularMatrix</c>).
    /// </summary>
    /// <remarks>
    /// An element with no candidates is refused only when <paramref name="elementsWithAbundance"/> names it (the
    /// second audit's fix: an element at zero everywhere is masked, as the equilibrium node masks any absent
    /// element, not refused). When an <paramref name="omit"/> or <paramref name="only"/> list removed the species
    /// that would otherwise have carried the element, the refusal names the list instead of the database (the
    /// second audit's observation 1).
    /// </remarks>
    public static void ValidateElementsHaveCandidates(SpeciesDatabase database, IReadOnlyList<string> elements, IReadOnlyList<string> candidates,
        IReadOnlyList<string> omit, IReadOnlyList<string>? only, IReadOnlyCollection<string> elementsWithAbundance)
    {
        var present = PresentElements(database, candidates);
        var isRestricted = only is not null || omit.Count > 0;
        var unrestricted = isRestricted ? PresentElements(database, Candidates(database, elements, [], null)) : null;
        foreach (var element in elements)
        {
            var spelling = Spelling(element);
            if (present.Contains(spelling) || !elementsWithAbundance.Contains(spelling))
            {
                continue;
            }

            if (unrestricted is not null && unrestricted.Contains(spelling))
            {
                var listName = only is not null ? "Only" : "Omit";
                throw new ArgumentException($"element '{spelling}' has no candidate species: the {listName} list excludes every species that would carry it");
            }

            throw new ArgumentException($"element '{spelling}' has no candidate species: only ionized or inert records carry it");
        }
    }

    /// <summary>Every name of an Only list must be a product species whose elements lie within the mixture's, and neither ionized nor inert (never a candidate).</summary>
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

            if (species.IsInert || IsIonized(species))
            {
                throw new ArgumentException($"species '{name}' of the Only list is ionized or an inert pseudo-element record, never a candidate");
            }

            if (!Fits(species, present))
            {
                throw new ArgumentException($"species '{name}' of the Only list contains an element the mixture does not have");
            }
        }
    }

    private static HashSet<string> PresentElements(SpeciesDatabase database, IReadOnlyList<string> candidates)
    {
        var present = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in candidates)
        {
            if (database.TryGet(name, out var species))
            {
                foreach (var pair in species.Formula)
                {
                    _ = present.Add(Spelling(pair.Symbol));
                }
            }
        }

        return present;
    }

    private static bool Fits(Species species, HashSet<string> present) => species.Formula.All(pair => present.Contains(Spelling(pair.Symbol)));

    /// <summary>An ion carries the electron pseudo-element in its formula (a trailing sign in the name is no criterion: "C3H4,cyclo-" is neutral).</summary>
    private static bool IsIonized(Species species) => species.Formula.Any(pair => Spelling(pair.Symbol) == "E");
}
