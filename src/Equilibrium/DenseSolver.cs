using ILGPU;

namespace AerospacePropellantThermodynamics.Equilibrium;

/// <summary>Gaussian elimination with partial pivoting on a small dense system, in place. Kernel-compatible.</summary>
internal static class DenseSolver
{
    /// <summary>A pivot smaller than this fraction of its row's largest initial entry marks the matrix singular.</summary>
    private const double PivotTolerance = 1.0e-13;

    /// <summary>
    /// Solves A x = b for a row-major n×n matrix held in <paramref name="matrix"/> (stride <paramref name="stride"/>) and the
    /// right-hand side in <paramref name="rhs"/>; the solution replaces <paramref name="rhs"/>. Returns false when a pivot vanishes.
    /// </summary>
    public static bool Solve(ArrayView<double> matrix, ArrayView<double> rhs, ArrayView<double> rowScale, int n, int stride)
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

        for (var k = 0; k < n; k++)
        {
            // Partial pivoting on the scaled column.
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

            if (!(best > PivotTolerance))
            {
                return false;
            }

            if (pivotRow != k)
            {
                for (var c = 0; c < n; c++)
                {
                    var t = matrix[k * stride + c];
                    matrix[k * stride + c] = matrix[pivotRow * stride + c];
                    matrix[pivotRow * stride + c] = t;
                }

                var tb = rhs[k];
                rhs[k] = rhs[pivotRow];
                rhs[pivotRow] = tb;
                var ts = rowScale[k];
                rowScale[k] = rowScale[pivotRow];
                rowScale[pivotRow] = ts;
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

        for (var k = n - 1; k >= 0; k--)
        {
            var sum = rhs[k];
            for (var c = k + 1; c < n; c++)
            {
                sum -= matrix[k * stride + c] * rhs[c];
            }

            rhs[k] = sum / matrix[k * stride + k];
        }

        return true;
    }
}
