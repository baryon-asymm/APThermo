using APThermo.Data;

namespace APThermo.Thermo;

/// <summary>
/// The flat layout of a species table in one place: the strides and slots that both the writer (<see cref="Flatten"/>,
/// below) and the reader (<see cref="SpeciesFunctions"/>) use, and the flattening of the resolved pieces into
/// <see cref="SpeciesTableArrays"/>.
/// </summary>
internal static class TableLayout
{
    /// <summary>[interval * BoundsStride + (0: TLow, 1: THigh)].</summary>
    public const int BoundsStride = 2;

    /// <summary>[interval * ExponentsPerInterval + k]: the eight exponents of T of the record.</summary>
    public const int ExponentsPerInterval = 8;

    /// <summary>a1 … a7 of a record's polynomial.</summary>
    public const int CoefficientsPerInterval = 7;

    /// <summary>[interval * CoefficientStride + k]: a1 … a7, then the B1Slot and B2Slot integration constants.</summary>
    public const int CoefficientStride = 9;

    public const int B1Slot = 7;

    public const int B2Slot = 8;

    /// <summary>Flattens the resolved pieces into the host arrays, in the given order.</summary>
    public static SpeciesTableArrays Flatten(IReadOnlyList<string> elements, IReadOnlyDictionary<string, int> elementIndex, IReadOnlyList<TablePiece> entries)
    {
        var count = entries.Count;
        var molarMass = new double[count];
        var formationEnthalpy = new double[count];
        var stoichiometry = new double[elements.Count * count];
        var intervalStart = new int[count];
        var intervalCount = new int[count];
        var total = entries.Sum(entry => entry.Intervals.Count);
        var bounds = new double[total * BoundsStride];
        var exponents = new double[total * ExponentsPerInterval];
        var coefficients = new double[total * CoefficientStride];
        var next = 0;
        for (var j = 0; j < count; j++)
        {
            var record = entries[j].Record;
            molarMass[j] = record.MolarMass;
            formationEnthalpy[j] = record.FormationEnthalpy;
            foreach (var pair in record.Formula)
            {
                stoichiometry[elementIndex[pair.Symbol] * count + j] += pair.Count;
            }

            intervalStart[j] = next;
            intervalCount[j] = entries[j].Intervals.Count;
            foreach (var (interval, _) in entries[j].Intervals)
            {
                WriteInterval(interval, next, bounds, exponents, coefficients);
                next++;
            }
        }

        return new SpeciesTableArrays(
            molarMass: molarMass, formationEnthalpy: formationEnthalpy, stoichiometry: stoichiometry,
            intervalStart: intervalStart, intervalCount: intervalCount, intervalBounds: bounds,
            exponents: exponents, coefficients: coefficients);
    }

    private static void WriteInterval(TemperatureInterval interval, int slot, double[] bounds, double[] exponents, double[] coefficients)
    {
        bounds[slot * BoundsStride] = interval.TLow;
        bounds[slot * BoundsStride + 1] = interval.THigh;
        for (var k = 0; k < ExponentsPerInterval; k++)
        {
            exponents[slot * ExponentsPerInterval + k] = k < interval.Exponents.Count ? interval.Exponents[k] : 0.0;
        }

        for (var k = 0; k < CoefficientsPerInterval; k++)
        {
            coefficients[slot * CoefficientStride + k] = interval.Coefficients[k];
        }

        coefficients[slot * CoefficientStride + B1Slot] = interval.B1;
        coefficients[slot * CoefficientStride + B2Slot] = interval.B2;
    }
}
