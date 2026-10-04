using APThermo.Thermo;

namespace APThermo.Equilibrium.Tests;

/// <summary>
/// The Gibbs minimum of the condensed species alone, found the slow way: every basis of at most as many records as the case
/// has active elements is tried, the least of the feasible ones is the minimum of the linear program. It shares no code with
/// the simplex it checks (GasPhase BOOT.md, the minimum is the minimum).
/// </summary>
internal static class BasisEnumeration
{
    /// <summary>A basis counts as feasible when its residual is within this times the largest element abundance.</summary>
    private const double Feasible = 1.0e-10;

    /// <summary>A negative amount within this is rounding.</summary>
    private const double NegativeRounding = 1.0e-13;

    /// <summary>The pivot below which a set of records is dependent.</summary>
    private const double Dependent = 1.0e-12;

    /// <summary>Σ n_j g°_j/RT over the condensed species of the solution, at its state's temperature.</summary>
    public static double GibbsEnergyOf(HostSolution solution)
    {
        var table = solution.Case.Table;
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;
        var sum = 0.0;
        for (var j = table.GasCount; j < table.SpeciesCount; j++)
        {
            sum += solution.Moles[j] * SpeciesFunctions.GOverRT(view, j, solution.State.Temperature);
        }

        return sum;
    }

    /// <summary>The least Σ n_j g°_j/RT over the feasible bases of the case's eligible condensed records at its temperature.</summary>
    public static double LeastGibbsEnergy(EquilibriumCase problem)
    {
        var table = problem.Table;
        using var buffers = SpeciesTableBuffers.Upload(CpuFixture.Shared.Accelerator, table);
        var view = buffers.View;
        var rows = Enumerable.Range(0, table.ElementCount).Where(i => problem.ElementMoles[i] > 0.0).ToArray();
        var candidates = Enumerable.Range(table.GasCount, table.SpeciesCount - table.GasCount)
            .Where(j => Eligible(problem, j)).ToArray();
        var cost = candidates.ToDictionary(j => j, j => SpeciesFunctions.GOverRT(view, j, problem.Temperature));
        var best = double.PositiveInfinity;
        foreach (var basis in Subsets(candidates, rows.Length))
        {
            var energy = Evaluate(problem, rows, basis, cost);
            best = Math.Min(best, energy);
        }

        return best;
    }

    /// <summary>The records that are no candidate of an absent element and whose data cover the temperature.</summary>
    private static bool Eligible(EquilibriumCase problem, int j)
    {
        var table = problem.Table;
        var arrays = table.Arrays;
        for (var i = 0; i < table.ElementCount; i++)
        {
            if (problem.ElementMoles[i] == 0.0 && arrays.Stoichiometry[i * table.SpeciesCount + j] != 0.0)
            {
                return false;
            }
        }

        var start = arrays.IntervalStart[j];
        var last = start + arrays.IntervalCount[j] - 1;
        return problem.Temperature >= arrays.IntervalBounds[start * 2] && problem.Temperature <= arrays.IntervalBounds[last * 2 + 1];
    }

    /// <summary>Every non-empty subset of the candidates of at most <paramref name="size"/> records.</summary>
    private static IEnumerable<int[]> Subsets(int[] candidates, int size)
    {
        var stack = new Stack<(int Next, int[] Chosen)>();
        stack.Push((0, []));
        while (stack.Count > 0)
        {
            var (next, chosen) = stack.Pop();
            if (chosen.Length > 0)
            {
                yield return chosen;
            }

            if (chosen.Length == size)
            {
                continue;
            }

            for (var k = next; k < candidates.Length; k++)
            {
                stack.Push((k + 1, [.. chosen, candidates[k]]));
            }
        }
    }

    /// <summary>The Gibbs energy of the basis if its records are independent, hold every element exactly and none is negative; +∞ otherwise.</summary>
    private static double Evaluate(EquilibriumCase problem, int[] rows, int[] basis, Dictionary<int, double> cost)
    {
        var table = problem.Table;
        var count = basis.Length;
        var a = new double[rows.Length][];
        for (var r = 0; r < rows.Length; r++)
        {
            a[r] = [.. basis.Select(j => table.Arrays.Stoichiometry[rows[r] * table.SpeciesCount + j])];
        }

        var b = rows.Select(i => problem.ElementMoles[i]).ToArray();
        var x = LeastSquares(a, b, count);
        if (x is null)
        {
            return double.PositiveInfinity;
        }

        var scale = b.Max();
        for (var r = 0; r < rows.Length; r++)
        {
            var residual = b[r];
            for (var k = 0; k < count; k++)
            {
                residual -= a[r][k] * x[k];
            }

            if (Math.Abs(residual) > Feasible * scale)
            {
                return double.PositiveInfinity;
            }
        }

        return x.Min() < -NegativeRounding * scale ? double.PositiveInfinity : x.Select((value, k) => value * cost[basis[k]]).Sum();
    }

    /// <summary>The solution of the normal equations by Gaussian elimination with partial pivoting; null when the columns are dependent.</summary>
    private static double[]? LeastSquares(double[][] a, double[] b, int count)
    {
        var rows = b.Length;
        var m = new double[count][];
        for (var row = 0; row < count; row++)
        {
            m[row] = new double[count + 1];
        }

        for (var p = 0; p < count; p++)
        {
            for (var q = 0; q < count; q++)
            {
                for (var r = 0; r < rows; r++)
                {
                    m[p][q] += a[r][p] * a[r][q];
                }
            }

            for (var r = 0; r < rows; r++)
            {
                m[p][count] += a[r][p] * b[r];
            }
        }

        for (var column = 0; column < count; column++)
        {
            var pivot = Enumerable.Range(column, count - column).MaxBy(row => Math.Abs(m[row][column]));
            if (Math.Abs(m[pivot][column]) < Dependent)
            {
                return null;
            }

            for (var q = 0; q <= count; q++)
            {
                (m[column][q], m[pivot][q]) = (m[pivot][q], m[column][q]);
            }

            for (var row = column + 1; row < count; row++)
            {
                var factor = m[row][column] / m[column][column];
                for (var q = column; q <= count; q++)
                {
                    m[row][q] -= factor * m[column][q];
                }
            }
        }

        var x = new double[count];
        for (var row = count - 1; row >= 0; row--)
        {
            var sum = m[row][count];
            for (var q = row + 1; q < count; q++)
            {
                sum -= m[row][q] * x[q];
            }

            x[row] = sum / m[row][row];
        }

        return x;
    }
}
