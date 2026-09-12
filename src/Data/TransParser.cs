using System.Text.RegularExpressions;

namespace AerospacePropellantThermodynamics.Data;

/// <summary>Reader of <c>trans.inp</c>: species blocks and binary-interaction blocks of viscosity and conductivity fits.</summary>
internal static partial class TransParser
{
    [GeneratedRegex(@"V(\d)C(\d)")]
    private static partial Regex FitCountCode();

    public static IReadOnlyList<TransportEntry> Parse(string[] lines, string? fileName)
    {
        var entries = new List<TransportEntry>();
        var i = 1; // line 1 is the title
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            if (line.StartsWith("end", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line[0] == ' ')
            {
                throw new DatabaseFormatException(fileName, i + 1, "expected a block header starting in column 1");
            }

            var species = ThermoParser.Columns(line, 1, 16).Trim();
            var partner = ThermoParser.Columns(line, 17, 16).Trim();
            var tail = line.Length > 32 ? line[32..] : string.Empty;
            var code = FitCountCode().Match(tail);
            if (!code.Success)
            {
                throw new DatabaseFormatException(fileName, i + 1, "block header lacks the VnCm fit-count code");
            }

            var viscosityCount = int.Parse(code.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var conductivityCount = int.Parse(code.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var reference = tail[(code.Index + code.Length)..].Trim();
            i++;

            var viscosity = new List<TransportFit>(viscosityCount);
            var conductivity = new List<TransportFit>(conductivityCount);
            for (var n = 0; n < viscosityCount + conductivityCount; n++)
            {
                if (i >= lines.Length)
                {
                    throw new DatabaseFormatException(fileName, lines.Length, $"file ends inside the block of {species}");
                }

                var fitLine = lines[i];
                var kind = ThermoParser.Columns(fitLine, 2, 1);
                TransportFit fit;
                try
                {
                    fit = new TransportFit(
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 3, 9)),
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 12, 9)),
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 21, 15)),
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 36, 15)),
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 51, 15)),
                        FortranNumber.Parse(ThermoParser.Columns(fitLine, 66, 15)));
                }
                catch (FormatException e)
                {
                    throw new DatabaseFormatException(fileName, i + 1, $"block of {species}: {e.Message}", e);
                }

                switch (kind)
                {
                    case "V":
                        viscosity.Add(fit);
                        break;
                    case "C":
                        conductivity.Add(fit);
                        break;
                    default:
                        throw new DatabaseFormatException(fileName, i + 1, $"block of {species}: a fit line must start with V or C");
                }

                i++;
            }

            if (viscosity.Count != viscosityCount || conductivity.Count != conductivityCount)
            {
                throw new DatabaseFormatException(fileName, i, $"block of {species}: fit lines do not match the code V{viscosityCount}C{conductivityCount}");
            }

            entries.Add(new TransportEntry(species, partner.Length == 0 ? null : partner, reference, viscosity, conductivity));
        }

        return entries;
    }
}
