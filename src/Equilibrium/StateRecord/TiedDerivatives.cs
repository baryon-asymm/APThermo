using APThermo.Equilibrium.Newton;
using APThermo.Thermo;

namespace APThermo.Equilibrium.StateRecord;

/// <summary>
/// The close's entry to the derivative system (BOOT.md, 2026-10-04, for 0.2.2): <see cref="DerivativeSystem.Solve"/> once,
/// and once more with the tie that <see cref="ElementCoupling.Find"/> shows over the species of the sums when the first
/// solve was singular and no tie is in force. A convergence that held no tie (the trace-gas pass, or the reduced iteration
/// where the carriers of a direction of the multipliers sit below the retention threshold) can leave a direction of the
/// element rows that no species of the sums sees; fixing its multiplier derivative at zero, as a surviving tie does, leaves
/// every derivative the state reports exact. Kernel-compatible; it changes only a state whose derivatives were unsolved.
/// </summary>
internal static class TiedDerivatives
{
    /// <summary>
    /// <see cref="DerivativeSystem.Solve"/>; when it is unsolved and the case's tie is not active, the first active element
    /// whose row <see cref="ElementCoupling.Find"/> expresses over the species of the sums is recorded as the tie in
    /// <paramref name="state"/> (field by field, as a surviving tie is) and the system is solved again.
    /// </summary>
    public static Derivatives Solve(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    ref IterationState state, int stride, in MixtureSums sums)
    {
        var derivatives = DerivativeSystem.Solve(table, scratch, result, state, stride, sums);
        if (derivatives.Solved || state.Tie.Active)
        {
            return derivatives;
        }

        for (var k = 0; k < table.ElementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                continue;
            }

            var tie = ElementCoupling.Find(table, scratch, result, state.CondensedCount, k);
            if (!tie.Active)
            {
                continue;
            }

            state.Tie.Active = true;
            state.Tie.Element = tie.Element;
            return DerivativeSystem.Solve(table, scratch, result, state, stride, sums);
        }

        return derivatives;
    }
}
