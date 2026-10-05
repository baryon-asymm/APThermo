using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The fifth start of a pass (BOOT.md, "The starts", "The gas basis", 2026-10-05), after Reynolds's STANJAN initializer: the
/// phase-one program with every gas a column at unit fraction (cost g/RT + ln(p/p°)), whose optimal basis holds the condensed
/// records and the gases that carry what the records cannot. The condensed set is the condensed basics, a basic at zero level
/// included at zero moles (the balancing phase: CaO beside CaCO3 under an excess of oxygen); π solves the stationarities of the
/// basis, a basic gas at its share of the basic gas (<c>ln x = ln(v/n)</c>), one at zero level at unit fraction; n is the basic gas.
/// The excess is then carried by the gas on its own branch (<c>n F(π) = b*</c> with <c>F</c> of the sign of <c>b*</c>), and every
/// gas amount follows from π at the first evaluation. When the basis holds records alone while their multipliers put the gas phase
/// above unit sum, the gas mixture enters as one column (BOOT.md, "the mixture column", 2026-10-05). Kernel-compatible; it borrows the
/// matrix scratch and <c>Tie.CondensedSet</c> and <c>Tie.CondensedMoles</c>, as the phase-one starts do.
/// </summary>
internal static class GasBasisSeed
{
    /// <summary>
    /// Places the start: the condensed basics into <c>scratch.CondensedInSolution</c> in species order and their amounts into the
    /// result's moles (every other mole zero), π into the result's multipliers (zero for an absent element), ln n into the state.
    /// False, the start skipped, when the program does not complete, an artificial column stays basic, no gas is basic with a
    /// positive amount and the gas mixture does not enter, or the basis is singular.
    /// </summary>
    public static bool Place(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, ref IterationState state)
    {
        if (!PhaseOneSeed.Point(table, problem, scratch, result, true, out _))
        {
            return false;
        }

        var rows = ActiveElements(table, scratch);
        var n = BasicGas(table, scratch, rows);
        var placed = n > 0.0
            ? Multipliers(table, problem, scratch, result, rows, n)
            : EnterTheMixture(table, problem, scratch, result, rows, out n);
        if (!placed)
        {
            return false;
        }

        state.CondensedCount = CondensedBasics(table, scratch, result, rows);
        state.LogN = Math.Log(n);
        return true;
    }

    /// <summary>The number of rows of the program: the active elements.</summary>
    private static int ActiveElements(in SpeciesTableView table, in EquilibriumScratch scratch)
    {
        var rows = 0;
        for (var i = 0; i < table.ElementCount; i++)
        {
            rows += scratch.ElementActive[i] != 0 ? 1 : 0;
        }

        return rows;
    }

    /// <summary>The sum of the basic gases' amounts; zero when an artificial column stays basic.</summary>
    private static double BasicGas(in SpeciesTableView table, in EquilibriumScratch scratch, int rows)
    {
        var n = 0.0;
        for (var k = 0; k < rows; k++)
        {
            var column = scratch.CondensedInSolution[k];
            if (column < 0)
            {
                return 0.0;
            }

            if (column < table.GasCount && scratch.Corrections[k] > 0.0)
            {
                n += scratch.Corrections[k];
            }
        }

        return n;
    }

