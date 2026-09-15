using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// Which condensed records stand in the solution, decided once between two convergences: a record whose mole number turned
/// negative is removed; a record beyond its effective range pairs with, or is switched for, the record of its formula on the
/// other side of their crossing; otherwise the inclusion test of RP-1311 section 3.4 adds the best candidate. One change per
/// convergence, in that order (the condensed-species rule of the node's BOOT.md). Kernel-compatible.
/// </summary>
internal static class CondensedSet
{
    /// <summary>K, section 3.5: closer than this to a transition, both records of the pair are kept and the temperature settles at it.</summary>
    private const double PhaseTransitionWindow = 50.0;

    /// <summary>
    /// A positive per-mole inclusion gain this far above zero is real at a converged state; below it is the rounding of the
    /// polished multipliers. Used only for the stand-down honesty check at an Ok exit.
    /// </summary>
    private const double ResidualGainLimit = 1.0e-9;

    /// <summary>Applies at most one change to the set and says whether it changed; the caller converges again if it did.</summary>
    public static bool Update(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                              in EquilibriumResult result, ref IterationState state)
    {
        var skipInclusion = state.LastRemovedForRange;
        state.LastRemovedForRange = -1;
        return RemoveNegative(scratch, result, ref state)
               || OutOfRange(table, problem, scratch, result, ref state)
               || Include(table, scratch, result, skipInclusion, ref state);
    }

    /// <summary>
    /// The per-mole gain of adding a condensed species at the current multipliers, Σ a_ij π_i − g_j/RT (RP-1311 section 3.4).
    /// The one source: the inclusion test ranks candidates by it, and the honesty guard of an Ok exit refuses a state that
    /// leaves a positive one out. The guard is only honest while it agrees with the test to the last operation.
    /// </summary>
    public static double InclusionGain(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int species)
    {
        var speciesCount = table.SpeciesCount;
        var gain = -scratch.GOverRT[species];
        for (var i = 0; i < table.ElementCount; i++)
        {
            gain += table.Stoichiometry[i * speciesCount + species] * result.Multipliers[i];
        }

        return gain;
    }

    /// <summary>
    /// True when a record stood down by the anti-cycling rule would qualify for inclusion at the final state: inside its
    /// effective range, no phase partner in the solution, and a gain above the rounding of the converged multipliers. An Ok
    /// status must not hide such a candidate (BOOT.md); the caller turns it into NotConverged.
    /// </summary>
    public static bool StoodDownCandidateRemains(in SpeciesTableView table, in EquilibriumScratch scratch,
                                                 in EquilibriumResult result, in IterationState state)
    {
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.StoodDown || PhaseGeometry.InSolution(scratch, state.CondensedCount, j))
            {
                continue;
            }

            if (!PhaseGeometry.InEffectiveRange(table, scratch, j, state.Temperature)
                || PhaseGeometry.PartnerInSolution(table, scratch, state.CondensedCount, j) >= 0)
            {
                continue;
            }

