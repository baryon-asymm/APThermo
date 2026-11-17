namespace APThermo.Data;

/// <summary>
/// Reader of <c>trans.inp</c>: the loop over species blocks and binary-interaction blocks, each read whole by
/// <see cref="TransportBlockReader"/>. The end marker <c>end</c> is compared ordinally in the file's own (lowercase)
/// case, the sentinel convention of the sibling <see cref="ThermoFile"/> (BOOT.md, F-TD-12).
/// </summary>
internal static class TransParser
{
    public static IReadOnlyList<TransportEntry> Parse(string[] lines, string? fileName)
    {
        var entries = new List<TransportEntry>();
        var keys = new HashSet<(string, string?)>();
        var i = 1; // line 1 is the title
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            if (line.Trim().Equals("end", StringComparison.Ordinal))
            {
                ValidateTrailer(lines, i + 1, fileName);
                return entries;
            }

            var headerLine = i + 1;
            var entry = TransportBlockReader.Read(lines, ref i, fileName);
            var key = Key(entry);
            if (!keys.Add(key))
            {
                throw new DatabaseFormatException(fileName, headerLine, "duplicate transport entry");
            }

            entries.Add(entry);
        }

        throw new DatabaseFormatException(fileName, lines.Length, "file ends before 'end'");
    }

    private static (string, string?) Key(TransportEntry entry) => entry.Partner is null ||
        string.CompareOrdinal(entry.Species, entry.Partner) <= 0
            ? (entry.Species, entry.Partner)
            : (entry.Partner, entry.Species);

    private static void ValidateTrailer(string[] lines, int start, string? fileName)
    {
        for (var i = start; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length > 0 && !line.StartsWith('!'))
            {
                throw new DatabaseFormatException(fileName, i + 1, "unexpected content after 'end'");
            }
        }
    }
}
