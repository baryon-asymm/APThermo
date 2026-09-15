using APThermo.Data;

namespace APThermo.Thermo;

/// <summary>
/// The NASA polynomials evaluated for one species at one temperature. Kernel-compatible: static, no allocation, no
/// exceptions, only <see cref="Math.Log(double)"/> and <see cref="Math.Pow(double, double)"/> of the root's list.
/// The layout constants (strides and slots) come from <see cref="TableLayout"/>, the same ones the builder writes by.
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
    /// <summary>
    /// |ΔH°/RT| at a bound shared by two condensed fits at or above which the two sides are a real transition: the
    /// builder cuts a species there (BOOT.md, join-and-cut) and the equilibrium solver pins a two-phase pair there.
    /// The smallest real transition of the committed file is BeO a/b at 1.34e-2, the largest interval-split artifact
    /// 3.9e-4 (Cr(cr)).
    /// </summary>
    public const double LatentHeatThreshold = 1.0e-3;

    /// <summary>The interval of the species used at the temperature, 0-based within the species: the first whose upper bound is not below T, else the last.</summary>
    public static int IntervalOf(in SpeciesTableView table, int species, double temperature)
    {
        var start = table.IntervalStart[species];
        var count = table.IntervalCount[species];
        for (var k = 0; k < count - 1; k++)
        {
            if (temperature <= table.IntervalBounds[(start + k) * TableLayout.BoundsStride + 1])
            {
                return k;
            }
        }

        return count - 1;
    }

    /// <summary>The first (lowest) lower bound of the species' intervals; with <see cref="RecordHigh"/>, the bounds <see cref="IsInRange"/> compares.</summary>
    public static double RecordLow(in SpeciesTableView table, int species) =>
        table.IntervalBounds[table.IntervalStart[species] * TableLayout.BoundsStride];

    /// <summary>The last (highest) upper bound of the species' intervals.</summary>
    public static double RecordHigh(in SpeciesTableView table, int species)
    {
        var last = table.IntervalStart[species] + table.IntervalCount[species] - 1;
        return table.IntervalBounds[last * TableLayout.BoundsStride + 1];
    }

    /// <summary>True when the temperature lies between the first interval's lower bound and the last interval's upper bound.</summary>
    public static bool IsInRange(in SpeciesTableView table, int species, double temperature) =>
        temperature >= RecordLow(table, species) && temperature <= RecordHigh(table, species);

    public static double CpOverR(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = 0.0;
        for (var k = 0; k < TableLayout.CoefficientsPerInterval; k++)
        {
            sum += table.Coefficients[interval * TableLayout.CoefficientStride + k] * Power(temperature, table.Exponents[interval * TableLayout.ExponentsPerInterval + k]);
        }

        return sum;
    }

    public static double HOverRT(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = table.Coefficients[interval * TableLayout.CoefficientStride + TableLayout.B1Slot] / temperature;
        for (var k = 0; k < TableLayout.CoefficientsPerInterval; k++)
        {
            sum += table.Coefficients[interval * TableLayout.CoefficientStride + k] * EnthalpyTerm(table.Exponents[interval * TableLayout.ExponentsPerInterval + k], temperature);
        }

        return sum;
    }

    /// <summary>
    /// H°/RT of one record interval, for the builder's join-and-cut test (host side: outside a kernel ILGPU gives no view
    /// over a managed array). The sum is written a second time here, sharing only <see cref="EnthalpyTerm"/> with the view
    /// overload: the declared deviation of BOOT.md's invariants, pinned bit for bit by <c>OverloadPinningTests</c>.
    /// </summary>
    internal static double HOverRT(TemperatureInterval interval, double temperature)
    {
        var sum = interval.B1 / temperature;
        for (var k = 0; k < TableLayout.CoefficientsPerInterval; k++)
        {
            var e = k < interval.Exponents.Count ? interval.Exponents[k] : 0.0;
            sum += interval.Coefficients[k] * EnthalpyTerm(e, temperature);
        }

        return sum;
    }

    public static double SOverR(in SpeciesTableView table, int species, double temperature)
    {
        var interval = table.IntervalStart[species] + IntervalOf(table, species, temperature);
        var sum = table.Coefficients[interval * TableLayout.CoefficientStride + TableLayout.B2Slot];
        for (var k = 0; k < TableLayout.CoefficientsPerInterval; k++)
        {
            var e = table.Exponents[interval * TableLayout.ExponentsPerInterval + k];
            var term = e == 0.0 ? Math.Log(temperature) : Power(temperature, e) / e;
            sum += table.Coefficients[interval * TableLayout.CoefficientStride + k] * term;
        }

        return sum;
    }

    /// <summary>H°/RT − S°/R.</summary>
    public static double GOverRT(in SpeciesTableView table, int species, double temperature) =>
        HOverRT(table, species, temperature) - SOverR(table, species, temperature);

    /// <summary>I_h(e, T) of the class remarks: the enthalpy integral of one polynomial term.</summary>
    private static double EnthalpyTerm(double e, double t) => e == -1.0 ? Math.Log(t) / t : Power(t, e) / (e + 1.0);

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