            if (InclusionGain(table, scratch, result, j) > ResidualGainLimit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Takes the condensed species at the given position out of the solution; returns the new count.</summary>
    public static int Remove(in EquilibriumScratch scratch, in EquilibriumResult result, int condensedCount, int position)
    {
        result.Moles[scratch.CondensedInSolution[position]] = 0.0;
        for (var d = position; d + 1 < condensedCount; d++)
        {
            scratch.CondensedInSolution[d] = scratch.CondensedInSolution[d + 1];
        }

        condensedCount--;
        scratch.CondensedInSolution[condensedCount] = -1;
        return condensedCount;
    }

    /// <summary>Rule 1: the first record whose mole number turned negative leaves the solution.</summary>
    private static bool RemoveNegative(in EquilibriumScratch scratch, in EquilibriumResult result, ref IterationState state)
    {
        for (var c = 0; c < state.CondensedCount; c++)
        {
            if (result.Moles[scratch.CondensedInSolution[c]] < 0.0)
            {
                state.CondensedCount = Remove(scratch, result, state.CondensedCount, c);
                return true;
            }
        }

        return false;
    }

    /// <summary>Rule 2: the first record beyond its effective range changes phase. A record whose partner stands beside it is exempt.</summary>
    private static bool OutOfRange(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result, ref IterationState state)
    {
        for (var c = 0; c < state.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            if (PhaseGeometry.InEffectiveRange(table, scratch, j, state.Temperature)
                || PhaseGeometry.PartnerInSolution(table, scratch, state.CondensedCount, j) >= 0)
            {
                continue;
            }

            Resolve(table, problem, scratch, result, c, ref state);
            return true;
        }

        return false;
    }

    /// <summary>Pair the record with its neighbour across the crossing, switch it for the record that holds the temperature, or drop it.</summary>
    private static void Resolve(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                in EquilibriumResult result, int position, ref IterationState state)
    {
        var j = scratch.CondensedInSolution[position];
        var above = state.Temperature > PhaseGeometry.EffectiveHigh(table, scratch, j);
        var adjacent = PhaseGeometry.Adjacent(table, scratch, j, above);
        var k = PhaseGeometry.PhaseAt(table, scratch, state.CondensedCount, j, state.Temperature);
        if (k < 0 && adjacent >= 0 && !PhaseGeometry.InSolution(scratch, state.CondensedCount, adjacent))
        {
            k = adjacent;
        }

        if (Pinnable(table, problem, j, k >= 0 && k == adjacent ? k : -1, above, state))
        {
            // The candidate enters at zero moles, both records stay, and the next convergence settles the temperature at T*.
            scratch.CondensedInSolution[state.CondensedCount++] = k;
            result.Moles[k] = 0.0;
            return;
        }

        state.CondensedCount = Remove(scratch, result, state.CondensedCount, position);
        if (k >= 0 && state.CondensedCount < ScratchLayout.MaxCondensedInSolution)
        {
            scratch.CondensedInSolution[state.CondensedCount++] = k;
            result.Moles[k] = 0.0;
            state.LastSwitchedOut = j;
        }
        else if (k < 0)
        {
            StandDown(scratch, j, ref state);
        }
    }

    /// <summary>
    /// Whether the record and its neighbour across a shared bound may stand in the solution together: the temperature must be
    /// a variable, the latent heat at the bound real, the set must have room, and the state must be within the transition
    /// window of the crossing — or the neighbour must be the record switched out last, which is the overshoot the switch
    /// memory exists for (BOOT.md).
    /// </summary>
    private static bool Pinnable(in SpeciesTableView table, in EquilibriumProblem problem, int j, int neighbour, bool above,
                                 in IterationState state)
    {
        if (neighbour < 0 || problem.Kind == ProblemKind.AssignedTemperaturePressure
            || state.CondensedCount >= ScratchLayout.MaxCondensedInSolution)
        {
            return false;
        }

        var bound = above ? SpeciesFunctions.RecordHigh(table, j) : SpeciesFunctions.RecordLow(table, j);
        var latent = Math.Abs(SpeciesFunctions.HOverRT(table, j, bound) - SpeciesFunctions.HOverRT(table, neighbour, bound));
        if (latent < SpeciesFunctions.LatentHeatThreshold)
        {
            return false;
        }

        var crossing = PhaseGeometry.Crossing(table, j, neighbour, bound);
        return Math.Abs(state.Temperature - crossing) <= PhaseTransitionWindow || neighbour == state.LastSwitchedOut;
    }

    /// <summary>
    /// Anti-cycling (BOOT.md): the first escape through its own bound is forgiven and only skipped for one inclusion pass;
    /// a record that escapes twice in one solve chases a temperature the solution keeps leaving, and stands down for the
    /// rest of it.
    /// </summary>
    private static void StandDown(in EquilibriumScratch scratch, int j, ref IterationState state)
    {
        if (SpeciesMarks.Of(scratch, j) == SpeciesMark.ForgivenOnce)
        {
            SpeciesMarks.Set(scratch, j, SpeciesMark.StoodDown);
        }
        else
        {
            SpeciesMarks.Set(scratch, j, SpeciesMark.ForgivenOnce);
            state.LastRemovedForRange = j;
        }
    }

    /// <summary>
    /// Rule 3: the candidate with the largest positive per-mole gain enters, one at a time. A species whose formula already
    /// stands in the solution is passed over — a pair is completed by rule 2, never by inclusion — and so is the record just
    /// removed for its range, unless it is the only positive candidate, in which case no equilibrium is lost by taking it.
    /// </summary>
    private static bool Include(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                               int skipInclusion, ref IterationState state)
    {
        if (state.CondensedCount >= ScratchLayout.MaxCondensedInSolution)
        {
            return false;
        }

        var best = -1;
        var bestGain = 0.0;
        var skippedGain = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j) || PhaseGeometry.InSolution(scratch, state.CondensedCount, j)
                || !PhaseGeometry.InEffectiveRange(table, scratch, j, state.Temperature)
                || PhaseGeometry.PartnerInSolution(table, scratch, state.CondensedCount, j) >= 0)
            {
                continue;
            }

            var gain = InclusionGain(table, scratch, result, j);
            if (j == skipInclusion)
            {
                skippedGain = gain;
                continue;
            }

            if (gain > bestGain)
            {
                bestGain = gain;
                best = j;
            }
        }

        if (best < 0 && skippedGain > 0.0)
        {
            best = skipInclusion;
        }

        if (best < 0)
        {
            return false;
        }

        scratch.CondensedInSolution[state.CondensedCount++] = best;
        result.Moles[best] = 0.0;
        return true;
    }
}
