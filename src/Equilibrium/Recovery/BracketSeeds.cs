using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// The compositions of the temperature bracket (BOOT.md, "The bracket"): the moles of an end saved into the
/// <c>BracketEnds</c> slice of the scratch, lower end first, a probe or the final attempt seeded from an end, the lever mix
/// of the two ends. The only writer of <c>BracketEnds</c>.
/// Kernel-compatible.
/// </summary>
internal static class BracketSeeds
{
    /// <summary>Saves the moles of the result as the lower end (<paramref name="below"/>) or the upper end.</summary>
    public static void Save(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, bool below)
    {
        var offset = below ? 0 : table.SpeciesCount;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.BracketEnds[offset + j] = result.Moles[j];
        }
    }

    /// <summary>Seeds the result's moles from the lower end (<paramref name="fromLow"/>) or the upper end.</summary>
    public static void Seed(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, bool fromLow)
    {
        var offset = fromLow ? 0 : table.SpeciesCount;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = scratch.BracketEnds[offset + j];
        }
    }

    /// <summary>
    /// The multipliers the result holds into <c>Tie.Elements.Multipliers</c>, where a trace-gas final takes its anchor from (seams (b)
    /// and (b′)): the last converged probe's, or the failed final's. The pass makes a value that is not finite zero.
    /// </summary>
    public static void Anchor(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result)
    {
        for (var i = 0; i < table.ElementCount; i++)
        {
            scratch.Tie.Elements.Multipliers[i] = result.Multipliers[i];
        }
    }

    /// <summary>The lever mix of the two ends into the result's moles: <c>(1 − f) n_low + f n_high</c>.</summary>
    public static void Lever(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double fraction)
    {
        var speciesCount = table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            result.Moles[j] = (1.0 - fraction) * scratch.BracketEnds[j] + fraction * scratch.BracketEnds[speciesCount + j];
        }
    }
}
