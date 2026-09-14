namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The remedies of RP-1311 section 3.6 for a Newton system that came out singular: reset the gaseous species that
/// vanished, twice; then drop the last condensed species. Kernel-compatible.
/// </summary>
internal static class SingularRemedies
{
    /// <summary>Section 3.6: the mole number a vanished gaseous species is reset to when the matrix comes out singular.</summary>
    private const double ResetMoles = 1.0e-6;

    private const int MaxSingularResets = 2;

    /// <summary>
    /// The remedies, in order: reset the gaseous species that vanished, twice; then drop the last condensed species. False
    /// when neither remedy is left and the case is singular.
    /// </summary>
    public static bool Recover(in EquilibriumScratch scratch, in EquilibriumResult result, int gasCount,
                               ref int singularResets, ref IterationState state)
    {
        if (singularResets < MaxSingularResets)
        {
            singularResets++;
            for (var j = 0; j < gasCount; j++)
            {
                if (CaseSetup.InPlay(scratch, j) && result.Moles[j] == 0.0)
                {
                    scratch.LogMoles[j] = Math.Log(ResetMoles);
                }
            }

            return true;
        }

        if (state.CondensedCount > 0)
        {
            state.CondensedCount = CondensedSet.Remove(scratch, result, state.CondensedCount, state.CondensedCount - 1);
            state.SetChanges++;
            singularResets = 0;
            return true;
        }

        return false;
    }
}
