using APThermo.Equilibrium.Condensed;
using APThermo.Thermo;

namespace APThermo.Equilibrium.GasPhase;

/// <summary>
/// The Gibbs minimum of the condensed species alone under element conservation: a linear program, since a pure condensed
/// phase has a constant chemical potential at fixed T and p. A two-phase revised simplex with Bland's rule (no cycling,
/// deterministic), one artificial column per active element. Kernel-compatible; it uses only scratch that is free once an
/// attempt has ended: <c>Tie.CondensedSet</c> (row → element), <c>CondensedInSolution</c> (the basis: a column, or
/// −1 − row for an artificial one), <c>Corrections</c> (the basic values), <c>Tie.LogMoles</c> (the multipliers by row),
/// the matrix, the right-hand side and the row scales (BOOT.md).
/// </summary>
internal static class CondensedSimplex
{
    /// <summary>A reduced cost below −this enters the basis; a record left out gains at most this per mole.</summary>
    private const double ReducedCostTolerance = 1.0e-9;

    /// <summary>A pivot, a direction entry or a coordinate slope below this is zero.</summary>
    internal const double PivotTolerance = 1.0e-11;

    /// <summary>A basic value (or an artificial one) within this times max(1, b_i) is zero.</summary>
    private const double ConservationTolerance = 1.0e-12;

    /// <summary>Pivots allowed per phase.</summary>
    private const int MaxPivots = 256;

    /// <summary>
    /// Runs both phases. Returns the number of rows, with the optimal basis in <c>CondensedInSolution</c>, the basic values in
    /// <c>Corrections</c> and the multipliers in <c>Tie.LogMoles</c>; or zero with <paramref name="stop"/> the verdict that ends
    /// the test: <see cref="GasVerdict.GasRequired"/> when the condensed species cannot hold an element, else
    /// <see cref="GasVerdict.Undecided"/>. Every record not absent is back in play: the program is global, and an attempt's
    /// anti-cycling memories do not bind it.
    /// </summary>
    public static int Minimize(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                               double temperature, out GasVerdict stop)
    {
        stop = GasVerdict.Undecided;
        var rows = PhaseOne(table, problem, scratch, temperature, out var residual);
        if (rows == 0)
        {
            return 0;
        }

        if (residual > 0.0)
        {
            stop = GasVerdict.GasRequired;
            return 0;
        }

        return PhaseTwo(table, problem, scratch, temperature, rows);
    }

    /// <summary>
    /// The point the trace-gas pass starts from (2026-10-04): the vertex phase one stops at when the condensed species cannot
    /// hold every element (<paramref name="residual"/>: the moles they cannot hold, positive), else the condensed minimum
    /// (residual zero), into <c>result.Moles</c> with every gas and every record at zero level zero. False, nothing written,
    /// when the program does not complete. The same program as <see cref="Minimize"/>, which it shares both phases with.
    /// </summary>
    public static bool Point(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                             in EquilibriumResult result, double temperature, out double residual)
    {
        var rows = PhaseOne(table, problem, scratch, temperature, out residual);
        if (rows > 0 && !(residual > 0.0))
        {
            rows = PhaseTwo(table, problem, scratch, temperature, rows);
        }

        if (rows == 0)
        {
            return false;
        }

        WriteMoles(table, scratch, result, problem, rows);
        return true;
    }

    /// <summary>
    /// Phase one: the artificial basis driven to its minimum. Returns the number of rows with the basis in
    /// <c>CondensedInSolution</c> and the basic values in <c>Corrections</c>, or zero when it does not complete;
    /// <paramref name="residual"/> is the sum of the artificial values above the conservation tolerance, the moles of the
    /// elements the condensed species cannot hold.
    /// </summary>
    private static int PhaseOne(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                double temperature, out double residual)
    {
        residual = 0.0;
        ResetMarks(table, scratch);
        var m = Rows(table, scratch);
        if (m == 0 || table.SpeciesCount < m)
        {
            return 0;
        }

        for (var r = 0; r < m; r++)
        {
            scratch.CondensedInSolution[r] = -1 - r;
        }

        if (!Phase(table, problem, scratch, temperature, m, phaseOne: true) || !Primal(table, problem, scratch, m))
        {
            return 0;
        }

        residual = Unheld(problem, scratch, m);
        return m;
    }

    /// <summary>Phase two from a feasible phase-one basis: the artificial columns at zero level driven out, the true cost minimized; the number of rows, or zero when it does not complete or a basic value is negative.</summary>
    private static int PhaseTwo(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch,
                                double temperature, int m)
    {
        DriveOutArtificials(table, scratch, temperature, m);
        if (!Phase(table, problem, scratch, temperature, m, phaseOne: false)
            || !Primal(table, problem, scratch, m) || !Dual(table, scratch, m, phaseOne: false))
        {
            return 0;
        }

        for (var r = 0; r < m; r++)
        {
            if (scratch.Corrections[r] < -ConservationTolerance)
            {
                return 0;
            }
        }

        return m;
    }

