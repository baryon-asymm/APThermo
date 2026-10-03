using APThermo.Thermo;

namespace APThermo.Execution.Tests;

/// <summary>
/// How far a computed composition is from closing the element balance of its case: <c>ρ_i = Σ_j a_ij n_j / b_i − 1</c> per element,
/// summed in double-double so that the sum's own rounding (about 1e-16 relative) does not hide a residual of 1e-14. Each
/// accelerator closes the balance only to about 1e-14, and a species the balance sets as a small difference of large amounts carries
/// that residual amplified (Execution.Tests BOOT.md, the balance-remnant correction).
/// </summary>
internal sealed class ElementBalance(SpeciesTable table, double[] elementMoles)
{
    /// <summary>The relative residual of every element at one station: <paramref name="moles"/> holds the station <paramref name="stationIndex"/>, the elements of case <paramref name="caseIndex"/> are the balance.</summary>
    public double[] Residuals(int caseIndex, double[] moles, long stationIndex)
    {
        ArgumentNullException.ThrowIfNull(moles);
        var speciesCount = table.SpeciesCount;
        var elementCount = table.ElementCount;
        var offset = stationIndex * speciesCount;
        var residuals = new double[elementCount];
        for (var i = 0; i < elementCount; i++)
        {
            var b = elementMoles[caseIndex * elementCount + i];
            var (high, low) = (-b, 0.0);
            for (var j = 0; j < speciesCount; j++)
            {
                var atoms = table.Arrays.Stoichiometry[i * speciesCount + j];
                if (atoms == 0.0)
                {
                    continue;
                }

                var product = atoms * moles[offset + j];
                var productError = Math.FusedMultiplyAdd(atoms, moles[offset + j], -product);
                var (sum, sumError) = TwoSum(high, product);
                high = sum;
                low += sumError + productError;
            }

            residuals[i] = b == 0.0 ? 0.0 : (high + low) / b;
        }

        return residuals;
    }

    /// <summary>The elements whose residual is above the bound of the tolerance table (a NaN residual is above it), as messages naming the side and the station.</summary>
    public IEnumerable<string> Exceeding(double[] residuals, string side, string label)
    {
        ArgumentNullException.ThrowIfNull(residuals);
        var bound = GpuCpuTolerances.Entries["balanceResidual"].Relative;
        for (var i = 0; i < residuals.Length; i++)
        {
            if (!(Math.Abs(residuals[i]) <= bound))
            {
                yield return $"{label}: element balance of {table.Elements[i]} on {side} closed only to {residuals[i]:E2}, above {bound:E0}";
            }
        }
    }

    /// <summary>The exact sum of two doubles as a rounded sum and the rounding error (Knuth's two-sum).</summary>
    private static (double Sum, double Error) TwoSum(double a, double b)
    {
        var sum = a + b;
        var virtualB = sum - a;
        return (sum, a - (sum - virtualB) + (b - virtualB));
    }
}
