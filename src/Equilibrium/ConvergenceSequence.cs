using APThermo.Equilibrium.Condensed;
using APThermo.Equilibrium.Newton;
using APThermo.Thermo;

namespace APThermo.Equilibrium;

/// <summary>
/// One convergence sequence of an attempt (moved out of <see cref="EquilibriumSolver"/> on 2026-10-03, BOOT.md,
/// "Structure"): the Newton loop, then one change of the condensed set per convergence until the report's tests hold
/// with no further change, with rule A's release of the element tie and its way back. Kernel-compatible; reached from
/// one call site, <see cref="EquilibriumSolver.Solve"/>.
/// </summary>
internal static class ConvergenceSequence
{
    /// <summary>One convergence sequence: the Newton loop, then one change of the condensed set per convergence until the report's tests hold with no further change.</summary>
    public static CaseStatus Run(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                 in EquilibriumResult result, double logPressure, ref IterationState state)
    {
        CaseStatus status;
        var awaitingRelease = false;
        var released = default(IterationState);
        while (true)
        {
            status = NewtonIteration.Converge(table, problem, scratch, result, logPressure, ref state);

            // Rule A's way back (BOOT.md, "Release", the third pass of 2026-09-28, finding F1): when the one
            // convergence the release allowed on the element's own row fails, the tied iterate the release started
            // from is restored and the case is closed with the tie in force, as a tie that survived to the close. A
            // release that converges by the report's tests but leaves an element's balance beyond the node's invariant
            // (2026-10-03, `ElementBalance.WithinInvariant`, the close's own predicate over the moles the close would
            // judge) has failed at the one thing it is for, and is undone the same way.
            if (awaitingRelease)
            {
                awaitingRelease = false;
                if (status == CaseStatus.Ok)
                {
                    Composition.Refresh(table, scratch, result, ref state);
                }

                if (status != CaseStatus.Ok || !ElementBalance.WithinInvariant(table, problem, scratch, result))
                {
                    state.LogN = released.LogN;
                    state.Temperature = released.Temperature;
                    state.CondensedCount = released.CondensedCount;
                    state.Tie = released.Tie;
                    TieSnapshot.Restore(table, scratch, result, released.CondensedCount);
                    Composition.Refresh(table, scratch, result, ref state);
                    status = CaseStatus.Ok;
                    break;
                }
            }

            if (status != CaseStatus.Ok)
            {
                break;
            }

            // One change of the condensed set per convergence; a change means converging again.
            Composition.Refresh(table, scratch, result, ref state);
            if (!CondensedSet.Update(table, problem, scratch, result, ref state))
            {
                // Rule A's release (BOOT.md of the Newton node): once the settled set finds no further change and some
                // species of the sums breaks the tied combination, the tie is released, at most once per solve, and the settled
                // set converges again on the element's own row.
                if (state.Tie.Active && !state.TieReleased
                    && !ElementCoupling.Coupled(table, scratch, result, state.CondensedCount, state.Tie))
                {
                    released = state;
                    TieSnapshot.Save(table, scratch, result, state.CondensedCount);
                    state.Tie = default;
                    state.TieReleased = true;
                    awaitingRelease = true;
                    continue;
                }

                break;
            }

            state.SetChanges++;
            if (state.SetChanges > EquilibriumSolver.MaxCondensedSetChanges)
            {
                status = CaseStatus.NotConverged;
                break;
            }
        }

        return status;
    }
}
