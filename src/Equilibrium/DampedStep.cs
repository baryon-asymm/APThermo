using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// One damped Newton step onto the iterate (RP-1311 chapter 3): the multipliers and the gaseous corrections of equation
/// (2.18), the control factor λ of equations (3.1)–(3.3) that limits every correction, and their application by equation
/// (3.4) with the temperature window. Kernel-compatible.
/// </summary>
internal static class DampedStep
{
    /// <summary>Equation (3.1): the weight of Δln n and Δln T in the largest correction, and the numerator of the factor.</summary>
    private const double ControlFactorWeight = 5.0;

    private const double ControlFactorLimit = 2.0;

    /// <summary>−ln(1e-4), the bound of equation (3.2) on the growth of a small species in one step.</summary>
    private const double SmallSpeciesBound = 9.2103404;

    /// <summary>
    /// The multipliers of this step, the gaseous corrections of equation (2.18) into the scratch, and the control factor λ
    /// of equations (3.1)–(3.3) that damps them all. Only growth is limited, as in the reference's code: a species on its
    /// way out may shrink by any factor in one step (BOOT.md).
    /// </summary>
    public static double ControlFactor(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                       in SystemLayout layout, in MixtureSums sums)
    {
        var speciesCount = table.SpeciesCount;
        var elementCount = layout.ElementCount;
        for (var i = 0; i < elementCount; i++)
        {
            result.Multipliers[i] = scratch.RightHandSide[i];
        }

        var deltaLogN = scratch.RightHandSide[layout.NRow];
        var deltaLogT = layout.IsTp ? 0.0 : scratch.RightHandSide[layout.TRow];
        var largest = Math.Max(ControlFactorWeight * Math.Abs(deltaLogT), ControlFactorWeight * Math.Abs(deltaLogN));
        var lambda2 = double.MaxValue;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (!CaseSetup.InPlay(scratch, j))
            {
                scratch.Corrections[j] = 0.0;
                continue;
            }

            var sum = deltaLogN + scratch.HOverRT[j] * deltaLogT;
            for (var i = 0; i < elementCount; i++)
            {
                sum += table.Stoichiometry[i * speciesCount + j] * result.Multipliers[i];
            }

            var mu = scratch.GOverRT[j] + scratch.LogMoles[j] - sums.LogN + sums.LogPressure;
            var delta = sum - mu;
            scratch.Corrections[j] = delta;
            var logFraction = scratch.LogMoles[j] - sums.LogN;
            if (delta <= 0.0)
            {
                continue;
            }

            if (logFraction > -EquilibriumSolver.TraceThreshold)
            {
                largest = Math.Max(largest, delta);
            }
            else if (delta - deltaLogN > 0.0)
            {
                lambda2 = Math.Min(lambda2, (-logFraction - SmallSpeciesBound) / (delta - deltaLogN));
            }
        }

        var lambda = 1.0;
        if (largest > 0.0)
        {
            lambda = Math.Min(lambda, ControlFactorLimit / largest);
        }

        return Math.Min(lambda, lambda2);
    }

    /// <summary>Equation (3.4): the damped corrections onto the iterate. False when the new temperature left the node's window.</summary>
    public static bool Apply(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                             in SystemLayout layout, double lambda, ref IterationState state)
    {
        for (var j = 0; j < table.GasCount; j++)
        {
            if (CaseSetup.InPlay(scratch, j))
            {
                scratch.LogMoles[j] += lambda * scratch.Corrections[j];
            }
        }

        for (var c = 0; c < state.CondensedCount; c++)
        {
            result.Moles[scratch.CondensedInSolution[c]] += lambda * scratch.RightHandSide[layout.ElementCount + c];
        }

        state.LogN += lambda * scratch.RightHandSide[layout.NRow];
        if (layout.IsTp)
        {
            return true;
        }

        state.Temperature = Math.Exp(Math.Log(state.Temperature) + lambda * scratch.RightHandSide[layout.TRow]);
        return state.Temperature >= EquilibriumSolver.MinTemperature && state.Temperature <= EquilibriumSolver.MaxTemperature;
    }
}
