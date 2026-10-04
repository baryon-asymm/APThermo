using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// Where a trace-gas pass starts from and what it puts back (BOOT.md, "The anchor and the entry", "The starts"): the entry saved
/// at the beginning of the pass and restored for each further start and when the pass fails, and the projection of the first
/// start, the weighted least squares that puts the gases the entry reports on their stationarities. Kernel-compatible; the entry
/// is kept field by field and never as a whole <see cref="IterationState"/> (BOOT.md, "No whole IterationState through ref").
/// </summary>
internal static class TraceGasStart
{
    /// <summary>The weight of a condensed stationarity in the least squares, against a gas's share of at most 1.</summary>
    private const double CondensedWeight = 1.0;

    /// <summary>The ridge toward the anchor, relative to the row's largest entry: a direction of π that no reported gas fixes keeps its anchor value.</summary>
    private const double Ridge = 1.0e-12;

    /// <summary>
    /// Saves the entry: the moles of the result into <c>Tie.LogMoles</c>, and the anchor of the multipliers. The anchor is
    /// <c>Tie.Elements.Multipliers</c>, which the verdict (seam (a)) or <c>BracketSeeds</c> (seams (b) and (b′)) leaves there;
    /// a multiplier that is not finite anchors at zero. It is put into the result's multipliers too, since
    /// <c>CaseSetup.Begin</c> zeroed them.
    /// </summary>
    public static void SaveEntry(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            scratch.Tie.LogMoles[j] = result.Moles[j];
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            var anchor = scratch.Tie.Elements.Multipliers[i];
            scratch.Tie.Elements.Multipliers[i] = Math.Abs(anchor) <= double.MaxValue ? anchor : 0.0;
            result.Multipliers[i] = scratch.Tie.Elements.Multipliers[i];
        }
    }

    /// <summary>
    /// Puts the entry back: the moles, the condensed set rebuilt from them in species order, the anchor multipliers, and the
    /// fields of the state the iteration moves (<paramref name="entryLogN"/>, <paramref name="entryTemperature"/>, the
    /// count of the condensed set, and the flags and memories a fresh <see cref="CaseSetup.Begin"/> leaves). The steps taken
    /// stay counted.
    /// </summary>
    public static void RestoreEntry(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                    ref IterationState state, double entryLogN, double entryTemperature)
    {
        var count = 0;
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = scratch.Tie.LogMoles[j];
            if (j >= table.GasCount && result.Moles[j] > 0.0 && count < ScratchLayout.MaxCondensedInSolution)
            {
                scratch.CondensedInSolution[count++] = j;
            }
        }

        for (var c = count; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = scratch.Tie.Elements.Multipliers[i];
        }

        state.LogN = entryLogN;
        state.Temperature = entryTemperature;
        state.CondensedCount = count;
        state.FunctionsAt = -1.0;
        state.SetChanges = 0;
        state.LastSwitchedOut = -1;
        state.LastRemovedForRange = -1;
        state.RetentionSecondStage = false;
        state.RetainedSetHeld = false;
        state.Tie.Active = false;
        state.TieReleased = false;
    }

    /// <summary>
    /// The projection: the multipliers that put the gases the result reports (moles above zero and above the second retention
    /// stage of ln n) closest to their stationarities, weighted by their share of the gas, with the condensed species of the
    /// solution as exact rows (weight <see cref="CondensedWeight"/>) and a ridge toward the multipliers in the result for the
    /// directions no gas fixes. <paramref name="logN"/> is the ln n the shares are taken against. The multipliers are left as
    /// they were when the normal equations are singular. <c>scratch.LogMoles</c> never selects the gases:
    /// <c>CaseSetup.Begin</c> writes an estimate there for every gas it was not given.
    /// </summary>
    public static void Project(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                               in SystemLayout layout, double logPressure, double logN)
    {
        for (var k = 0; k < layout.ElementCount * layout.Stride; k++)
        {
            scratch.Matrix[k] = 0.0;
        }

        for (var k = 0; k < layout.ElementCount; k++)
        {
            scratch.RightHandSide[k] = 0.0;
        }

        for (var j = 0; j < table.GasCount; j++)
        {
            if (!SpeciesMarks.InPlay(scratch, j) || !(result.Moles[j] > 0.0))
            {
                continue;
            }

            var logFraction = Math.Log(result.Moles[j]) - logN;
            if (logFraction > -EquilibriumSolver.SecondStageTraceThreshold)
            {
                Accumulate(table, scratch, layout, j, Math.Exp(logFraction), logFraction + scratch.GOverRT[j] + logPressure);
            }
        }

        for (var c = 0; c < layout.CondensedCount; c++)
        {
            var j = scratch.CondensedInSolution[c];
            Accumulate(table, scratch, layout, j, CondensedWeight, scratch.GOverRT[j]);
        }

        AnchorAndSolve(scratch, result, layout);
    }

    /// <summary>Adds the row of species <paramref name="j"/>, weight <paramref name="w"/> and target <paramref name="target"/>, to the normal equations: <c>w a_kj a_ij</c> into the matrix, <c>w a_kj target</c> into the right-hand side.</summary>
    private static void Accumulate(in SpeciesTableView table, in EquilibriumScratch scratch, in SystemLayout layout, int j,
                                   double w, double target)
    {
        for (var k = 0; k < layout.ElementCount; k++)
        {
            var akj = table.Stoichiometry[k * table.SpeciesCount + j];
            if (akj == 0.0)
            {
                continue;
            }

            for (var i = 0; i < layout.ElementCount; i++)
            {
                scratch.Matrix[k * layout.Stride + i] += w * akj * table.Stoichiometry[i * table.SpeciesCount + j];
            }

            scratch.RightHandSide[k] += w * akj * target;
        }
    }

    /// <summary>The unit row of an absent element, the ridge toward the anchor on every other row, and the solve: the multipliers become the solution when it exists.</summary>
    private static void AnchorAndSolve(in EquilibriumScratch scratch, in EquilibriumResult result, in SystemLayout layout)
    {
        var stride = layout.Stride;
        for (var k = 0; k < layout.ElementCount; k++)
        {
            if (scratch.ElementActive[k] == 0)
            {
                for (var i = 0; i < layout.ElementCount; i++)
                {
                    scratch.Matrix[k * stride + i] = 0.0;
                }

                scratch.Matrix[k * stride + k] = 1.0;
                scratch.RightHandSide[k] = result.Multipliers[k];
                continue;
            }

            var ridge = 0.0;
            for (var i = 0; i < layout.ElementCount; i++)
            {
                ridge = KernelMath.Max(ridge, Math.Abs(scratch.Matrix[k * stride + i]));
            }

            ridge *= Ridge;
            scratch.Matrix[k * stride + k] += ridge;
            scratch.RightHandSide[k] += ridge * result.Multipliers[k];
        }

        if (DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, layout.ElementCount, stride))
        {
            for (var k = 0; k < layout.ElementCount; k++)
            {
                result.Multipliers[k] = scratch.RightHandSide[k];
            }
        }
    }
}
