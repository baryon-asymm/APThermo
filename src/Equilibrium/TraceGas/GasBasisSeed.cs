using APThermo.Thermo;

namespace APThermo.Equilibrium.TraceGas;

/// <summary>
/// The fifth start of a pass (BOOT.md, "The starts", "The gas basis", 2026-10-05), after Reynolds's STANJAN initializer: the
/// phase-one program with every gas a column at unit fraction (cost g/RT + ln(p/p°)), whose optimal basis holds the condensed
/// records and the gases that carry what the records cannot. The condensed set is the condensed basics, a basic at zero level
/// included at zero moles (the balancing phase: CaO beside CaCO3 under an excess of oxygen); π solves the stationarities of the
/// basis, a basic gas at its share of the basic gas (<c>ln x = ln(v/n)</c>), one at zero level at unit fraction; n is the basic gas.
/// The excess is then carried by the gas on its own branch (<c>n F(π) = b*</c> with <c>F</c> of the sign of <c>b*</c>), and every
/// gas amount follows from π at the first evaluation. Kernel-compatible; it borrows the matrix scratch and <c>Tie.CondensedSet</c>
/// and <c>Tie.CondensedMoles</c>, as the phase-one starts do.
/// </summary>
internal static class GasBasisSeed
{
    /// <summary>
    /// Places the start: the condensed basics into <c>scratch.CondensedInSolution</c> in species order and their amounts into the
    /// result's moles (every other mole zero), π into the result's multipliers (zero for an absent element), ln n into the state.
    /// False, the start skipped, when the program does not complete, an artificial column stays basic, no gas is basic with a
    /// positive amount, or the basis is singular.
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
        if (!(n > 0.0) || !Multipliers(table, problem, scratch, result, rows, n))
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
}
