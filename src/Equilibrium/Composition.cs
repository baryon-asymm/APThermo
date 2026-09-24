using APThermo.Thermo;

namespace APThermo.Equilibrium;

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

    /// <summary>Evaluates the species functions when the temperature has moved since they were last evaluated.</summary>
    public static void Evaluate(in SpeciesTableView table, in EquilibriumScratch scratch, ref IterationState state)
    {
        if (state.FunctionsAt == state.Temperature)
        {
            return;
        }

        EvaluateFunctions(table, scratch, state.Temperature);
        state.FunctionsAt = state.Temperature;
    }

    /// <summary>
    /// The gaseous mole numbers the trace rule of section 3.2 retains: a species below the trace threshold is held at zero
    /// in the sums and keeps its logarithm for the next step. Returns their sum. The one place the rule is applied.
    /// </summary>
    public static double Retain(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, double logN)
    {
        var sumGas = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            var retained = SpeciesMarks.InPlay(scratch, j) && scratch.LogMoles[j] - logN > -EquilibriumSolver.TraceThreshold;
            result.Moles[j] = retained ? Math.Exp(scratch.LogMoles[j]) : 0.0;
            sumGas += result.Moles[j];
        }

        return sumGas;
    }

    /// <summary>The final iterate of a convergence: the species functions at the settled temperature and the retained moles.</summary>
    public static void Refresh(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                               ref IterationState state)
    {
        Evaluate(table, scratch, ref state);
        _ = Retain(table, scratch, result, state.LogN);
    }

    /// <summary>
    /// The sums of the current iterate over the whole composition, in ascending species order: what the reduced system and
    /// the state record are both built from. Retains the gaseous moles first, so that the sums and the reported moles are
    /// the same numbers.
    /// </summary>
    public static MixtureSums Sums(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                   double logN, double logPressure, double temperature)
    {
        var gasCount = table.GasCount;
        var sumGas = Retain(table, scratch, result, logN);
        var sums = new MixtureSums
        {
            LogN = logN,
            LogPressure = logPressure,
            Temperature = temperature,
            N = Math.Exp(logN),
            SumGas = sumGas,
        };
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            var nj = result.Moles[j];
            if (nj == 0.0)
            {
                continue;
            }

            sums.HOverRT += nj * scratch.HOverRT[j];
            sums.SOverR += j < gasCount
                ? nj * (scratch.SOverR[j] - scratch.LogMoles[j] + logN - logPressure)
                : nj * scratch.SOverR[j];
            sums.CpOverR += nj * scratch.CpOverR[j];
            if (j >= gasCount)
            {
                sums.CondensedMoles += nj;
            }
        }

        return sums;
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
