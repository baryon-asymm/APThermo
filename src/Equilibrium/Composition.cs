using AerospacePropellantThermodynamics.Thermo;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>
/// The composition side of one case: the four species functions at the case temperature, the gaseous mole numbers the trace
/// rule retains, and the sums over the composition that the system and the state are built from. Kernel-compatible.
/// </summary>
internal static class Composition
{
    /// <summary>Evaluates h°/RT, s°/R, cp°/R and g°/RT of every species of the table at one temperature into the scratch.</summary>
    public static void EvaluateFunctions(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var h = SpeciesFunctions.HOverRT(table, j, temperature);
            var s = SpeciesFunctions.SOverR(table, j, temperature);
            scratch.HOverRT[j] = h;
            scratch.SOverR[j] = s;
            scratch.CpOverR[j] = SpeciesFunctions.CpOverR(table, j, temperature);
            scratch.GOverRT[j] = h - s;
        }
    }

    /// <summary>
    /// The sums of a frozen mixture, whose composition is given rather than iterated: the gaseous logarithms come from the
    /// mole numbers themselves, there being no <c>LogMoles</c> to read. Leaves <c>N</c> at zero: the frozen path has no system.
    /// </summary>
    public static MixtureSums FrozenSums(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                         in IterationState state, double logPressure)
    {
        var gasCount = table.GasCount;
        var sums = new MixtureSums { LogN = state.LogN, LogPressure = logPressure, Temperature = state.Temperature };
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            sums.HOverRT += nj * scratch.HOverRT[j];
            sums.CpOverR += nj * scratch.CpOverR[j];
            if (j < gasCount)
            {
                sums.SumGas += nj;
                sums.SOverR += nj * (scratch.SOverR[j] - Math.Log(nj) + state.LogN - logPressure);
            }
            else
            {
                sums.SOverR += nj * scratch.SOverR[j];
                sums.CondensedMoles += nj;
            }
        }

        return sums;
    }
}
