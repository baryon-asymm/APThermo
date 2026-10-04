using System.Runtime.CompilerServices;
using APThermo.Thermo;

namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// The verdict on the gas phase of a tp case at its assigned temperature and pressure: the Gibbs minimum of the condensed
/// species alone (<see cref="CondensedSimplex"/>) and the tangent-plane certificate that no gas lowers it
/// (<see cref="TangentPlane"/>). A verdict is a proof or nothing (BOOT.md). Kernel-compatible; reached from one call site,
/// <c>Recovery.AttemptPlan.Next</c>.
/// </summary>
internal static class GasPhaseVerdict
{
    /// <summary>
    /// The verdict for the tp <paramref name="problem"/> at its temperature, reached through this one method so that ILGPU
    /// compiles it once (root BOOT.md, compile size). <see cref="GasVerdict.Gasless"/>: <c>result.Moles</c> holds the condensed
    /// minimum with every gas zero, <c>result.Multipliers</c> the certificate's, and <paramref name="figures"/> the minimum's
    /// enthalpy, entropy and heat capacity. Any other verdict: <c>result.Moles</c> and <c>result.Multipliers</c> are as on
    /// entry, bit for bit (the failed attempt's iterate, which a march warm-starts from), and the figures are default.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static GasVerdict Decide(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, out CondensedFigures figures)
    {
        Keep(table, scratch, result);
        var verdict = Judge(table, problem, scratch, result);
        figures = default;
        if (verdict == GasVerdict.Gasless)
        {
            figures = Figures(table, scratch, result, problem.Temperature);
        }
        else
        {
            Restore(table, scratch, result);
        }

        return verdict;
    }

    /// <summary>ln S of the gas phase at the multipliers in <c>result.Multipliers</c> (the certificate's quantity).</summary>
    public static double LogTangentSum(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logPressure) =>
        TangentPlane.LogTangentSum(table, scratch, result, logPressure);

    private static GasVerdict Judge(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result)
    {
        var temperature = problem.Temperature;
        Composition.EvaluateFunctions(table, scratch, temperature);
        var m = CondensedSimplex.Minimize(table, problem, scratch, temperature, out var stop);
        if (m == 0)
        {
            return stop;
        }

        CondensedSimplex.WriteMoles(table, scratch, result, problem, m);
        var directions = TangentPlane.Directions(table, problem, scratch, m);
        if (directions < 0)
        {
            return GasVerdict.Undecided;
        }

        var face = new Face(m, directions, TangentPlane.DirectionOffset(table), CaseSetup.LogPressure(problem), temperature);
        var logS = TangentPlane.Search(table, scratch, result, face);
        return logS < -TangentPlane.Margin ? GasVerdict.Gasless : logS > TangentPlane.Margin ? GasVerdict.GasRequired : GasVerdict.Undecided;
    }

    /// <summary>h, s and cp of the condensed minimum in <c>result.Moles</c> at the temperature.</summary>
    private static CondensedFigures Figures(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                            double temperature)
    {
        var enthalpy = 0.0;
        var entropy = 0.0;
        var heatCapacity = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            var moles = result.Moles[j];
            if (moles > 0.0)
            {
                enthalpy += moles * scratch.HOverRT[j];
                entropy += moles * scratch.SOverR[j];
                heatCapacity += moles * scratch.CpOverR[j];
            }
        }

        return new CondensedFigures
        {
            Enthalpy = PhysicalConstants.R * temperature * enthalpy,
            Entropy = PhysicalConstants.R * entropy,
            HeatCapacity = PhysicalConstants.R * heatCapacity,
        };
    }

    /// <summary>The failed attempt's moles and multipliers aside, in scratch the verdict does not otherwise use.</summary>
    private static void Keep(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.LogMoles[j] = result.Moles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            scratch.Tie.Elements.Multipliers[i] = result.Multipliers[i];
        }
    }

    private static void Restore(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = scratch.LogMoles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = scratch.Tie.Elements.Multipliers[i];
        }
    }
}
