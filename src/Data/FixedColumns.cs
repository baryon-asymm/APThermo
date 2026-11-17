namespace APThermo.Data;

/// <summary>A 1-based Fortran-style column field, for both <c>thermo.inp</c> and <c>trans.inp</c>.</summary>
internal static class FixedColumns
{
    /// <summary>Require a complete physical fixed-column record without changing blank-field semantics.</summary>
    public static void RequireWidth(string line, int width)
    {
        if (line.Length < width)
        {
            throw new FormatException($"fixed-column line is truncated: {width} columns required, {line.Length} found");
        }
    }

    /// <summary>The field of <paramref name="length"/> characters starting at the 1-based column <paramref name="start"/>; a short line reads as blank-padded.</summary>
    public static string Field(string line, int start, int length)
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
