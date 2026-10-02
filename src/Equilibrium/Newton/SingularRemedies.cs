using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;
using ILGPU;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Two rules come before the remedies of RP-1311 section 3.6 (BOOT.md, "Two rules come before the remedies above",
/// 2026-09-28): rule B resolves a condensed set whose last species is a linear combination of the others
/// (<see cref="CondensedDependency"/>), and rule A ties an element row found to duplicate another's
/// (<see cref="ElementCoupling"/>) instead of answering it with a removal. Only once both find nothing do the
/// report's own remedies run: reset the gaseous species that vanished, twice; then remove one condensed species
/// chosen by the row whose pivot failed (2026-09-28, cea 3.3.4 <c>equilibrium.f90:2001-2059</c>), as
/// <see cref="TargetedPosition"/> picks it, rather than always the last species of the solution. Kernel-compatible.
/// </summary>
internal static class SingularRemedies
{
    /// <summary>Section 3.6: the mole number a vanished gaseous species is reset to when the matrix comes out singular.</summary>
    private const double ResetMoles = 1.0e-6;

    private const int MaxSingularResets = 2;

    /// <summary>
    /// The remedies, in order: rule B's dependent-set ratio test; rule A's element tie; then reset the gaseous species
    /// that vanished, twice; then remove the species <see cref="TargetedPosition"/> names. False when nothing is left
    /// and the case is singular. A removal is a change of the condensed set: it restarts <paramref name="loop"/>'s
    /// step count, as every other change does (BOOT.md, the loop's bookkeeping, 2026-09-26), and marks the removed
    /// record for the anti-cycling skip, as a removal for range does (BOOT.md, the targeted singular remedy,
    /// 2026-09-28).
    /// </summary>
    public static bool Recover(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                               int failedRow, ref NewtonLoopState loop, ref IterationState state)
    {
        // Rule B (BOOT.md, 2026-09-28): a dependent condensed set is resolved by the ratio test before any other
        // remedy. A change of the set, and not marked for the anti-cycling skip: the species that stays is the one
        // just included, not one that cycled out.
        if (state.CondensedCount >= 2)
        {
            var leaving = CondensedDependency.LeavingPosition(table, scratch, result, state.CondensedCount);
            if (leaving >= 0)
            {
                state.CondensedCount = CondensedSet.Remove(scratch, result, state.CondensedCount, leaving);
                state.SetChanges++;
                loop.RecordSetChange();
                loop.SingularResets = 0;
                return true;
            }
        }

        // Rule A (BOOT.md, 2026-09-28): an element row that duplicates another's is tied, not answered by a removal —
        // at once when a condensed species of the solution holds the pair, otherwise only after the two resets below,
        // which still handle transient couplings.
        if (!state.Tie.Active && failedRow >= 0 && failedRow < table.ElementCount
            && (state.CondensedCount > 0 || loop.SingularResets >= MaxSingularResets))
        {
            var tie = ElementCoupling.Find(table, scratch, result, state.CondensedCount, failedRow);
            if (tie.Active
                && (loop.SingularResets >= MaxSingularResets || ElementCoupling.HeldByCondensed(table, scratch, state.CondensedCount, tie)))
            {
                state.Tie = tie;
                loop.SingularResets = 0;
                return true;
            }
        }

        if (loop.SingularResets < MaxSingularResets)
        {
            loop.SingularResets++;
            for (var j = 0; j < table.GasCount; j++)
            {
                if (SpeciesMarks.InPlay(scratch, j) && result.Moles[j] == 0.0)
                {
                    scratch.LogMoles[j] = Math.Log(ResetMoles);
                }
            }

            return true;
        }

        if (state.CondensedCount <= 0)
        {
            return false;
        }

        var position = TargetedPosition(table, scratch, result, failedRow, state.CondensedCount);
        var removed = scratch.CondensedInSolution[position];
        state.CondensedCount = CondensedSet.Remove(scratch, result, state.CondensedCount, position);
        CondensedSet.MarkRemoved(scratch, removed, ref state);
        state.SetChanges++;
        loop.RecordSetChange();
        loop.SingularResets = 0;
        return true;
    }

    /// <summary>
    /// The row whose pivot failed picks the species removed (BOOT.md, the targeted singular remedy, 2026-09-28; cea
    /// 3.3.4 <c>equilibrium.f90:2001-2059</c>): a failed condensed row removes the smallest-mole species of the
    /// solution that shares an element with the last one added; a failed element row removes the smallest-mole
    /// species that carries that element; any other row, or no such species, removes the last condensed species, as
    /// before. <paramref name="failedRow"/> is <see cref="DenseSolver.Solve(ArrayView{double}, ArrayView{double}, ArrayView{double}, int, int, out int)"/>'s
    /// own row numbering: element rows first, then one condensed row per slot of the solution.
    /// </summary>
    private static int TargetedPosition(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                        int failedRow, int condensedCount)
    {
        var elementCount = table.ElementCount;
        var last = condensedCount - 1;
        if (failedRow >= elementCount && failedRow < elementCount + condensedCount)
        {
            var position = SmallestSharingAnElement(table, scratch, result, condensedCount, scratch.CondensedInSolution[last]);
            return position >= 0 ? position : last;
        }

        if (failedRow >= 0 && failedRow < elementCount)
        {
            var position = SmallestCarryingElement(table, scratch, result, condensedCount, failedRow);
            return position >= 0 ? position : last;
        }

        return last;
    }

    /// <summary>The position, other than <paramref name="lastAdded"/>'s own, of the smallest-mole species in the solution sharing an element with it; −1 if none.</summary>
    private static int SmallestSharingAnElement(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                                int condensedCount, int lastAdded)
    {
        var best = -1;
        var bestMoles = double.PositiveInfinity;
        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (j == lastAdded || !SharesAnElement(table, j, lastAdded))
            {
                continue;
            }

            var moles = result.Moles[j];
            if (moles < bestMoles)
            {
                bestMoles = moles;
                best = c;
            }
        }

        return best;
    }

    /// <summary>The position of the smallest-mole species in the solution carrying <paramref name="element"/>; −1 if none.</summary>
    private static int SmallestCarryingElement(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                               int condensedCount, int element)
    {
        var speciesCount = table.SpeciesCount;
        var best = -1;
        var bestMoles = double.PositiveInfinity;
        for (var c = 0; c < condensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (table.Stoichiometry[element * speciesCount + j] == 0.0)
            {
                continue;
            }

            var moles = result.Moles[j];
            if (moles < bestMoles)
            {
                bestMoles = moles;
                best = c;
            }
        }

        return best;
    }

    /// <summary>True when two species have at least one element in common.</summary>
    private static bool SharesAnElement(in SpeciesTableView table, int j, int k)
    {
        var speciesCount = table.SpeciesCount;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (table.Stoichiometry[i * speciesCount + j] != 0.0 && table.Stoichiometry[i * speciesCount + k] != 0.0)
            {
                return true;
            }
        }

        return false;
    }
}
