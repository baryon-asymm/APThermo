namespace APThermo.Data;

/// <summary>
/// The skeleton of <c>thermo.inp</c> (NASA Glenn format, TP-2002-211556): comments before the <c>thermo</c> line,
/// the interval-bounds header, the PRODUCTS and REACTANTS sections, and the record loop. One sentinel convention:
/// the section and end markers are compared ordinally in the file's own case (<c>END PRODUCTS</c>,
/// <c>END REACTANTS</c>), and the <c>thermo</c> line alone is matched case-insensitively, as the format description
/// allows (BOOT.md, F-TD-12).
/// </summary>
internal static class ThermoFile
{
    internal sealed record Result(
        IReadOnlyList<Species> Products, IReadOnlyList<Species> Reactants, string HeaderDate, IReadOnlyList<double> DefaultIntervalBounds);

    public static Result Parse(string[] lines, string? fileName)
    {
        var products = new List<Species>();
        var reactants = new List<Species>();
        var i = SkipLeadingComments(lines, fileName);
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
                RequireSection(section, SpeciesSection.Products, fileName, i + 1);
                section = SpeciesSection.Reactants;
                i++;
                continue;
            }

            if (line.StartsWith("END REACTANTS", StringComparison.Ordinal))
            {
                RequireSection(section, SpeciesSection.Reactants, fileName, i + 1);
                ValidateTrailer(lines, i + 1, fileName);
                break;
            }

            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('!'))
            {
                i++;
                continue;
            }

            var species = SpeciesRecordReader.Read(lines, ref i, section, fileName);
            (section == SpeciesSection.Products ? products : reactants).Add(species);
        }

        return new Result(products, reactants, headerDate, bounds);
    }

    private static void RequireSection(SpeciesSection actual, SpeciesSection expected, string? fileName, int line)
    {
        if (actual != expected)
        {
            throw new DatabaseFormatException(fileName, line, expected == SpeciesSection.Reactants
                ? "'END REACTANTS' appeared before 'END PRODUCTS'"
                : "'END PRODUCTS' was repeated after the reactants section began");
        }
    }

    private static void ValidateTrailer(string[] lines, int first, string? fileName)
    {
        for (var index = first; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length > 0 && !line.StartsWith('!'))
            {
                throw new DatabaseFormatException(fileName, index + 1, "unexpected content after 'END REACTANTS'");
            }
        }
    }
    private static int SkipLeadingComments(string[] lines, string? fileName)
    {
        var i = 0;
        while (i < lines.Length && !lines[i].StartsWith("thermo", StringComparison.OrdinalIgnoreCase))
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('!'))
            {
                throw new DatabaseFormatException(fileName, i + 1, "expected a comment line or the 'thermo' line");
            }

            i++;
        }

        return i >= lines.Length ? throw new DatabaseFormatException(fileName, lines.Length, "no 'thermo' line found") : i;
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

            if (!double.IsFinite(value))
            {
                throw new DatabaseFormatException(fileName, lineNumber, "a header interval bound must be finite");
            }

            bounds.Add(value);
        }

        return bounds.Count == 0
            ? throw new DatabaseFormatException(fileName, lineNumber, "the line after 'thermo' must start with the interval bounds")
            : (bounds, string.Join(' ', tokens.Skip(k)));
    }
}
