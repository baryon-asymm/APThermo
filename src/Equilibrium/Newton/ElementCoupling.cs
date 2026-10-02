using APThermo.Thermo;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Rule A's read-only queries (BOOT.md, "Two rules come before the remedies above", 2026-09-28, "Rule A: an element
/// tie"): whether an element's row duplicates another's because every species with a nonzero amount in the sums,
/// gaseous or condensed, carries the two in one ratio; whether a tie already found still holds; and whether a
/// condensed species of the solution holds both elements. Kernel-compatible.
/// </summary>
internal static class ElementCoupling
{
    private const double RatioTolerance = 1.0e-12;

    /// <summary>
    /// The tie of <paramref name="element"/> with the one other active element every in-play species agrees with it
    /// on, in one ratio; <see cref="ElementTie.Active"/> is false when no single element couples with it.
    /// </summary>
    public static ElementTie Find(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int element)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (i == element || scratch.ElementActive[i] == 0)
            {
                continue;
            }

            var candidate = new ElementTie { Element = element, Partner = i };
            if (TryRatio(table, scratch, result, condensedCount, candidate, out var ratio))
            {
                return new ElementTie { Active = true, Element = element, Partner = i, Ratio = ratio };
            }
        }

        return default;
    }

    /// <summary>True when every species of the sums carrying either element of <paramref name="tie"/> still carries both, in its own ratio.</summary>
    public static bool Coupled(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, in ElementTie tie) =>
        TryRatio(table, scratch, result, condensedCount, tie, out _);

    /// <summary>True when a condensed species of the solution carries both elements of <paramref name="tie"/>.</summary>
    public static bool HeldByCondensed(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, in ElementTie tie)
    {
        var speciesCount = table.SpeciesCount;
        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (table.Stoichiometry[tie.Element * speciesCount + j] != 0.0 && table.Stoichiometry[tie.Partner * speciesCount + j] != 0.0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether every retained gaseous species and every condensed species of the solution agrees on one ratio a_k/a_i; the ratio when it does.</summary>
    private static bool TryRatio(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, in ElementTie tie, out double ratio)
    {
        ratio = 0.0;
        var any = false;
        var speciesCount = table.SpeciesCount;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] == 0.0)
            {
                continue;
            }

            if (!Agrees(table.Stoichiometry[tie.Element * speciesCount + j], table.Stoichiometry[tie.Partner * speciesCount + j], ref ratio, ref any))
            {
                return false;
            }
        }

        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (!Agrees(table.Stoichiometry[tie.Element * speciesCount + j], table.Stoichiometry[tie.Partner * speciesCount + j], ref ratio, ref any))
            {
                return false;
            }
        }

        return any;
    }

    /// <summary>True when the two elements' amounts in one species (zero and zero, or a shared ratio within tolerance) agree with the ratio found so far.</summary>
    private static bool Agrees(double ak, double ai, ref double ratio, ref bool any)
    {
        if (ak == 0.0 && ai == 0.0)
        {
            return true;
        }

        if (ak == 0.0 || ai == 0.0)
        {
            return false;
        }

        var r = ak / ai;
        if (!any)
        {
            ratio = r;
            any = true;
            return true;
        }

        return Math.Abs(r - ratio) <= RatioTolerance * Math.Abs(ratio);
    }
}
