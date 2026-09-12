namespace AerospacePropellantThermodynamics.Data;

/// <summary>Column-based reader of <c>thermo.inp</c> (NASA Glenn format, TP-2002-211556).</summary>
internal static class ThermoParser
{
    internal sealed record Result(
        IReadOnlyList<Species> Products,
        IReadOnlyList<Species> Reactants,
        string HeaderDate,
        IReadOnlyList<double> DefaultIntervalBounds);

    private const int CoefficientsPerInterval = 7;
    private const int ExponentsPerInterval = 8;

    public static Result Parse(string[] lines, string? fileName)
    {
        var products = new List<Species>();
        var reactants = new List<Species>();
        var i = 0;

        // Comments before the "thermo" line.
        while (i < lines.Length && !lines[i].StartsWith("thermo", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('!'))
            {
                throw new DatabaseFormatException(fileName, i + 1, "expected a comment line or the 'thermo' line");
            }

            i++;
        }

        if (i >= lines.Length)
        {
            throw new DatabaseFormatException(fileName, lines.Length, "no 'thermo' line found");
        }

        i++;
        if (i >= lines.Length)
        {
            throw new DatabaseFormatException(fileName, i, "missing the interval-bounds line after 'thermo'");
        }

        var (bounds, headerDate) = ParseHeader(lines[i], fileName, i + 1);
        i++;

        var section = SpeciesSection.Products;
        while (true)
        {
            if (i >= lines.Length)
            {
                throw new DatabaseFormatException(fileName, lines.Length, "file ends before 'END REACTANTS'");
            }

            var line = lines[i];
            if (line.StartsWith("END PRODUCTS", StringComparison.Ordinal))
            {
                section = SpeciesSection.Reactants;
                i++;
                continue;
            }

            if (line.StartsWith("END REACTANTS", StringComparison.Ordinal))
            {
                break;
            }

            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('!'))
            {
                i++;
                continue;
            }

            var species = ParseRecord(lines, ref i, section, fileName);
            (section == SpeciesSection.Products ? products : reactants).Add(species);
        }

