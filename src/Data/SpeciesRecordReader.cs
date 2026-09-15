namespace APThermo.Data;

/// <summary>
/// One species record of <c>thermo.inp</c>, whole or absent: the identity line, the properties line (interval count,
/// date code, formula, phase, molar mass, formation enthalpy), then either the assigned-temperature line (no
/// intervals) or the record's intervals, each read by <see cref="IntervalReader"/>. A field error on any of its lines
/// fails the load as a <see cref="DatabaseFormatException"/> naming the bad field's line and the record's start line,
/// as <see cref="TransportBlockReader"/> does for a block of <c>trans.inp</c>.
/// </summary>
internal static class SpeciesRecordReader
{
    public static Species Read(string[] lines, ref int i, SpeciesSection section, string? fileName)
    {
        var first = i;
        try
        {
            var l1 = lines[i];
            var (name, comment) = LineErrors.OnLine(i, () => ReadIdentity(l1));

            var l2 = LineErrors.Require(lines, i + 1, fileName, "a record");
            var properties = LineErrors.OnLine(i + 1, () => ReadProperties(l2));

            var intervals = new List<TemperatureInterval>(properties.IntervalCount);
            var assignedTemperature = 0.0;
            if (properties.IntervalCount == 0)
            {
                var l3 = LineErrors.Require(lines, i + 2, fileName, "a record");
                assignedTemperature = LineErrors.OnLine(i + 2, () => ReadAssignedTemperature(l3));
                i += 3;
            }
            else
            {
                i += 2;
                for (var n = 0; n < properties.IntervalCount; n++)
                {
                    intervals.Add(IntervalReader.Read(lines, ref i, fileName));
                }
            }

            return new Species(
                Name: name,
                Comment: comment,
                DateCode: properties.DateCode,
                Formula: properties.Formula,
                Phase: properties.Phase,
                MolarMass: properties.MolarMass,
                FormationEnthalpy: properties.FormationEnthalpy,
                AssignedTemperature: assignedTemperature,
                Intervals: intervals,
                Section: section,
                IsInert: name.StartsWith("Inert", StringComparison.Ordinal));
        }
        catch (FieldException e)
        {
            throw new DatabaseFormatException(fileName, e.LineIndex + 1, $"record starting at line {first + 1}: {e.Message}", e.InnerException);
        }
    }

    private static (string Name, string Comment) ReadIdentity(string line)
    {
        var name = FixedColumns.Field(line, RecordColumns.NameStart, RecordColumns.NameLength).Trim();
        var comment = line.Length > RecordColumns.NameLength ? line[RecordColumns.NameLength..].Trim() : string.Empty;
        if (name.Length == 0 || line[0] == ' ')
        {
            throw new FormatException("a record must start with a species name in column 1");
        }

        return (name, comment);
    }

    private readonly record struct Properties(
        int IntervalCount, string DateCode, IReadOnlyList<ElementCount> Formula, SpeciesPhase Phase, double MolarMass, double FormationEnthalpy);

    private static Properties ReadProperties(string line)
    {
        var intervalCount = FortranNumber.ParseInt(FixedColumns.Field(line, RecordColumns.IntervalCountStart, RecordColumns.IntervalCountLength));
        if (intervalCount < 0)
        {
            // A negative count is a format error like any other bad field, not an ArgumentOutOfRangeException
            // without a file or a line, which is what List<T>'s constructor would raise further down (F-TD-08).
            throw new FormatException($"a negative interval count ({intervalCount}) is not valid");
        }

        var dateCode = FixedColumns.Field(line, RecordColumns.DateCodeStart, RecordColumns.DateCodeLength).Trim();
        var formula = ReadFormula(line);
        var phase = FortranNumber.ParseInt(FixedColumns.Field(line, RecordColumns.PhaseStart, RecordColumns.PhaseLength)) == 0 ? SpeciesPhase.Gas : SpeciesPhase.Condensed;
        var molarMass = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.MolarMassStart, RecordColumns.MolarMassLength));
        var formationEnthalpy = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FormationEnthalpyStart, RecordColumns.FormationEnthalpyLength));
        if (molarMass <= 0.0)
        {
            throw new FormatException("molar mass must be positive");
        }

        return new Properties(intervalCount, dateCode, formula, phase, molarMass, formationEnthalpy);
    }

    private static IReadOnlyList<ElementCount> ReadFormula(string line)
    {
        var formula = new List<ElementCount>(RecordColumns.FormulaPairs);
        for (var k = 0; k < RecordColumns.FormulaPairs; k++)
        {
            var symbol = FixedColumns.Field(line, RecordColumns.FormulaSymbolStart + RecordColumns.FormulaPairStride * k, RecordColumns.FormulaSymbolLength).Trim();
            var count = FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FormulaCountStart + RecordColumns.FormulaPairStride * k, RecordColumns.FormulaCountLength));
            if (symbol.Length == 0)
            {
                if (count != 0.0)
                {
                    throw new FormatException($"formula pair {k + 1} has a count without an element symbol");
                }

                continue;
            }

            formula.Add(new ElementCount(symbol, count));
        }

        if (formula.Count == 0)
        {
            throw new FormatException("a record must name at least one element");
        }

        return formula;
    }

    private static double ReadAssignedTemperature(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new FormatException("a record without intervals must give the temperature of its assigned enthalpy");
        }

        return FortranNumber.Parse(tokens[0]);
    }
}
