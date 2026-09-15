namespace AerospacePropellantThermodynamics.Data;

/// <summary>A field-level format error, stamped with the 0-based index of the line it was read from.</summary>
internal sealed class FieldException(int lineIndex, string message, Exception? inner) : FormatException(message, inner)
{
    public int LineIndex { get; } = lineIndex;
}

/// <summary>
/// A field error stamped with its line and file, for both parsers: runs one line's field reads and turns any
/// <see cref="FormatException"/> it raises into a <see cref="FieldException"/> naming that line, and fetches a line
/// that must exist or fails with a <see cref="DatabaseFormatException"/> naming the file's end.
/// </summary>
internal static class LineErrors
{
    /// <summary>Runs one line's field reads and stamps any format error with that line.</summary>
    public static T OnLine<T>(int lineIndex, Func<T> read)
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

    /// <summary>The <see cref="Action"/> form, for a line whose fields are written into scratch rather than returned, so no reader returns a value nobody reads.</summary>
    public static void OnLine(int lineIndex, Action read) => OnLine<object?>(lineIndex, () =>
    {
        read();
        return null;
    });

    /// <summary>The line at <paramref name="index"/>, or a <see cref="DatabaseFormatException"/> naming the file's end when <paramref name="context"/> (e.g. "a record", "the block of H2") runs out of lines.</summary>
    public static string Require(string[] lines, int index, string? fileName, string context)
    {
        if (index >= lines.Length)
        {
            throw new DatabaseFormatException(fileName, lines.Length, $"file ends inside {context}");
        }

        return lines[index];
    }
}
