using APThermo.Data;

namespace APThermo.Performance.Tests;

/// <summary>
/// Builds a chemical system directly from elements and mass fractions, for the second hidden-defect audit's cases
/// the throat fixture family does not carry as a fixture (BOOT.md, 2026-09-28, finding F4 and observation F3): the
/// database's own candidate products for a set of elements, and the element moles per kilogram a set of parts
/// (symbol, atoms per formula unit, mass fraction) gives at their reference state. Mirrors, in shape, the audit's own
/// scratch harness (<c>ZzAudit.Simple</c>, never in the tree); no production code reads it.
/// </summary>
internal static class ElementMixture
{
    /// <summary>Every candidate product of <paramref name="database"/> whose formula lies entirely within
    /// <paramref name="elements"/>: no ion, no inert pseudo-element record, no record without thermodynamic data.</summary>
    public static string[] ProductsFor(SpeciesDatabase database, IReadOnlyList<string> elements)
    {
        var set = new HashSet<string>(elements, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var names = new List<string>();
        foreach (var species in database.Products)
        {
            if (species.IsInert || species.Intervals.Count == 0
                || species.Name.EndsWith('+') || species.Name.EndsWith('-')
                || species.Formula.Any(f => f.Symbol.Equals("E", StringComparison.OrdinalIgnoreCase))
                || !species.Formula.All(f => set.Contains(f.Symbol)))
            {
                continue;
            }

            if (seen.Add(species.Name))
            {
                names.Add(species.Name);
            }
        }

        return [.. names];
    }

    /// <summary>Element moles per kilogram of a mixture given as (symbol, atoms per formula unit, mass fraction)
    /// parts, each at its reference state (so the mixture's own reactant enthalpy at 298.15 K is zero).</summary>
    public static double[] ElementMoles(SpeciesDatabase database, string[] elements,
                                        params (string Symbol, double Atoms, double MassFraction)[] parts)
    {
        var moles = new double[elements.Length];
        foreach (var (symbol, atoms, massFraction) in parts)
        {
            var molarMass = atoms * database.AtomicWeight(symbol);
            for (var i = 0; i < elements.Length; i++)
            {
                if (elements[i].Equals(symbol, StringComparison.OrdinalIgnoreCase))
                {
                    moles[i] += massFraction / molarMass * atoms;
                }
            }
        }

        return moles;
    }
}
