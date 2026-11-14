using System.Runtime.CompilerServices;
using APThermo.Thermo;

namespace APThermo.Equilibrium.Condensed;

/// <summary>Stable same-formula choices where data ranges overlap, independently of their printed endpoints.</summary>
internal static class PhasePreference
{

    /// <summary>Replace one metastable eligible record, or pair it at a crossing in the overlap.</summary>
    public static bool Update(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                              in EquilibriumResult result, ref IterationState state)
    {
        for (var c = 0; c < state.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            var k = Preferred(table, scratch, state, j);
            if (k < 0)
            {
                continue;
            }

            if (CanPair(table, problem, scratch, j, k, state))
            {
                scratch.CondensedInSolution[state.CondensedCount++] = k;
                result.Moles[k] = 0.0;
            }
            else
            {
                scratch.CondensedInSolution[c] = k;
                result.Moles[k] = result.Moles[j];
                result.Moles[j] = 0.0;
                state.LastSwitchedOut = j;
            }

            return true;
        }

        return false;
    }

    /// <summary>The lowest-potential eligible alternative that improves on a held in-range record.</summary>
    private static int Preferred(in SpeciesTableView table, in EquilibriumScratch scratch, in IterationState state, int j)
    {
        if (!PhaseGeometry.InEffectiveRange(table, scratch, j, state.Temperature))
        {
            return -1;
        }

        var best = -1;
        var gain = CondensedSet.ResidualGainLimit;
        for (var k = table.GasCount; k < table.SpeciesCount; k++)
        {
            if (k != j && SpeciesMarks.InPlay(scratch, k) && !PhaseGeometry.InSolution(scratch, state.CondensedCount, k)
                && PhaseGeometry.SameFormula(table, j, k) && PhaseGeometry.InEffectiveRange(table, scratch, k, state.Temperature)
                && scratch.GOverRT[j] - scratch.GOverRT[k] > gain)
            {
                best = k;
                gain = scratch.GOverRT[j] - scratch.GOverRT[k];
            }
        }

        return best;
    }

    /// <summary>An overlap crossing may pin only under the existing capacity, latent-heat and transition-window rules.</summary>
    private static bool CanPair(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                int j, int k, in IterationState state)
    {
        if (problem.Kind == ProblemKind.AssignedTemperaturePressure || state.CondensedCount >= ScratchLayout.MaxCondensedInSolution
            || PhaseGeometry.PartnerInSolution(table, scratch, state.CondensedCount, j) >= 0)
        {
            return false;
        }

        var crossingTable = table;
        var crossing = Crossing(crossingTable, j, k);
        if (!(crossing > 0.0))
        {
            return false;
        }

        var latent = Math.Abs(SpeciesFunctions.HOverRT(table, j, crossing) - SpeciesFunctions.HOverRT(table, k, crossing));
        return latent >= SpeciesFunctions.LatentHeatThreshold
            && (Math.Abs(state.Temperature - crossing) <= CondensedSet.PhaseTransitionWindow || k == state.LastSwitchedOut);
    }

    /// <summary>A sign-bracketed crossing inside the common raw interval; zero when none is bracketed.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static double Crossing(in SpeciesTableView table, int j, int k)
    {
        var low = KernelMath.Max(SpeciesFunctions.RecordLow(table, j), SpeciesFunctions.RecordLow(table, k));
        var high = KernelMath.Min(SpeciesFunctions.RecordHigh(table, j), SpeciesFunctions.RecordHigh(table, k));
        if (!(high > low))
        {
            return 0.0;
        }

        var atLow = Difference(table, j, k, low);
        var atHigh = Difference(table, j, k, high);
        if (atLow == 0.0 || atHigh == 0.0)
        {
            return atLow == 0.0 ? low : high;
        }

        if ((atLow < 0.0) == (atHigh < 0.0))
        {
            return 0.0;
        }

        for (var step = 0; step < 64; step++)
        {
            var middle = low + (high - low) * 0.5;
            if (middle == low || middle == high)
            {
                break;
            }

            var value = Difference(table, j, k, middle);
            if ((value < 0.0) == (atLow < 0.0))
            {
                low = middle;
                atLow = value;
            }
            else
            {
                high = middle;
            }
        }

        return low + (high - low) * 0.5;
    }

    /// <summary>The Gibbs difference of the two records; the species functions own their polynomial arithmetic.</summary>
    private static double Difference(in SpeciesTableView table, int j, int k, double temperature) =>
        SpeciesFunctions.GOverRT(table, j, temperature) - SpeciesFunctions.GOverRT(table, k, temperature);
}
