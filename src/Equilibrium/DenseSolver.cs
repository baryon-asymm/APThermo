using ILGPU;

namespace APThermo.Equilibrium;

/// <summary>Gaussian elimination with partial pivoting on a small dense system, in place. Kernel-compatible. Shared with the transport node.</summary>
public static class DenseSolver
{
    /// <summary>A pivot smaller than this fraction of its row's largest initial entry marks the matrix singular.</summary>
    private const double PivotTolerance = 1.0e-13;

    /// <summary>
    /// Solves A x = b for a row-major n×n matrix held in <paramref name="matrix"/> (stride <paramref name="stride"/>) and the
    /// right-hand side in <paramref name="rhs"/>; the solution replaces <paramref name="rhs"/>. Returns false when a pivot vanishes.
    /// </summary>
    public static bool Solve(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride)
    {
        Scale(matrix, rowScale, n, stride);
        if (!Eliminate(matrix, rhs, rowScale, n, stride))
        {
            return false;
        }

        BackSubstitute(matrix, rhs, n, stride);
        return true;
    }

    /// <summary>Each row's largest entry, against which its pivot candidate is measured; rows keep their scale through the swaps.</summary>
    private static void Scale(ArrayView<double> matrix, ArrayView<double> rowScale, int n, int stride)
    {
        for (var r = 0; r < n; r++)
        {
            var scale = 0.0;
            for (var c = 0; c < n; c++)
            {
                scale = Math.Max(scale, Math.Abs(matrix[r * stride + c]));
            }

            rowScale[r] = scale;
        }
    }

    /// <summary>Forward elimination with scaled partial pivoting; false when the column has no usable pivot left.</summary>
    private static bool Eliminate(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride)
    {
        for (var k = 0; k < n; k++)
        {
            var pivotRow = PivotRow(matrix, rowScale, k, n, stride);
            if (pivotRow < 0)
            {
                return false;
            }

            if (pivotRow != k)
            {
                for (var c = 0; c < n; c++)
                {
                    var held = matrix[k * stride + c];
                    matrix[k * stride + c] = matrix[pivotRow * stride + c];
                    matrix[pivotRow * stride + c] = held;
                }

                var heldRhs = rhs[k];
                rhs[k] = rhs[pivotRow];
                rhs[pivotRow] = heldRhs;
                var heldScale = rowScale[k];
                rowScale[k] = rowScale[pivotRow];
                rowScale[pivotRow] = heldScale;
            }

            var pivot = matrix[k * stride + k];
            for (var r = k + 1; r < n; r++)
            {
                var factor = matrix[r * stride + k] / pivot;
                if (factor == 0.0)
                {
                    continue;
                }

                matrix[r * stride + k] = 0.0;
                for (var c = k + 1; c < n; c++)
                {
                    matrix[r * stride + c] -= factor * matrix[k * stride + c];
                }

                rhs[r] -= factor * rhs[k];
            }
        }

        return true;
    }

    /// <summary>The row of the largest scaled entry in column k, at or below the diagonal; −1 when even that is at rounding level.</summary>
    private static int PivotRow(ArrayView<double> matrix, ArrayView<double> rowScale, int k, int n, int stride)
    {
        var pivotRow = k;
        var best = -1.0;
        for (var r = k; r < n; r++)
        {
            var scale = rowScale[r];
            var candidate = scale > 0.0 ? Math.Abs(matrix[r * stride + k]) / scale : 0.0;
            if (candidate > best)
            {
                best = candidate;
                pivotRow = r;
            }
        }

        return best > PivotTolerance ? pivotRow : -1;
    }

    /// <summary>Back substitution over the upper triangle; the solution replaces the right-hand side.</summary>
    private static void BackSubstitute(ArrayView<double> matrix, ArrayView<double> rhs, int n, int stride)
    {
        for (var k = n - 1; k >= 0; k--)
        {
            var sum = rhs[k];
            for (var c = k + 1; c < n; c++)
            {
                sum -= matrix[k * stride + c] * rhs[c];
            }

            rhs[k] = sum / matrix[k * stride + k];
        }
    }
}
