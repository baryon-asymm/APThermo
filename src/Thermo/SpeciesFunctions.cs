namespace AerospacePropellantThermodynamics.Thermo;

/// <summary>
/// The NASA polynomials evaluated for one species at one temperature. Kernel-compatible: static, no allocation, no
/// exceptions, only <see cref="Math.Log(double)"/> and <see cref="Math.Pow(double, double)"/> of the root's list.
/// </summary>
/// <remarks>
/// With the exponents e_k and coefficients a_k of the record (the Data node's format facts):
/// <code>
/// Cp°/R = Σ a_k T^e_k
/// H°/RT = Σ a_k I_h(e_k, T) + b1 / T,   I_h(e, T) = T^e / (e + 1),  I_h(−1, T) = ln T / T
/// S°/R  = Σ a_k I_s(e_k, T) + b2,       I_s(e, T) = T^e / e,        I_s(0, T) = ln T
/// G°/RT = H°/RT − S°/R
/// </code>
/// The interval used is the first whose upper bound is not below T; below the first bound or above the last the
/// nearest interval's polynomial is evaluated and <see cref="IsInRange"/> reports false.
/// </remarks>
public static class SpeciesFunctions
{
    private const int CoefficientsPerInterval = 7;
    private const int ExponentStride = 8;
    private const int CoefficientStride = 9;

    /// <summary>The interval of the species used at the temperature, 0-based within the species: the first whose upper bound is not below T, else the last.</summary>
    public static int IntervalOf(in SpeciesTableView table, int species, double temperature)
    {
        var start = table.IntervalStart[species];
        var count = table.IntervalCount[species];
        for (var k = 0; k < count - 1; k++)
        {
            if (temperature <= table.IntervalBounds[(start + k) * 2 + 1])
            {
                return k;
            }
        }

        return count - 1;
    }

    /// <summary>True when the temperature lies between the first interval's lower bound and the last interval's upper bound.</summary>
    public static bool IsInRange(in SpeciesTableView table, int species, double temperature)
    {
        var start = table.IntervalStart[species];
        var last = start + table.IntervalCount[species] - 1;
        return temperature >= table.IntervalBounds[start * 2] && temperature <= table.IntervalBounds[last * 2 + 1];
    }

    public static double CpOverR(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = 0.0;
        for (var k = 0; k < CoefficientsPerInterval; k++)
        {
            sum += table.Coefficients[interval * CoefficientStride + k] * Power(temperature, table.Exponents[interval * ExponentStride + k]);
        }

        return sum;
    }

    public static double HOverRT(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = table.Coefficients[interval * CoefficientStride + 7] / temperature;
        for (var k = 0; k < CoefficientsPerInterval; k++)
        {
            var e = table.Exponents[interval * ExponentStride + k];
            var term = e == -1.0 ? Math.Log(temperature) / temperature : Power(temperature, e) / (e + 1.0);
            sum += table.Coefficients[interval * CoefficientStride + k] * term;
        }

        return sum;
    }

    public static double SOverR(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = table.Coefficients[interval * CoefficientStride + 8];
        for (var k = 0; k < CoefficientsPerInterval; k++)
        {
            var e = table.Exponents[interval * ExponentStride + k];
            var term = e == 0.0 ? Math.Log(temperature) : Power(temperature, e) / e;
            sum += table.Coefficients[interval * CoefficientStride + k] * term;
        }

        return sum;
    }

    /// <summary>H°/RT − S°/R.</summary>
    public static double GOverRT(in SpeciesTableView table, int species, double temperature) =>
        HOverRT(table, species, temperature) - SOverR(table, species, temperature);

    /// <summary>T^e: the usual exponents −2 … 4 by multiplication, any other through Math.Pow.</summary>
    private static double Power(double t, double e)
    {
        if (e == 0.0)
        {
            return 1.0;
        }

        if (e == 1.0)
        {
            return t;
        }

        if (e == 2.0)
        {
            return t * t;
        }

        if (e == -1.0)
        {
            return 1.0 / t;
        }

        if (e == -2.0)
        {
            return 1.0 / (t * t);
        }

        if (e == 3.0)
        {
            return t * t * t;
        }

        if (e == 4.0)
        {
            var t2 = t * t;
            return t2 * t2;
        }

        return Math.Pow(t, e);
    }
}
