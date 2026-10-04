using System.Runtime.CompilerServices;
using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// One trace-gas pass of a case (BOOT.md, "The sequence", "The starts"): the facade the <c>Recovery</c> node schedules after a
/// <c>GasRequired</c> verdict and as the final of a temperature bracket. It converges the case by the trace-gas Newton from
/// up to four starts, each from the entry, with the condensed set changing between convergences as in
/// <c>ConvergenceSequence</c>, and puts the entry back when no start converges. Kernel-compatible.
/// </summary>
internal static class TraceGasPass
{
    /// <summary>The starts, in order: the projection, the phase-one point with its own amounts, the same set by least squares, the point without its zero-level records, the gas basis.</summary>
    private const int StartCount = 5;

    /// <summary>The start from the phase-one point without its zero-level records: the last that needs the point.</summary>
    private const int DropLevelStart = 3;

    /// <summary>The start from the gas basis (2026-10-05): the program with every gas a column at unit fraction.</summary>
    private const int GasBasisStart = 4;

    /// <summary>
    /// One trace-gas pass from the entry <c>CaseSetup.Begin</c> prepared (the failed iterate, or the seed of a final), the anchor of
    /// the multipliers in <c>scratch.Tie.Elements.Multipliers</c>. <c>Ok</c> leaves what a converged
    /// <c>ConvergenceSequence</c> leaves: the temperature, <c>LogN</c>, the condensed set and its count, the moles refreshed at
    /// the second retention stage, no tie, the steps counted. Any other status puts the entry's moles and multipliers back
    /// (the steps stay counted) and is the last start's. Reached through this one method so that ILGPU compiles it once
    /// (root BOOT.md, compile size); one call site, <c>EquilibriumSolver.Solve</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static CaseStatus Run(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        var entryLogN = state.LogN;
        var entryTemperature = state.Temperature;
        TraceGasStart.SaveEntry(table, scratch, result);
        var status = CaseStatus.NotConverged;
        var havePoint = false;
        for (var start = 0; start < StartCount; start++)
        {
            if (start == 1)
            {
                havePoint = PhaseOneSeed.Fetch(table, problem, scratch, result, out _);
            }

            if (start > 0)
            {
                TraceGasStart.RestoreEntry(table, scratch, result, ref state, entryLogN, entryTemperature);
            }

            var dropLevel = start == DropLevelStart;
            if (dropLevel && !havePoint)
            {
                continue;
            }

            if (start > 0 && start < GasBasisStart && havePoint)
            {
                PhaseOneSeed.LoadPoint(table, scratch, result, ref state, dropLevel);
            }

            if (!Place(table, problem, scratch, result, ref state, start))
            {
                continue;
            }

            status = Sequence(table, problem, scratch, result, logPressure, ref state);
            if (status == CaseStatus.Ok)
            {
                return status;
            }
        }

        TraceGasStart.RestoreEntry(table, scratch, result, ref state, entryLogN, entryTemperature);
        return status;
    }

    /// <summary>
    /// Places start <paramref name="start"/> on the condensed set the state holds: the projection for the first, the phase-one
    /// placement for the second to the fourth (the second with the amounts the result holds, the others by least squares), the gas
    /// basis for the last (its own set). False when the start is skipped.
    /// </summary>
    private static bool Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                              in EquilibriumResult result, ref IterationState state, int start)
    {
        if (start == GasBasisStart)
        {
            return GasBasisSeed.Place(table, problem, scratch, result, ref state);
        }

        var frame = TraceGasFrame.AtStart(table, problem, state.CondensedCount, state.LogN, state.Temperature);
        if (start == 0)
        {
            TraceGasStart.Project(table, scratch, result, frame.Layout, frame.LogPressure, state.LogN);
            return true;
        }

        var n = PhaseOneSeed.Place(table, problem, scratch, result, frame, start > 1);
        if (!(n > 0.0))
        {
            return false;
        }

        state.LogN = Math.Log(n);
        return true;
    }

    /// <summary>
    /// Converges the placed start, refreshes the moles, and lets the condensed set change once per convergence: a change counts
    /// in <c>SetChanges</c> against <see cref="EquilibriumSolver.MaxCondensedSetChanges"/>, is followed by the projection from the present
    /// iterate and another convergence; no change ends the start <c>Ok</c>.
    /// </summary>
    private static CaseStatus Sequence(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                       in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        while (true)
        {
            var status = TraceGasIteration.Converge(table, problem, scratch, result, logPressure, ref state);
            if (status != CaseStatus.Ok)
            {
                return status;
            }

            Composition.Refresh(table, scratch, result, ref state);
            if (!CondensedSet.Update(table, problem, scratch, result, ref state))
            {
                return CaseStatus.Ok;
            }

            state.SetChanges++;
            if (state.SetChanges > EquilibriumSolver.MaxCondensedSetChanges)
            {
                return CaseStatus.NotConverged;
            }

            PhaseOneSeed.KeepRoomForTheGas(table, scratch, result, ref state);
            TraceGasStart.Project(table, scratch, result, TraceGasFrame.LayoutFor(table, problem, state.CondensedCount), logPressure, state.LogN);
        }
    }
}
