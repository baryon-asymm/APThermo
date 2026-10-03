using APThermo.Thermo;

namespace APThermo.Equilibrium.Newton;

/// <summary>
/// Rule A's way back (BOOT.md, "Release", the third pass of 2026-09-28, finding F1): the tied converged iterate a
/// release starts from — the gaseous logarithms, the condensed set with its mole numbers, the Lagrange
/// multipliers and the coefficients of the tie's combination — copied into the scratch's own snapshot slices so that a re-convergence which fails on the
/// element's own row can be undone rather than reported as the release's own failure. <c>n</c>, <c>T</c>, the
/// condensed count and the tie itself are small enough to travel as the caller's own locals across the one Newton
/// call the release makes; only the per-species and per-slot arrays need the scratch. Kernel-compatible.
/// </summary>
internal static class TieSnapshot
{
    /// <summary>Copies the gaseous logarithms, the condensed set with its mole numbers, the multipliers and the tie's coefficients into the scratch's own snapshot slices.</summary>
    public static void Save(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            scratch.Tie.LogMoles[j] = scratch.LogMoles[j];
        }

        for (var c = 0; c < condensedCount; c++)
        {
            var species = scratch.CondensedInSolution[c];
            scratch.Tie.CondensedSet[c] = species;
            scratch.Tie.CondensedMoles[c] = result.Moles[species];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            scratch.Tie.Elements.Multipliers[i] = result.Multipliers[i];
            scratch.Tie.Elements.CoefficientSnapshot[i] = scratch.Tie.Elements.Coefficients[i];
        }
    }

    /// <summary>Restores the snapshot <see cref="Save"/> took, undoing whatever the failed re-convergence left in the scratch and the result.</summary>
    public static void Restore(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            scratch.LogMoles[j] = scratch.Tie.LogMoles[j];
        }

        for (var c = 0; c < condensedCount; c++)
        {
            var species = scratch.Tie.CondensedSet[c];
            scratch.CondensedInSolution[c] = species;
            result.Moles[species] = scratch.Tie.CondensedMoles[c];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = scratch.Tie.Elements.Multipliers[i];
            scratch.Tie.Elements.Coefficients[i] = scratch.Tie.Elements.CoefficientSnapshot[i];
        }
    }
}