    /// <summary>Whether the basic value at a row is zero: within the conservation tolerance of the row's element.</summary>
    public static bool ZeroLevel(in EquilibriumProblem problem, in EquilibriumScratch scratch, int r) =>
        scratch.Corrections[r] <= ConservationTolerance * KernelMath.Max(1.0, problem.ElementMoles[scratch.Tie.CondensedSet[r]]);

    /// <summary>Whether the record is a column of the program: its elements are present and it lies in its effective range.</summary>
    public static bool Eligible(in SpeciesTableView table, in EquilibriumScratch scratch, int j, double temperature) =>
        SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent && PhaseGeometry.InEffectiveRange(table, scratch, j, temperature);

    /// <summary>Whether the record is a basic column of the first <paramref name="m"/> rows.</summary>
    public static bool InBasis(in EquilibriumScratch scratch, int m, int j)
    {
        for (var r = 0; r < m; r++)
        {
            if (scratch.CondensedInSolution[r] == j)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Entry of a column in a row: the stoichiometry of a record, or the unit entry of an artificial column (−1 − row).</summary>
    public static double Entry(in SpeciesTableView table, in EquilibriumScratch scratch, int column, int row) =>
        column >= 0 ? table.Stoichiometry[scratch.Tie.CondensedSet[row] * table.SpeciesCount + column] : (-1 - column == row ? 1.0 : 0.0);

    /// <summary>The multipliers of the optimal basis: π = B⁻ᵀ c_B into <c>Tie.LogMoles</c>.</summary>
    public static bool Dual(in SpeciesTableView table, in EquilibriumScratch scratch, int m, bool phaseOne)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        AssembleBasis(table, scratch, m, stride, transpose: true);
        for (var k = 0; k < m; k++)
        {
            scratch.RightHandSide[k] = Cost(scratch, scratch.CondensedInSolution[k], phaseOne);
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, m, stride))
        {
            return false;
        }

        for (var r = 0; r < m; r++)
        {
            scratch.Tie.LogMoles[r] = scratch.RightHandSide[r];
        }

        return true;
    }

