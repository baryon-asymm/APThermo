using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// What a case needs before its first Newton step: the input checked, the element and species marks written, and the initial
/// estimate laid into the scratch — the defaults of RP-1311 section 3.1 or the caller's previous solution. Kernel-compatible.
/// </summary>
internal static class CaseSetup
{
    /// <summary>Pa: the thermodynamic data are for 1 bar, so this is the p° of the mixing terms.</summary>
    private const double StandardPressure = 1.0e5;

    /// <summary>K, section 3.1: the temperature an hp or sp case starts from when the caller gives none.</summary>
    private const double DefaultTemperatureEstimate = 3800.0;

    /// <summary>Section 3.1: the total gaseous moles per kilogram a case starts from, shared out over the active species.</summary>
    private const double InitialGaseousMoles = 0.1;

    /// <summary>One e-fold below the trace threshold: where a gaseous species the caller's estimate does not mention starts.</summary>
    private const double UnestimatedOffset = 1.0;

    /// <summary>ln(p/p°) of the case.</summary>
    public static double LogPressure(in EquilibriumProblem problem) => Math.Log(problem.Pressure / StandardPressure);

    /// <summary>The assigned temperature for tp, the caller's estimate for hp and sp, or the default of section 3.1.</summary>
    public static double InitialTemperature(in EquilibriumProblem problem) =>
        problem.Kind == ProblemKind.AssignedTemperaturePressure
            ? problem.Temperature
            : (problem.Temperature > 0.0 ? problem.Temperature : DefaultTemperatureEstimate);

    /// <summary>
    /// Checks the input, masks the absent elements and their species, and writes the initial estimate and the carried state.
    /// InvalidInput leaves every output view untouched; the caller has already written that status.
    /// </summary>
    public static CaseStatus Begin(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                   in EquilibriumResult result, EstimateSource source, ref IterationState state)
    {
        if (table.SpeciesCount <= 0 || table.ElementCount <= 0 || !(problem.Pressure > 0.0)
            || table.ElementCount > TableLimits.MaxElements)
        {
            return CaseStatus.InvalidInput;
        }

        if (problem.Kind == ProblemKind.AssignedTemperaturePressure && !(problem.Temperature > 0.0))
        {
            return CaseStatus.InvalidInput;
        }

        if (!MaskElements(table, problem, scratch))
        {
            return CaseStatus.InvalidInput;
        }

        var activeGases = MaskSpecies(table, scratch);
        if (activeGases == 0)
        {
            return CaseStatus.InvalidInput;
        }

        state = new IterationState
        {
            Temperature = InitialTemperature(problem),
            FunctionsAt = -1.0,
            LastSwitchedOut = -1,
            LastRemovedForRange = -1,
        };
        Estimate(table, scratch, result, source, activeGases, ref state);
        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        return CaseStatus.Ok;
    }

    /// <summary>An element with zero abundance is a mask, not an error; a negative one, or no element at all, is invalid input.</summary>
    private static bool MaskElements(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch)
    {
        var anyElement = false;
        for (var i = 0; i < table.ElementCount; i++)
        {
            var b = problem.ElementMoles[i];
            if (!(b >= 0.0))
            {
                return false;
            }

            scratch.ElementActive[i] = b > 0.0 ? 1 : 0;
            anyElement |= b > 0.0;
        }

        return anyElement;
    }

    /// <summary>An absent element marks out every species that contains it; returns how many gaseous species are left in play.</summary>
    private static int MaskSpecies(in SpeciesTableView table, in EquilibriumScratch scratch)
    {
        var speciesCount = table.SpeciesCount;
        var activeGases = 0;
        for (var j = 0; j < speciesCount; j++)
        {
            var present = true;
            for (var i = 0; i < table.ElementCount; i++)
            {
                if (table.Stoichiometry[i * speciesCount + j] != 0.0 && scratch.ElementActive[i] == 0)
                {
                    present = false;
                }
            }

            SpeciesMarks.Set(scratch, j, present ? SpeciesMark.Active : SpeciesMark.Absent);
            if (present && j < table.GasCount)
            {
                activeGases++;
            }
        }

        return activeGases;
    }

    private static void Estimate(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                 EstimateSource source, int activeGases, ref IterationState state)
    {
        for (var c = 0; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        if (source == EstimateSource.PreviousSolution)
        {
            FromPreviousSolution(table, scratch, result, ref state);
        }
        else
        {
            FromDefaults(table, scratch, result, activeGases, ref state);
        }
    }

    /// <summary>Section 3.1: every active gaseous species gets an equal share of the initial moles, the condensed ones none.</summary>
    private static void FromDefaults(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                     int activeGases, ref IterationState state)
    {
        state.LogN = Math.Log(InitialGaseousMoles);
        var each = Math.Log(InitialGaseousMoles / activeGases);
        for (var j = 0; j < table.GasCount; j++)
        {
            scratch.LogMoles[j] = each;
        }

        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = 0.0;
        }
    }

    /// <summary>
    /// The caller's previous solution: the gaseous moles are its own, a species it does not carry starts one e-fold below
    /// the trace threshold, and its condensed species enter the set (the nozzle hands its last station over this way).
    /// </summary>
    private static void FromPreviousSolution(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                             ref IterationState state)
    {
        var gasCount = table.GasCount;
        var estimate = 0.0;
        for (var j = 0; j < gasCount; j++)
        {
            if (SpeciesMarks.InPlay(scratch, j) && result.Moles[j] > 0.0)
            {
                estimate += result.Moles[j];
            }
        }

        if (!(estimate > 0.0))
        {
            estimate = InitialGaseousMoles;
        }

        state.LogN = Math.Log(estimate);
        for (var j = 0; j < gasCount; j++)
        {
            scratch.LogMoles[j] = SpeciesMarks.InPlay(scratch, j) && result.Moles[j] > 0.0
                ? Math.Log(result.Moles[j])
                : state.LogN - EquilibriumSolver.TraceThreshold - UnestimatedOffset;
        }

        for (var j = gasCount; j < table.SpeciesCount; j++)
        {
            if (SpeciesMarks.InPlay(scratch, j) && result.Moles[j] > 0.0 && state.CondensedCount < ScratchLayout.MaxCondensedInSolution)
            {
                scratch.CondensedInSolution[state.CondensedCount++] = j;
            }
            else
            {
                result.Moles[j] = 0.0;
            }
        }
    }
}
