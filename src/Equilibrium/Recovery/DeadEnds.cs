using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Recovery;

/// <summary>
/// The dead-end floors of a table (BOOT.md, "Dead-end floors"): the lowest bound of a condensed record that is neither the
/// gas data floor, where the record is open below, nor the upper bound of another record of its formula. Across such a
/// floor the candidate set gains or loses a phase at once, so the equilibrium enthalpy and entropy of the case jump there and
/// an hp or sp target can have a root on either side. Kernel-compatible.
/// </summary>
internal static class DeadEnds
{
    /// <summary>The relative tolerance of the range comparison, <c>PhaseGeometry</c>'s.</summary>
    private const double RangeMargin = 1.0 + 1.0e-9;

    /// <summary>Whether the lowest bound of condensed record <paramref name="species"/> is a dead-end floor.</summary>
    public static bool IsDeadEnd(in SpeciesTableView table, int species)
    {
        var low = SpeciesFunctions.RecordLow(table, species);
        if (low == EquilibriumSolver.GasDataFloor)
        {
            return false;
        }

        for (var k = table.GasCount; k < table.SpeciesCount; k++)
        {
            if (k != species && SpeciesFunctions.RecordHigh(table, k) == low && PhaseGeometry.SameFormula(table, species, k))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The highest dead-end floor above <paramref name="temperature"/> of a record whose elements are present and whose
    /// inclusion gain at the final multipliers is positive: the gas is supersaturated with respect to a phase the data
    /// stop short of, and a state of the same h or s holding it may lie above its floor. 0 when there is none.
    /// </summary>
    public static double FloorAbove(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double temperature)
    {
        var floor = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            var low = SpeciesFunctions.RecordLow(table, j);
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent && temperature * RangeMargin < low && IsDeadEnd(table, j)
                && CondensedSet.InclusionGain(table, scratch, result, j) > 0.0)
            {
                floor = KernelMath.Max(floor, low);
            }
        }

        return floor;
    }

    /// <summary>
    /// The highest dead-end floor below <paramref name="temperature"/> of a record whose elements are present, held by the
    /// probe or not (a probe of the gas alone may stand just above the floor of the phase that is to join), which a
    /// downward step lands on instead of crossing; 0 when there is none. A record adjoined by a lower record of its formula
    /// is no floor: the pinned plateau keeps the assigned property continuous across it.
    /// </summary>
    public static double FloorBelow(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature)
    {
        var floor = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            var low = SpeciesFunctions.RecordLow(table, j);
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent && low * RangeMargin < temperature && IsDeadEnd(table, j))
            {
                floor = KernelMath.Max(floor, low);
            }
        }

        return floor;
    }
}