    /// <summary>The condensed minimum into <c>result.Moles</c>: every gas and every record at zero level zero.</summary>
    public static void WriteMoles(in SpeciesTableView table, in EquilibriumScratch scratch, in EquilibriumResult result,
                                  in EquilibriumProblem problem, int m)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            result.Moles[j] = 0.0;
        }

        for (var r = 0; r < m; r++)
        {
            var column = scratch.CondensedInSolution[r];
            if (column >= 0 && !ZeroLevel(problem, scratch, r))
            {
                result.Moles[column] += scratch.Corrections[r];
            }
        }
    }

    /// <summary>The basis matrix (or its transpose) of the first <paramref name="m"/> rows into the matrix scratch.</summary>
    public static void AssembleBasis(in SpeciesTableView table, in EquilibriumScratch scratch, int m, int stride, bool transpose)
    {
        for (var r = 0; r < m; r++)
        {
            for (var k = 0; k < m; k++)
            {
                var value = Entry(table, scratch, scratch.CondensedInSolution[k], r);
                scratch.Matrix[transpose ? k * stride + r : r * stride + k] = value;
            }
        }
    }

    private static double Cost(in EquilibriumScratch scratch, int column, bool phaseOne) =>
        phaseOne ? (column < 0 ? 1.0 : 0.0) : (column < 0 ? 0.0 : scratch.GOverRT[column]);

    private static void ResetMarks(in SpeciesTableView table, in EquilibriumScratch scratch)
    {
        for (var j = 0; j < table.SpeciesCount; j++)
        {
            if (SpeciesMarks.Of(scratch, j) != SpeciesMark.Absent)
            {
                SpeciesMarks.Set(scratch, j, SpeciesMark.Active);
            }
        }
    }

    /// <summary>The active elements into <c>Tie.CondensedSet</c> (row → element); returns their count.</summary>
    private static int Rows(in SpeciesTableView table, in EquilibriumScratch scratch)
    {
        var m = 0;
        for (var i = 0; i < table.ElementCount && m < ScratchLayout.MaxCondensedInSolution; i++)
        {
            if (scratch.ElementActive[i] != 0)
            {
                scratch.Tie.CondensedSet[m++] = i;
            }
        }

        return m;
    }

    /// <summary>After phase one: the sum of the artificial columns' values above the conservation tolerance; zero when the condensed species hold every element.</summary>
    private static double Unheld(in EquilibriumProblem problem, in EquilibriumScratch scratch, int m)
    {
        var residual = 0.0;
        for (var r = 0; r < m; r++)
        {
            var b = problem.ElementMoles[scratch.Tie.CondensedSet[r]];
            if (scratch.CondensedInSolution[r] < 0 && scratch.Corrections[r] > ConservationTolerance * KernelMath.Max(1.0, b))
            {
                residual += scratch.Corrections[r];
            }
        }

        return residual;
    }

    /// <summary>x_B = B⁻¹ b into <c>Corrections</c>.</summary>
    private static bool Primal(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, int m)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        AssembleBasis(table, scratch, m, stride, transpose: false);
        for (var r = 0; r < m; r++)
        {
            scratch.RightHandSide[r] = problem.ElementMoles[scratch.Tie.CondensedSet[r]];
        }

        if (!DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, m, stride))
        {
            return false;
        }

        for (var r = 0; r < m; r++)
        {
            scratch.Corrections[r] = scratch.RightHandSide[r];
        }

        return true;
    }

    /// <summary>d = B⁻¹ a_column into the right-hand side.</summary>
    private static bool Direction(in SpeciesTableView table, in EquilibriumScratch scratch, int m, int column)
    {
        var stride = ScratchLayout.MaxUnknowns(table.ElementCount);
        AssembleBasis(table, scratch, m, stride, transpose: false);
        for (var r = 0; r < m; r++)
        {
            scratch.RightHandSide[r] = Entry(table, scratch, column, r);
        }

        return DenseSolver.Solve(scratch.Matrix, scratch.RightHandSide, scratch.RowScale, m, stride);
    }

    private static double ReducedCost(in SpeciesTableView table, in EquilibriumScratch scratch, int m, int j, bool phaseOne)
    {
        var d = Cost(scratch, j, phaseOne);
        for (var r = 0; r < m; r++)
        {
            d -= scratch.Tie.LogMoles[r] * Entry(table, scratch, j, r);
        }

        return d;
    }

    /// <summary>One phase of the simplex to optimality, Bland's rule for both the entering and the leaving column.</summary>
    private static bool Phase(in SpeciesTableView table, in EquilibriumProblem problem, in EquilibriumScratch scratch, double temperature,
                              int m, bool phaseOne)
    {
        for (var pivot = 0; pivot < MaxPivots; pivot++)
        {
            if (!Primal(table, problem, scratch, m) || !Dual(table, scratch, m, phaseOne))
            {
                return false;
            }

            var entering = Entering(table, scratch, temperature, m, phaseOne);
            if (entering < 0)
            {
                return true;
            }

            if (!Direction(table, scratch, m, entering))
            {
                return false;
            }

            var leaving = Leaving(scratch, m);
            if (leaving < 0)
            {
                return false;
            }

            scratch.CondensedInSolution[leaving] = entering;
        }

        return false;
    }

    /// <summary>The lowest eligible record outside the basis with a reduced cost below the tolerance; −1 when the basis is optimal.</summary>
    private static int Entering(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature, int m, bool phaseOne)
    {
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (Eligible(table, scratch, j, temperature) && !InBasis(scratch, m, j)
                && ReducedCost(table, scratch, m, j, phaseOne) < -ReducedCostTolerance)
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>The ratio test over d in the right-hand side and x_B in <c>Corrections</c>; ties leave the smallest column index.</summary>
    private static int Leaving(in EquilibriumScratch scratch, int m)
    {
        var leaving = -1;
        var best = double.MaxValue;
        for (var r = 0; r < m; r++)
        {
            var d = scratch.RightHandSide[r];
            if (!(d > PivotTolerance))
            {
                continue;
            }

            var ratio = KernelMath.Max(scratch.Corrections[r], 0.0) / d;
            if (leaving < 0 || ratio < best
                || (ratio == best && scratch.CondensedInSolution[r] < scratch.CondensedInSolution[leaving]))
            {
                leaving = r;
                best = ratio;
            }
        }

        return leaving;
    }

    /// <summary>Every artificial column left in the basis at zero level is replaced by an eligible record with a nonzero entry in its row; a row none can enter is redundant and keeps its artificial.</summary>
    private static void DriveOutArtificials(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature, int m)
    {
        for (var r = 0; r < m; r++)
        {
            if (scratch.CondensedInSolution[r] >= 0)
            {
                continue;
            }

            var replacement = Replacement(table, scratch, temperature, m, r);
            if (replacement >= 0)
            {
                scratch.CondensedInSolution[r] = replacement;
            }
        }
    }

    /// <summary>The lowest eligible record outside the basis whose direction has a nonzero entry in the row; −1 when none.</summary>
    private static int Replacement(in SpeciesTableView table, in EquilibriumScratch scratch, double temperature, int m, int r)
    {
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            if (!Eligible(table, scratch, j, temperature) || InBasis(scratch, m, j) || !Direction(table, scratch, m, j))
            {
                continue;
            }

            if (Math.Abs(scratch.RightHandSide[r]) > PivotTolerance)
            {
                return j;
            }
        }

        return -1;
    }
}