    /// <summary>
    /// π from <c>Σ_i a_ik π_i = g_k/RT</c> for a condensed basic and <c>g_k/RT + ln(p/p°) + ln(v_k/n)</c> for a basic gas (unit fraction
    /// when its amount is not positive), over the <paramref name="rows"/> rows of the program (<c>Tie.CondensedSet</c>: row → element); false when singular.
    /// </summary>
    private static bool Multipliers(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                    in EquilibriumResult result, int rows, double n)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var logPressure = CaseSetup.LogPressure(problem);
        for (var k = 0; k < rows; k++)
        {
            var column = scratch.CondensedInSolution[k];
            for (var r = 0; r < rows; r++)
            {
                scratch.Matrix[k * stride + r] = table.Stoichiometry[scratch.Tie.CondensedSet[r] * table.SpeciesCount + column];
            }

            var amount = scratch.Corrections[k];
            scratch.RightHandSide[k] = column >= table.GasCount ? scratch.GOverRT[column]
                : scratch.GOverRT[column] + logPressure + (amount > 0.0 ? Math.Log(amount / n) : 0.0);
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, rows, stride))
        {
            return false;
        }

        for (var i = 0; i < table.ElementCount; i++)
        {
            result.Multipliers[i] = 0.0;
        }

        for (var r = 0; r < rows; r++)
        {
            result.Multipliers[scratch.Tie.CondensedSet[r]] = scratch.RightHandSide[r];
        }

        return true;
    }

    /// <summary>
    /// The condensed basics as the condensed set, in species order, at their amounts (a basic at zero level, or the rounding below it,
    /// at zero); every other mole zero. Returns their count.
    /// </summary>
    private static int CondensedBasics(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result, int rows)
    {
        var count = 0;
        for (var k = 0; k < rows; k++)
        {
            var column = scratch.CondensedInSolution[k];
            if (column >= table.GasCount)
            {
                scratch.Tie.CondensedSet[count] = column;
                scratch.Tie.CondensedMoles[count++] = KernelMath.Max(scratch.Corrections[k], 0.0);
            }
        }

        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = 0.0;
        }

        var placed = 0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            for (var c = 0; c < count; c++)
            {
                if (scratch.Tie.CondensedSet[c] == j)
                {
                    result.Moles[j] = scratch.Tie.CondensedMoles[c];
                    scratch.CondensedInSolution[placed++] = j;
                }
            }
        }

        for (var c = placed; c < ScratchLayout.MaxCondensedInSolution; c++)
        {
            scratch.CondensedInSolution[c] = -1;
        }

        return placed;
    }

    /// <summary>A pivot entry at or below this is zero, as the program's own pivot tolerance.</summary>
    private const double MixturePivotTolerance = 1.0e-11;

    /// <summary>
    /// The optimal basis holds records alone while the gas phase lowers the Gibbs energy at its multipliers (ln S above zero): every
    /// gas is below unit fraction, so no gas column entered, yet their mixture does (BOOT.md, "The gas basis", the mixture column). The
    /// gas mixture at the vertex, <c>y_j = x_j(π0)/S0</c>, is the column of least reduced cost, <c>−ln S0</c>; one simplex pivot brings
    /// it in: the ratio test picks the record it replaces, n is the ratio, and π solves the remaining records' stationarities and the
    /// mixture's, <c>Σ_j y_j a_j·π = Σ_j y_j (g_j/RT + ln(p/p°) + ln y_j)</c>. False when a basic is not a record, the gas is not
    /// needed, no record leaves at a positive ratio, or a system is singular.
    /// </summary>
    private static bool EnterTheMixture(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                        in EquilibriumResult result, int rows, out double n)
    {
        n = 0.0;
        for (var k = 0; k < rows; k++)
        {
            if (scratch.CondensedInSolution[k] < table.GasCount)
            {
                return false;
            }
        }

        if (!Multipliers(table, problem, scratch, result, rows, 1.0))
        {
            return false;
        }

        var logSum = MixtureColumn(table, problem, scratch, result, rows);
        if (!(logSum > 0.0))
        {
            return false;
        }

        var leaving = Leaving(table, scratch, rows, out n);
        return leaving >= 0 && MixtureMultipliers(table, scratch, result, rows, leaving, logSum);
    }

    /// <summary>
    /// The gas mixture at the multipliers in the result: its element content per mole of gas, <c>Σ_j a_ij x_j / S</c>, into
    /// <c>Tie.CondensedMoles</c> by row; returns ln S over every gas whose elements are present.
    /// </summary>
    private static double MixtureColumn(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                        in EquilibriumResult result, int rows)
    {
        var logPressure = CaseSetup.LogPressure(problem);
        var largest = double.NegativeInfinity;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
            {
                largest = KernelMath.Max(largest, LogFraction(table, scratch, result, logPressure, j));
            }
        }

        var sum = 0.0;
        for (var j = 0; j < table.GasCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
            {
                sum += Math.Exp(LogFraction(table, scratch, result, logPressure, j) - largest);
            }
        }

        var logSum = largest + Math.Log(sum);
        for (var r = 0; r < rows; r++)
        {
            var content = 0.0;
            var element = scratch.Tie.CondensedSet[r];
            for (var j = 0; j < table.GasCount; j++)
            {
                if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
                {
                    content += table.Stoichiometry[element * table.SpeciesCount + j]
                               * Math.Exp(LogFraction(table, scratch, result, logPressure, j) - logSum);
                }
            }

            scratch.Tie.CondensedMoles[r] = content;
        }

        return logSum;
    }

    /// <summary>ln x_j at the multipliers in the result: <c>Σ_i a_ij π_i − g_j/RT − ln(p/p°)</c>.</summary>
    private static double LogFraction(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                      double logPressure, int j)
    {
        var exponent = -scratch.GOverRT[j] - logPressure;
        for (var i = 0; i < table.ElementCount; i++)
        {
            exponent += table.Stoichiometry[i * table.SpeciesCount + j] * result.Multipliers[i];
        }

        return exponent;
    }

    /// <summary>
    /// The ratio test of the mixture column: <c>B w = y</c> over the basis, the leaving basic the least <c>v_k / w_k</c> over
    /// <c>w_k</c> above the pivot tolerance (the lowest row on a tie); the basics move to <c>v − θ w</c>, the leaving one to zero, and
    /// <paramref name="n"/> is θ. Returns the leaving row, or −1 when none leaves at a positive ratio or the basis is singular.
    /// </summary>
    private static int Leaving(in SpeciesTableView table, in EquilibriumScratch scratch, int rows, out double n)
    {
        n = 0.0;
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        for (var r = 0; r < rows; r++)
        {
            for (var k = 0; k < rows; k++)
            {
                scratch.Matrix[r * stride + k] = table.Stoichiometry[scratch.Tie.CondensedSet[r] * table.SpeciesCount + scratch.CondensedInSolution[k]];
            }

            scratch.RightHandSide[r] = scratch.Tie.CondensedMoles[r];
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, rows, stride))
        {
            return -1;
        }

        var leaving = -1;
        var ratio = double.PositiveInfinity;
        for (var k = 0; k < rows; k++)
        {
            var w = scratch.RightHandSide[k];
            if (w > MixturePivotTolerance && KernelMath.Max(scratch.Corrections[k], 0.0) / w < ratio)
            {
                ratio = KernelMath.Max(scratch.Corrections[k], 0.0) / w;
                leaving = k;
            }
        }

        if (leaving < 0 || !(ratio > 0.0))
        {
            return -1;
        }

        for (var k = 0; k < rows; k++)
        {
            scratch.Corrections[k] = k == leaving ? 0.0 : scratch.Corrections[k] - ratio * scratch.RightHandSide[k];
        }

        n = ratio;
        return leaving;
    }

    /// <summary>
    /// π of the basis after the pivot: the remaining records' stationarities and the mixture's row, <c>y·π = y·π0 − ln S0</c>; the
    /// leaving row is marked out of the condensed basics.
    /// </summary>
    private static bool MixtureMultipliers(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                           int rows, int leaving, double logSum)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        var mixtureCost = -logSum;
        for (var r = 0; r < rows; r++)
        {
            mixtureCost += scratch.Tie.CondensedMoles[r] * result.Multipliers[scratch.Tie.CondensedSet[r]];
        }

        for (var k = 0; k < rows; k++)
        {
            var column = scratch.CondensedInSolution[k];
            for (var r = 0; r < rows; r++)
            {
                scratch.Matrix[k * stride + r] = k == leaving
                    ? scratch.Tie.CondensedMoles[r]
                    : table.Stoichiometry[scratch.Tie.CondensedSet[r] * table.SpeciesCount + column];
            }

            scratch.RightHandSide[k] = k == leaving ? mixtureCost : scratch.GOverRT[column];
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, rows, stride))
        {
            return false;
        }

        for (var r = 0; r < rows; r++)
        {
            result.Multipliers[scratch.Tie.CondensedSet[r]] = scratch.RightHandSide[r];
        }

        scratch.CondensedInSolution[leaving] = -1 - leaving;
        return true;
    }
}
