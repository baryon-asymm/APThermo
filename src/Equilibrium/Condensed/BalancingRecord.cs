using APThermo.Thermo;

namespace APThermo.Equilibrium.Condensed;

/// <summary>
/// The balancing record (BOOT.md, "A balancing record stays"): a condensed record whose mole number came out negative
/// by no more than the rounding of the element balances it carries, and whose removal would leave the element rows of the
/// retained gases and the remaining condensed records dependent, is kept at zero moles instead of removed. Its true amount
/// is positive and below what the balances can resolve; it is the only carrier of a direction of the multipliers.
/// Kernel-compatible; uses the dead Newton matrix as the workspace of its rank test.
/// </summary>
internal static class BalancingRecord
{
    /// <summary>The unit roundoff bound of a double sum, 2^-52, the relative spacing of the doubles at one.</summary>
    private const double MachineEpsilon = 2.220446049250313e-16;

    /// <summary>The ulps of the largest term of an element's balance below which a negative amount is rounding.</summary>
    private const double RoundingUlps = 4.0;

    /// <summary>
    /// True when the record at <paramref name="position"/> of the condensed set, whose mole number is negative, is a balancing
    /// record: within the rounding of every balance it carries, the element system independent with it, and either the only thing
    /// that keeps that system independent or, at the <paramref name="lastChange"/> the cap of set changes allows, the record the
    /// set has kept removing and bringing back (BOOT.md, "A balancing record stays", the last change, 2026-10-05).
    /// </summary>
    public static bool Holds(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                             int condensedCount, int position, bool lastChange) =>
        WithinRounding(table, scratch, result, condensedCount, scratch.CondensedInSolution[position])
        && (Dependent(table, scratch, result, condensedCount, position) || lastChange)
        && !Dependent(table, scratch, result, condensedCount, -1);

    /// <summary>
    /// True when, for every active element of species <paramref name="j"/>, its contribution to the balance is at most
    /// <see cref="RoundingUlps"/> ulps of the largest term of that balance, the retained gases and the condensed set.
    /// </summary>
    private static bool WithinRounding(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                       int condensedCount, int j)
    {
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var share = Math.Abs(table.Stoichiometry[i * speciesCount + j] * result.Moles[j]);
            if (scratch.ElementActive[i] != 0 && share > 0.0 && share > RoundingUlps * MachineEpsilon * LargestTerm(table, scratch, result, condensedCount, i))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The largest |a_ik n_k| of element <paramref name="i"/> over the retained gases and the condensed set.</summary>
    private static double LargestTerm(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      int condensedCount, int i)
    {
        var speciesCount = table.SpeciesCount;
        var largest = 0.0;
        for (var k = 0; k < table.GasCount; k++)
        {
            largest = KernelMath.Max(largest, Math.Abs(table.Stoichiometry[i * speciesCount + k] * result.Moles[k]));
        }

        for (var c = 0; c < condensedCount; c++)
        {
            var k = scratch.CondensedInSolution[c];
            largest = KernelMath.Max(largest, Math.Abs(table.Stoichiometry[i * speciesCount + k] * result.Moles[k]));
        }

        return largest;
    }

    /// <summary>
    /// True when the element vectors of the retained gases and of the condensed set without position
    /// <paramref name="excluded"/> (−1 for none) span less than the active elements: the Gram matrix
    /// <c>G_il = Σ_j a_ij a_lj</c> of those species, with a unit row for each absent element, is singular to
    /// <see cref="DenseSolver"/>.
    /// </summary>
    private static bool Dependent(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                  int condensedCount, int excluded)
    {
        var elementCount = table.ElementCount;
        for (var i = 0; i < elementCount * elementCount; i++)
        {
            scratch.Matrix[i] = 0.0;
        }

        for (var j = 0; j < table.GasCount; j++)
        {
            if (result.Moles[j] > 0.0)
            {
                AddSpecies(table, scratch, j);
            }
        }

        for (var c = 0; c < condensedCount; c++)
        {
            if (c != excluded)
            {
                AddSpecies(table, scratch, scratch.CondensedInSolution[c]);
            }
        }

        for (var i = 0; i < elementCount; i++)
        {
            scratch.RightHandSide[i] = 1.0;
            if (scratch.ElementActive[i] == 0)
            {
                scratch.Matrix[i * elementCount + i] = 1.0;
            }
        }

        return !DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, elementCount, elementCount);
    }

    /// <summary>One species' share of the Gram matrix.</summary>
    private static void AddSpecies(in SpeciesTableView table, in EquilibriumScratch scratch, int species)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            var ai = table.Stoichiometry[i * speciesCount + species];
            if (ai == 0.0)
            {
                continue;
            }

            for (var l = 0; l < elementCount; l++)
            {
                scratch.Matrix[i * elementCount + l] += ai * table.Stoichiometry[l * speciesCount + species];
            }
        }
    }
}
