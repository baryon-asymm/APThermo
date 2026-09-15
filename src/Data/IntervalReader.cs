namespace APThermo.Data;

/// <summary>One temperature interval of a species record: the bounds-and-exponents line, then two coefficient lines.</summary>
internal static class IntervalReader
{
    public static TemperatureInterval Read(string[] lines, ref int i, string? fileName)
    {
        var header = LineErrors.Require(lines, i, fileName, "a record");
        var (tLow, tHigh, exponents, enthalpyOffset) = LineErrors.OnLine(i, () => ReadHeader(header));

        var c1 = LineErrors.Require(lines, i + 1, fileName, "a record");
        var c2 = LineErrors.Require(lines, i + 2, fileName, "a record");
        var coefficients = new double[RecordColumns.CoefficientsPerInterval];
        LineErrors.OnLine(i + 1, () => ReadFirstCoefficients(c1, coefficients));
        var (b1, b2) = LineErrors.OnLine(i + 2, () => ReadSecondCoefficients(c2, coefficients));

        i += 3;
        return new TemperatureInterval(
            TLow: tLow, THigh: tHigh, Exponents: exponents, Coefficients: coefficients, B1: b1, B2: b2, EnthalpyOffset: enthalpyOffset);
    }

    private static (double TLow, double THigh, double[] Exponents, double EnthalpyOffset) ReadHeader(string line)
    {
        var tLow = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.TLowStart, RecordColumns.TLowLength));
        var tHigh = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.THighStart, RecordColumns.THighLength));
        var coefficientCount = FortranNumber.ParseInt(FixedColumns.Field(line, RecordColumns.CoefficientCountStart, RecordColumns.CoefficientCountLength));
        if (coefficientCount != RecordColumns.CoefficientsPerInterval)
        {
            throw new FormatException($"an interval with {coefficientCount} coefficients is not supported; {RecordColumns.CoefficientsPerInterval} expected");
        }

        // The bounds are kept as written: eleven condensed records of the NASA file carry a first interval whose
        // upper bound is not above its lower bound (Br2(cr): 300..265.9); the tests node's approved anomaly list
        // records them, and rejecting them would lose the records.
        if (double.IsNaN(tLow) || double.IsNaN(tHigh))
        {
            throw new FormatException($"interval bounds {tLow}..{tHigh} are not numbers");
        }

        var exponents = new double[RecordColumns.ExponentsPerInterval];
        for (var k = 0; k < RecordColumns.ExponentsPerInterval; k++)
        {
            exponents[k] = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.ExponentStart + RecordColumns.ExponentStride * k, RecordColumns.ExponentLength));
        }

        var enthalpyOffset = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.EnthalpyOffsetStart, RecordColumns.EnthalpyOffsetLength));
        return (tLow, tHigh, exponents, enthalpyOffset);
    }

    private static void ReadFirstCoefficients(string line, double[] coefficients)
    {
        for (var k = 0; k < 5; k++)
        {
            coefficients[k] = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.CoefficientStart + RecordColumns.CoefficientStride * k, RecordColumns.CoefficientLength));
        }
    }

    private static (double B1, double B2) ReadSecondCoefficients(string line, double[] coefficients)
    {
        coefficients[5] = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.CoefficientStart, RecordColumns.CoefficientLength));
        coefficients[6] = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.CoefficientStart + RecordColumns.CoefficientStride, RecordColumns.CoefficientLength));
        var b1 = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.B1Start, RecordColumns.B1Length));
        var b2 = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.B2Start, RecordColumns.B2Length));
        return (b1, b2);
    }
}
