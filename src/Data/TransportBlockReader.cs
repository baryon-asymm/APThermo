using System.Globalization;
using System.Text.RegularExpressions;

namespace APThermo.Data;

/// <summary>
/// One block of <c>trans.inp</c>: the header (one species name, or two for a binary interaction, the <c>VnCm</c>
/// fit-count code and the reference), then its viscosity and conductivity fit lines, dispatched by their leading
/// V or C and checked against the header's count.
/// </summary>
internal static partial class TransportBlockReader
{
    [GeneratedRegex(@"V(\d)C(\d)")]
    private static partial Regex FitCountCode();

    public static TransportEntry Read(string[] lines, ref int i, string? fileName)
    {
        var line = lines[i];
        if (line[0] == ' ')
        {
            throw new DatabaseFormatException(fileName, i + 1, "expected a block header starting in column 1");
        }

        var species = FixedColumns.Field(line, RecordColumns.TransSpeciesStart, RecordColumns.TransSpeciesLength).Trim();
        var partner = FixedColumns.Field(line, RecordColumns.TransPartnerStart, RecordColumns.TransPartnerLength).Trim();
        var headerWidth = RecordColumns.TransSpeciesLength + RecordColumns.TransPartnerLength;
        var tail = line.Length > headerWidth ? line[headerWidth..] : string.Empty;
        var code = FitCountCode().Match(tail);
        if (!code.Success)
        {
            throw new DatabaseFormatException(fileName, i + 1, "block header lacks the VnCm fit-count code");
        }

        var viscosityCount = int.Parse(code.Groups[1].Value, CultureInfo.InvariantCulture);
        var conductivityCount = int.Parse(code.Groups[2].Value, CultureInfo.InvariantCulture);
        var reference = tail[(code.Index + code.Length)..].Trim();
        i++;

        var (viscosity, conductivity) = ReadFits(lines, ref i, fileName, species, viscosityCount, conductivityCount);
        return new TransportEntry(species, partner.Length == 0 ? null : partner, reference, viscosity, conductivity);
    }

    private static (List<TransportFit> Viscosity, List<TransportFit> Conductivity) ReadFits(
        string[] lines, ref int i, string? fileName, string species, int viscosityCount, int conductivityCount)
    {
        var viscosity = new List<TransportFit>(viscosityCount);
        var conductivity = new List<TransportFit>(conductivityCount);
        for (var n = 0; n < viscosityCount + conductivityCount; n++)
        {
            var fitLine = LineErrors.Require(lines, i, fileName, $"the block of {species}");
            TransportFit fit;
            try
            {
                fit = LineErrors.OnLine(i, () => ReadFit(fitLine));
            }
            catch (FieldException e)
            {
                throw new DatabaseFormatException(fileName, e.LineIndex + 1, $"block of {species}: {e.Message}", e.InnerException);
            }

            switch (FixedColumns.Field(fitLine, RecordColumns.FitKindStart, RecordColumns.FitKindLength))
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

        return viscosity.Count != viscosityCount || conductivity.Count != conductivityCount
            ? throw new DatabaseFormatException(fileName, i, $"block of {species}: fit lines do not match the code V{viscosityCount}C{conductivityCount}")
            : (viscosity, conductivity);
    }

    private static TransportFit ReadFit(string line) => new(
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitTLowStart, RecordColumns.FitTLowLength)),
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitTHighStart, RecordColumns.FitTHighLength)),
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitAStart, RecordColumns.FitALength)),
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitBStart, RecordColumns.FitBLength)),
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitCStart, RecordColumns.FitCLength)),
        FortranNumber.Parse(FixedColumns.Field(line, RecordColumns.FitDStart, RecordColumns.FitDLength)));
}
