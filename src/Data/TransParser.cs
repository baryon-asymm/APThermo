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
        var i = 1; // line 1 is the title
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            if (line.StartsWith("end", StringComparison.Ordinal))
            {
                break;
            }

            entries.Add(TransportBlockReader.Read(lines, ref i, fileName));
        }

        return entries;
    }
}
