using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// Where two records of one formula meet: the record's own bounds, the adjacent record of the same substance, the crossing T*
/// of their linearized Gibbs curves, the effective range that crossing defines, and who of a formula already stands in the
/// solution. The rules and the derivation of T* are in the node's BOOT.md (the condensed-species rule, effective range);
/// this class is their one arithmetic. Kernel-compatible: static, no allocation, no exceptions.
/// </summary>
/// <remarks>
/// The pure half of this geometry — which condensed records share a bound, and where each pair crosses — is a property of the
/// table rather than of a case, and could live in Thermo beside the join-and-cut rule and LatentHeatThreshold it already owns.
/// Moving it changes Thermo's contract and the arithmetic path on CUDA, so it is a design session of the root, not part of the
/// decomposition of 2026-09-14 (BOOT.md, ## Structure).
/// </remarks>
internal static class PhaseGeometry
{
    /// <summary>K: a crossing farther from the shared bound than this means inconsistent fits (BOOT.md), and the bound stands.</summary>
    private const double CrossingLimit = 1.0;

    /// <summary>Relative tolerance of the effective-range comparisons (BOOT.md).</summary>
    private const double RangeTolerance = 1.0e-9;

    /// <summary>True when two species have the same stoichiometry column: two records of one substance.</summary>
    public static bool SameFormula(in SpeciesTableView table, int j, int k)
    {
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (table.Stoichiometry[i * speciesCount + k] != table.Stoichiometry[i * speciesCount + j])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Another record of the same formula in the solution; −1 if none.</summary>
    public static int PartnerInSolution(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, int species)
    {
        for (var c = 0; c < condensedCount; c++)
        {
            var k = scratch.CondensedInSolution[c];
            if (k != species && SameFormula(table, k, species))
            {
                return k;
            }
        }

        return -1;
    }

    /// <summary>Whether the species stands in the solution's condensed set.</summary>
    public static bool InSolution(in EquilibriumScratch scratch, int condensedCount, int species)
    {
        for (var c = 0; c < condensedCount; c++)
        {
            if (scratch.CondensedInSolution[c] == species)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The active record of the same formula whose range begins where j's ends (above) or ends where j's begins; −1 if none.</summary>
    public static int Adjacent(in SpeciesTableView table, in EquilibriumScratch scratch, int j, bool above)
    {
        for (var k = table.GasCount; k < table.SpeciesCount; k++)
        {
            if (k == j || !SpeciesMarks.InPlay(scratch, k) || !SameFormula(table, j, k))
            {
                continue;
            }

            if (above ? SpeciesFunctions.RecordLow(table, k) == SpeciesFunctions.RecordHigh(table, j)
                      : SpeciesFunctions.RecordHigh(table, k) == SpeciesFunctions.RecordLow(table, j))
            {
                return k;
            }
        }

        return -1;
    }

    /// <summary>Where G°/RT of two adjacent records cross, linearized at their shared bound; the bound when there is no latent heat or the fits disagree.</summary>
    public static double Crossing(in SpeciesTableView table, int j, int k, double bound)
    {
        var dg = SpeciesFunctions.GOverRT(table, j, bound) - SpeciesFunctions.GOverRT(table, k, bound);
        var dh = SpeciesFunctions.HOverRT(table, j, bound) - SpeciesFunctions.HOverRT(table, k, bound);
        if (Math.Abs(dh) < SpeciesFunctions.LatentHeatThreshold)
        {
            return bound;
        }

        var crossing = bound * (1.0 + dg / dh);
        return Math.Abs(crossing - bound) <= CrossingLimit ? crossing : bound;
    }

    /// <summary>The record's lower bound, moved to the crossing when it touches an adjacent record of its formula.</summary>
    public static double EffectiveLow(in SpeciesTableView table, in EquilibriumScratch scratch, int j)
    {
        var below = Adjacent(table, scratch, j, false);
        return below >= 0 ? Crossing(table, below, j, SpeciesFunctions.RecordLow(table, j)) : SpeciesFunctions.RecordLow(table, j);
    }

    /// <summary>The record's upper bound, moved to the crossing when it touches an adjacent record of its formula.</summary>
    public static double EffectiveHigh(in SpeciesTableView table, in EquilibriumScratch scratch, int j)
    {
        var over = Adjacent(table, scratch, j, true);
        return over >= 0 ? Crossing(table, j, over, SpeciesFunctions.RecordHigh(table, j)) : SpeciesFunctions.RecordHigh(table, j);
    }

    /// <summary>Whether the temperature lies in the record's effective range, within the relative range tolerance.</summary>
    public static bool InEffectiveRange(in SpeciesTableView table, in EquilibriumScratch scratch, int j, double temperature)
    {
        var tolerance = RangeTolerance * temperature;
        return temperature >= EffectiveLow(table, scratch, j) - tolerance && temperature <= EffectiveHigh(table, scratch, j) + tolerance;
    }

    /// <summary>A record of the same formula, active, not in the solution, whose effective range holds the temperature; −1 if none.</summary>
    public static int PhaseAt(in SpeciesTableView table, in EquilibriumScratch scratch, int condensedCount, int j, double temperature)
    {
        for (var k = table.GasCount; k < table.SpeciesCount; k++)
        {
            if (k == j || !SpeciesMarks.InPlay(scratch, k) || InSolution(scratch, condensedCount, k) || !SameFormula(table, j, k)
                || !InEffectiveRange(table, scratch, k, temperature))
            {
                continue;
            }

            return k;
        }

        return -1;
    }
}
