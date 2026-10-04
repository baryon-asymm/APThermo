using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// The compositions of the temperature bracket (BOOT.md, "The bracket"): the moles of an end saved into the
/// <c>BracketEnds</c> slice of the scratch, lower end first, a probe or the final attempt seeded from an end, the lever mix
/// of the two ends, and the highest record floor below a temperature. The only writer of <c>BracketEnds</c>.
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

    /// <summary>The lever mix of the two ends into the result's moles: <c>(1 − f) n_low + f n_high</c>.</summary>
    public static void Lever(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double fraction)
    {
        var speciesCount = table.SpeciesCount;
        for (var j = 0; j < speciesCount; j++)
        {
            result.Moles[j] = (1.0 - fraction) * scratch.BracketEnds[j] + fraction * scratch.BracketEnds[speciesCount + j];
        }
    }

    /// <summary>
    /// The highest lowest-record-bound (times <see cref="TemperatureBracket.FloorMargin"/>) below <paramref name="temperature"/>
    /// over the condensed species the result holds; 0 when none.
    /// </summary>
    public static double HighestFloorBelow(in SpeciesTableView table, in EquilibriumResult result, double temperature)
    {
        var floor = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (result.Moles[j] > 0.0)
            {
                var low = SpeciesFunctions.RecordLow(table, j) * TemperatureBracket.FloorMargin;
                if (low < temperature / TemperatureBracket.FloorMargin)
                {
                    floor = KernelMath.Max(floor, low);
                }
            }
        }

        return floor;
    }
}