        return new Result(products, reactants, headerDate, bounds);
    }

    private static (IReadOnlyList<double> Bounds, string Date) ParseHeader(string line, string? fileName, int lineNumber)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bounds = new List<double>();
        var k = 0;
        for (; k < tokens.Length; k++)
        {
            if (!double.TryParse(tokens[k], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                break;
            }

            bounds.Add(value);
        }

        if (bounds.Count == 0)
        {
            throw new DatabaseFormatException(fileName, lineNumber, "the line after 'thermo' must start with the interval bounds");
        }

        return (bounds, string.Join(' ', tokens.Skip(k)));
    }

    private static Species ParseRecord(string[] lines, ref int i, SpeciesSection section, string? fileName)
    {
        var first = i;
        try
        {
            var l1 = lines[i];
            var (name, comment) = OnLine(i, () =>
            {
                var name = Columns(l1, 1, 18).Trim();
                var comment = l1.Length > 18 ? l1[18..].Trim() : string.Empty;
                if (name.Length == 0 || l1[0] == ' ')
                {
                    throw new FormatException("a record must start with a species name in column 1");
                }

                return (name, comment);
            });

            var l2 = Line(lines, i + 1, fileName);
            var (intervalCount, dateCode, formula, phase, molarMass, formationEnthalpy) = OnLine(i + 1, () =>
            {
                var intervalCount = FortranNumber.ParseInt(Columns(l2, 1, 2));
                var dateCode = Columns(l2, 4, 6).Trim();
                var formula = new List<ElementCount>(5);
                for (var k = 0; k < 5; k++)
                {
                    var symbol = Columns(l2, 11 + 8 * k, 2).Trim();
                    var count = FortranNumber.Parse(Columns(l2, 13 + 8 * k, 6));
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

                var phase = FortranNumber.ParseInt(Columns(l2, 51, 2)) == 0 ? SpeciesPhase.Gas : SpeciesPhase.Condensed;
                var molarMass = FortranNumber.Parse(Columns(l2, 53, 13));
                var formationEnthalpy = FortranNumber.Parse(Columns(l2, 66, 15));
                if (molarMass <= 0.0)
                {
                    throw new FormatException("molar mass must be positive");
                }

                return (intervalCount, dateCode, (IReadOnlyList<ElementCount>)formula, phase, molarMass, formationEnthalpy);
            });

            var intervals = new List<TemperatureInterval>(intervalCount);
            var assignedTemperature = 0.0;
            if (intervalCount == 0)
            {
                var l3 = Line(lines, i + 2, fileName);
                assignedTemperature = OnLine(i + 2, () =>
                {
                    var tokens = l3.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length == 0)
                    {
                        throw new FormatException("a record without intervals must give the temperature of its assigned enthalpy");
                    }

                    return FortranNumber.Parse(tokens[0]);
                });
                i += 3;
            }
            else
            {
                i += 2;
                for (var n = 0; n < intervalCount; n++)
                {
                    intervals.Add(ParseInterval(lines, ref i, fileName));
                }
            }

            return new Species(
                name,
                comment,
                dateCode,
                formula,
                phase,
                molarMass,
                formationEnthalpy,
                assignedTemperature,
                intervals,
                section,
                name.StartsWith("Inert", StringComparison.Ordinal));
        }
        catch (FieldException e)
        {
            throw new DatabaseFormatException(fileName, e.LineIndex + 1, $"record starting at line {first + 1}: {e.Message}", e.InnerException);
        }
    }

    /// <summary>A <see cref="FormatException"/> that knows the index of the line it was raised on.</summary>
    private sealed class FieldException(int lineIndex, string message, Exception? inner) : FormatException(message, inner)
    {
        public int LineIndex { get; } = lineIndex;
    }

    /// <summary>Runs one line's field reads and stamps any format error with that line.</summary>
    private static T OnLine<T>(int lineIndex, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (FormatException e) when (e is not FieldException)
        {
            throw new FieldException(lineIndex, e.Message, e);
        }
    }

    private static TemperatureInterval ParseInterval(string[] lines, ref int i, string? fileName)
    {
        var header = Line(lines, i, fileName);
        var (tLow, tHigh, exponents, enthalpyOffset) = OnLine(i, () =>
        {
            var tLow = FortranNumber.Parse(Columns(header, 1, 11));
            var tHigh = FortranNumber.Parse(Columns(header, 12, 11));
            var coefficientCount = FortranNumber.ParseInt(Columns(header, 23, 1));
            if (coefficientCount != CoefficientsPerInterval)
            {
                throw new FormatException($"an interval with {coefficientCount} coefficients is not supported; {CoefficientsPerInterval} expected");
            }

            // The bounds are kept as written: eleven condensed records of the NASA file carry a first
            // interval whose upper bound is not above its lower bound (Br2(cr): 300..265.9); the
            // anomaly list in the tests node records them, and rejecting them would lose the records.
            if (double.IsNaN(tLow) || double.IsNaN(tHigh))
            {
                throw new FormatException($"interval bounds {tLow}..{tHigh} are not numbers");
            }

            var exponents = new double[ExponentsPerInterval];
            for (var k = 0; k < ExponentsPerInterval; k++)
            {
                exponents[k] = FortranNumber.Parse(Columns(header, 24 + 5 * k, 5));
            }

            return (tLow, tHigh, exponents, FortranNumber.Parse(Columns(header, 66, 15)));
        });

        var c1 = Line(lines, i + 1, fileName);
        var c2 = Line(lines, i + 2, fileName);
        var coefficients = new double[CoefficientsPerInterval];
        OnLine(i + 1, () =>
        {
            for (var k = 0; k < 5; k++)
            {
                coefficients[k] = FortranNumber.Parse(Columns(c1, 1 + 16 * k, 16));
            }

            return 0;
        });

        var (b1, b2) = OnLine(i + 2, () =>
        {
            coefficients[5] = FortranNumber.Parse(Columns(c2, 1, 16));
            coefficients[6] = FortranNumber.Parse(Columns(c2, 17, 16));
            return (FortranNumber.Parse(Columns(c2, 49, 16)), FortranNumber.Parse(Columns(c2, 65, 16)));
        });
        i += 3;
        return new TemperatureInterval(tLow, tHigh, exponents, coefficients, b1, b2, enthalpyOffset);
    }

    private static string Line(string[] lines, int index, string? fileName)
    {
        if (index >= lines.Length)
        {
            throw new DatabaseFormatException(fileName, lines.Length, "file ends inside a record");
        }

        return lines[index];
    }

    /// <summary>Columns are 1-based as in Fortran formats; a short line reads as blank-padded.</summary>
    internal static string Columns(string line, int start, int length)
    {
        var from = start - 1;
        if (from >= line.Length)
        {
            return new string(' ', length);
        }

        var available = Math.Min(length, line.Length - from);
        var text = line.Substring(from, available);
        return available < length ? text.PadRight(length) : text;
    }
}
